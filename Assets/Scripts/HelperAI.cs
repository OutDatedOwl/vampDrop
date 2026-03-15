using UnityEngine;
using UnityEngine.AI;
using Unity.Entities;
using Unity.Collections;
using System.Collections;

namespace Vampire.Helpers
{
    using DropPuzzle;
    
    /// <summary>
    /// AI behavior for helper creatures (goblins/ghouls) that automatically collect rice
    /// Integrates with ECS rice system and zone deployment
    /// </summary>
    public class HelperAI : MonoBehaviour
    {
        [Header("Helper Configuration")]
        public DropPuzzle.HelperType helperType = DropPuzzle.HelperType.Goblin;
        public string helperId;
        public string deployedZoneId;
        
        [Header("Collection Settings")]
        public float collectionInterval = 1.0f;
        public float detectionRange = 10f;
        public float collectionDistance = 2f;
        public int maxCarryCapacity = 5;
        
        [Header("Movement Settings")]
        public float moveSpeed = 3.5f;
        public float waitTime = 0.5f;
        public float roamRadius = 20f;
        
        [Header("Animation Settings")]
        public Animator helperAnimator;
        public ParticleSystem collectionEffect;
        
        [Header("Audio")]
        public AudioClip collectSound;
        public AudioClip[] ambientSounds;
        public AudioSource audioSource;
        
        // Components
        private NavMeshAgent navAgent;
        private EntityManager entityManager;
        private DropPuzzle.PlayerDataManager playerData => DropPuzzle.PlayerDataManager.Instance;
        
        // Helper State
        private HelperState currentState = HelperState.Idle;
        private Vector3 spawnPosition;
        private Vector3 targetPosition;
        private int currentCarryCount = 0;
        private float lastCollectionTime = 0f;
        private float lastAmbientSoundTime = 0f;
        private const float ambientSoundInterval = 15f;
        
        // Rice Detection
        private EntityQuery riceQuery;
        private NativeArray<Unity.Entities.Entity> nearbyRice;
        private bool hasValidTarget = false;
        
        // Collection tracking
        private int totalRiceCollected = 0;
        private float deploymentTime = 0f;
        
        #region Unity Events
        
        private void Awake()
        {
            // Generate unique ID if not set
            if (string.IsNullOrEmpty(helperId))
            {
                helperId = System.Guid.NewGuid().ToString();
            }
            
            // Get components
            navAgent = GetComponent<NavMeshAgent>();
            if (navAgent == null)
            {
                navAgent = gameObject.AddComponent<NavMeshAgent>();
            }
            
            // Configure NavMesh agent
            navAgent.speed = moveSpeed;
            navAgent.stoppingDistance = collectionDistance;
            navAgent.acceleration = 8f;
            navAgent.angularSpeed = 360f;
            
            // Setup audio
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.volume = 0.7f;
            audioSource.minDistance = 5f;
            audioSource.maxDistance = 20f;
        }
        
        private void Start()
        {
            spawnPosition = transform.position;
            deploymentTime = Time.time;
            
            // Initialize ECS rice detection
            InitializeRiceDetection();
            
            // Start AI behavior
            StartCoroutine(HelperAILoop());
            StartCoroutine(RiceCollectionLoop());
            
            Debug.Log($"[HelperAI] {helperType} helper '{helperId}' deployed in zone '{deployedZoneId}' at {spawnPosition}");
        }
        
        private void Update()
        {
            UpdateAnimation();
            UpdateAmbientSounds();
            DebugDrawHelper();
        }
        
        private void OnDestroy()
        {
            // Clean up
            if (nearbyRice.IsCreated)
            {
                nearbyRice.Dispose();
            }
            
            Debug.Log($"[HelperAI] {helperType} helper '{helperId}' recalled. Collected {totalRiceCollected} rice in {Time.time - deploymentTime:F1} seconds.");
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeRiceDetection()
        {
            // Get ECS world
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogWarning("[HelperAI] ECS World not found - rice detection disabled");
                return;
            }
            
            entityManager = world.EntityManager;
            
            // Create rice query (same as FPSAudioManager)
            riceQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<Vampire.Rice.RiceEntity>(),
                ComponentType.ReadOnly<Unity.Transforms.LocalTransform>()
            );
            
            Debug.Log($"[HelperAI] Rice detection initialized. Found {riceQuery.CalculateEntityCount()} rice entities.");
        }
        
        #endregion
        
        #region AI Behavior
        
        private IEnumerator HelperAILoop()
        {
            while (true)
            {
                switch (currentState)
                {
                    case HelperState.Idle:
                        HandleIdleState();
                        break;
                        
                    case HelperState.Seeking:
                        HandleSeekingState();
                        break;
                        
                    case HelperState.Collecting:
                        HandleCollectingState();
                        break;
                        
                    case HelperState.Returning:
                        HandleReturningState();
                        break;
                        
                    case HelperState.Depositing:
                        HandleDepositingState();
                        break;
                }
                
                yield return new WaitForSeconds(0.2f); // Update AI 5x per second
            }
        }
        
