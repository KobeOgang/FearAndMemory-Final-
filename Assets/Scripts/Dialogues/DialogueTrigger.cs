using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DialogueTrigger : MonoBehaviour
{
    [Header("Dialogue Data")]
    public DialogueData dialogueToPlay;

    [Header("Trigger Settings")]
    [Tooltip("If true, dialogue starts on enter. If false, requires E key press.")]
    public bool triggerOnEnter = false;

    [Header("Scene Transition")]
    [Tooltip("If true, automatically changes scene when dialogue finishes.")]
    public bool changeSceneAfterDialogue = false;

    [Tooltip("Name of the scene to load after dialogue completes.")]
    public string nextSceneName = "";

    [Tooltip("Delay in seconds before changing scene (optional).")]
    public float sceneChangeDelay = 1f;

    [Header("Interaction UI")]
    [Tooltip("Assign the 'Press E to talk' UI element here.")]
    public GameObject interactPrompt;

    private bool isPlayerNearby = false;
    private bool isTalked = false;
    private PersistentObjectID objectID;
    private bool isWaitingForSceneChange = false;

    private void Awake()
    {
        objectID = GetComponent<PersistentObjectID>();

        // Check the WorldStateManager on load
        if (objectID != null && WorldStateManager.Instance.IsObjectCollected(objectID.uniqueID))
        {
            // If this dialogue has already been triggered, mark it as talked.
            this.isTalked = true;
        }
    }

    private void Start()
    {
        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }
    }

    private void Update()
    {
        if (!triggerOnEnter && isPlayerNearby && !isTalked && Input.GetKeyDown(KeyCode.E))
        {
            StartTriggeredDialogue();
        }

        // Check if dialogue has finished and scene change is enabled
        if (changeSceneAfterDialogue && !isWaitingForSceneChange && isTalked && !DialogueManager.IsDialogueActive)
        {
            isWaitingForSceneChange = true;
            StartCoroutine(ChangeSceneAfterDelay());
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;

            if (!triggerOnEnter && !isTalked && interactPrompt != null)
            {
                interactPrompt.SetActive(true);
            }

            if (triggerOnEnter && !isTalked)
            {
                StartTriggeredDialogue();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;

            if (interactPrompt != null)
            {
                interactPrompt.SetActive(false);
            }
        }
    }

    private void StartTriggeredDialogue()
    {
        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }

        DialogueManager.Instance.StartDialogue(dialogueToPlay);
        isTalked = true;

        if (objectID != null)
        {
            WorldStateManager.Instance.RecordObjectAsCollected(objectID.uniqueID);
        }
    }

    private IEnumerator ChangeSceneAfterDelay()
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(sceneChangeDelay);

        // Validate scene name
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError($"DialogueTrigger on {gameObject.name}: Next scene name is empty!");
            yield break;
        }

        // Check if scene exists in build settings
        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.Log($"Loading scene: {nextSceneName}");
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogError($"DialogueTrigger on {gameObject.name}: Scene '{nextSceneName}' not found in build settings!");
        }
    }
}
