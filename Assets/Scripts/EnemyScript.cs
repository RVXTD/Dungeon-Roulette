using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.XR;
public enum EnemyState { Patrol, Chase, Attack }

public class EnemyScript : MonoBehaviour
{
    private CharacterController control;
    private Vector3 moveDirection;
    float baseMoveSpeed = 5f;
    float health = 10;
    public EnemyState currentState = EnemyState.Patrol;
    public Transform player;
    public float detectionRange = 10f;
    public float patrolSpeed = 3f;
    public float chaseSpeed = 5f;

    private NavMeshAgent agent;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    // Update is called once per frame
    void Update()
    {
        switch (currentState)
        {
            case EnemyState.Patrol:
                // Implement patrolling logic (e.g., move to random points)
                if (Vector3.Distance(transform.position, player.position) < detectionRange)
                {
                    currentState = EnemyState.Chase;
                }
                break;
            case EnemyState.Chase:
                agent.SetDestination(player.position);
                agent.speed = chaseSpeed;
                if (Vector3.Distance(transform.position, player.position) > detectionRange * 1.5f) // Lost player
                {
                    currentState = EnemyState.Patrol;
                }
                break;
        }
    }
}
