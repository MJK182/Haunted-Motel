using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Investigate, Search }

    [Header("Viitteet")]
    public Transform player;                 // jos tyhjä, haetaan tagilla "Player"
    public Light playerFlashlight;           // jos tyhjä, haetaan pelaajan lapsista
    public Transform[] patrolPoints;         // jos tyhjä, zombi kuljeskelee satunnaisesti

    [Header("Nopeudet")]
    public float patrolSpeed = 1.6f;
    public float investigateSpeed = 3.7f;
    public float chaseSpeed = 5.0f;

    [Header("Aistit")]
    public float sightRangeLit = 22f;
    public float sightRangeDark = 8f;
    public float fieldOfView = 130f;
    public float closeSenseRange = 4f;
    public float hearingRange = 9f;
    public float runSoundSpeed = 4.8f;
    public LayerMask visionBlockers = ~0;

    [Header("Käyttäytyminen")]
    public float catchDistance = 1.3f;
    public float searchTime = 4f;
    public float wanderRadius = 25f;
    public float spawnMinDistance = 20f;
    public bool randomizeStart = true;
    public float restartDelay = 2f;

    [Header("Turvallisuus")]
    public float maxAllowedSpeed = 12f;      // tätä nopeampi liike palautetaan
    public bool disableChildColliders = true;

    [Header("Debug")]
    public bool showDebug = true;

    public State CurrentState { get; private set; } = State.Patrol;
    public bool IsChasing => CurrentState == State.Chase;
    public bool PlayerHidden { get; private set; }
    public event System.Action<State> OnStateChanged;

    NavMeshAgent agent;
    CharacterController playerController;
    Vector3 lastKnown, hideSpot, lastGoodPos;
    bool sawHide, caught, ready, hasGoodPos;
    float searchTimer, repathTimer, stuckTimer;
    int patrolIndex;
    Transform[] modelRoots;
    Vector3[] modelStartPos;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        agent.enabled = true;
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.isStopped = false;
        agent.acceleration = 12f;
        agent.angularSpeed = 240f;
        agent.stoppingDistance = 0.5f;
        agent.autoBraking = true;

        FixPhysicsAndAnimation();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null)
        {
            Debug.LogError("ZombieAI: Player puuttuu. Vedä Player Player-kenttään tai anna sille tagi 'Player'.", this);
            return;
        }

        playerController = player.GetComponent<CharacterController>();
        if (playerFlashlight == null) playerFlashlight = player.GetComponentInChildren<Light>();

        if (!PlaceOnNavMesh())
        {
            Debug.LogError("ZombieAI: Zombi ei ole NavMeshin päällä. Tee Bake ja siirrä zombi sinisen alueen päälle.", this);
            return;
        }

        if (randomizeStart) RandomizeStart();

        lastGoodPos = transform.position;
        hasGoodPos = true;
        ready = true;
        SetState(State.Patrol, true);
    }

    // Poistaa yleisimmät syyt siihen, että zombi lentää tai kulkee seinien läpi
    void FixPhysicsAndAnimation()
    {
        var list = new System.Collections.Generic.List<Transform>();

        foreach (var a in GetComponentsInChildren<Animator>())
        {
            a.applyRootMotion = false;                    // animaatio ei saa liikuttaa zombia
            if (a.transform != transform) list.Add(a.transform);
        }

        modelRoots = list.ToArray();
        modelStartPos = new Vector3[modelRoots.Length];
        for (int i = 0; i < modelRoots.Length; i++)
            modelStartPos[i] = modelRoots[i].localPosition;

        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (disableChildColliders)
            foreach (var c in GetComponentsInChildren<Collider>())
                if (c.gameObject != gameObject) c.enabled = false;
    }

    void Update()
    {
        if (!ready || caught || player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        bool seen = CanSee(dist);
        bool heard = !seen && CanHear(dist);

        if (PlayerHidden)
        {
            seen = false;
            heard = false;
            if (sawHide && Vector3.Distance(transform.position, hideSpot) < 2.2f)
            {
                Catch();
                return;
            }
        }

        if (seen || heard)
        {
            lastKnown = player.position;
            if (CurrentState != State.Chase) SetState(State.Chase);
        }
        else if (CurrentState == State.Chase)
        {
            SetState(State.Investigate);
        }

        switch (CurrentState)
        {
            case State.Chase:
                repathTimer -= Time.deltaTime;
                if (repathTimer <= 0f) { agent.SetDestination(lastKnown); repathTimer = 0.15f; }
                if (dist <= catchDistance && !PlayerHidden) Catch();
                break;

            case State.Investigate:
                if (Arrived()) SetState(State.Search);
                break;

            case State.Search:
                transform.Rotate(0f, 90f * Time.deltaTime, 0f);
                searchTimer -= Time.deltaTime;
                if (searchTimer <= 0f) SetState(State.Patrol, true);
                break;

            case State.Patrol:
                UpdatePatrol();
                break;
        }
    }

    void LateUpdate()
    {
        if (!ready) return;

        // Pidä malli (lapsi) paikallaan parentissa
        for (int i = 0; i < modelRoots.Length; i++)
            modelRoots[i].localPosition = modelStartPos[i];

        // Suojaus: jos zombi liikkuu mahdottoman nopeasti, palauta edelliseen paikkaan
        float dt = Time.deltaTime;
        if (hasGoodPos && dt > 0.0001f && dt < 0.1f)
        {
            float speed = (transform.position - lastGoodPos).magnitude / dt;
            if (speed > maxAllowedSpeed)
            {
                Debug.LogWarning("ZombieAI: zombi liikkui " + speed.ToString("0") + " m/s. Palautetaan. " +
                                 "Tarkista: Apply Root Motion, Rigidbody, Colliderit ja muut skriptit, jotka liikuttavat zombia.", this);
                agent.Warp(lastGoodPos);
                return;
            }
        }
        lastGoodPos = transform.position;
        hasGoodPos = true;
    }

    // ---------- Partio ----------

    void UpdatePatrol()
    {
        if (agent.velocity.magnitude < 0.1f) stuckTimer += Time.deltaTime; else stuckTimer = 0f;

        if (Arrived() || stuckTimer > 3f)
        {
            stuckTimer = 0f;
            NextPatrolTarget();
        }
    }

    void NextPatrolTarget()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            if (!agent.SetDestination(patrolPoints[patrolIndex].position))
                Debug.LogWarning("ZombieAI: patrol point " + patrolIndex + " ei ole NavMeshin päällä.", patrolPoints[patrolIndex]);
        }
        else if (RandomNavPoint(transform.position, wanderRadius, out Vector3 p))
        {
            agent.SetDestination(p);
        }
    }

    // ---------- Aistit ----------

    bool CanSee(float dist)
    {
        bool lit = playerFlashlight != null && playerFlashlight.enabled && playerFlashlight.intensity > 0.1f;
        float range = lit ? sightRangeLit : sightRangeDark;
        if (dist > range) return false;

        Vector3 eye = transform.position + Vector3.up * 1.6f;
        Vector3 target = player.position + Vector3.up * 1.0f;
        Vector3 dir = target - eye;

        if (dist > closeSenseRange)
        {
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (Vector3.Angle(transform.forward, flat) > fieldOfView * 0.5f) return false;
        }

        if (Physics.Raycast(eye, dir.normalized, out RaycastHit hit, range, visionBlockers, QueryTriggerInteraction.Ignore))
            return hit.transform == player || hit.transform.IsChildOf(player);

        return false;
    }

    bool CanHear(float dist)
    {
        if (dist > hearingRange || playerController == null) return false;
        Vector3 v = playerController.velocity;
        return new Vector3(v.x, 0f, v.z).magnitude > runSoundSpeed;
    }

    // ---------- Piilopaikat ----------

    public void PlayerHides(Vector3 spot)
    {
        PlayerHidden = true;
        hideSpot = spot;
        sawHide = CurrentState == State.Chase;
        if (sawHide) { lastKnown = spot; agent.SetDestination(spot); }
    }

    public void PlayerLeavesHiding()
    {
        PlayerHidden = false;
        sawHide = false;
    }

    // ---------- Apufunktiot ----------

    void SetState(State s, bool force = false)
    {
        if (s == CurrentState && !force) return;
        CurrentState = s;
        agent.isStopped = false;

        switch (s)
        {
            case State.Chase:
                agent.speed = chaseSpeed;
                agent.SetDestination(lastKnown);
                repathTimer = 0.15f;
                break;

            case State.Investigate:
                agent.speed = investigateSpeed;
                agent.SetDestination(lastKnown);
                break;

            case State.Search:
                agent.speed = patrolSpeed;
                agent.ResetPath();
                searchTimer = searchTime;
                break;

            case State.Patrol:
                agent.speed = patrolSpeed;
                stuckTimer = 0f;
                if (patrolPoints != null && patrolPoints.Length > 0)
                    agent.SetDestination(patrolPoints[patrolIndex % patrolPoints.Length].position);
                else if (RandomNavPoint(transform.position, wanderRadius, out Vector3 p))
                    agent.SetDestination(p);
                break;
        }

        if (showDebug) Debug.Log("Zombi: " + s, this);
        OnStateChanged?.Invoke(s);
    }

    bool Arrived()
    {
        if (agent.pathPending) return false;
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid) return true;
        if (!agent.hasPath) return true;
        return agent.remainingDistance <= agent.stoppingDistance + 0.3f;
    }

    bool PlaceOnNavMesh()
    {
        if (agent.isOnNavMesh) return true;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            return agent.isOnNavMesh;
        }
        return false;
    }

    void RandomizeStart()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        var candidates = new System.Collections.Generic.List<int>();
        for (int i = 0; i < patrolPoints.Length; i++)
            if (Vector3.Distance(patrolPoints[i].position, player.position) > spawnMinDistance)
                candidates.Add(i);

        int pick = candidates.Count > 0
            ? candidates[Random.Range(0, candidates.Count)]
            : Random.Range(0, patrolPoints.Length);

        if (NavMesh.SamplePosition(patrolPoints[pick].position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            agent.Warp(hit.position);

        patrolIndex = pick;
    }

    bool RandomNavPoint(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 20; i++)
        {
            Vector3 p = center + Random.insideUnitSphere * radius;
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = center;
        return false;
    }

    void Catch()
    {
        if (caught) return;
        caught = true;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        Vector3 look = player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);

        var anim = GetComponent<ZombieAnimator>();
        if (anim != null) anim.PlayAttack();

        var pc = player.GetComponent<PlayerController>();
        if (pc != null) pc.canMove = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Invoke(nameof(RestartScene), restartDelay);
    }

    void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, sightRangeLit);
        Gizmos.color = Color.red;    Gizmos.DrawWireSphere(transform.position, sightRangeDark);
        Gizmos.color = Color.cyan;   Gizmos.DrawWireSphere(transform.position, hearingRange);
    }
}