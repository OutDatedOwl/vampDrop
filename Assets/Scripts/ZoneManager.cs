using UnityEngine;
using System.Collections.Generic;

namespace Vampire.Helpers
{
    using DropPuzzle;
    
    /// <summary>
    /// Manages zones where helpers can be deployed
    /// Tracks unlocked areas and validates deployment locations
    /// </summary>
    public class ZoneManager : MonoBehaviour
    {
        [Header("Zone Management")]
        [Tooltip("Unique ID for this zone (e.g. 'fps_zone_1', 'puzzle_arena_2')")]
        public string zoneId = "fps_zone_1";
        
        [Tooltip("Display name for UI")]
        public string zoneName = "Training Ground";
        
        [Tooltip("Description of this zone")]
        public string zoneDescription = "A basic area perfect for new helpers to learn rice collection.";
        
        [Header("Zone Properties")]
        [Tooltip("Rice spawning density in this zone")]
        public float riceDensity = 1.0f;
        
        [Tooltip("Maximum helpers that can be deployed here")]
        public int maxHelpersAllowed = 3;
        
        [Tooltip("Zone difficulty (affects helper efficiency)")]
        [Range(1, 10)]
        public int zoneDifficulty = 1;
        
        [Tooltip("Prerequisites to unlock this zone")]
        public List<string> unlockRequirements = new List<string>();
        
        [Header("Deployment Bounds")]
        [Tooltip("Area where helpers can move and collect rice")]
        public BoxCollider deploymentBounds;
        
        [Header("Debug")]
        public bool showDebugInfo = false;
        
        // Static tracking
        private static Dictionary<string, ZoneManager> allZones = new Dictionary<string, ZoneManager>();
        
        // Singleton for easy access
        public static ZoneManager Instance { get; private set; }
        
        #region Unity Events
        
        private void Awake()
        {
            // Register this zone
            if (!allZones.ContainsKey(zoneId))
            {
                allZones[zoneId] = this;
                Debug.Log($"[ZoneManager] Registered zone: {zoneId} ({zoneName})");
            }
            else
            {
                Debug.LogWarning($"[ZoneManager] Duplicate zone ID detected: {zoneId}");
            }
            
            // Set as instance if this is the first/main zone
            if (Instance == null || zoneId == "fps_zone_1")
            {
                Instance = this;
            }
            
            // Setup deployment bounds if not assigned
            if (deploymentBounds == null)
            {
                SetupDefaultBounds();
            }
            
            // Auto-unlock the first zone on first load
            if (zoneId == "fps_zone_1")
            {
                UnlockZone();
            }
        }
        
        private void Start()
        {
            // Check if this zone should be unlocked based on requirements
            CheckUnlockEligibility();
        }
        
        private void OnDestroy()
        {
            // Unregister zone
            if (allZones.ContainsKey(zoneId) && allZones[zoneId] == this)
            {
                allZones.Remove(zoneId);
            }
            
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        #endregion
        
        #region Zone Management
        
        /// <summary>
        /// Unlock this zone for helper deployment
        /// </summary>
        public void UnlockZone()
        {
            var playerData = DropPuzzle.PlayerDataManager.Instance;
            if (playerData != null)
            {
                if (!playerData.Helpers.unlockedZones.Contains(zoneId))
                {
                    playerData.Helpers.unlockedZones.Add(zoneId);
                    Debug.Log($"[ZoneManager] ✅ Zone unlocked: {zoneName} ({zoneId})");
                    
                    // Save progress
                    playerData.SavePlayerData();
                }
            }
        }
        
        /// <summary>
        /// Check if this zone is unlocked
        /// </summary>
        public bool IsUnlocked()
        {
            var playerData = DropPuzzle.PlayerDataManager.Instance;
            return playerData?.Helpers.unlockedZones.Contains(zoneId) ?? false;
        }
        
        /// <summary>
        /// Check if player meets requirements to unlock this zone
        /// </summary>
        public bool MeetsUnlockRequirements()
        {
            var playerData = DropPuzzle.PlayerDataManager.Instance;
            if (playerData == null) return false;
            
            foreach (string requirement in unlockRequirements)
            {
                if (!CheckRequirement(requirement, playerData))
                {
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Check a specific unlock requirement
        /// </summary>
        private bool CheckRequirement(string requirement, DropPuzzle.PlayerDataManager playerData)
        {
            // Parse requirement strings (e.g., "tutorial_complete", "rice_collected:100", "zone_unlocked:fps_zone_1")
            string[] parts = requirement.Split(':');
            string requirementType = parts[0];
            
            switch (requirementType.ToLower())
            {
                case "tutorial_complete":
                    return playerData.TutorialCompleted;
                    
                case "rice_collected":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int riceRequired))
                    {
                        return playerData.TotalCurrencyEarned >= riceRequired;
                    }
                    break;
                    
                case "zone_unlocked":
                    if (parts.Length > 1)
                    {
                        return playerData.Helpers.unlockedZones.Contains(parts[1]);
                    }
                    break;
                    
                case "runs_completed":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int runsRequired))
                    {
                        return playerData.TotalRunsCompleted >= runsRequired;
                    }
                    break;
                    
                case "currency_earned":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int currencyRequired))
                    {
                        return playerData.TotalCurrencyEarned >= currencyRequired;
                    }
                    break;
                    
                default:
                    Debug.LogWarning($"[ZoneManager] Unknown requirement type: {requirementType}");
                    break;
            }
            
