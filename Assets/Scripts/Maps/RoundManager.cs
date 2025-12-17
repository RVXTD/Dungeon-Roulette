using System.Collections;
using UnityEngine;
using TMPro;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Header("UI")]
    public GameObject roundPanel;
    public TMP_Text roundText;
    public float messageTime = 1.5f;

    [Header("Refs")]
    public Generator3D generator;
    public EnemyRoomSpawner enemySpawner;
    public Transform player;

    [Header("Player Spawn")]
    public float playerSpawnHeight = 1.2f;

    [Header("Rounds")]
    public int maxRounds = 3;

    private int currentRound = 1;
    private int aliveEnemies = 0;
    private bool roundEnding = false;

    private PlayerHealth playerHealth;
    private SimplePlayerController playerController;

    private SimplePlayerController.AbilityType lastAbility = (SimplePlayerController.AbilityType)(-1);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (roundPanel != null)
            roundPanel.SetActive(false);
    }

    private IEnumerator Start()
    {
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            playerController = player.GetComponent<SimplePlayerController>();
        }

        // Round 1 setup
        playerHealth?.RestoreFullHealth();

        if (playerController != null)
        {
            var a = GetRandomAbilityNoRepeat();
            playerController.SetAbility(a);
            lastAbility = a;
        }

        // ? Tell spawner what round it is BEFORE first spawn (if your spawner auto-spawns, this still helps)
        if (enemySpawner != null)
            enemySpawner.ConfigureForRound(currentRound);

        yield return ShowMessage(GetRoundTitle(currentRound));
    }

    public void RegisterEnemy()
    {
        aliveEnemies++;
    }

    public void UnregisterEnemy()
    {
        aliveEnemies = Mathf.Max(0, aliveEnemies - 1);

        if (aliveEnemies == 0 && !roundEnding)
            StartCoroutine(NextRoundFlow());
    }

    private IEnumerator NextRoundFlow()
    {
        roundEnding = true;

        if (currentRound >= maxRounds)
        {
            yield return ShowMessage("YOU WIN!");
            yield break;
        }

        yield return ShowMessage("ROUND CLEARED!");

        currentRound++;

        // Rebuild dungeon
        if (generator != null)
        {
            generator.ClearDungeon();
            yield return null;
            generator.GenerateDungeon();
        }

        // Wait until rooms exist
        yield return new WaitUntil(() =>
            generator != null &&
            generator.roomCenters != null &&
            generator.roomCenters.Count > 0);

        // Teleport player
        if (player != null)
        {
            Vector3 spawn = generator.roomCenters[0] + Vector3.up * playerSpawnHeight;
            player.position = spawn;
        }

        // Full heal at start of new round
        playerHealth?.RestoreFullHealth();

        // Give new random ability (no repeat)
        if (playerController != null)
        {
            var a = GetRandomAbilityNoRepeat();
            playerController.SetAbility(a);
            lastAbility = a;
        }

        // Reset enemy count BEFORE spawning
        aliveEnemies = 0;

        // ? Configure difficulty for this round BEFORE spawning
        if (enemySpawner != null)
            enemySpawner.ConfigureForRound(currentRound);

        // Spawn enemies
        if (enemySpawner != null)
            yield return StartCoroutine(enemySpawner.SpawnEnemiesWhenReady());

        // Show round title
        yield return ShowMessage(GetRoundTitle(currentRound));

        roundEnding = false;
    }

    private SimplePlayerController.AbilityType GetRandomAbilityNoRepeat()
    {
        int r = Random.Range(0, 4);
        var ability = (SimplePlayerController.AbilityType)r;

        if ((int)lastAbility != -1 && ability == lastAbility)
        {
            r = (r + 1 + Random.Range(0, 3)) % 4; // shift by 1..3
            ability = (SimplePlayerController.AbilityType)r;
        }

        return ability;
    }

    private IEnumerator ShowMessage(string msg)
    {
        if (roundPanel == null || roundText == null)
        {
            Debug.Log(msg);
            yield return new WaitForSeconds(messageTime);
            yield break;
        }

        roundText.text = msg;
        roundPanel.SetActive(true);
        yield return new WaitForSeconds(messageTime);
        roundPanel.SetActive(false);
    }

    private string GetRoundTitle(int round)
    {
        return round switch
        {
            1 => "ROUND ONE",
            2 => "ROUND TWO",
            3 => "ROUND THREE",
            _ => $"ROUND {round}"
        };
    }
}