        private void HandleIdleState()
        {
            // Look for rice to collect
            if (currentCarryCount < maxCarryCapacity && FindNearestRice())
            {
                SetState(HelperState.Seeking);
            }
            else if (currentCarryCount > 0)
            {
                // Carry capacity full, return to deposit
                SetState(HelperState.Returning);
            }
            else
            {
                // Nothing to do, roam around
                if (!navAgent.hasPath || navAgent.remainingDistance < 1f)
                {
                    Vector3 roamTarget = GetRandomRoamPosition();
                    navAgent.SetDestination(roamTarget);
                }
            }
        }
        
        private void HandleSeekingState()
        {
            // Move towards target rice
            if (hasValidTarget)
            {
                if (Vector3.Distance(transform.position, targetPosition) <= collectionDistance)
                {
                    SetState(HelperState.Collecting);
                }
            }
            else
            {
                SetState(HelperState.Idle);
            }
        }
        
        private void HandleCollectingState()
        {
            // Wait for collection animation/effect
            navAgent.isStopped = true;
            
            if (Time.time - lastCollectionTime >= waitTime)
            {
                navAgent.isStopped = false;
                
                if (currentCarryCount >= maxCarryCapacity)
                {
                    SetState(HelperState.Returning);
                }
                else
                {
                    SetState(HelperState.Idle);
                }
            }
        }
        
        private void HandleReturningState()
        {
            // Move back to spawn/deposit point
            navAgent.SetDestination(spawnPosition);
            
            if (Vector3.Distance(transform.position, spawnPosition) <= 3f)
            {
                SetState(HelperState.Depositing);
            }
        }
        
        private void HandleDepositingState()
        {
            // Deposit collected rice
            navAgent.isStopped = true;
            
            if (currentCarryCount > 0)
            {
                DepositCollectedRice();
            }
            
            navAgent.isStopped = false;
            SetState(HelperState.Idle);
        }
        
        private void SetState(HelperState newState)
        {
            if (currentState != newState)
            {
                Debug.Log($"[HelperAI] {helperId} state: {currentState} -> {newState}");
                currentState = newState;
            }
        }
        
        #endregion
        
        #region Rice Detection & Collection
        
        private IEnumerator RiceCollectionLoop()
        {
            while (true)
            {
                if (currentState == HelperState.Collecting && Time.time - lastCollectionTime >= collectionInterval)
                {
                    TryCollectNearbyRice();
                }
                
                yield return new WaitForSeconds(collectionInterval);
            }
        }
        
        private bool FindNearestRice()
        {
            if (riceQuery.IsEmpty) return false;
            
            try
            {
                var transforms = riceQuery.ToComponentDataArray<Unity.Transforms.LocalTransform>(Allocator.TempJob);
                
                float nearestDistance = float.MaxValue;
                Vector3 nearestPosition = Vector3.zero;
                bool foundRice = false;
                
                foreach (var transform in transforms)
                {
                    Vector3 ricePos = transform.Position;
                    float distance = Vector3.Distance(this.transform.position, ricePos);
                    
                    if (distance <= detectionRange && distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestPosition = ricePos;
                        foundRice = true;
                    }
                }
                
                transforms.Dispose();
                
                if (foundRice)
                {
                    targetPosition = nearestPosition;
                    navAgent.SetDestination(targetPosition);
                    hasValidTarget = true;
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HelperAI] Rice detection failed: {ex.Message}");
            }
            
            hasValidTarget = false;
            return false;
        }
        