            return false;
        }
        
        /// <summary>
        /// Check unlock eligibility and auto-unlock if requirements met
        /// </summary>
        private void CheckUnlockEligibility()
        {
            if (!IsUnlocked() && MeetsUnlockRequirements())
            {
                UnlockZone();
            }
        }
        
        #endregion
        
        #region Helper Deployment
        
        /// <summary>
        /// Check if more helpers can be deployed to this zone
        /// </summary>
        public bool CanDeployMoreHelpers()
        {
            if (!IsUnlocked()) return false;
            
            int currentHelpersInZone = GetHelpersInZone().Count;
            return currentHelpersInZone < maxHelpersAllowed;
        }
        
        /// <summary>
        /// Get all helpers currently deployed in this zone
        /// </summary>
        public List<DropPuzzle.DeployedHelper> GetHelpersInZone()
        {
            var playerData = DropPuzzle.PlayerDataManager.Instance;
            if (playerData == null) return new List<DropPuzzle.DeployedHelper>();
            
            List<DropPuzzle.DeployedHelper> helpersInZone = new List<DropPuzzle.DeployedHelper>();
            foreach (var helper in playerData.Helpers.deployedHelpers)
            {
                if (helper.zoneId == zoneId)
                {
                    helpersInZone.Add(helper);
                }
            }
            
            return helpersInZone;
        }
        
        /// <summary>
        /// Get a random valid deployment position within the zone bounds
        /// </summary>
        public Vector3 GetRandomDeploymentPosition()
        {
            if (deploymentBounds == null)
            {
                Debug.LogWarning($"[ZoneManager] No deployment bounds set for zone {zoneId}");
                return transform.position;
            }
            
            Bounds bounds = deploymentBounds.bounds;
            
            Vector3 randomPos = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.center.y, // Keep Y at bounds center
                Random.Range(bounds.min.z, bounds.max.z)
            );
            
            return randomPos;
        }
        
        /// <summary>
        /// Check if a position is within the deployment bounds
        /// </summary>
        public bool IsValidDeploymentPosition(Vector3 position)
        {
            if (deploymentBounds == null) return true;
            
            return deploymentBounds.bounds.Contains(position);
        }
        
        #endregion
        
        #region Static Zone Access
        
        /// <summary>
        /// Get a zone by ID
        /// </summary>
        public static ZoneManager GetZone(string zoneId)
        {
            allZones.TryGetValue(zoneId, out ZoneManager zone);
            return zone;
        }
        
        /// <summary>
        /// Get all registered zones
        /// </summary>
        public static Dictionary<string, ZoneManager> GetAllZones()
        {
            return new Dictionary<string, ZoneManager>(allZones);
        }
        
        /// <summary>
        /// Get all unlocked zones
        /// </summary>
        public static List<ZoneManager> GetUnlockedZones()
        {
            List<ZoneManager> unlockedZones = new List<ZoneManager>();
            
            foreach (var zone in allZones.Values)
            {
                if (zone.IsUnlocked())
                {
                    unlockedZones.Add(zone);
                }
            }
            
            return unlockedZones;
        }
        
        #endregion
        
        #region Setup & Debug
        
        /// <summary>
        /// Setup default deployment bounds if none assigned
        /// </summary>
        private void SetupDefaultBounds()
        {
            GameObject boundsObj = new GameObject($"{zoneId}_DeploymentBounds");
            boundsObj.transform.SetParent(transform);
            boundsObj.transform.localPosition = Vector3.zero;
            
            deploymentBounds = boundsObj.AddComponent<BoxCollider>();
            deploymentBounds.isTrigger = true;
            deploymentBounds.size = new Vector3(20f, 5f, 20f); // Default 20x5x20 area
            
            Debug.Log($"[ZoneManager] Created default deployment bounds for zone {zoneId}");
        }
        
        private void OnDrawGizmosSelected()
        {
            if (deploymentBounds != null)
            {
                // Draw deployment bounds
                Gizmos.color = IsUnlocked() ? Color.green : Color.red;
                Gizmos.matrix = deploymentBounds.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, deploymentBounds.size);
                
                // Draw fill for unlocked zones
                if (IsUnlocked())
                {
                    Gizmos.color = new Color(0, 1, 0, 0.1f);
                    Gizmos.DrawCube(Vector3.zero, deploymentBounds.size);
                }
            }
            
            if (showDebugInfo)
            {
                // Draw zone info (would need text rendering in actual game)
                Debug.Log($"Zone: {zoneName} | Unlocked: {IsUnlocked()} | Helpers: {GetHelpersInZone().Count}/{maxHelpersAllowed}");
            }
        }
        
        /// <summary>
        /// Debug method to force unlock this zone
        /// </summary>
        public void DEBUG_ForceUnlock()
        {
            UnlockZone();
            Debug.Log($"[ZoneManager] DEBUG: Force unlocked {zoneName}");
        }
        
        /// <summary>
        /// Debug method to show zone status
        /// </summary>
        public void DEBUG_ShowStatus()
        {
            Debug.Log($"=== Zone Status: {zoneName} ({zoneId}) ===");
            Debug.Log($"Unlocked: {IsUnlocked()}");
            Debug.Log($"Meets Requirements: {MeetsUnlockRequirements()}");
            Debug.Log($"Helpers Deployed: {GetHelpersInZone().Count}/{maxHelpersAllowed}");
            Debug.Log($"Can Deploy More: {CanDeployMoreHelpers()}");
            Debug.Log($"Rice Density: {riceDensity}");
            Debug.Log($"Difficulty: {zoneDifficulty}");
            
            if (unlockRequirements.Count > 0)
            {
                Debug.Log($"Requirements: {string.Join(", ", unlockRequirements)}");
            }
            
            Debug.Log("================================");
        }
        
        #endregion
    }
}