using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("Enemy Type")]
    public EnemyType enemyType = EnemyType.EyeMonster;

    [Header("Health System")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("Animation")]
    private Animator animator;
    private EnemyState previousState;

    [Header("Damage Settings")]
    public float damageToPlayer = 25f;
    public float attackRange = 2f;
    public float attackCooldown = 2f;
    private float lastAttackTime;

    [Header("DOT (Damage Over Time) Settings")]
    [Tooltip("How much the movement speed is reduced when taking DOT damage (0.5 = 50% slower)")]
    public float dotSpeedReduction = 0.5f;
    private bool isTakingDOT = false;
    private Coroutine dotCoroutine;

    public NavMeshAgent agent;

    [SerializeField] private Transform player;
    public Transform orientation;

    [Header("Movement Speeds")]
    public float walkSpeed = 3f;
    public float chaseSpeed = 6f;
    private float originalWalkSpeed;
    private float originalChaseSpeed;

    [Header("Waypoint System")]
    [Tooltip("List of waypoints for patrolling. Enemy will move between these points.")]
    public List<Transform> waypoints = new List<Transform>();
    [Tooltip("Time to wait at each waypoint before moving to the next one.")]
    public float waitTimeAtWaypoint = 2f;
    [Tooltip("Should the enemy patrol waypoints in order or randomly?")]
    public bool patrolInOrder = true;

    [Header("Spatial Audio Settings")]
    [Tooltip("Audio source component for 3D spatial audio")]
    public AudioSource enemyAudioSource;
    [Tooltip("Audio clip that plays randomly during patrolling and searching states")]
    public AudioClip idleAudioClip;
    [Tooltip("Time range for random idle sound intervals (min, max seconds)")]
    public Vector2 idleSoundIntervalRange = new Vector2(5f, 15f);
    [Tooltip("Audio clip that plays when taking damage")]
    public AudioClip hurtAudioClip;
    [Tooltip("Audio clip that plays when dying")]
    public AudioClip deathAudioClip;

    [Header("Combat BGM")]
    [Tooltip("BGM to play when this enemy enters combat with the player")]
    public AudioClip combatBGM;

    private static EnemyAI combatBGMTrigger = null;
    private static AudioClip previousBGM = null;

    private Coroutine idleSoundCoroutine;

    private int currentWaypointIndex = 0;
    private float waitTimer = 0f;
    private bool isWaitingAtWaypoint = false;

    public LayerMask whatIsGround, whatIsPlayer;

    // States
    public float sightRange = 10f;
    public bool playerInSightRange;

    // Search behavior
    public float searchTime = 5f;
    private float currentSearchTimer;
    private Vector3 lastKnownPosition;

    // Animation parameter names (matching your controller)
    private readonly string SPEED_PARAM = "Speed";
    private readonly string IS_ATTACKING_PARAM = "IsAttacking";
    private readonly string IS_DEAD_PARAM = "IsDead";
    private readonly string TAKE_DAMAGE_PARAM = "TakeDamage";

    public enum EnemyType
    {
        EyeMonster,
        EarMonster
    }

    public enum EnemyState
    {
        Patrolling,
        Chasing,
        SearchingLastKnown,
        Attacking,
        Dead,
        Idle
    }

    public EnemyState currentState = EnemyState.Patrolling;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        currentHealth = maxHealth;

        originalWalkSpeed = walkSpeed;
        originalChaseSpeed = chaseSpeed;

        animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("No Animator found in children of " + gameObject.name);
        }

        SetupSpatialAudio();
    }

    private void Start()
    {
        agent.speed = walkSpeed;

        if (player == null)
        {
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                player = playerGO.transform;
            }
        }

        PersistentObjectID objectID = GetComponent<PersistentObjectID>();
        if (objectID != null && WorldStateManager.Instance != null)
        {
            if (WorldStateManager.Instance.IsObjectCollected(objectID.uniqueID))
            {
                // This enemy was already killed, destroy it immediately
                gameObject.SetActive(false);
                return;
            }
        }

        if (waypoints.Count == 0)
        {
            Debug.LogWarning("No waypoints assigned to " + gameObject.name + ". Enemy will stay idle.");
            currentState = EnemyState.Idle;
        }
        else
        {
            currentWaypointIndex = 0;
            MoveToCurrentWaypoint();
        }
    }

    private void Update()
    {
        if (currentState == EnemyState.Dead) return;

        bool shouldPlayIdleSounds = (currentState == EnemyState.Patrolling || currentState == EnemyState.SearchingLastKnown);

        if (shouldPlayIdleSounds && idleSoundCoroutine == null)
        {
            idleSoundCoroutine = StartCoroutine(IdleSoundCoroutine());
        }
        else if (!shouldPlayIdleSounds && idleSoundCoroutine != null)
        {
            StopCoroutine(idleSoundCoroutine);
            idleSoundCoroutine = null;
        }

        switch (currentState)
        {
            case EnemyState.Patrolling:
                Patrolling();
                break;

            case EnemyState.Chasing:
                ChasePlayer();
                break;

            case EnemyState.SearchingLastKnown:
                SearchLastKnownPosition();
                break;

            case EnemyState.Attacking:
                AttackPlayer();
                break;

            case EnemyState.Idle:
                // Idle logic - enemy stays in place
                break;
        }

        // Update animations every frame, not just on state changes
        UpdateAnimations();

        // Track state changes for debug purposes
        if (currentState != previousState)
        {
            Debug.Log($"State changed from {previousState} to {currentState}");
            previousState = currentState;
        }
    }

    private void SetupSpatialAudio()
    {
        // Get or create AudioSource component
        if (enemyAudioSource == null)
        {
            enemyAudioSource = GetComponent<AudioSource>();
            if (enemyAudioSource == null)
            {
                enemyAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Configure for 3D spatial audio
        enemyAudioSource.spatialBlend = 1.0f; // Full 3D spatial audio
        enemyAudioSource.rolloffMode = AudioRolloffMode.Linear;
        enemyAudioSource.minDistance = 60f; // Distance where volume starts to decrease
        enemyAudioSource.maxDistance = 160f; // Distance where volume reaches zero
        enemyAudioSource.volume = 1f; // Base volume
        enemyAudioSource.playOnAwake = false;
        enemyAudioSource.loop = false;
    }

    #region Health and Damage System
    public void TakeDamage(float damage, DamageType damageType = DamageType.Generic)
    {
        if (currentState == EnemyState.Dead) return;

        // Apply damage modifiers based on enemy type and damage source
        float finalDamage = CalculateDamage(damage, damageType);

        if (finalDamage <= 0) return; // No damage dealt

        currentHealth -= finalDamage;

        if (currentHealth > 0 && animator != null)
        {
            PlayEnemySpatialAudio(hurtAudioClip);

            animator.SetTrigger(TAKE_DAMAGE_PARAM);
        }

        Debug.Log($"{enemyType} took {finalDamage} damage. Health: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // React to damage - enter chase state if not already chasing
            if (currentState != EnemyState.Chasing && currentState != EnemyState.Attacking)
            {
                TriggerCombatBGM();

                currentState = EnemyState.Chasing;
                if (player != null)
                {
                    lastKnownPosition = player.position;
                }
            }
        }
    }

    public void StartDOT(float dotDamage, float duration, float interval)
    {
        if (currentState == EnemyState.Dead) return;

        // Only Eye Monsters can receive DOT damage
        if (enemyType != EnemyType.EyeMonster) return;

        // Stop any existing DOT
        if (dotCoroutine != null)
        {
            StopCoroutine(dotCoroutine);
        }

        dotCoroutine = StartCoroutine(DOTCoroutine(dotDamage, duration, interval));
    }

    private IEnumerator DOTCoroutine(float dotDamage, float duration, float interval)
    {
        isTakingDOT = true;
        ApplyDOTEffects();

        float elapsed = 0f;

        while (elapsed < duration && currentState != EnemyState.Dead)
        {
            if (animator != null)
            {
                animator.SetTrigger(TAKE_DAMAGE_PARAM);
            }

            TakeDamage(dotDamage, DamageType.Flashlight);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        isTakingDOT = false;
        RemoveDOTEffects();
        dotCoroutine = null;
    }

    private void ApplyDOTEffects()
    {
        // Reduce movement speed during DOT
        walkSpeed = originalWalkSpeed * dotSpeedReduction;
        chaseSpeed = originalChaseSpeed * dotSpeedReduction;

        // Update agent speed if currently moving
        if (currentState == EnemyState.Patrolling)
        {
            agent.speed = walkSpeed;
        }
        else if (currentState == EnemyState.Chasing)
        {
            agent.speed = chaseSpeed;
        }

        Debug.Log($"{enemyType} movement speed reduced due to DOT effect!");
    }

    private void RemoveDOTEffects()
    {
        // Restore original movement speed
        walkSpeed = originalWalkSpeed;
        chaseSpeed = originalChaseSpeed;

        // Update agent speed
        if (currentState == EnemyState.Patrolling)
        {
            agent.speed = walkSpeed;
        }
        else if (currentState == EnemyState.Chasing)
        {
            agent.speed = chaseSpeed;
        }

        Debug.Log($"{enemyType} movement speed restored!");
    }

    private float CalculateDamage(float baseDamage, DamageType damageType)
    {
        float finalDamage = baseDamage;

        // Apply enemy-specific damage modifiers
        switch (enemyType)
        {
            case EnemyType.EyeMonster:
                // Eye monsters take damage from flashlight
                if (damageType == DamageType.Flashlight)
                {
                    finalDamage = baseDamage; // Normal damage
                }
                break;

            case EnemyType.EarMonster:
                // Ear monsters are immune to flashlight damage
                if (damageType == DamageType.Flashlight)
                {
                    finalDamage = 0f;
                    Debug.Log("Ear Monster is immune to flashlight damage!");
                }
                // Leave other damage types for future implementation
                break;
        }

        return finalDamage;
    }

    private void Die()
    {
        PlayEnemySpatialAudio(deathAudioClip);

        RestorePreviousBGM();

        if (idleSoundCoroutine != null)
        {
            StopCoroutine(idleSoundCoroutine);
            idleSoundCoroutine = null;
        }

        PersistentObjectID objectID = GetComponent<PersistentObjectID>();
        if (objectID != null && WorldStateManager.Instance != null)
        {
            WorldStateManager.Instance.RecordObjectAsCollected(objectID.uniqueID);
            Debug.Log($"Enemy {objectID.uniqueID} recorded as killed");
        }
        else
        {
            Debug.LogWarning($"Enemy {gameObject.name} missing PersistentObjectID component!");
        }

        // Get ragdoll component
        EnemyRagdollDeath ragdollDeath = GetComponent<EnemyRagdollDeath>();

        if (ragdollDeath != null)
        {
            // Calculate impact from player position if available
            Vector3 impactForce = Vector3.zero;
            if (player != null)
            {
                Vector3 direction = (transform.position - player.position).normalized;
                impactForce = direction * 400f; // Adjust force as needed
            }

            ragdollDeath.TriggerDeath(transform.position, impactForce);
        }
        else
        {
            // Fallback to destroying GameObject
            Destroy(gameObject);
        }
    }

    private IEnumerator DeathSequence()
    {
        // Disable colliders to prevent further interactions
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        // Wait a bit before destroying or disabling
        yield return new WaitForSeconds(3f);

        gameObject.SetActive(false);
    }

    public bool IsAlive()
    {
        return currentState != EnemyState.Dead;
    }

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }

    public bool IsTakingDOT()
    {
        return isTakingDOT;
    }
    #endregion

    #region Combat System
    private void AttackPlayer()
    {
        if (player == null) return;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRange)
        {
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                PerformAttack();
                lastAttackTime = Time.time;
            }
            // Stay in attack state while in range and during cooldown
        }
        else
        {
            // Player moved out of range, return to chasing
            agent.isStopped = false;
            currentState = EnemyState.Chasing;
        }
    }

    private void PerformAttack()
    {
        Debug.Log($"{enemyType} attacks the player for {damageToPlayer} damage!");


        // Deal damage to player
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damageToPlayer);
        }
    }
    #endregion

    #region AI Behavior
    private void Patrolling()
    {
        agent.speed = walkSpeed;
        agent.isStopped = false;

        // If we have no waypoints, go idle
        if (waypoints.Count == 0)
        {
            currentState = EnemyState.Idle;
            return;
        }

        // Check if we're waiting at a waypoint
        if (isWaitingAtWaypoint)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= waitTimeAtWaypoint)
            {
                // Finished waiting, move to next waypoint
                isWaitingAtWaypoint = false;
                waitTimer = 0f;
                SelectNextWaypoint();
                MoveToCurrentWaypoint();
            }
        }
        else
        {
            // Check if we've reached our current waypoint
            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                // Arrived at waypoint, start waiting
                isWaitingAtWaypoint = true;
                waitTimer = 0f;
            }
        }

        // Check for the player while patrolling
        CheckForPlayer();
    }

    private void ChasePlayer()
    {
        agent.speed = chaseSpeed;
        agent.isStopped = false;

        if (player == null) return;

        // Check if we can still see the player
        bool canSeePlayer = false;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (Physics.CheckSphere(transform.position, sightRange, whatIsPlayer))
        {
            Vector3 directionToPlayer = player.position - orientation.position;
            if (Physics.Raycast(orientation.position, directionToPlayer, out RaycastHit hit, sightRange))
            {
                if (hit.transform == player)
                {
                    canSeePlayer = true;
                }
            }
        }

        if (canSeePlayer)
        {
            // Check if close enough to attack
            if (distanceToPlayer <= attackRange)
            {
                currentState = EnemyState.Attacking;
                return;
            }

            // Chase the player and update last known position
            agent.SetDestination(player.position);
            lastKnownPosition = player.position;
        }
        else
        {
            // Lost sight, switch to searching
            currentState = EnemyState.SearchingLastKnown;
        }
    }

    private void SearchLastKnownPosition()
    {
        agent.speed = walkSpeed;
        agent.isStopped = false;
        agent.SetDestination(lastKnownPosition);

        // If we reach the last known position, return to patrolling
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            ReturnToPatrolling();
        }

        // While searching, if we spot the player again, immediately go back to chasing
        CheckForPlayer();
    }

    private void ReturnToPatrolling()
    {
        currentState = EnemyState.Patrolling;
        isWaitingAtWaypoint = false;
        waitTimer = 0f;

        // Find the closest waypoint to resume patrolling
        FindClosestWaypoint();
        MoveToCurrentWaypoint();
    }

    private void FindClosestWaypoint()
    {
        if (waypoints.Count == 0) return;

        float closestDistance = float.MaxValue;
        int closestWaypointIndex = 0;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] != null)
            {
                float distance = Vector3.Distance(transform.position, waypoints[i].position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestWaypointIndex = i;
                }
            }
        }

        currentWaypointIndex = closestWaypointIndex;
    }

    private void SelectNextWaypoint()
    {
        if (waypoints.Count == 0) return;

        if (patrolInOrder)
        {
            // Move to next waypoint in sequence
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
        }
        else
        {
            // Select random waypoint (but not the current one if we have multiple)
            if (waypoints.Count > 1)
            {
                int newIndex;
                do
                {
                    newIndex = Random.Range(0, waypoints.Count);
                } while (newIndex == currentWaypointIndex);
                currentWaypointIndex = newIndex;
            }
        }
    }

    private void MoveToCurrentWaypoint()
    {
        if (waypoints.Count > 0 && currentWaypointIndex < waypoints.Count && waypoints[currentWaypointIndex] != null)
        {
            agent.SetDestination(waypoints[currentWaypointIndex].position);
        }
    }

    private void CheckForPlayer()
    {
        if (Physics.CheckSphere(transform.position, sightRange, whatIsPlayer))
        {
            Vector3 directionToPlayer = player.position - orientation.position;
            if (Physics.Raycast(orientation.position, directionToPlayer, out RaycastHit hit, sightRange))
            {
                if (hit.transform == player)
                {
                    if (currentState != EnemyState.Chasing)
                    {
                        TriggerCombatBGM();
                    }

                    currentState = EnemyState.Chasing;
                }
            }
        }
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        // Calculate actual movement speed for animation
        float currentSpeed = agent.velocity.magnitude;
        float normalizedSpeed = 0f;

        // Normalize speed based on current movement type
        if (currentState == EnemyState.Chasing)
        {
            normalizedSpeed = currentSpeed / chaseSpeed;
        }
        else if (currentState == EnemyState.Patrolling || currentState == EnemyState.SearchingLastKnown)
        {
            normalizedSpeed = currentSpeed / walkSpeed;
        }
        else
        {
            normalizedSpeed = 0f; // Idle, Attacking, or Dead
        }

        // Set speed parameter continuously
        animator.SetFloat(SPEED_PARAM, normalizedSpeed);

        // Handle attack state properly
        bool shouldBeAttacking = (currentState == EnemyState.Attacking);
        animator.SetBool(IS_ATTACKING_PARAM, shouldBeAttacking);
    }

    #endregion
    private IEnumerator IdleSoundCoroutine()
    {
        while (currentState == EnemyState.Patrolling || currentState == EnemyState.SearchingLastKnown)
        {
            float waitTime = Random.Range(idleSoundIntervalRange.x, idleSoundIntervalRange.y);
            yield return new WaitForSeconds(waitTime);

            // Check if still in valid state before playing sound
            if (currentState == EnemyState.Patrolling || currentState == EnemyState.SearchingLastKnown)
            {
                PlayEnemySpatialAudio(idleAudioClip);
            }
        }
        idleSoundCoroutine = null;
    }
    private void OnDrawGizmosSelected()
    {
        // Draw sight range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Draw waypoints and connections
        if (waypoints != null && waypoints.Count > 1)
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] != null)
                {
                    // Draw waypoint
                    Gizmos.DrawWireSphere(waypoints[i].position, 1f);

                    // Draw path lines
                    if (patrolInOrder)
                    {
                        // Draw lines between sequential waypoints
                        int nextIndex = (i + 1) % waypoints.Count;
                        if (waypoints[nextIndex] != null)
                        {
                            Gizmos.DrawLine(waypoints[i].position, waypoints[nextIndex].position);
                        }
                    }
                }
            }

            // Highlight current waypoint
            if (currentWaypointIndex < waypoints.Count && waypoints[currentWaypointIndex] != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(waypoints[currentWaypointIndex].position, 1.2f);
            }
        }
    }

    public void OnAttackAnimationEnd()
    {
        // This ensures the attack bool gets reset after animation completes
        if (currentState == EnemyState.Attacking)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer > attackRange)
            {
                currentState = EnemyState.Chasing;
            }
        }
    }

    private void PlayEnemySpatialAudio(AudioClip clip)
    {
        if (clip != null && enemyAudioSource != null)
        {
            enemyAudioSource.PlayOneShot(clip);
        }
    }

    private void TriggerCombatBGM()
    {
        // Only trigger if we have a combat BGM and no other enemy is already controlling the BGM
        if (combatBGM != null && combatBGMTrigger == null && AudioManager.instance != null)
        {
            // Check if the desired BGM is already playing
            if (AudioManager.instance.musicSource.clip != combatBGM)
            {
                // Store the current BGM before changing to combat music
                previousBGM = AudioManager.instance.musicSource.clip;

                // Set this enemy as the BGM controller
                combatBGMTrigger = this;

                // Change to combat BGM with fade
                AudioManager.instance.ChangeBGMWithFade(combatBGM);

                Debug.Log($"{enemyType} triggered combat BGM: {combatBGM.name}. Previous BGM: {(previousBGM != null ? previousBGM.name : "None")}");
            }
            else
            {
                Debug.Log($"Combat BGM {combatBGM.name} is already playing, not triggering again");
            }
        }
    }

    private void RestorePreviousBGM()
    {
        // Only restore BGM if this enemy was the one controlling it
        if (combatBGMTrigger == this && AudioManager.instance != null)
        {
            // Clear the BGM controller
            combatBGMTrigger = null;

            // Restore to the previous BGM that was playing before combat
            if (previousBGM != null)
            {
                AudioManager.instance.ChangeBGMWithFade(previousBGM);
                Debug.Log($"{enemyType} death triggered return to previous BGM: {previousBGM.name}");
            }
            else
            {
                // Fallback to game BGM if no previous BGM was stored
                if (AudioManager.instance.gameBGM != null)
                {
                    AudioManager.instance.ChangeBGMWithFade(AudioManager.instance.gameBGM);
                    Debug.Log($"{enemyType} death triggered return to default game BGM (no previous BGM stored)");
                }
            }

            // Clear the stored previous BGM
            previousBGM = null;
        }
    }

    private void OnDestroy()
    {
        // Clean up BGM controller reference if this enemy was controlling it
        if (combatBGMTrigger == this)
        {
            RestorePreviousBGM();
        }
    }
}
