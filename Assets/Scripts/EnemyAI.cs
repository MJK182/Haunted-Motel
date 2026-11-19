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
    [SerializeField] [Range(10f, 180f)] private float fieldOfView = 90f;
    [SerializeField] private LayerMask obstacleMask;    // seinät yms., jotka estävät näkyvyyden

    [Header("Nopeudet")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float chaseSpeed = 5f;

    [Header("Muut")]
    [SerializeField] private float loseSightTime = 4f;  // kauanko etsii, ennen kuin palaa kiertämään
    [SerializeField] private float catchDistance = 1.2f;

    private NavMeshAgent agent;
    private Transform player;
    private State state = State.Patrol;
    private int waypointIndex;
    private float waitTimer;
    private float searchTimer;
    private Vector3 lastKnownPosition;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (eyes == null) eyes = transform;
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        GoToWaypoint();
    }

    void Update()
    {
        bool canSee = CanSeePlayer();

        switch (state)
        {
            case State.Patrol:
                agent.speed = patrolSpeed;
                if (canSee) { state = State.Chase; break; }
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
                    // Menetti pelaajan näkyvistä: mene viimeiseen tunnettuun paikkaan
                    state = State.Search;
                    searchTimer = loseSightTime;
                    agent.SetDestination(lastKnownPosition);
                }
                break;

            case State.Search:
                agent.speed = chaseSpeed;
                if (canSee) { state = State.Chase; break; }

                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    searchTimer -= Time.deltaTime;
                    if (searchTimer <= 0f)
                    {
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

    if (distance > sightRange)
    {
        Debug.Log($"Liian kaukana: {distance:F1} m");
        return false;
    }

    float angle = Vector3.Angle(eyes.forward, toPlayer);
    if (angle > fieldOfView * 0.5f)
    {
        Debug.Log($"Pelaaja näkökentän ulkopuolella: kulma {angle:F0}°");
        return false;
    }

    if (Physics.Raycast(eyes.position, toPlayer.normalized, out RaycastHit hit,
                        distance + 0.5f, ~0, QueryTriggerInteraction.Ignore))
    {
        bool isPlayer = hit.transform == player || hit.transform.IsChildOf(player);
        if (!isPlayer)
            Debug.Log($"Näkyvyyden estää: {hit.collider.name}");
        return isPlayer;
    }

    Debug.Log("Raycast ei osunut mihinkään (pelaajalla ei ehkä ole collideria)");
    return false;
}

    private void CaughtPlayer()
    {
        Debug.Log("Pelaaja jäi kiinni!");
        // Tähän esim. game over, scenen uudelleenlataus tms.
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
    }
}