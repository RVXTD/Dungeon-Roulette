using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState { Patrol, Chase, Attack }

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class EnemyScript : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    [Header("Sensing")]
    [Tooltip("Begin chasing when the player is within this range. Outside of this, go back to patrol immediately.")]
    public float detectionRange = 12f;

    [Header("Ranges")]
    [Tooltip("How close we must be to start attacking.")]
    public float attackRange = 2f;
    [Tooltip("Random patrol radius around the current position.")]
    public float patrolRadius = 15f;

    [Header("Speeds")]
    [Tooltip("NavMeshAgent speed while patrolling.")]
    public float patrolSpeed = 1.8f;
    [Tooltip("NavMeshAgent speed while chasing.")]
    public float chaseSpeed = 3.6f;

    [Header("Patrol Behavior")]
    [Tooltip("How long to pause at each patrol point before picking a new one.")]
    public float patrolPauseTime = 1.5f;

    [Header("Combat")]
    public float attackDamage = 10f;
    public float timeBetweenAttacks = 1.5f;

    [Header("Death Settings")]
    public float lifetime = 15f;
    public float deathAnimationLength = 3f;
    public float fadeDuration = 2f;

    // internals
    private bool isDead = false;
    private bool isPausing = false;
    private float pauseTimer = 0f;

    private NavMeshAgent agent;
    private Animator anim;
    private EnemyState currentState = EnemyState.Patrol;

    private Vector3 patrolPoint;
    private float lastAttackTime;

    // animator hashes
    private static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int AttackTrigger = Animator.StringToHash("Attack");
    private static readonly int DieTrigger = Animator.StringToHash("Die");

    // renderers/materials for fade on death
    private List<(Renderer rend, Material[] mats)> rendererMats;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>(true);
        if (!anim) Debug.LogError("EnemyScript: No Animator found on enemy.");
    }

    void Start()
    {
        agent.stoppingDistance = Mathf.Max(0f, attackRange - 0.2f);
        EnterPatrol();                  // ensures agent is moving & has a destination
        StartCoroutine(DieAfterTime(lifetime));
    }

    void Update()
    {
        if (isDead || !player)
        {
            if (isDead && anim) anim.SetFloat(MoveSpeed, 0f);
            return;
        }

        if (anim && anim.runtimeAnimatorController)
            anim.SetFloat(MoveSpeed, agent.velocity.magnitude);

        float dist = Vector3.Distance(transform.position, player.position);

        // --- State transitions (NO lose-sight buffer) ---
        if (currentState == EnemyState.Patrol && dist <= detectionRange)
        {
            EnterChase();
        }
        else if ((currentState == EnemyState.Chase || currentState == EnemyState.Attack) && dist > detectionRange)
        {
            // Player left detection range -> go straight back to patrol
            EnterPatrol();
        }

        // --- State behavior ---
        switch (currentState)
        {
            case EnemyState.Patrol:
                PatrolTick();
                break;
            case EnemyState.Chase:
                ChaseTick(dist);
                break;
            case EnemyState.Attack:
                AttackTick(dist);
                break;
        }
    }

    // =========================
    // STATE ENTER HELPERS
    // =========================
    private void EnterPatrol()
    {
        currentState = EnemyState.Patrol;

        // reset flags that could cause idling
        isPausing = false;
        pauseTimer = 0f;
        agent.isStopped = false;            // <- critical to avoid getting stuck idle
        agent.speed = patrolSpeed;

        SetNextPatrolPoint(true);
    }

    private void EnterChase()
    {
        currentState = EnemyState.Chase;
        isPausing = false;
        pauseTimer = 0f;
        agent.isStopped = false;            // ensure we resume movement
        agent.speed = chaseSpeed;
    }

    private void EnterAttack()
    {
        currentState = EnemyState.Attack;
        agent.isStopped = true;             // stand still while attacking
        agent.velocity = Vector3.zero;
        agent.ResetPath();
    }

    // =========================
    // PATROL
    // =========================
    private void PatrolTick()
    {
        if (isPausing)
        {
            // stay idle for a bit
            pauseTimer += Time.deltaTime;
            if (pauseTimer >= patrolPauseTime)
            {
                isPausing = false;
                pauseTimer = 0f;
                agent.isStopped = false; // resume moving
                SetNextPatrolPoint(false);
            }
            return;
        }

        // ensure we actually have somewhere to go
        if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.05f)
        {
            // reached point -> start a short idle pause, then pick next
            agent.isStopped = true;
            isPausing = true;
            pauseTimer = 0f;
            return;
        }
    }

    private void SetNextPatrolPoint(bool forceNewCenter)
    {
        // Optionally recenter the random area after a chase to avoid picking current pos again
        Vector3 center = forceNewCenter ? transform.position : patrolPoint;

        // Try multiple samples for a good point on the NavMesh
        for (int i = 0; i < 10; i++)
        {
            Vector3 candidate = center + Random.insideUnitSphere * patrolRadius;
            if (NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas))
            {
                patrolPoint = hit.position;
                agent.isStopped = false; // make sure we're moving
                agent.SetDestination(patrolPoint);
                return;
            }
        }

        // fallback: nudge forward a bit so we don't sit exactly on our current position
        Vector3 forward = transform.forward;
        if (NavMesh.SamplePosition(transform.position + forward * 2f, out var hit2, 2f, NavMesh.AllAreas))
        {
            patrolPoint = hit2.position;
            agent.isStopped = false;
            agent.SetDestination(patrolPoint);
        }
    }

    // =========================
    // CHASE
    // =========================
    private void ChaseTick(float distanceToPlayer)
    {
        agent.isStopped = false;        // just in case we came from an idle pause
        agent.speed = chaseSpeed;
        agent.SetDestination(player.position);

        if (distanceToPlayer <= attackRange)
        {
            EnterAttack();
        }
    }

    // =========================
    // ATTACK
    // =========================
    private void AttackTick(float distanceToPlayer)
    {
        // Face the player smoothly
        Vector3 look = player.position - transform.position; look.y = 0f;
        if (look != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 10f * Time.deltaTime);

        if (Time.time > lastAttackTime + timeBetweenAttacks)
        {
            if (anim) anim.SetTrigger(AttackTrigger);
            lastAttackTime = Time.time;
            // TODO: apply damage via hitbox or player health script
        }

        // out of range? go back to chase
        if (distanceToPlayer > attackRange)
        {
            EnterChase();
        }
    }

    // =========================
    // DEATH / FADE
    // =========================
    private IEnumerator DieAfterTime(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        DoDeath();
    }

    public void DoDeath()
    {
        if (isDead) return;
        isDead = true;

        if (agent)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
            agent.enabled = false;
        }

        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        if (anim) anim.SetTrigger(DieTrigger);

        CacheRendererMaterialsForFade();
        StartCoroutine(FadeOutAndDestroy());
    }

    private void CacheRendererMaterialsForFade()
    {
        rendererMats = new List<(Renderer, Material[])>();
        foreach (var rend in GetComponentsInChildren<Renderer>(true))
        {
            var mats = rend.materials; // instanced
            rendererMats.Add((rend, mats));

            foreach (var m in mats)
            {
                if (!m) continue;
                if (m.HasProperty("_Color"))
                {
                    var c = m.GetColor("_Color"); c.a = 1f; m.SetColor("_Color", c);
                }
                if (m.HasProperty("_BaseColor"))
                {
                    var bc = m.GetColor("_BaseColor"); bc.a = 1f; m.SetColor("_BaseColor", bc);
                }
                SetMaterialToFade(m);
            }
        }
    }

    private IEnumerator FadeOutAndDestroy()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, deathAnimationLength * 0.5f));

        float t = 0f;
        var start = new List<(Material m, bool hasColor, Color c, bool hasBase, Color bc)>();
        foreach (var pair in rendererMats)
        {
            foreach (var m in pair.mats)
            {
                bool hc = m.HasProperty("_Color");
                bool hb = m.HasProperty("_BaseColor");
                start.Add((m, hc, hc ? m.GetColor("_Color") : Color.white,
                              hb, hb ? m.GetColor("_BaseColor") : Color.white));
            }
        }

        while (t < fadeDuration)
        {
            float a = Mathf.Lerp(1f, 0f, t / fadeDuration);
            foreach (var s in start)
            {
                if (s.hasColor) { var c = s.c; c.a = a; s.m.SetColor("_Color", c); }
                if (s.hasBase) { var bc = s.bc; bc.a = a; s.m.SetColor("_BaseColor", bc); }
            }
            t += Time.deltaTime;
            yield return null;
        }

        foreach (var s in start)
        {
            if (s.hasColor) { var c = s.c; c.a = 0f; s.m.SetColor("_Color", c); }
            if (s.hasBase) { var bc = s.bc; bc.a = 0f; s.m.SetColor("_BaseColor", bc); }
        }

        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }

    private static void SetMaterialToFade(Material mat)
    {
        if (!mat) return;

        // Built-in Standard
        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 2f); // Fade
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        // URP/HDRP Lit
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f); // Transparent
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
