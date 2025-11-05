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
    public float detectionRange = 12f;

    [Header("Ranges")]
    public float attackRange = 2f;
    public float patrolRadius = 15f;

    [Header("Speeds")]
    public float patrolSpeed = 1.8f;
    public float chaseSpeed = 3.6f;

    [Header("Patrol Behavior")]
    public float patrolPauseTime = 1.5f;

    [Header("Combat")]
    public float attackDamage = 10f;
    public float timeBetweenAttacks = 1.2f;
    public float attackWindupTime = 0.4f;
    public float hitActiveTime = 0.3f;

    [Header("Death FX")]
    [Tooltip("Wait this long after Die trigger before fading (lets death anim start).")]
    public float fadeDelay = 0.4f;
    [Tooltip("How long the visual fade takes.")]
    public float fadeDuration = 1.2f;
    [Tooltip("Remove colliders during fade so it doesn't block player.")]
    public bool disableCollidersOnDeath = true;
    [Tooltip("Destroy the GameObject after fading.")]
    public bool destroyAfterFade = true;

    // internals
    private NavMeshAgent agent;
    private Animator anim;
    private EnemyState currentState = EnemyState.Patrol;

    private bool isDead = false;
    private bool isPausing = false;
    private float pauseTimer = 0f;
    private Vector3 patrolPoint;
    private float lastAttackTime;

    // animator hashes
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int AttackTrigger = Animator.StringToHash("Attack");
    private static readonly int DieTrigger = Animator.StringToHash("Die");

    // fade caches
    private Renderer[] _renderers;
    private readonly List<Material[]> _instancedMatsPerRenderer = new List<Material[]>();
    private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor"); // URP Lit
    private static readonly int ColorProp = Shader.PropertyToID("_Color");     // Standard

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

        if (!anim) Debug.LogError("EnemyScript: No Animator found.");
        if (!agent) Debug.LogError("EnemyScript: No NavMeshAgent found.");

        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    void Start()
    {
        agent.stoppingDistance = Mathf.Max(0f, attackRange - 0.2f);
        agent.updateRotation = false; // we rotate manually in Face()
        EnterPatrol();
    }

    void Update()
    {
        if (isDead || !player)
        {
            SetMoveSpeed(0f);
            return;
        }

        float dist = Vector3.Distance(transform.position, player.position);

        // state transitions
        if (currentState == EnemyState.Patrol && dist <= detectionRange)
            EnterChase();
        else if ((currentState == EnemyState.Chase || currentState == EnemyState.Attack) && dist > detectionRange)
            EnterPatrol();

        // state ticks
        switch (currentState)
        {
            case EnemyState.Patrol: PatrolTick(); break;
            case EnemyState.Chase: ChaseTick(dist); break;
            case EnemyState.Attack: AttackTick(dist); break;
        }

        SetMoveSpeed(agent.velocity.magnitude);
    }

    // -------------------
    // State enters
    // -------------------
    private void EnterPatrol()
    {
        currentState = EnemyState.Patrol;
        isPausing = false;
        pauseTimer = 0f;
        if (agent)
        {
            agent.isStopped = false;
            agent.speed = patrolSpeed;
        }
        SetNextPatrolPoint(true);
    }

    private void EnterChase()
    {
        currentState = EnemyState.Chase;
        isPausing = false;
        pauseTimer = 0f;
        if (agent)
        {
            agent.isStopped = false;
            agent.speed = chaseSpeed;
        }
    }

    private void EnterAttack()
    {
        currentState = EnemyState.Attack;
        if (agent)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }

        // allow an immediate first swing
        lastAttackTime = Time.time - timeBetweenAttacks;
    }

    // -------------------
    // Patrol
    // -------------------
    private void PatrolTick()
    {
        if (agent == null) return;

        if (isPausing)
        {
            pauseTimer += Time.deltaTime;
            if (pauseTimer >= patrolPauseTime)
            {
                isPausing = false;
                pauseTimer = 0f;
                agent.isStopped = false;
                SetNextPatrolPoint(false);
            }
            return;
        }

        if (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.05f)
        {
            agent.isStopped = true;
            isPausing = true;
            pauseTimer = 0f;
        }
        else
        {
            Face(agent.steeringTarget);
        }
    }

    private void SetNextPatrolPoint(bool recenter)
    {
        Vector3 center = recenter ? transform.position : patrolPoint;

        for (int i = 0; i < 10; i++)
        {
            Vector3 candidate = center + Random.insideUnitSphere * patrolRadius;
            if (NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas))
            {
                patrolPoint = hit.position;
                if (agent)
                {
                    agent.isStopped = false;
                    agent.SetDestination(patrolPoint);
                }
                return;
            }
        }

        if (NavMesh.SamplePosition(transform.position + transform.forward * 2f, out var hit2, 2f, NavMesh.AllAreas))
        {
            patrolPoint = hit2.position;
            if (agent)
            {
                agent.isStopped = false;
                agent.SetDestination(patrolPoint);
            }
        }
    }

    // -------------------
    // Chase
    // -------------------
    private void ChaseTick(float distanceToPlayer)
    {
        if (agent == null || player == null) return;

        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.SetDestination(player.position);
        Face(agent.steeringTarget);

        if (distanceToPlayer <= attackRange)
            EnterAttack();
    }

    // -------------------
    // Attack
    // -------------------
    private void AttackTick(float distanceToPlayer)
    {
        if (player == null) return;

        Face(player.position);
        TryAttack();
        if (distanceToPlayer > attackRange)
            EnterChase();
    }

    private void TryAttack()
    {
        if (Time.time >= lastAttackTime + timeBetweenAttacks)
        {
            lastAttackTime = Time.time;
            if (anim) anim.SetTrigger(AttackTrigger);
            StartCoroutine(AttackWindow());
        }
    }

    private IEnumerator AttackWindow()
    {
        // Wait for the animation to reach the hit frame
        yield return new WaitForSeconds(attackWindupTime);

        // Deal damage once during the swing
        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= attackRange + 0.5f)
            {
                var dmg = player.GetComponent<IDamageable>();
                if (dmg != null)
                {
                    dmg.TakeDamage(attackDamage);
                    Debug.Log($"{name} hit {player.name} for {attackDamage} damage!");
                }
            }
        }

        // Wait out the rest of the active time
        yield return new WaitForSeconds(hitActiveTime);
    }

    // -------------------
    // Death + Fade
    // -------------------
    public void DoDeath()
    {
        if (isDead) return;
        isDead = true;

        if (agent)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            agent.ResetPath();
        }

        if (anim) anim.SetTrigger(DieTrigger);

        // stop other behaviors
        currentState = EnemyState.Patrol;
        isPausing = false;

        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (disableCollidersOnDeath)
        {
            foreach (var col in GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }

        if (fadeDelay > 0f)
            yield return new WaitForSeconds(fadeDelay);

        PrepareInstancedTransparentMaterials();

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / fadeDuration));
            SetAlphaOnInstancedMaterials(a);
            yield return null;
        }
        SetAlphaOnInstancedMaterials(0f);

        if (destroyAfterFade) Destroy(gameObject);
        else gameObject.SetActive(false);
    }

    private void PrepareInstancedTransparentMaterials()
    {
        _instancedMatsPerRenderer.Clear();

        foreach (var r in _renderers)
        {
            if (!r || r.sharedMaterials == null || r.sharedMaterials.Length == 0) continue;

            var shared = r.sharedMaterials;
            var instanced = new Material[shared.Length];

            for (int i = 0; i < shared.Length; i++)
            {
                var m = shared[i];
                if (m == null) continue;

                var mi = new Material(m);

                // Built-in Standard: set Rendering Mode = Fade
                if (mi.HasProperty("_Mode"))
                {
                    mi.SetFloat("_Mode", 2f); // 0 Opaque, 1 Cutout, 2 Fade, 3 Transparent
                    mi.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mi.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mi.SetInt("_ZWrite", 0);
                    mi.DisableKeyword("_ALPHATEST_ON");
                    mi.EnableKeyword("_ALPHABLEND_ON");
                    mi.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mi.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }

                // URP Lit: Surface = Transparent
                if (mi.HasProperty("_Surface")) // 0 Opaque, 1 Transparent
                {
                    mi.SetFloat("_Surface", 1f);
                    mi.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mi.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }

                instanced[i] = mi;
            }

            r.materials = instanced; // assign instanced array so we don't edit shared assets
            _instancedMatsPerRenderer.Add(instanced);
        }
    }

    private void SetAlphaOnInstancedMaterials(float alpha)
    {
        foreach (var mats in _instancedMatsPerRenderer)
        {
            if (mats == null) continue;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;

                if (m.HasProperty(BaseColorProp))
                {
                    var c = m.GetColor(BaseColorProp);
                    c.a = alpha;
                    m.SetColor(BaseColorProp, c);
                }
                else if (m.HasProperty(ColorProp))
                {
                    var c = m.GetColor(ColorProp);
                    c.a = alpha;
                    m.SetColor(ColorProp, c);
                }
            }
        }
    }

    // -------------------
    // Helpers
    // -------------------
    private void Face(Vector3 worldTarget)
    {
        Vector3 dir = worldTarget - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 10f * Time.deltaTime);
        }
    }

    private void SetMoveSpeed(float v)
    {
        if (anim) anim.SetFloat(MoveSpeedHash, v);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
