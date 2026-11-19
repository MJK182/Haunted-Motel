using System.Linq;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    private enum State { Patrol, Chase, Search }

    [Header("Reittipisteet")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float waitAtWaypoint = 2f;

    [Header("Näkö")]
    [SerializeField] private Transform eyes;            // valinnainen, esim. pään kohta
    [SerializeField] private float sightRange = 15f;
    [SerializeField] [Range(10f, 360f)] private float fieldOfView = 90f;

    [Header("Nopeudet")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 5f;

    [Header("Muut")]
    [SerializeField] private float loseSightTime = 4f;  // kauanko etsii, ennen kuin palaa kiertämään
    [SerializeField] private float catchDistance = 1.2f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private NavMeshAgent agent;
    private Transform player;
    private PlayerRespawn playerRespawn;

    private State state = State.Patrol;
    private int waypointIndex;
    private float waitTimer;
    private float searchTimer;
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
        player = GameObject.FindGameObjectWithTag("Player").transform;
        playerRespawn = player.GetComponent<PlayerRespawn>();

        if (playerRespawn == null)
            Debug.LogWarning("Player-objektilta puuttuu PlayerRespawn-skripti!");

        enemyStartPosition = transform.position;
        enemyStartRotation = transform.rotation;

        GoToWaypoint();
    }

    void Update()
    {
        bool canSee = CanSeePlayer();

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
                    agent.SetDestination(player.position);

                    if (Vector3.Distance(transform.position, player.position) <= catchDistance)
                        CaughtPlayer();
                }
                else
                {
                    // Pelaaja katosi näkyvistä: mene viimeiseen tunnettuun paikkaan
                    if (debugLogs) Debug.Log("Pelaaja kadotettu, etsitään");
                    state = State.Search;
                    searchTimer = loseSightTime;
                    agent.SetDestination(lastKnownPosition);
                }
                break;

            case State.Search:
                agent.speed = chaseSpeed;
                if (canSee)
                {
                    state = State.Chase;
                    break;
                }

                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    searchTimer -= Time.deltaTime;
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

    private void Patrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
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
        agent.SetDestination(waypoints[waypointIndex].position);
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

        // Kaikki osumat säteen matkalla, lähimmästä kaukaisimpaan
        RaycastHit[] hits = Physics.RaycastAll(
            eyes.position, toPlayer.normalized, distance + 0.5f,
            ~0, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits.OrderBy(h => h.distance))
        {
            // Ohita vihollisen omat colliderit
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            bool isPlayer = hit.transform == player || hit.transform.IsChildOf(player);

            if (!isPlayer && debugLogs)
                Debug.Log($"Näkyvyyden estää: {hit.collider.name}");

            return isPlayer;
        }

        if (debugLogs)
            Debug.Log("Raycast ei osunut pelaajaan (onko pelaajalla collider?)");
        return false;
    }

    private void CaughtPlayer()
    {
        if (debugLogs) Debug.Log("Pelaaja jäi kiinni!");

        if (playerRespawn != null)
            playerRespawn.Respawn();

        // Vihollinen palaa alkupisteeseensä ja jatkaa kiertämistä
        agent.Warp(enemyStartPosition);
        transform.rotation = enemyStartRotation;
        state = State.Patrol;
        waypointIndex = 0;
        GoToWaypoint();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
    }
}