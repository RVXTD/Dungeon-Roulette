using System.Collections;
using System.Linq;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [Header("Refs")]
    public Generator3D generator;   // auto-found if not assigned
    public Transform player;        // drag your Player here
    public Transform spawnMarker;   // optional manual spawn override

    [Header("Mode")]
    public bool preferMarker = false;

    public enum AutoMode { FirstRoom, RandomRoom, FarthestPairStart }
    public AutoMode autoMode = AutoMode.RandomRoom;

    void Awake()
    {
        if (generator == null)
            generator = FindObjectOfType<Generator3D>();
    }

    IEnumerator Start()
    {
        // Wait until the generator has produced rooms
        yield return new WaitUntil(() =>
            generator != null &&
            generator.roomCenters != null &&
            generator.roomCenters.Count > 0);

        Spawn();
    }

    public void Spawn()
    {
        if (player == null)
        {
            Debug.LogError("SpawnManager: Assign the Player transform in the Inspector.");
            return;
        }

        if (generator == null || generator.roomCenters == null || generator.roomCenters.Count == 0)
        {
            Debug.LogError("SpawnManager: Generator or room centers not ready.");
            return;
        }

        // --- pick spawn position ---
        Vector3 spawnPos;

        if (preferMarker && spawnMarker != null)
        {
            spawnPos = SnapToGrid(spawnMarker.position);
        }
        else
        {
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

        // --- raycast down to hit floor ---
        if (Physics.Raycast(spawnPos + Vector3.up * 20f,
                            Vector3.down,
                            out var hit,
                            100f,
                            ~0,
                            QueryTriggerInteraction.Ignore))
        {
            spawnPos = hit.point + Vector3.up * 0.1f;
        }
        else
        {
            spawnPos.y += 1f;
        }

        // --- move player safely (for CharacterController) ---
        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;

        player.SetPositionAndRotation(spawnPos, Quaternion.identity);

        if (cc) cc.enabled = true;

        Debug.Log($"SpawnManager: Moved Player to {spawnPos}");
    }

    static Vector3 SnapToGrid(Vector3 p) =>
        new Vector3(Mathf.Round(p.x) + 0.5f, 0f, Mathf.Round(p.z) + 0.5f);

    static Vector3 FindFarthestRoom(System.Collections.Generic.List<Vector3> centers)
    {
        if (centers == null || centers.Count == 0)
            return Vector3.zero;

        float best = -1f;
        Vector3 bestCenter = centers[0];

        for (int i = 0; i < centers.Count; i++)
        {
            for (int j = i + 1; j < centers.Count; j++)
            {
                float d2 = (new Vector2(centers[i].x, centers[i].z) -
                            new Vector2(centers[j].x, centers[j].z)).sqrMagnitude;

                if (d2 > best)
                {
                    best = d2;
                    bestCenter = centers[i];
                }
            }
        }

        return bestCenter;
    }
}