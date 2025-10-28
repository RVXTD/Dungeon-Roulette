using System.Linq;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [Header("Refs")]
    public Generator3D generator;   // drag your Generator3D here
    public Transform playerPrefab;  // drag the Player prefab here
    public Transform spawnMarker;   // optional: a visible sphere you can move

    [Header("Mode")]
    public bool preferMarker = true;         // if true and marker exists, use it
    public enum AutoMode { FirstRoom, RandomRoom, FarthestPairStart }
    public AutoMode autoMode = AutoMode.FirstRoom;

    GameObject playerInstance;

    void Start()
    {
        // Important: make sure this runs AFTER generation finishes.
        // If your generator runs in Start(), set Script Execution Order so
        // Generator3D executes before SpawnManager; or call SpawnManager.Spawn() from generator once done.
        Spawn();
    }

    public void Spawn()
    {
        if (playerPrefab == null) { Debug.LogWarning("SpawnManager: No playerPrefab."); return; }
        if (generator == null) { Debug.LogError("SpawnManager: No Generator3D reference."); return; }
        if (generator.roomCenters == null || generator.roomCenters.Count == 0)
        {
            Debug.LogError("SpawnManager: No room centers found. Did generation run?");
            return;
        }

        Vector3 spawnPos;
        if (preferMarker && spawnMarker != null)
        {
            spawnPos = SnapToGrid(spawnMarker.position);
        }
        else
        {
            // pick room center based on mode
            Vector3 target = generator.roomCenters[0];

            switch (autoMode)
            {
                case AutoMode.RandomRoom:
                    target = generator.roomCenters[Random.Range(0, generator.roomCenters.Count)];
                    break;
                case AutoMode.FarthestPairStart:
                    target = FindFarthestRoom(generator.roomCenters);
                    break;
            }

            spawnPos = target;
        }

        spawnPos.y += 1f; // lift off floor a bit
        if (playerInstance != null) Destroy(playerInstance);
        playerInstance = Instantiate(playerPrefab.gameObject, spawnPos, Quaternion.identity);
        Debug.Log($"Spawned player at {spawnPos}");
    }

    static Vector3 SnapToGrid(Vector3 p)
    {
        // If you have a grid cell size other than 1, adjust here.
        return new Vector3(Mathf.Round(p.x) + 0.5f, 0f, Mathf.Round(p.z) + 0.5f);
    }

    static Vector3 FindFarthestRoom(System.Collections.Generic.List<Vector3> centers)
    {
        float best = -1f; Vector3 aBest = centers[0], bBest = centers[0];
        for (int i = 0; i < centers.Count; i++)
            for (int j = i + 1; j < centers.Count; j++)
            {
                float d2 = (new Vector2(centers[i].x, centers[i].z) - new Vector2(centers[j].x, centers[j].z)).sqrMagnitude;
                if (d2 > best) { best = d2; aBest = centers[i]; bBest = centers[j]; }
            }
        // Use one end as the start point
        return aBest;
    }
}
