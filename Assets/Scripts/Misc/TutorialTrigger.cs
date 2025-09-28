using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialTrigger : MonoBehaviour
{
    [Header("Tutorial UI Settings")]
    [Tooltip("The UI GameObject (Image with Text child) to fade in/out")]
    public GameObject tutorialUI;

    [Header("Animation Settings")]
    [Tooltip("Time it takes to fade in (in seconds)")]
    public float fadeInDuration = 0.5f;

    [Tooltip("How long the tutorial stays visible (in seconds)")]
    public float displayDuration = 3f;

    [Tooltip("Time it takes to fade out (in seconds)")]
    public float fadeOutDuration = 0.5f;

    [Header("Trigger Settings")]
    [Tooltip("Initial delay before showing tutorial (in seconds)")]
    public float activationDelay = 0f;

    [Tooltip("Should the trigger be destroyed after activation?")]
    public bool destroyTriggerAfterActivation = true;

    [Header("Debug")]
    [Tooltip("Show debug messages in console")]
    public bool showDebugMessages = true;

    private bool hasTriggered = false;
    private CanvasGroup canvasGroup;

    private void Start()
    {
        // Ensure this GameObject has a trigger collider
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            Debug.LogError("TutorialTrigger: No Collider found! Please add a Collider component and set it as trigger.");
            return;
        }

        if (!triggerCollider.isTrigger)
        {
            triggerCollider.isTrigger = true;
            if (showDebugMessages)
                Debug.Log("TutorialTrigger: Automatically set collider as trigger.");
        }

        // Validate tutorial UI reference
        if (tutorialUI == null)
        {
            Debug.LogError("TutorialTrigger: Tutorial UI is not assigned!");
            return;
        }

        // Get or add CanvasGroup component for fading
        canvasGroup = tutorialUI.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = tutorialUI.AddComponent<CanvasGroup>();
            if (showDebugMessages)
                Debug.Log("TutorialTrigger: Added CanvasGroup component to tutorial UI.");
        }

        // Ensure UI starts invisible
        canvasGroup.alpha = 0f;
        tutorialUI.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if it's the player and we haven't triggered yet
        if (!hasTriggered && other.CompareTag("Player"))
        {
            hasTriggered = true;

            if (showDebugMessages)
                Debug.Log($"TutorialTrigger: Player entered trigger zone. Showing tutorial UI.");

            if (activationDelay > 0f)
            {
                Invoke(nameof(ShowTutorial), activationDelay);
            }
            else
            {
                ShowTutorial();
            }
        }
    }

    private void ShowTutorial()
    {
        if (tutorialUI != null && canvasGroup != null)
        {
            StartCoroutine(TutorialSequence());
        }
    }

    private IEnumerator TutorialSequence()
    {
        // Activate UI and start fade in
        tutorialUI.SetActive(true);

        if (showDebugMessages)
            Debug.Log("TutorialTrigger: Starting tutorial fade in.");

        // Fade in
        yield return StartCoroutine(FadeCanvasGroup(canvasGroup, 0f, 1f, fadeInDuration));

        if (showDebugMessages)
            Debug.Log("TutorialTrigger: Tutorial displayed, waiting for display duration.");

        // Wait for display duration
        yield return new WaitForSeconds(displayDuration);

        if (showDebugMessages)
            Debug.Log("TutorialTrigger: Starting tutorial fade out.");

        // Fade out
        yield return StartCoroutine(FadeCanvasGroup(canvasGroup, 1f, 0f, fadeOutDuration));

        // Hide UI
        tutorialUI.SetActive(false);

        if (showDebugMessages)
            Debug.Log("TutorialTrigger: Tutorial sequence complete.");

        // Destroy trigger if specified
        if (destroyTriggerAfterActivation)
        {
            if (showDebugMessages)
                Debug.Log("TutorialTrigger: Destroying trigger after activation.");

            Destroy(gameObject);
        }
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float fromAlpha, float toAlpha, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / duration;
            canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, normalizedTime);
            yield return null;
        }

        canvasGroup.alpha = toAlpha;
    }

    // Optional: Method to manually show tutorial (useful for testing)
    [ContextMenu("Test Show Tutorial")]
    public void TestShowTutorial()
    {
        if (Application.isPlaying && !hasTriggered)
        {
            hasTriggered = true;
            ShowTutorial();
        }
    }
}
