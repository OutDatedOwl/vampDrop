using UnityEngine;
using System.Collections.Generic;

namespace Vampire.DropPuzzle
{
    public class PuzzleEnhancer : MonoBehaviour
    {
        [Header("Enhancement Settings")]
        [Tooltip("Check user stats to determine enhancements")]
        public bool UseUserStats = true;
        
        [Tooltip("Use fake stats for testing (overrides UseUserStats)")]
        public bool UseTestStats = false;
        
        [Header("Test Stats (only used if UseTestStats is enabled)")]
        [Tooltip("Test value for rice collected")]
        public int TestRiceCollected = 5000;
        
        [Tooltip("Test value for helper count")]
        public int TestHelperCount = 5;
        
        [Header("Enhancement Prefabs")]
        [Tooltip("2x Multiplier drop zone prefab")]
        public GameObject Multiplier2xPrefab;
        
        [Tooltip("3x Multiplier drop zone prefab")]  
        public GameObject Multiplier3xPrefab;
        
        [Tooltip("Special bonus zone prefab")]
        public GameObject BonusZonePrefab;
        
        [Header("User Stats Thresholds")]
        [Tooltip("Rice collected needed for 2x zones")]
        public int RiceFor2xZones = 1000;
        
        [Tooltip("Rice collected needed for 3x zones")]
        public int RiceFor3xZones = 5000;
        
        [Tooltip("Helper count needed for bonus zones")]
        public int HelpersForBonusZones = 3;

        public void EnhancePuzzle(GameObject puzzleInstance)
        {
            if (puzzleInstance == null)
            {
                Debug.LogError("[PuzzleEnhancer] ❌ Puzzle instance is NULL!");
                return;
            }
            
            Debug.Log($"[PuzzleEnhancer] 🔧 Starting puzzle enhancement for: {puzzleInstance.name}");
            
            // Get user stats
            UserProgression userStats = GetUserStats();
            Debug.Log($"[PuzzleEnhancer] 📊 User Stats: Rice={userStats.TotalRiceCollected}, Helpers={userStats.ActiveHelperCount}, Level={userStats.CompletedLevels}");
            Debug.Log($"[PuzzleEnhancer] 🎯 Thresholds: 2x@{RiceFor2xZones} rice, 3x@{RiceFor3xZones} rice, Bonus@{HelpersForBonusZones} helpers");
            
            // Find enhancement markers in the puzzle
            EnhancementMarker[] markers = puzzleInstance.GetComponentsInChildren<EnhancementMarker>();
            
            Debug.Log($"[PuzzleEnhancer] 🔍 Found {markers.Length} EnhancementMarkers in puzzle");
            
            if (markers.Length == 0)
            {
                Debug.LogWarning("[PuzzleEnhancer] ⚠️ No EnhancementMarkers found! Add EnhancementMarker components to your puzzle prefab to enable dynamic zones.");
                return;
            }
            
            int enhancedCount = 0;
            int removedCount = 0;
            foreach (EnhancementMarker marker in markers)
            {
                bool wasEnhanced = ProcessEnhancementMarker(marker, userStats);
                if (wasEnhanced) 
                    enhancedCount++;
                else
                    removedCount++;
            }
            
            Debug.Log($"[PuzzleEnhancer] ✅ Result: {enhancedCount} enhanced, {removedCount} removed (unqualified)");
        }

