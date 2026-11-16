using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAnimator : MonoBehaviour
{
    public Animator animator;     // jos tyhja, haetaan lapsesta

    NavMeshAgent agent;
    ZombieAI ai;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        ai = GetComponent<ZombieAI>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        animator.SetFloat("Speed", agent.velocity.magnitude, 0.1f, Time.deltaTime);
        animator.SetBool("Chasing", ai.IsChasing);
    }

    public void PlayAttack()
    {
        animator.SetTrigger("Attack");
    }
}