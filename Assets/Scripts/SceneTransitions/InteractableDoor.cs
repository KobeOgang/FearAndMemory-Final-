using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractableDoor : MonoBehaviour
{
    [Header("Door Configuration")]
    public string sceneToLoad;
    public string targetSpawnPointID;

    [Header("Audio Configuration")]
    [Tooltip("BGM to play in the target scene. Leave null to keep current BGM playing.")]
    public AudioClip sceneBGM;

    [Tooltip("SFX to play immediately when opening the door. Leave null for no sound.")]
    public AudioClip doorOpenSFX;

    [Header("Interaction UI")]
    public GameObject interactPrompt;

    private bool isPlayerNearby = false;

    private void Start()
    {
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    private void Update()
    {
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            if (doorOpenSFX != null && AudioManager.instance != null)
            {
                AudioManager.instance.PlayClip(doorOpenSFX);
            }

            if (SceneLoader.Instance != null)
            {
                if (sceneBGM != null && AudioManager.instance != null)
                {
                    AudioManager.instance.ChangeBGMWithFade(sceneBGM);
                }

                // Load the target scene
                SceneLoader.Instance.LoadScene(sceneToLoad, targetSpawnPointID);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            if (interactPrompt != null)
                interactPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            if (interactPrompt != null)
                interactPrompt.SetActive(false);
        }
    }
}
