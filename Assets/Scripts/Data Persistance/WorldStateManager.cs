using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldStateManager : MonoBehaviour
{
    public static WorldStateManager Instance;

    private HashSet<string> collectedObjectIDs = new HashSet<string>();
    private Dictionary<string, int> puzzleStates = new Dictionary<string, int>();
    private HashSet<string> killedEnemyIDs = new HashSet<string>();
    private string currentBGMName = "";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    // === COLLECTED OBJECTS ===
    public void RecordObjectAsCollected(string uniqueID)
    {
        if (!collectedObjectIDs.Contains(uniqueID))
        {
            collectedObjectIDs.Add(uniqueID);
        }
    }

    public bool IsObjectCollected(string uniqueID)
    {
        return collectedObjectIDs.Contains(uniqueID);
    }

    // === PUZZLE STATES ===
    public void RecordPuzzleState(string uniqueID, int state)
    {
        if (puzzleStates.ContainsKey(uniqueID))
        {
            puzzleStates[uniqueID] = state;
        }
        else
        {
            puzzleStates.Add(uniqueID, state);
        }
    }

    public bool GetPuzzleState(string uniqueID, out int state)
    {
        return puzzleStates.TryGetValue(uniqueID, out state);
    }

    // === KILLED ENEMIES ===
    public void RecordEnemyAsKilled(string uniqueID)
    {
        if (!killedEnemyIDs.Contains(uniqueID))
        {
            killedEnemyIDs.Add(uniqueID);
            Debug.Log($"WorldStateManager: Recorded enemy {uniqueID} as killed");
        }
    }

    public bool IsEnemyKilled(string uniqueID)
    {
        return killedEnemyIDs.Contains(uniqueID);
    }

    // === BGM STATE ===
    public void SetCurrentBGM(string bgmName)
    {
        currentBGMName = bgmName;
        Debug.Log($"WorldStateManager: Current BGM set to '{bgmName}'");
    }

    public string GetCurrentBGM()
    {
        return currentBGMName;
    }

    public void UpdateBGMFromAudioManager()
    {
        if (AudioManager.instance != null && AudioManager.instance.musicSource != null)
        {
            AudioClip currentClip = AudioManager.instance.musicSource.clip;
            if (currentClip != null)
            {
                currentBGMName = currentClip.name;
            }
            else
            {
                currentBGMName = "";
            }
        }
    }

    // === DATA ACCESS METHODS ===
    public HashSet<string> GetAllCollectedIDs()
    {
        return collectedObjectIDs;
    }

    public Dictionary<string, int> GetAllPuzzleStates()
    {
        return puzzleStates;
    }

    public HashSet<string> GetAllKilledEnemyIDs()
    {
        return killedEnemyIDs;
    }

    // === LOAD DATA APPLICATION ===
    public void ApplyLoadedData(GameData data)
    {
        // Apply existing data
        this.collectedObjectIDs = new HashSet<string>(data.collectedObjectIDs_LIST);

        this.puzzleStates = new Dictionary<string, int>();
        for (int i = 0; i < data.puzzleStates_KEYS.Count; i++)
        {
            this.puzzleStates.Add(data.puzzleStates_KEYS[i], data.puzzleStates_VALUES[i]);
        }

        // Apply new BGM data
        if (!string.IsNullOrEmpty(data.currentBGMName))
        {
            this.currentBGMName = data.currentBGMName;

            // Restore BGM in AudioManager if possible
            RestoreBGMInAudioManager(data.currentBGMName);
        }

        // Apply new killed enemies data
        if (data.killedEnemyIDs_LIST != null)
        {
            this.killedEnemyIDs = new HashSet<string>(data.killedEnemyIDs_LIST);

            // Apply enemy death states to current scene
            ApplyEnemyDeathStates();
        }

        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.ApplyLoadedData(data);
        }
    }

    private void RestoreBGMInAudioManager(string bgmName)
    {
        if (AudioManager.instance == null) return;

        // Try to find and play the BGM by name
        AudioClip targetClip = null;

        // Check common BGM clips in AudioManager
        if (AudioManager.instance.mainMenuMusic != null && AudioManager.instance.mainMenuMusic.name == bgmName)
            targetClip = AudioManager.instance.mainMenuMusic;
        else if (AudioManager.instance.gameBGM != null && AudioManager.instance.gameBGM.name == bgmName)
            targetClip = AudioManager.instance.gameBGM;
        else if (AudioManager.instance.gameOverScreenMusic != null && AudioManager.instance.gameOverScreenMusic.name == bgmName)
            targetClip = AudioManager.instance.gameOverScreenMusic;

        // If we found the clip, play it
        if (targetClip != null)
        {
            AudioManager.instance.ChangeBGMWithFade(targetClip);
            Debug.Log($"WorldStateManager: Restored BGM '{bgmName}' from save data");
        }
        else
        {
            Debug.LogWarning($"WorldStateManager: Could not find BGM clip named '{bgmName}' to restore");
        }
    }

    private void ApplyEnemyDeathStates()
    {
        // Find all enemies in the current scene and check if they should be dead
        EnemyRagdollDeath[] allEnemies = FindObjectsOfType<EnemyRagdollDeath>();

        foreach (EnemyRagdollDeath enemy in allEnemies)
        {
            // Get unique ID for this enemy (you may need to add this to EnemyRagdollDeath)
            string enemyID = GetEnemyUniqueID(enemy);

            if (IsEnemyKilled(enemyID))
            {
                // Kill this enemy immediately without effects
                enemy.gameObject.SetActive(false);
                Debug.Log($"WorldStateManager: Deactivated killed enemy '{enemyID}'");
            }
        }
    }

    private string GetEnemyUniqueID(EnemyRagdollDeath enemy)
    {
        // Create a unique ID based on scene name and enemy position/name
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Vector3 pos = enemy.transform.position;
        return $"{sceneName}_{enemy.name}_{pos.x:F1}_{pos.y:F1}_{pos.z:F1}";
    }

    // === ENEMY DEATH HELPER FOR OTHER SCRIPTS ===
    public void OnEnemyDeath(GameObject enemyObject)
    {
        // Get the EnemyRagdollDeath component
        EnemyRagdollDeath enemyScript = enemyObject.GetComponent<EnemyRagdollDeath>();
        if (enemyScript != null)
        {
            string enemyID = GetEnemyUniqueID(enemyScript);
            RecordEnemyAsKilled(enemyID);
        }
        else
        {
            // Fallback ID generation if no EnemyRagdollDeath component
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Vector3 pos = enemyObject.transform.position;
            string enemyID = $"{sceneName}_{enemyObject.name}_{pos.x:F1}_{pos.y:F1}_{pos.z:F1}";
            RecordEnemyAsKilled(enemyID);
        }
    }

    // === DEBUG METHODS ===
    [ContextMenu("Debug Print Stored Data")]
    public void DebugPrintStoredData()
    {
        Debug.Log("--- WorldStateManager Live Data ---");
        Debug.Log($"Current BGM: '{currentBGMName}'");

        Debug.Log($"There are {collectedObjectIDs.Count} collected IDs:");
        foreach (string id in collectedObjectIDs)
        {
            Debug.Log("- " + id);
        }

        Debug.Log($"There are {puzzleStates.Count} puzzle states:");
        foreach (var puzzle in puzzleStates)
        {
            Debug.Log($"- ID: {puzzle.Key}, State: {puzzle.Value}");
        }

        Debug.Log($"There are {killedEnemyIDs.Count} killed enemies:");
        foreach (string id in killedEnemyIDs)
        {
            Debug.Log("- " + id);
        }
        Debug.Log("---------------------------------");
    }

    [ContextMenu("Update BGM From AudioManager")]
    public void DebugUpdateBGM()
    {
        UpdateBGMFromAudioManager();
        Debug.Log($"Updated BGM from AudioManager: '{currentBGMName}'");
    }
}
