using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TankControlsToggleHandler : MonoBehaviour
{
    private Toggle tankControlsToggle;

    private void Start()
    {
        tankControlsToggle = GetComponent<Toggle>();

        if (tankControlsToggle == null)
        {
            Debug.LogError("TankControlsToggleHandler: No Toggle component found!");
            return;
        }

        // Set initial state from settings
        UpdateToggleFromSettings();

        // Listen for toggle changes
        tankControlsToggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private void OnToggleChanged(bool isOn)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetUseTankControls(isOn);
        }
    }

    public void UpdateToggleFromSettings()
    {
        if (tankControlsToggle != null && SettingsManager.Instance != null)
        {
            // Temporarily remove listener to prevent recursive calls
            tankControlsToggle.onValueChanged.RemoveListener(OnToggleChanged);

            // Update toggle state
            tankControlsToggle.isOn = SettingsManager.Instance.GetUseTankControls();

            // Re-add listener
            tankControlsToggle.onValueChanged.AddListener(OnToggleChanged);
        }
    }

    private void OnDestroy()
    {
        if (tankControlsToggle != null)
        {
            tankControlsToggle.onValueChanged.RemoveListener(OnToggleChanged);
        }
    }
}
