using UnityEngine;

namespace Vampire.DropPuzzle
{
    /// <summary>
    /// Component to mark zones for dynamic enhancement based on player progression
    /// Place this on placeholder objects in your puzzle prefabs
    /// </summary>
    public class EnhancementMarker : MonoBehaviour
    {
        [Header("Enhancement Settings")]
        [Tooltip("What type of enhancement this marker represents")]
        public EnhancementType EnhancementType = EnhancementType.MultiplierZone;
        
        [Tooltip("Copy tags and layers to the replacement prefab")]
        public bool CopyTagsAndLayers = true;
        
        [Header("Visual Indicator")]
        [Tooltip("Show enhancement potential in editor (gizmo wireframe)")]
        public bool ShowInEditor = true;
        
        [Header("Info")]
        [TextArea(3, 5)]
        public string notes = "This marker will be replaced at runtime based on player stats. The replacement prefab will have its own collider and components.";
        
        private void OnDrawGizmos()
        {
            if (ShowInEditor)
            {
                // Draw different colors for different enhancement types
                switch (EnhancementType)
                {
                    case EnhancementType.MultiplierZone:
                        Gizmos.color = new Color(1f, 0.8f, 0f, 0.5f); // Yellow
                        break;
                    case EnhancementType.BonusZone:
                        Gizmos.color = new Color(1f, 0f, 1f, 0.5f); // Magenta
                        break;
                    case EnhancementType.ConditionalWall:
                        Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // Cyan
                        break;
                }
                
                Gizmos.DrawWireCube(transform.position, transform.localScale);
                Gizmos.DrawCube(transform.position, transform.localScale * 0.1f); // Small center dot
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw solid when selected
            if (ShowInEditor)
            {
                Color color = Gizmos.color;
                color.a = 0.3f;
                Gizmos.color = color;
                Gizmos.DrawCube(transform.position, transform.localScale);
            }
        }
    }
    
    /// <summary>
    /// Types of dynamic enhancements available
    /// </summary>
    public enum EnhancementType
    {
        MultiplierZone,     // Can become 2x, 3x, etc. based on rice collected
        BonusZone,          // Special bonus zones based on helper count
        ConditionalWall     // Walls that appear/disappear based on difficulty
    }
}