using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyFreezeController : MonoBehaviour
{
    public bool IsFrozen { get; private set; }

    private NavMeshAgent agent;
    private Animator anim;
    private EnemyScript enemyScript;
    private Coroutine freezeRoutine;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        enemyScript = GetComponent<EnemyScript>();
    }

    public void FreezeForDuration(float duration)
    {
        if (freezeRoutine != null)
            StopCoroutine(freezeRoutine);

        freezeRoutine = StartCoroutine(FreezeRoutine(duration));
    }

    private IEnumerator FreezeRoutine(float duration)
    {
        if (IsFrozen) yield break;
        IsFrozen = true;

        if (agent != null)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (enemyScript != null)
            enemyScript.enabled = false;

        if (anim != null)
            anim.speed = 0f;

        yield return new WaitForSeconds(duration);

        if (agent != null)
            agent.isStopped = false;

        if (enemyScript != null)
            enemyScript.enabled = true;

        if (anim != null)
            anim.speed = 1f;

        IsFrozen = false;
        freezeRoutine = null;
    }
}
