using System.Linq;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAI : MonoBehaviour
{
    private enum State { Patrol, Chase, Search }

    [Header("Reittipisteet")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float waitAtWaypoint = 2f;

    [Header("Näkö")]
    [SerializeField] private Transform eyes;
    [SerializeField] private float sightRange = 15f;
    [SerializeField] [Range(10f, 360f)] private float fieldOfView = 90f;
    [SerializeField] private float lostSightGrace = 0.5f;   // sekuntia, ennen kuin näön menetys lasketaan

    [Header("Nopeudet")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 5f;

    [Header("Muut")]
    [SerializeField] private float loseSightTime = 2f;
    [SerializeField] private float catchDistance = 1.5f;
    [SerializeField] private bool returnToStartOnCatch = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private NavMeshAgent agent;
    private Transform player;
    private PlayerRespawn playerRespawn;

    private State state = State.Patrol;
    private int waypointIndex;
    private float waitTimer;
    private float searchTimer;
    private float timeSinceSeen = 999f;
    private Vector3 lastKnownPosition;
    private Vector3 enemyStartPosition;
    private Quaternion enemyStartRotation;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (eyes == null) eyes = transform;
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null)
        {
            Debug.LogError("Pelaajaa ei löytynyt: lisää Player-objektille tagi 'Player'.");
            enabled = false;
            return;
        }
        player = playerObj.transform;

        // Etsi PlayerRespawn objektista, sen vanhemmista tai lapsista
        playerRespawn = player.GetComponentInParent<PlayerRespawn>();
        if (playerRespawn == null)
            playerRespawn = player.GetComponentInChildren<PlayerRespawn>();
        if (playerRespawn == null)
            Debug.LogError("PlayerRespawn-skriptiä ei löytynyt Player-objektista (" + player.name + ")!");

        catchDistance = Mathf.Max(catchDistance, agent.stoppingDistance + 0.5f);

        enemyStartPosition = transform.position;
        enemyStartRotation = transform.rotation;

        GoToWaypoint();
    }

    void Update()
    {
        bool canSee = CanSeePlayer();
        if (canSee) timeSinceSeen = 0f;
        else timeSinceSeen += Time.deltaTime;

        switch (state)
        {
            case State.Patrol:
                agent.speed = patrolSpeed;
                if (canSee)
                {
                    if (debugLogs) Debug.Log("Chase alkaa");
                    state = State.Chase;
                    break;
                }
                Patrol();
                break;

            case State.Chase:
                agent.speed = chaseSpeed;
                if (canSee)
                {
                    lastKnownPosition = player.position;
                    MoveTo(player.position);

                    Vector3 flat = player.position - transform.position;
                    flat.y = 0f;
                    if (flat.magnitude <= catchDistance)
                        CaughtPlayer();
                }
                else if (timeSinceSeen > lostSightGrace)
                {
                    if (debugLogs) Debug.Log("Pelaaja kadotettu, etsitään");
                    state = State.Search;
                    searchTimer = loseSightTime;
                    MoveTo(lastKnownPosition);
                }
                break;

            case State.Search:
                agent.speed = chaseSpeed;
                if (canSee)
                {
                    state = State.Chase;
                    break;
                }

                if (HasArrived())
                {
                    searchTimer -= Time.deltaTime;
                    transform.Rotate(0f, 90f * Time.deltaTime, 0f); // katselee ympärilleen

                    if (searchTimer <= 0f)
                    {
                        if (debugLogs) Debug.Log("Palataan kiertämään");
                        state = State.Patrol;
                        GoToWaypoint();
                    }
                }
                break;
        }
    }

    private bool HasArrived()
    {
        if (agent.pathPending) return false;
        if (agent.pathStatus == NavMeshPathStatus.PathInvalid) return true;
        return agent.remainingDistance <= agent.stoppingDistance + 0.1f;
    }

    private bool MoveTo(Vector3 target)
    {
        if (NavMesh.SamplePosition(target, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
        {
            agent.SetDestination(navHit.position);
            return true;
        }

        if (debugLogs) Debug.Log("Kohde ei ole NavMeshillä: " + target);
        return false;
    }

    private void Patrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        if (HasArrived())
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitAtWaypoint)
            {
                waypointIndex = (waypointIndex + 1) % waypoints.Length;
                GoToWaypoint();
            }
        }
    }

    private void GoToWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        waitTimer = 0f;
        MoveTo(waypoints[waypointIndex].position);
    }

    private bool CanSeePlayer()
    {
        Vector3 targetPoint = player.position + Vector3.up * 0.8f;
        Vector3 toPlayer = targetPoint - eyes.position;
        float distance = toPlayer.magnitude;

        Debug.DrawRay(eyes.position, eyes.forward * sightRange, Color.green);
        Debug.DrawRay(eyes.position, toPlayer, Color.red);

        if (distance > sightRange) return false;

        float angle = Vector3.Angle(eyes.forward, toPlayer);
        if (angle > fieldOfView * 0.5f) return false;

        RaycastHit[] hits = Physics.RaycastAll(
            eyes.position, toPlayer.normalized, distance + 0.5f,
            ~0, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits.OrderBy(h => h.distance))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            return hit.transform == player || hit.transform.IsChildOf(player);
        }

        return false;
    }

    private void CaughtPlayer()
    {
        if (debugLogs) Debug.Log("Pelaaja jäi kiinni!");

        if (playerRespawn != null)
            playerRespawn.Respawn();
        else
            Debug.LogError("Respawn ei onnistu: PlayerRespawn puuttuu!");

        if (returnToStartOnCatch)
        {
            if (!agent.Warp(enemyStartPosition))
                transform.position = enemyStartPosition;
            transform.rotation = enemyStartRotation;
        }

        agent.ResetPath();
        state = State.Patrol;
        waypointIndex = 0;
        timeSinceSeen = 999f;
        GoToWaypoint();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
    }
}