        private void TryCollectNearbyRice()
        {
            if (currentCarryCount >= maxCarryCapacity) return;
            
            // Find rice within collection distance
            if (riceQuery.IsEmpty) return;
            
            try
            {
                var entities = riceQuery.ToEntityArray(Allocator.TempJob);
                var transforms = riceQuery.ToComponentDataArray<Unity.Transforms.LocalTransform>(Allocator.TempJob);
                
                for (int i = 0; i < entities.Length; i++)
                {
                    Vector3 ricePos = transforms[i].Position;
                    float distance = Vector3.Distance(this.transform.position, ricePos);
                    
                    if (distance <= collectionDistance)
                    {
                        // Collect this rice
                        CollectRice(entities[i], ricePos);
                        break; // Only collect one at a time
                    }
                }
                
                entities.Dispose();
                transforms.Dispose();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HelperAI] Rice collection failed: {ex.Message}");
            }
        }
        
        private void CollectRice(Unity.Entities.Entity riceEntity, Vector3 ricePosition)
        {
            // Destroy the rice entity
            entityManager.DestroyEntity(riceEntity);
            
            // Update carry count
            currentCarryCount++;
            totalRiceCollected++;
            lastCollectionTime = Time.time;
            
            // Play collection effects
            PlayCollectionEffects(ricePosition);
            
            Debug.Log($"[HelperAI] {helperId} collected rice! Carrying: {currentCarryCount}/{maxCarryCapacity}");
        }
        
        private void DepositCollectedRice()
        {
            if (playerData != null && currentCarryCount > 0)
            {
                // Add rice to player inventory
                playerData.AddCurrency(currentCarryCount);
                
                Debug.Log($"[HelperAI] {helperId} deposited {currentCarryCount} rice! Player total: {playerData.TotalCurrency}");
                
                // Reset carry count
                currentCarryCount = 0;
                
                // Play deposit effects
                PlayDepositEffects();
            }
        }
        
        #endregion
        
        #region Movement & Navigation
        
        private Vector3 GetRandomRoamPosition()
        {
            // Get random position within roam radius
            Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * roamRadius;
            randomDirection += spawnPosition;
            
            // Make sure it's on the ground
            if (Physics.Raycast(randomDirection + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
            {
                return hit.point;
            }
            
            return spawnPosition; // Fallback to spawn position
        }
        
        #endregion
        
        #region Effects & Audio
        
        private void PlayCollectionEffects(Vector3 position)
        {
            // Play collection particle effect
            if (collectionEffect != null)
            {
                collectionEffect.transform.position = position;
                collectionEffect.Play();
            }
            
            // Play collection sound
            if (collectSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(collectSound);
            }
        }
        
        private void PlayDepositEffects()
        {
            // Could add deposit particle effect here
            Debug.Log($"[HelperAI] {helperId} plays deposit effects");
        }
        
        private void UpdateAmbientSounds()
        {
            if (Time.time - lastAmbientSoundTime >= ambientSoundInterval && ambientSounds.Length > 0 && audioSource != null)
            {
                AudioClip randomClip = ambientSounds[UnityEngine.Random.Range(0, ambientSounds.Length)];
                audioSource.PlayOneShot(randomClip, 0.3f);
                lastAmbientSoundTime = Time.time;
            }
        }
        
        #endregion
        
        #region Animation
        
        private void UpdateAnimation()
        {
            if (helperAnimator == null) return;
            
            // Set animation parameters based on state
            bool isWalking = navAgent.velocity.magnitude > 0.1f;
            bool isCollecting = currentState == HelperState.Collecting;
            
            helperAnimator.SetBool("IsWalking", isWalking);
            helperAnimator.SetBool("IsCollecting", isCollecting);
            helperAnimator.SetFloat("Speed", navAgent.velocity.magnitude);
        }
        
        #endregion
        
        #region Debug & Gizmos
        
        private void DebugDrawHelper()
        {
            if (Application.isPlaying)
            {
                // Draw path to target
                if (hasValidTarget)
                {
                    UnityEngine.Debug.DrawLine(transform.position, targetPosition, Color.red, 0.1f);
                }
            }
        }
        
        private void OnDrawGizmos()
        {
            // Detection range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            
            // Roam area
            Vector3 spawn = Application.isPlaying ? spawnPosition : transform.position;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawn, roamRadius);
            
            // Collection distance
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, collectionDistance);
        }
        
        public void DEBUG_ShowStatus()
        {
            Debug.Log($"=== Helper {helperId} Status ===");
            Debug.Log($"Type: {helperType}");
            Debug.Log($"Zone: {deployedZoneId}");
            Debug.Log($"State: {currentState}");
            Debug.Log($"Carrying: {currentCarryCount}/{maxCarryCapacity}");
            Debug.Log($"Total Collected: {totalRiceCollected}");
            Debug.Log($"Deployment Time: {Time.time - deploymentTime:F1}s");
            Debug.Log($"==============================");
        }
        
        #endregion
        
        #region Public Interface
        
        /// <summary>
        /// Initialize helper with deployment data
        /// </summary>
        public void Initialize(string zoneId, DropPuzzle.HelperType type, Vector3 deployPosition)
        {
            deployedZoneId = zoneId;
            helperType = type;
            transform.position = deployPosition;
            spawnPosition = deployPosition;
        }
        
        /// <summary>
        /// Get helper performance stats
        /// </summary>
        public HelperStats GetStats()
        {
            return new HelperStats
            {
                helperId = this.helperId,
                helperType = this.helperType,
                deployedZoneId = this.deployedZoneId,
                totalRiceCollected = this.totalRiceCollected,
                currentCarryCount = this.currentCarryCount,
                deploymentTime = this.deploymentTime,
                currentState = this.currentState
            };
        }
        
        /// <summary>
        /// Recall this helper (destroy and return rice)
        /// </summary>
        public void RecallHelper()
        {
            // Deposit any carried rice before being recalled
            if (currentCarryCount > 0)
            {
                DepositCollectedRice();
            }
            
            Destroy(gameObject);
        }
        
        #endregion
    }
    
    /// <summary>
    /// States for helper AI behavior
    /// </summary>
    public enum HelperState
    {
        Idle,       // Looking for rice or roaming
        Seeking,    // Moving towards target rice
        Collecting, // Picking up rice
        Returning,  // Moving back to deposit point
        Depositing  // Dropping off collected rice
    }
    
    /// <summary>
    /// Performance stats for a helper
    /// </summary>
    [System.Serializable]
    public class HelperStats
    {
        public string helperId;
        public DropPuzzle.HelperType helperType;
        public string deployedZoneId;
        public int totalRiceCollected;
        public int currentCarryCount;
        public float deploymentTime;
        public HelperState currentState;
    }
}