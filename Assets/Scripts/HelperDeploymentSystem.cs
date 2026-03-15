using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Vampire.Helpers
{
    using DropPuzzle;
    
    /// <summary>
    /// Master system for deploying and managing helpers in zones
    /// Integrates with PlayerDataManager, ZoneManager, and HelperShop
    /// </summary>
    public class HelperDeploymentSystem : MonoBehaviour
    {
        [Header("Helper Prefabs")]
        public GameObject goblinPrefab;
        public GameObject ghoulPrefab;
        
        [Header("Deployment Settings")]
        public float spawnHeightOffset = 1f;
        public bool enableDebugLogging = true;
        
        [Header("Auto Collection")]
        public bool enablePassiveGeneration = true;
        public float passiveGenerationInterval = 1f;
        
        // Singleton instance
        public static HelperDeploymentSystem Instance { get; private set; }
        
        // Active deployed helpers
        private Dictionary<string, HelperAI> activeHelpers = new Dictionary<string, HelperAI>();
        
        // References
        private DropPuzzle.PlayerDataManager playerData => DropPuzzle.PlayerDataManager.Instance;
        
        #region Unity Events
        
        private void Awake()
        {
            // Singleton pattern
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        
        private void Start()
        {
            if (enablePassiveGeneration)
            {
                StartCoroutine(PassiveRiceGenerationLoop());
            }
            
            // Load any saved deployed helpers
            LoadDeployedHelpers();
            
            Debug.Log("[HelperDeploymentSystem] System initialized");
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                // Save helper states before destruction
                SaveDeployedHelpers();
                Instance = null;
            }
        }
        
        #endregion
        
        #region Helper Deployment
        
        /// <summary>
        /// Deploy a helper to a specific zone
        /// </summary>
        public bool DeployHelper(DropPuzzle.HelperType helperType, string zoneId, Vector3? customPosition = null)
        {
            if (!ValidateDeployment(helperType, zoneId))
            {
                return false;
            }
            
            // Get zone information
            var zone = ZoneManager.GetZone(zoneId);
            if (zone == null)
            {
                Debug.LogError($"[HelperDeploymentSystem] Zone '{zoneId}' not found!");
                return false;
            }
            
            // Check if zone can support more helpers
            var currentHelpersInZone = GetHelpersInZone(zoneId);
            if (currentHelpersInZone.Count >= zone.maxHelpersAllowed)
            {
                Debug.LogWarning($"[HelperDeploymentSystem] Zone '{zoneId}' is at maximum helper capacity ({zone.maxHelpersAllowed})");
                return false;
            }
            
            // Select deployment position
            Vector3 deployPosition;
            if (customPosition.HasValue)
            {
                deployPosition = customPosition.Value;
            }
            else
            {
                deployPosition = GetRandomDeploymentPosition(zoneId);
            }
            
            // Instantiate helper
            GameObject helperPrefab = GetHelperPrefab(helperType);
            if (helperPrefab == null)
            {
                Debug.LogError($"[HelperDeploymentSystem] No prefab found for {helperType} helper!");
                return false;
            }
            
            GameObject helperObj = Instantiate(helperPrefab, deployPosition, Quaternion.identity);
            HelperAI helperAI = helperObj.GetComponent<HelperAI>();
            
            if (helperAI == null)
            {
                Debug.LogError("[HelperDeploymentSystem] Helper prefab missing HelperAI component!");
                Destroy(helperObj);
                return false;
            }
            
            // Initialize helper
            helperAI.Initialize(zoneId, helperType, deployPosition);
            
            // Track deployed helper
            activeHelpers[helperAI.helperId] = helperAI;
            
            // Update player data
            if (playerData != null)
            {
                var deployedHelper = new DropPuzzle.DeployedHelper(helperAI.helperId, helperType, zoneId);
                
                playerData.Helpers.deployedHelpers.Add(deployedHelper);
                playerData.SavePlayerData();
            }
            
            if (enableDebugLogging)
            {
                Debug.Log($"[HelperDeploymentSystem] Deployed {helperType} helper '{helperAI.helperId}' to zone '{zoneId}' at {deployPosition}");
            }
            
            return true;
        }
        
        /// <summary>
        /// Recall a specific helper by ID
        /// </summary>
        public bool RecallHelper(string helperId)
        {
            if (!activeHelpers.ContainsKey(helperId))
            {
                Debug.LogWarning($"[HelperDeploymentSystem] Helper '{helperId}' not found for recall");
                return false;
            }
            
            HelperAI helper = activeHelpers[helperId];
            
            // Remove from player data
            if (playerData != null)
            {
                var deployedHelper = playerData.Helpers.deployedHelpers.FirstOrDefault(h => h.helperId == helperId);
                if (deployedHelper != null)
                {
                    playerData.Helpers.deployedHelpers.Remove(deployedHelper);
                    playerData.SavePlayerData();
                }
            }
            
            // Remove from tracking
            activeHelpers.Remove(helperId);
            
            // Destroy helper object
            if (helper != null)
            {
                helper.RecallHelper();
            }
            
            if (enableDebugLogging)
            {
                Debug.Log($"[HelperDeploymentSystem] Recalled helper '{helperId}'");
            }
            
            return true;
        }
        
        /// <summary>
        /// Recall all helpers from a specific zone
        /// </summary>
        public int RecallHelpersFromZone(string zoneId)
        {
            var helpersInZone = GetHelpersInZone(zoneId);
            int recallCount = 0;
            
            foreach (var helper in helpersInZone)
            {
                if (RecallHelper(helper.helperId))
                {
                    recallCount++;
                }
            }
            
            if (enableDebugLogging)
            {
                Debug.Log($"[HelperDeploymentSystem] Recalled {recallCount} helpers from zone '{zoneId}'");
            }
            
            return recallCount;
        }
        
        /// <summary>
        /// Recall all helpers
        /// </summary>
        public int RecallAllHelpers()
        {
            var helperIds = activeHelpers.Keys.ToList();
            int recallCount = 0;
            
            foreach (var helperId in helperIds)
            {
                if (RecallHelper(helperId))
                {
                    recallCount++;
                }
            }
            
            if (enableDebugLogging)
            {
                Debug.Log($"[HelperDeploymentSystem] Recalled all helpers ({recallCount} total)");
            }
            
            return recallCount;
        }
        
        #endregion
        
        #region Helper Management
        
        /// <summary>
        /// Get all helpers currently deployed in a zone
        /// </summary>
        public List<HelperAI> GetHelpersInZone(string zoneId)
        {
            return activeHelpers.Values.Where(h => h.deployedZoneId == zoneId).ToList();
        }
        
        /// <summary>
        /// Get all currently active helpers
        /// </summary>
        public List<HelperAI> GetAllActiveHelpers()
        {
            return activeHelpers.Values.ToList();
        }
        
        /// <summary>
        /// Get a specific helper by ID
        /// </summary>
        public HelperAI GetHelper(string helperId)
        {
            return activeHelpers.ContainsKey(helperId) ? activeHelpers[helperId] : null;
        }
        
        /// <summary>
        /// Get total number of deployed helpers
        /// </summary>
        public int GetTotalDeployedHelpers()
        {
            return activeHelpers.Count;
        }
        
        /// <summary>
        /// Get helpers by type
        /// </summary>
        public List<HelperAI> GetHelpersByType(DropPuzzle.HelperType type)
        {
            return activeHelpers.Values.Where(h => h.helperType == type).ToList();
        }
        
        #endregion
        
        #region Validation & Utilities
        
        /// <summary>
        /// Validate if a helper can be deployed
        /// </summary>
        private bool ValidateDeployment(DropPuzzle.HelperType helperType, string zoneId)
        {
            // Check if player data is available
            if (playerData == null)
            {
                Debug.LogError("[HelperDeploymentSystem] PlayerData not available!");
                return false;
            }
            
            // Check if player owns this helper type
            int ownedCount = helperType == DropPuzzle.HelperType.Goblin ? 
                playerData.Helpers.ownedGoblins : 
                playerData.Helpers.ownedGhouls;
                
            if (ownedCount <= 0)
            {
                Debug.LogWarning($"[HelperDeploymentSystem] Player doesn't own any {helperType} helpers!");
                return false;
            }
            
            // Check if already at max deployed helpers
            if (GetTotalDeployedHelpers() >= playerData.Helpers.GetMaxHelpers())
            {
                Debug.LogWarning($"[HelperDeploymentSystem] At maximum helper deployment capacity ({playerData.Helpers.GetMaxHelpers()})");
                return false;
            }
            
            // Check if zone is unlocked
            if (!playerData.Helpers.unlockedZones.Contains(zoneId))
            {
                Debug.LogWarning($"[HelperDeploymentSystem] Zone '{zoneId}' is not unlocked!");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Get helper prefab for specific type
        /// </summary>
        private GameObject GetHelperPrefab(DropPuzzle.HelperType helperType)
        {
            switch (helperType)
            {
                case DropPuzzle.HelperType.Goblin:
                    return goblinPrefab;
                case DropPuzzle.HelperType.Ghoul:
                    return ghoulPrefab;
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// Get a random deployment position within a zone
        /// </summary>
        private Vector3 GetRandomDeploymentPosition(string zoneId)
        {
            // Find ZoneManager in scene for this zone
            var zoneManagers = FindObjectsOfType<ZoneManager>();
            var targetZone = zoneManagers.FirstOrDefault(zm => zm.zoneId == zoneId);
            
            if (targetZone != null && targetZone.deploymentBounds != null)
            {
                // Use zone deployment bounds
                Bounds bounds = targetZone.deploymentBounds.bounds;
                Vector3 randomPoint = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    bounds.center.y + spawnHeightOffset,
                    Random.Range(bounds.min.z, bounds.max.z)
                );
                
                // Try to place on ground
                if (Physics.Raycast(randomPoint + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
                {
                    return hit.point + Vector3.up * spawnHeightOffset;
                }
                
                return randomPoint;
            }
            
            // Fallback: Deploy near player or at world origin
            Vector3 fallbackPosition = Vector3.zero;
            if (Camera.main != null)
            {
                fallbackPosition = Camera.main.transform.position + Vector3.forward * 5f;
            }
            
            return fallbackPosition + Vector3.up * spawnHeightOffset;
        }
        
        #endregion
        
        #region Passive Generation
        
        /// <summary>
        /// Passive rice generation from deployed helpers
        /// </summary>
        private IEnumerator PassiveRiceGenerationLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(passiveGenerationInterval);
                
                if (activeHelpers.Count > 0 && playerData != null)
                {
                    float totalRicePerSecond = playerData.Helpers.ricePerSecond;
                    int passiveRice = Mathf.FloorToInt(totalRicePerSecond * activeHelpers.Count * passiveGenerationInterval);
                    
                    if (passiveRice > 0)
                    {
                        playerData.AddCurrency(passiveRice);
                        
                        if (enableDebugLogging && Time.frameCount % 300 == 0) // Log every 5 seconds
                        {
                            Debug.Log($"[HelperDeploymentSystem] Passive generation: +{passiveRice} rice from {activeHelpers.Count} helpers");
                        }
                    }
                }
            }
        }
        
        #endregion
        
        #region Save/Load System
        
        /// <summary>
        /// Save deployed helper states
        /// </summary>
        public void SaveDeployedHelpers()
        {
            if (playerData == null) return;
            
            // Update deployed helper data with current stats
            foreach (var helper in activeHelpers.Values)
            {
                var deployedHelper = playerData.Helpers.deployedHelpers.FirstOrDefault(h => h.helperId == helper.helperId);
                if (deployedHelper != null)
                {
                    var stats = helper.GetStats();
                    deployedHelper.totalRiceCollected = stats.totalRiceCollected;
                }
            }
            
            playerData.SavePlayerData();
            
            if (enableDebugLogging)
            {
                Debug.Log($"[HelperDeploymentSystem] Saved {activeHelpers.Count} deployed helpers");
            }
        }
        
        /// <summary>
        /// Load and restore deployed helpers
        /// </summary>
        public void LoadDeployedHelpers()
        {
            if (playerData == null) return;
            
            int restoredCount = 0;
            var helpersToRestore = playerData.Helpers.deployedHelpers.ToList();
            
            foreach (var deployedHelper in helpersToRestore)
            {
                // Validate zone is still available
                if (ZoneManager.GetZone(deployedHelper.zoneId) != null)
                {
                    if (DeployHelper(deployedHelper.type, deployedHelper.zoneId))
                    {
                        restoredCount++;
                    }
                }
                else
                {
                    // Remove invalid zone helpers
                    playerData.Helpers.deployedHelpers.Remove(deployedHelper);
                }
            }
            
            if (restoredCount > 0 && enableDebugLogging)
            {
                Debug.Log($"[HelperDeploymentSystem] Restored {restoredCount} deployed helpers");
            }
        }
        
        #endregion
        
        #region Debug & Information
        
        /// <summary>
        /// Get comprehensive system status
        /// </summary>
        public HelperDeploymentStatus GetSystemStatus()
        {
            var status = new HelperDeploymentStatus();
            
            if (playerData != null)
            {
                status.totalOwnedHelpers = playerData.Helpers.ownedGoblins + playerData.Helpers.ownedGhouls;
                status.maxDeployableHelpers = playerData.Helpers.GetMaxHelpers();
                status.currentEfficiencyRate = playerData.Helpers.ricePerSecond;
            }
            
            status.currentlyDeployed = GetTotalDeployedHelpers();
            status.goblinsDeployed = GetHelpersByType(DropPuzzle.HelperType.Goblin).Count;
            status.ghoulsDeployed = GetHelpersByType(DropPuzzle.HelperType.Ghoul).Count;
            
            status.activeZones = activeHelpers.Values.Select(h => h.deployedZoneId).Distinct().ToList();
            
            return status;
        }
        
        /// <summary>
        /// Debug method to show system status
        /// </summary>
        public void DEBUG_ShowSystemStatus()
        {
            var status = GetSystemStatus();
            
            Debug.Log("=== Helper Deployment System Status ===");
            Debug.Log($"Owned Helpers: {status.totalOwnedHelpers}");
            Debug.Log($"Deployed: {status.currentlyDeployed}/{status.maxDeployableHelpers}");
            Debug.Log($"Goblins: {status.goblinsDeployed}, Ghouls: {status.ghoulsDeployed}");
            Debug.Log($"Active Zones: {string.Join(", ", status.activeZones)}");
            Debug.Log($"Efficiency Rate: {status.currentEfficiencyRate:F1} rice/second");
            Debug.Log("=========================================");
        }
        
        private void OnDrawGizmos()
        {
            // Draw deployment system info in scene
            if (Application.isPlaying)
            {
                foreach (var helper in activeHelpers.Values)
                {
                    if (helper != null)
                    {
                        // Draw helper connection to deployment system
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawLine(transform.position, helper.transform.position);
                    }
                }
            }
        }
        
        #endregion
        
        #region Static Helpers
        
        /// <summary>
        /// Quick deploy helper from anywhere in code
        /// </summary>
        public static bool QuickDeploy(DropPuzzle.HelperType type, string zoneId)
        {
            if (Instance != null)
            {
                return Instance.DeployHelper(type, zoneId);
            }
            
            Debug.LogError("[HelperDeploymentSystem] No instance available for QuickDeploy!");
            return false;
        }
        
        /// <summary>
        /// Quick recall helper from anywhere in code
        /// </summary>
        public static bool QuickRecall(string helperId)
        {
            if (Instance != null)
            {
                return Instance.RecallHelper(helperId);
            }
            
            Debug.LogError("[HelperDeploymentSystem] No instance available for QuickRecall!");
            return false;
        }
        
        #endregion
    }
    
    /// <summary>
    /// Status information for the helper deployment system
    /// </summary>
    [System.Serializable]
    public class HelperDeploymentStatus
    {
        public int totalOwnedHelpers;
        public int currentlyDeployed;
        public int maxDeployableHelpers;
        public int goblinsDeployed;
        public int ghoulsDeployed;
        public float currentEfficiencyRate;
        public List<string> activeZones;
    }
}