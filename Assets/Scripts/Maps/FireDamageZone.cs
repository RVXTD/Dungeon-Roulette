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

    private Collider triggerCollider;
    private ParticleSystem[] fireParticles;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        fireParticles = GetComponentsInChildren<ParticleSystem>();
    }

    private void Start()
    {
        isActive = startActive;
        ApplyActiveState();

        // start the on/off loop
        StartCoroutine(FireCycle());
    }

    private IEnumerator FireCycle()
    {
        while (true)
        {
            if (isActive)
            {
                // stay ON
                yield return new WaitForSeconds(activeDuration);
                isActive = false;
            }
            else
            {
                // stay OFF
                yield return new WaitForSeconds(inactiveDuration);
                isActive = true;
            }

            ApplyActiveState();
        }
    }

    private void ApplyActiveState()
    {
        // Enable/disable collider so no damage when off
        if (triggerCollider != null)
            triggerCollider.enabled = isActive;

        // Play/stop particle systems
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
        // extra safety: if somehow called while off, do nothing
        if (!isActive) return;

        // --- PLAYER DAMAGE ---
        if (other.TryGetComponent<PlayerHealth>(out var player))
        {
            player.TakeDamage(playerDamagePerSecond * Time.deltaTime);
            return;
        }

        // --- ENEMY DAMAGE ---
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
