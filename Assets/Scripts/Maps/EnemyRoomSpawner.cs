using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyRoomSpawner : MonoBehaviour
{
    [Header("Refs")]
    public Generator3D generator;      // your dungeon generator
    public GameObject enemyPrefab;     // your ork enemy prefab

    [Header("Spawn Settings")]
    public int enemiesPerRoom = 1;
    public int maxEnemies = 10;
    public float randomOffset = 0.4f;   // little jitter so they don't stack
    public float raycastHeight = 20f;   // height we raycast from

    void Awake()
    {
        if (generator == null)
            generator = FindObjectOfType<Generator3D>();
    }

    IEnumerator Start()
    {
        // Wait until the dungeon has created roomCenters
        yield return new WaitUntil(() =>
            generator != null &&
            generator.roomCenters != null &&
            generator.roomCenters.Count > 0);

        // tiny delay so SpawnManager can move the player if needed
        yield return new WaitForSeconds(0.1f);

        SpawnEnemies();
    }

    void SpawnEnemies()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("EnemyRoomSpawner: Assign enemyPrefab in the Inspector.");
            return;
        }

        List<Vector3> centers = generator.roomCenters;
        if (centers == null || centers.Count == 0) return;

        int spawned = 0;

        foreach (var center in centers)
        {
            if (spawned >= maxEnemies)
                break;

            for (int i = 0; i < enemiesPerRoom && spawned < maxEnemies; i++)
            {
                // random offset inside the room tile area
                Vector3 pos = center + new Vector3(
                    Random.Range(-randomOffset, randomOffset),
                    0f,
                    Random.Range(-randomOffset, randomOffset)
                );

                // Raycast down to find the floor
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

                // Try to snap the enemy onto the NavMesh
                var agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    NavMeshHit navHit;
                    float searchRadius = 5f;   // keep this fairly big

                    if (NavMesh.SamplePosition(pos, out navHit, searchRadius, NavMesh.AllAreas))
                    {
                        agent.Warp(navHit.position);
                        agent.isStopped = false;

                        // NEW: debug check
                        Debug.Log($"[Spawner] Warped enemy to {navHit.position}, isOnNavMesh={agent.isOnNavMesh}");
                    }
                    else
                    {
                        Debug.LogWarning($"EnemyRoomSpawner: No NavMesh found near {pos}");
                    }
                }
            }
        }

        Debug.Log($"EnemyRoomSpawner: Spawned {spawned} enemies.");
    }
}