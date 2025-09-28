using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

    [Header("Game Settings")]
    private bool useTankControls = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettingsFromPlayerPrefs();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    // === TANK CONTROLS SETTING ===
    public bool GetUseTankControls()
    {
        return useTankControls;
    }

    public void SetUseTankControls(bool value)
    {
        useTankControls = value;

        // Apply to PlayerController immediately
        ApplyTankControlsToPlayer();

        // Save to PlayerPrefs for immediate persistence
        PlayerPrefs.SetInt("UseTankControls", value ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log($"SettingsManager: Tank Controls set to {value}");
    }

    private void ApplyTankControlsToPlayer()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.useTankControls = useTankControls;
        }
    }

    // === SAVE/LOAD INTEGRATION ===
    public void ApplyLoadedData(GameData data)
    {
        useTankControls = data.useTankControls;
        ApplyTankControlsToPlayer();

        // Update UI toggles to match loaded settings
        UpdateUIToggles();
    }

    public void UpdateUIToggles()
    {
        // Find and update the tank controls toggle
        TankControlsToggleHandler toggleHandler = FindObjectOfType<TankControlsToggleHandler>();
        if (toggleHandler != null)
        {
            toggleHandler.UpdateToggleFromSettings();
        }
    }

    // === PLAYERPREFS BACKUP ===
    private void LoadSettingsFromPlayerPrefs()
    {
        useTankControls = PlayerPrefs.GetInt("UseTankControls", 0) == 1;
        ApplyTankControlsToPlayer();
    }

    // === PUBLIC ACCESS FOR SAVE SYSTEM ===
    public bool GetTankControlsSetting()
    {
        return useTankControls;
    }
}
