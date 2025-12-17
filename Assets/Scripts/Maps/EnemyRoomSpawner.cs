using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyRoomSpawner : MonoBehaviour
{
    [Header("Refs")]
    public Generator3D generator;      // your dungeon generator
    public GameObject enemyPrefab;     // your enemy prefab

    [Header("Spawn Settings (Base)")]
    public int minEnemiesPerRoom = 0;
    public int maxEnemiesPerRoom = 3;

    [Tooltip("This gets overridden by ConfigureForRound().")]
    public int maxEnemiesTotal = 20;

    public float randomOffset = 0.4f;   // jitter so they don't stack
    public float raycastHeight = 20f;   // height we raycast from
    public float navMeshSearchRadius = 5f;

    [Header("Round Difficulty")]
    public int round1MaxEnemies = 10;
    public int round2MaxEnemies = 20;
    public int round3MaxEnemies = 30;

    [Header("Round Difficulty - Health")]
    public float round3MaxHealth = 150f; // <- set whatever you want (ex: 150)

    [Tooltip("Extra chase speed added in round 2+ (example: +0.5)")]
    public float round2ChaseSpeedBonus = 0.5f;

    [Tooltip("Extra chase speed added in round 3 (example: +1.0)")]
    public float round3ChaseSpeedBonus = 1.0f;

    [Tooltip("Override attack damage in round 3 (set to 10)")]
    public float round3AttackDamage = 10f;

    private int currentRound = 1;

    private void Awake()
    {
        if (generator == null)
            generator = FindObjectOfType<Generator3D>();
    }

    private IEnumerator Start()
    {
        yield return WaitForRoomCenters();
        yield return new WaitForSeconds(0.1f);
        SpawnEnemies();
    }

    // ? Call this from RoundManager before spawning
    public void ConfigureForRound(int round)
    {
        currentRound = Mathf.Max(1, round);

        switch (currentRound)
        {
            case 1:
                maxEnemiesTotal = round1MaxEnemies;
                break;
            case 2:
                maxEnemiesTotal = round2MaxEnemies;
                break;
            default: // 3+
                maxEnemiesTotal = round3MaxEnemies;
                break;
        }
    }

    public void SpawnEnemies()
    {
        if (generator == null)
        {
            Debug.LogError("EnemyRoomSpawner: generator reference is missing.");
            return;
        }

        if (enemyPrefab == null)
        {
            Debug.LogError("EnemyRoomSpawner: Assign enemyPrefab in the Inspector.");
            return;
        }

        List<Vector3> centers = generator.roomCenters;
        if (centers == null || centers.Count == 0)
        {
            Debug.LogWarning("EnemyRoomSpawner: No room centers found to spawn enemies.");
            return;
        }

        int spawned = 0;

        foreach (var center in centers)
        {
            if (spawned >= maxEnemiesTotal)
                break;

            int enemiesThisRoom = Random.Range(minEnemiesPerRoom, maxEnemiesPerRoom + 1);

            for (int i = 0; i < enemiesThisRoom && spawned < maxEnemiesTotal; i++)
            {
                Vector3 pos = center + new Vector3(
                    Random.Range(-randomOffset, randomOffset),
                    0f,
                    Random.Range(-randomOffset, randomOffset)
                );

                if (Physics.Raycast(pos + Vector3.up * raycastHeight,
                                    Vector3.down,
                                    out RaycastHit hit,
                                    raycastHeight + 5f,
                                    ~0,
                                    QueryTriggerInteraction.Ignore))
                {
                    pos = hit.point + Vector3.up * 0.1f;
                }
                else
                {
                    pos.y += 1f;
                }

                GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity);
                spawned++;

                // Snap onto NavMesh
                var agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, navMeshSearchRadius, NavMesh.AllAreas))
                    {
                        agent.Warp(navHit.position);
                        agent.isStopped = false;
                    }
                    else
                    {
                        Debug.LogWarning($"EnemyRoomSpawner: No NavMesh found near {pos}");
                    }
                }

                // ? Apply round difficulty to this enemy
                ApplyDifficultyToEnemy(enemy);
            }
        }

        Debug.Log($"EnemyRoomSpawner: Spawned {spawned} enemies total (Round {currentRound}).");
    }

    private void ApplyDifficultyToEnemy(GameObject enemy)
    {
        var es = enemy.GetComponent<EnemyScript>();
        if (es == null) return;

        if (currentRound == 2)
        {
            es.chaseSpeed += round2ChaseSpeedBonus;
        }
        else if (currentRound >= 3)
        {
            es.chaseSpeed += round3ChaseSpeedBonus;
            es.attackDamage = round3AttackDamage;

            // Round 3 health buff
            var eh = enemy.GetComponentInChildren<EnemyHealth>();
            if (eh != null)
            {
                eh.maxHealth = round3MaxHealth;
                eh.currentHealth = round3MaxHealth;
            }
        }
    }


    public IEnumerator SpawnEnemiesWhenReady(float extraDelay = 0.1f)
    {
        yield return WaitForRoomCenters();
        if (extraDelay > 0f) yield return new WaitForSeconds(extraDelay);
        SpawnEnemies();
    }

    private IEnumerator WaitForRoomCenters()
    {
        yield return new WaitUntil(() =>
            generator != null &&
            generator.roomCenters != null &&
            generator.roomCenters.Count > 0);
    }
}
