using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Investigate, Search }

    [Header("Viitteet")]
    public Transform player;
    public Light playerFlashlight;          // jos tyhjä, haetaan pelaajan lapsista
    public Transform[] patrolPoints;

    [Header("Nopeudet")]
    public float patrolSpeed = 1.4f;
    public float investigateSpeed = 3.7f;
    public float chaseSpeed = 5.0f;         // nopeampi, kun näkee tai kuulee sinut

    [Header("Aistit")]
    public float sightRangeLit = 22f;       // taskulamppu päällä
    public float sightRangeDark = 8f;       // taskulamppu pois
    public float fieldOfView = 130f;
    public float closeSenseRange = 4f;      // aivan läheltä huomaa ilman näkökulmaa
    public float hearingRange = 9f;         // pelaajan juoksu kuuluu tähän asti (seinien läpi)
    public float runSoundSpeed = 4.8f;      // tätä nopeampi liike kuuluu
    public LayerMask visionBlockers = ~0;   // jos zombilla on oma layer, poista se tästä

    [Header("Käyttäytyminen")]
    public float catchDistance = 1.2f;
    public float searchTime = 4f;
    public float spawnMinDistance = 40f;    // satunnainen aloituspaikka vähintään näin kaukana
    public float restartDelay = 2f;

    public State CurrentState { get; private set; } = State.Patrol;
    public bool IsChasing => CurrentState == State.Chase;   // esim. jahtimusiikkia varten
    public bool PlayerHidden { get; private set; }
    public event System.Action<State> OnStateChanged;

    NavMeshAgent agent;
    CharacterController playerController;
    Vector3 lastKnown;
    Vector3 hideSpot;
    bool sawHide;
    bool caught;
    float searchTimer;
    int patrolIndex;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        playerController = player.GetComponent<CharacterController>();
        if (playerFlashlight == null)
            playerFlashlight = player.GetComponentInChildren<Light>();

        RandomizeStart();
        SetState(State.Patrol);
    }

    void Update()
    {
        if (caught) return;

        float dist = Vector3.Distance(transform.position, player.position);
        bool seen = CanSee(dist);
        bool heard = !seen && CanHear(dist);

        if (PlayerHidden)
        {
            seen = false;
            heard = false;
            // Jos zombi näki sinun piiloutuvan, se tulee kaapille ja löytää sinut
            if (sawHide && Vector3.Distance(transform.position, hideSpot) < 2.2f)
            {
                Catch();
                return;
            }
        }

        if (seen || heard)
        {
            lastKnown = player.position;
            SetState(State.Chase);
        }
        else if (CurrentState == State.Chase)
        {
            SetState(State.Investigate);   // menetti näkyvyyden: menee viimeiseen paikkaan
        }

        switch (CurrentState)
        {
            case State.Chase:
                agent.SetDestination(lastKnown);
                if (dist <= catchDistance && !PlayerHidden) Catch();
                break;

            case State.Investigate:
                agent.SetDestination(lastKnown);
                if (Arrived()) SetState(State.Search);
                break;

            case State.Search:
                transform.Rotate(0f, 90f * Time.deltaTime, 0f);   // katselee ympärilleen
                searchTimer -= Time.deltaTime;
                if (searchTimer <= 0f) SetState(State.Patrol);
                break;

            case State.Patrol:
                if (patrolPoints.Length > 0 && Arrived())
                {
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                    agent.SetDestination(patrolPoints[patrolIndex].position);
                }
                break;
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
        float speed = new Vector3(v.x, 0f, v.z).magnitude;
        return speed > runSoundSpeed;   // vain juoksu kuuluu, hiipiminen ei
    }

    // ---------- Piilopaikat (kutsu piilopaikka-skriptistä) ----------

    public void PlayerHides(Vector3 spot)
    {
        PlayerHidden = true;
        hideSpot = spot;
        sawHide = CurrentState == State.Chase;
        if (sawHide) lastKnown = spot;
    }

    public void PlayerLeavesHiding()
    {
        PlayerHidden = false;
        sawHide = false;
    }

    // ---------- Apufunktiot ----------

    void SetState(State s)
    {
        if (s == CurrentState && agent.hasPath) return;
        bool changed = s != CurrentState;
        CurrentState = s;
        agent.isStopped = false;

        switch (s)
        {
            case State.Chase:       agent.speed = chaseSpeed; break;
            case State.Investigate: agent.speed = investigateSpeed; break;
            case State.Search:
                agent.speed = patrolSpeed;
                agent.ResetPath();
                searchTimer = searchTime;
                break;
            case State.Patrol:
                agent.speed = patrolSpeed;
                if (patrolPoints.Length > 0)
                    agent.SetDestination(patrolPoints[patrolIndex].position);
                break;
        }

        if (changed) OnStateChanged?.Invoke(s);
    }

    bool Arrived()
    {
        return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;
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

        agent.Warp(patrolPoints[pick].position);
        patrolIndex = (pick + 1) % patrolPoints.Length;
    }

    void Catch()
    {
        if (caught) return;
        caught = true;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        // Käänny pelaajaan päin ja soita hyökkäysanimaatio (jos ZombieAnimator on olemassa)
        Vector3 look = player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
        var anim = GetComponent<ZombieAnimator>();
        if (anim != null) anim.PlayAttack();

        // Pelaaja jäätyy (PlayerController.canMove)
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