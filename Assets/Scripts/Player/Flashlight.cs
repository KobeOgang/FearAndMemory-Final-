using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Flashlight : MonoBehaviour
{
    [Header("Flashlight Settings")]
    public GameObject flashlight;
    public Light flashlightLight;

    [Header("Audio Settings")]
    [Tooltip("SFX to play when toggling the flashlight on/off")]
    public AudioClip flashlightToggleSFX;

    [Header("Player Light Settings")]
    public Light playerLight; 
    public bool playerLightAlwaysOn = true; 

    [Header("DOT Damage Settings")]
    public float dotDamagePerTick = 5f;
    public float dotTickInterval = 0.5f; 
    public float dotDuration = 2f; 
    public float damageRange = 10f;
    public LayerMask enemyLayerMask = -1;

    [Header("Beam Settings")]
    public float beamAngle = 30f; 

    public bool on;
    public bool off;

    private Animator animator;
    private Dictionary<EnemyAI, Coroutine> activeEnemyDOTs = new Dictionary<EnemyAI, Coroutine>();

    private void Start()
    {
        off = true;
        flashlight.SetActive(false);
        animator = GetComponentInChildren<Animator>();

        if (flashlightLight == null && flashlight != null)
        {
            flashlightLight = flashlight.GetComponent<Light>();
        }

        // Setup player light if it exists
        SetupPlayerLight();
    }

    private void SetupPlayerLight()
    {
        if (playerLight != null)
        {
            // Configure the player light as a point light with gentle settings
            playerLight.type = LightType.Point;
            playerLight.range = 3f; // Small range around player
            playerLight.intensity = 0.5f; // Subtle intensity
            playerLight.color = new Color(0.9f, 0.9f, 0.7f, 1f); // Warm white
            playerLight.shadows = LightShadows.None; // No shadows for performance

            // Enable player light based on settings
            playerLight.enabled = playerLightAlwaysOn;
        }
    }

    private void Update()
    {
        HandleFlashlightToggle();

        if (on)
        {
            ApplyFlashlightDOT();
        }
        else
        {
            StopAllDOTs();
        }
    }

    private void HandleFlashlightToggle()
    {
        if (off && Input.GetKeyDown(KeyCode.F))
        {
            // Play flashlight toggle SFX
            PlayFlashlightToggleSFX();

            flashlight.SetActive(true);
            off = false;
            on = true;

            // Enable player light when flashlight is on
            if (playerLight != null)
            {
                playerLight.enabled = true;
            }

            // Animation variant
            // animator.SetBool("IsHoldingFlashlight", true);
            // animator.SetLayerWeight(1, 1f);
        }
        else if (on && Input.GetKeyDown(KeyCode.F))
        {
            // Play flashlight toggle SFX
            PlayFlashlightToggleSFX();

            flashlight.SetActive(false);
            on = false;
            off = true;

            // Handle player light when flashlight is off
            if (playerLight != null)
            {
                playerLight.enabled = playerLightAlwaysOn;
            }

            // Animation variant
            // animator.SetBool("IsHoldingFlashlight", false);
            // animator.SetLayerWeight(1, 0f);
        }
    }

    private void PlayFlashlightToggleSFX()
    {
        if (flashlightToggleSFX != null && AudioManager.instance != null)
        {
            AudioManager.instance.PlayClip(flashlightToggleSFX);
        }
    }

    private void ApplyFlashlightDOT()
    {
        // Cast a sphere to detect enemies in range
        Collider[] enemiesInRange = Physics.OverlapSphere(transform.position, damageRange, enemyLayerMask);

        List<EnemyAI> enemiesCurrentlyInBeam = new List<EnemyAI>();

        foreach (Collider enemyCollider in enemiesInRange)
        {
            EnemyAI enemy = enemyCollider.GetComponent<EnemyAI>();
            if (enemy != null && enemy.IsAlive())
            {
                // Check if enemy is within the flashlight beam
                if (IsEnemyInBeam(enemy.transform))
                {
                    // Only apply DOT to Eye Monsters
                    if (enemy.enemyType == EnemyAI.EnemyType.EyeMonster)
                    {
                        enemiesCurrentlyInBeam.Add(enemy);

                        // Start DOT if not already active
                        if (!activeEnemyDOTs.ContainsKey(enemy))
                        {
                            StartDOTForEnemy(enemy);
                        }
                    }
                }
            }
        }

        // Stop DOT for enemies no longer in beam
        List<EnemyAI> enemiesToRemove = new List<EnemyAI>();
        foreach (var kvp in activeEnemyDOTs)
        {
            if (!enemiesCurrentlyInBeam.Contains(kvp.Key))
            {
                enemiesToRemove.Add(kvp.Key);
            }
        }

        foreach (var enemy in enemiesToRemove)
        {
            StopDOTForEnemy(enemy);
        }
    }

    private void StartDOTForEnemy(EnemyAI enemy)
    {
        if (activeEnemyDOTs.ContainsKey(enemy)) return;

        // Start DOT on the enemy
        enemy.StartDOT(dotDamagePerTick, float.MaxValue, dotTickInterval); // Infinite duration while in beam

        // Track the DOT locally (we don't need a coroutine since enemy handles it)
        activeEnemyDOTs[enemy] = null;

        Debug.Log($"Started DOT on {enemy.enemyType}");
    }

    private void StopDOTForEnemy(EnemyAI enemy)
    {
        if (!activeEnemyDOTs.ContainsKey(enemy)) return;

        // Stop the DOT and apply lingering effect
        enemy.StartDOT(dotDamagePerTick, dotDuration, dotTickInterval);

        activeEnemyDOTs.Remove(enemy);

        Debug.Log($"Stopped active DOT on {enemy.enemyType}, applying lingering effect");
    }

    private void StopAllDOTs()
    {
        List<EnemyAI> enemiesToRemove = new List<EnemyAI>(activeEnemyDOTs.Keys);

        foreach (var enemy in enemiesToRemove)
        {
            StopDOTForEnemy(enemy);
        }
    }

    private bool IsEnemyInBeam(Transform enemyTransform)
    {
        Vector3 directionToEnemy = (enemyTransform.position - transform.position).normalized;
        Vector3 flashlightForward = flashlight.transform.forward;

        // Calculate angle between flashlight direction and direction to enemy
        float angle = Vector3.Angle(flashlightForward, directionToEnemy);

        // Check if enemy is within the beam cone
        if (angle <= beamAngle / 2f)
        {
            // Perform raycast to ensure there's line of sight
            RaycastHit hit;
            if (Physics.Raycast(transform.position, directionToEnemy, out hit, damageRange))
            {
                return hit.transform == enemyTransform;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (on && flashlight != null)
        {
            // Draw damage range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, damageRange);

            // Draw beam cone
            Gizmos.color = Color.red;
            Vector3 forward = flashlight.transform.forward;
            Vector3 right = flashlight.transform.right;
            Vector3 up = flashlight.transform.up;

            // Calculate cone edges
            float halfAngle = beamAngle / 2f;
            Vector3 coneEdge1 = Quaternion.AngleAxis(halfAngle, up) * forward * damageRange;
            Vector3 coneEdge2 = Quaternion.AngleAxis(-halfAngle, up) * forward * damageRange;
            Vector3 coneEdge3 = Quaternion.AngleAxis(halfAngle, right) * forward * damageRange;
            Vector3 coneEdge4 = Quaternion.AngleAxis(-halfAngle, right) * forward * damageRange;

            // Draw cone lines
            Gizmos.DrawLine(transform.position, transform.position + coneEdge1);
            Gizmos.DrawLine(transform.position, transform.position + coneEdge2);
            Gizmos.DrawLine(transform.position, transform.position + coneEdge3);
            Gizmos.DrawLine(transform.position, transform.position + coneEdge4);
        }

        // Draw player light range
        if (playerLight != null && playerLight.enabled)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(playerLight.transform.position, playerLight.range);
        }
    }
}
