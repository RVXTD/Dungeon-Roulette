using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.XR;
public enum EnemyState { Patrol, Chase, Attack }

public class EnemyScript : MonoBehaviour
{
    public Transform player;
    public float detectionRange = 10f;
    public float attackRange = 2f;
    public float patrolRadius = 15f;
    public float patrolSpeed = 3f;
    public float chaseSpeed = 5f;
    public float timeBetweenAttacks = 1.5f;

    private NavMeshAgent agent;
    private EnemyState currentState = EnemyState.Patrol;
    private Vector3 patrolTarget;
    private float lastAttackTime;

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        SetNewPatrolPoint();
    }

    // Update is called once per frame
    void Update()
    {
        if (player == null)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        switch (currentState)
        {
            case EnemyState.Patrol:
                Patrol();
                if (distanceToPlayer < detectionRange)
                    currentState = EnemyState.Chase;
                break;

            case EnemyState.Chase:
                ChasePlayer(distanceToPlayer);
                break;

            case EnemyState.Attack:
                AttackPlayer(distanceToPlayer);
                break;
        }
    }
    private void Patrol()
    {
        agent.speed = patrolSpeed;
        agent.SetDestination(patrolTarget);

        // Reached patrol point → pick a new one
        if (!agent.pathPending && agent.remainingDistance < 1f)
            SetNewPatrolPoint();
    }

    private void ChasePlayer(float distanceToPlayer)
    {
        agent.speed = chaseSpeed;
        agent.SetDestination(player.position);

        if (distanceToPlayer <= attackRange)
        {
            currentState = EnemyState.Attack;
        }
        else if (distanceToPlayer > detectionRange * 1.5f)
        {
            currentState = EnemyState.Patrol;
            SetNewPatrolPoint();
        }
    }

    private void AttackPlayer(float distanceToPlayer)
    {
        agent.ResetPath(); // stop moving for attack animation
        transform.LookAt(player);

        if (Time.time > lastAttackTime + timeBetweenAttacks)
        {
            // Attack logic here (e.g., deal damage)
            Debug.Log($"{gameObject.name} attacks the player!");
            lastAttackTime = Time.time;
        }

        if (distanceToPlayer > attackRange)
        {
            currentState = EnemyState.Chase;
        }
    }
    private void SetNewPatrolPoint()
    {
        Vector3 randomDir = Random.insideUnitSphere * patrolRadius;
        randomDir += transform.position;

        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
        {
            patrolTarget = hit.position;
        }
    }


}