        private bool ProcessEnhancementMarker(EnhancementMarker marker, UserProgression stats)
        {
            GameObject replacementPrefab = null;
            string enhancementType = "none";
            
            Debug.Log($"[PuzzleEnhancer] 🔎 Processing marker: {marker.name} (Type: {marker.EnhancementType})");
            
            // Determine what enhancement to apply based on marker type and user stats
            switch (marker.EnhancementType)
            {
                case EnhancementType.MultiplierZone:
                    if (stats.TotalRiceCollected >= RiceFor3xZones && Multiplier3xPrefab != null)
                    {
                        replacementPrefab = Multiplier3xPrefab;
                        enhancementType = "3x Multiplier";
                        Debug.Log($"[PuzzleEnhancer]   → Qualified for 3x (Rice: {stats.TotalRiceCollected} >= {RiceFor3xZones})");
                    }
                    else if (stats.TotalRiceCollected >= RiceFor2xZones && Multiplier2xPrefab != null)
                    {
                        replacementPrefab = Multiplier2xPrefab;
                        enhancementType = "2x Multiplier";
                        Debug.Log($"[PuzzleEnhancer]   → Qualified for 2x (Rice: {stats.TotalRiceCollected} >= {RiceFor2xZones})");
                    }
                    else
                    {
                        Debug.Log($"[PuzzleEnhancer]   → Not qualified (Rice: {stats.TotalRiceCollected}, need {RiceFor2xZones} for 2x)");
                    }
                    break;
                    
                case EnhancementType.BonusZone:
                    if (stats.ActiveHelperCount >= HelpersForBonusZones && BonusZonePrefab != null)
                    {
                        replacementPrefab = BonusZonePrefab;
                        enhancementType = "Bonus Zone";
                        Debug.Log($"[PuzzleEnhancer]   → Qualified for Bonus (Helpers: {stats.ActiveHelperCount} >= {HelpersForBonusZones})");
                    }
                    else
                    {
                        Debug.Log($"[PuzzleEnhancer]   → Not qualified (Helpers: {stats.ActiveHelperCount}, need {HelpersForBonusZones})");
                    }
                    break;
                    
                case EnhancementType.ConditionalWall:
                    Debug.Log($"[PuzzleEnhancer]   → ConditionalWall not yet implemented");
                    break;
            }
            
            // Replace the marker with the enhanced zone
            if (replacementPrefab != null)
            {
                ReplaceWithEnhancement(marker, replacementPrefab, enhancementType);
                return true;
            }
            else
            {
                // Destroy the marker since player doesn't qualify for this enhancement
                Debug.Log($"[PuzzleEnhancer]   🗑️ Removing unqualified marker: {marker.name}");
                DestroyImmediate(marker.gameObject);
                return false;
            }
        }

        private void ReplaceWithEnhancement(EnhancementMarker marker, GameObject replacementPrefab, string enhancementType)
        {
            // Store original transform data
            Vector3 position = marker.transform.position;
            Quaternion rotation = marker.transform.rotation;
            Vector3 scale = marker.transform.localScale;
            Transform parent = marker.transform.parent;
            
            // Create the enhanced zone
            GameObject enhanced = Instantiate(replacementPrefab, position, rotation, parent);
            enhanced.transform.localScale = scale;
            enhanced.name = $"{marker.name}_{enhancementType.Replace(" ", "")}";
            
            // Copy any special properties from the marker
            if (marker.CopyTagsAndLayers)
            {
                enhanced.tag = marker.gameObject.tag;
                enhanced.layer = marker.gameObject.layer;
            }
            
            // Destroy the original marker
            DestroyImmediate(marker.gameObject);
            
            Debug.Log($"[PuzzleEnhancer] ✨ Enhanced {marker.name} → {enhancementType}");
        }

        private UserProgression GetUserStats()
        {
            // Test mode override for debugging
            if (UseTestStats)
            {
                Debug.Log($"[PuzzleEnhancer] 🧪 Using TEST STATS: Rice={TestRiceCollected}, Helpers={TestHelperCount}");
                return new UserProgression
                {
                    TotalRiceCollected = TestRiceCollected,
                    ActiveHelperCount = TestHelperCount,
                    CompletedLevels = 10
                };
            }
            
            if (!UseUserStats)
            {
                // Return test stats for debugging
                return new UserProgression
                {
                    TotalRiceCollected = 2000,
                    ActiveHelperCount = 2,
                    CompletedLevels = 5
                };
            }
            
            // Connect to your existing PlayerDataManager system
            if (PlayerDataManager.Instance != null)
            {
                var pdm = PlayerDataManager.Instance;
                
                return new UserProgression
                {
                    TotalRiceCollected = pdm.RiceGrains,
                    ActiveHelperCount = pdm.Helpers.ownedGoblins + pdm.Helpers.ownedGhouls,
                    CompletedLevels = pdm.HighestLevelReached
                };
            }
            
            // Fallback if PlayerDataManager not found
            Debug.LogWarning("[PuzzleEnhancer] PlayerDataManager.Instance not found! Using defaults.");
            return new UserProgression
            {
                TotalRiceCollected = 0,
                ActiveHelperCount = 0,
                CompletedLevels = 0
            };
        }
    }

    // Data structure to hold user progression
    [System.Serializable]
    public struct UserProgression
    {
        public int TotalRiceCollected;
        public int ActiveHelperCount;
        public int CompletedLevels;
    }
}