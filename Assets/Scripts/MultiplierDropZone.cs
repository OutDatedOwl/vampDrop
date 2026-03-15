using UnityEngine;

namespace Vampire.DropPuzzle
{
    /// <summary>
    /// Multiplier drop zone - awards bonus points when rice balls pass through
    /// Place this on trigger volumes in your puzzle
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MultiplierDropZone : MonoBehaviour
    {
        [Header("Multiplier Settings")]
        [Tooltip("Points multiplier (2 = double points, 3 = triple, etc.)")]
        public int Multiplier = 2;
        
        [Tooltip("Visual color for this multiplier zone")]
        public Color ZoneColor = new Color(1f, 0.65f, 0f, 1f); // Orange for 2x
        
        [Header("Visual Effects")]
        [Tooltip("Optional particle effect when balls enter")]
        public ParticleSystem MultiplierEffect;
        
        [Tooltip("Optional sound when balls enter")]
        public AudioClip MultiplierSound;
        
        [Header("Setup Notes")]
        [TextArea(2, 4)]
        public string setupInfo = "REQUIRED: BoxCollider with 'Is Trigger' enabled!\n" +
                                   "This component detects rice balls passing through.";
        
        private void Start()
        {
            SetupCollider();
            SetupVisuals();
        }
        
        private void SetupCollider()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true; // Ensure it's a trigger
                Debug.Log($"[MultiplierDropZone] {name} setup as {Multiplier}x zone (trigger={col.isTrigger})");
            }
            else
            {
                Debug.LogError($"[MultiplierDropZone] {name} is missing a Collider component!");
            }
        }
        
        private void SetupVisuals()
        {
            // Set material color based on multiplier
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat.shader == null)
                {
                    mat = new Material(Shader.Find("Standard"));
                }
                mat.color = ZoneColor;
                mat.SetFloat("_Metallic", 0.5f);
                mat.SetFloat("_Smoothness", 0.8f);
                renderer.material = mat;
            }
            
            // Add pulsing animation for visual feedback
            StartCoroutine(PulseEffect());
        }
        
        private System.Collections.IEnumerator PulseEffect()
        {
            Vector3 originalScale = transform.localScale;
            float pulseSpeed = 2f;
            float pulseAmount = 0.1f;
            
            while (true)
            {
                float pulse = 1f + (Mathf.Sin(Time.time * pulseSpeed) * pulseAmount);
                transform.localScale = originalScale * pulse;
                yield return null;
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            // Check if a rice ball entered
            if (other.CompareTag("RiceBall") || other.name.Contains("RiceBall"))
            {
                Debug.Log($"[MultiplierDropZone] Rice ball entered {Multiplier}x zone!");
                
                // TODO: Apply multiplier to ball scoring
                // You can add a component to the ball or send a message here
                
                // Play effect
                if (MultiplierEffect != null)
                {
                    MultiplierEffect.Play();
                }
                
                // Play sound
                if (MultiplierSound != null)
                {
                    AudioSource.PlayClipAtPoint(MultiplierSound, transform.position);
                }
            }
        }
        
        private void OnValidate()
        {
            // Update color preview in editor
            if (Application.isPlaying) return;
            
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                renderer.sharedMaterial.color = ZoneColor;
            }
        }
    }
}