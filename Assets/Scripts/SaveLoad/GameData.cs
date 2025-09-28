using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    // Player's Location
    public string sceneName;
    public float[] playerPosition;

    // Manager Data
    public List<ItemData> inventoryItems;
    public List<ItemData> codexDocuments;
    public List<string> collectedObjectIDs_LIST;
    public List<string> puzzleStates_KEYS;
    public List<int> puzzleStates_VALUES;

    // NEW: BGM and Enemy State Data
    public string currentBGMName;
    public List<string> killedEnemyIDs_LIST;
    public bool useTankControls;

    // Save Metadata
    public string saveTimestamp;

    public GameData()
    {
        this.sceneName = "OutdoorScene";
        this.playerPosition = new float[] { 0, 0, 0 };
        this.inventoryItems = new List<ItemData>();
        this.codexDocuments = new List<ItemData>();
        this.collectedObjectIDs_LIST = new List<string>();
        this.puzzleStates_KEYS = new List<string>();
        this.puzzleStates_VALUES = new List<int>();

        // Initialize new fields
        this.currentBGMName = "";
        this.killedEnemyIDs_LIST = new List<string>();
        this.useTankControls = false;
    }
}
