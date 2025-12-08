using UnityEngine;
using System.Collections;

public class FireDamageZone : MonoBehaviour
{
    [Header("Damage Settings")]
    public float playerDamagePerSecond = 15f;
    public float enemyDamagePerSecond = 40f;

    [Header("Timing Settings")]
    public bool startActive = true;
    public float activeDuration = 25f;   // fire ON time
    public float inactiveDuration = 15f; // fire OFF time

    private bool isActive = true;

    [Header("Internal References")]
    [SerializeField] private Collider damageTriggerCollider;   // <-- trigger only
    private ParticleSystem[] fireParticles;

    private void Awake()
    {
        // If not assigned in Inspector, try to find a trigger collider on this object
        if (damageTriggerCollider == null)
        {
            foreach (var col in GetComponents<Collider>())
            {
                if (col.isTrigger)
                {
                    damageTriggerCollider = col;
                    break;
                }
            }
        }

        fireParticles = GetComponentsInChildren<ParticleSystem>();
    }

    private void Start()
    {
        isActive = startActive;
        ApplyActiveState();

        StartCoroutine(FireCycle());
    }

    private IEnumerator FireCycle()
    {
        while (true)
        {
            if (isActive)
            {
                yield return new WaitForSeconds(activeDuration);
                isActive = false;
            }
            else
            {
                yield return new WaitForSeconds(inactiveDuration);
                isActive = true;
            }

            ApplyActiveState();
        }
    }

    private void ApplyActiveState()
    {
        // Only toggle the trigger collider, NOT the floor collider
        if (damageTriggerCollider != null)
            damageTriggerCollider.enabled = isActive;

        if (fireParticles != null)
        {
            foreach (var ps in fireParticles)
            {
                if (ps == null) continue;

                if (isActive && !ps.isPlaying)
                    ps.Play();
                else if (!isActive && ps.isPlaying)
                    ps.Stop();
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isActive) return;

        if (other.TryGetComponent<PlayerHealth>(out var player))
        {
            player.TakeDamage(playerDamagePerSecond * Time.deltaTime);
            return;
        }

        if (other.TryGetComponent<EnemyHealth>(out var enemy))
        {
            enemy.TakeDamage(enemyDamagePerSecond * Time.deltaTime);
            return;
        }

        var enemyParent = other.GetComponentInParent<EnemyHealth>();
        if (enemyParent != null)
        {
            enemyParent.TakeDamage(enemyDamagePerSecond * Time.deltaTime);
        }
    }
}
