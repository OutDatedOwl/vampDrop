using UnityEngine;
using UnityEngine.UI;

namespace Vampire.Player
{
    /// <summary>
    /// Simple UI for controlling audio volumes during gameplay
    /// Can be toggled with a key press to adjust audio settings
    /// </summary>
    public class AudioControlUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The panel containing all audio controls")]
        public GameObject audioControlPanel;
        
        [Tooltip("Master volume slider")]
        public Slider masterVolumeSlider;
        
        [Tooltip("Music volume slider")]
        public Slider musicVolumeSlider;
        
        [Tooltip("Footstep volume slider")]
        public Slider footstepVolumeSlider;
        
        [Tooltip("SFX volume slider")]
        public Slider sfxVolumeSlider;
        
        [Tooltip("Mute toggle button")]
        public Toggle muteToggle;
        
        [Header("Settings")]
        [Tooltip("Key to toggle the audio control panel")]
        public KeyCode toggleKey = KeyCode.M;
        
        [Tooltip("Should the UI be visible at start?")]
        public bool showOnStart = false;
        
        // Private variables
        private FPSAudioManager audioManager;
        private bool isPanelOpen = false;
        
        private void Start()
        {
            // Find audio manager
            audioManager = FPSAudioManager.Instance;
            if (audioManager == null)
            {
                audioManager = FindObjectOfType<FPSAudioManager>();
            }
            
            // Setup initial panel state
            isPanelOpen = showOnStart;
            if (audioControlPanel != null)
            {
                audioControlPanel.SetActive(isPanelOpen);
            }
            
            // Setup sliders if available
            SetupSliders();
            
            // Setup toggle if available
            if (muteToggle != null)
            {
                muteToggle.onValueChanged.AddListener(OnMuteToggleChanged);
            }
            
            // Debug.Log("[AudioControlUI] Initialized");
        }
        
        private void Update()
        {
            // Toggle panel with key press
            if (Input.GetKeyDown(toggleKey))
            {
                TogglePanel();
            }
        }
        
        /// <summary>
        /// Setup slider references and initial values
        /// </summary>
        private void SetupSliders()
        {
            if (audioManager == null) return;
            
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = audioManager.masterVolume;
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }
            
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.value = audioManager.musicVolume;
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }
            
            if (footstepVolumeSlider != null)
            {
                footstepVolumeSlider.value = audioManager.footstepVolume;
                footstepVolumeSlider.onValueChanged.AddListener(OnFootstepVolumeChanged);
            }
            
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.value = audioManager.sfxVolume;
                sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
            }
        }
        
        /// <summary>
        /// Toggle the audio control panel
        /// </summary>
        public void TogglePanel()
        {
            isPanelOpen = !isPanelOpen;
            
            if (audioControlPanel != null)
            {
                audioControlPanel.SetActive(isPanelOpen);
            }
            
            // Show/hide cursor based on panel state
            if (isPanelOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            
            // Debug.Log($"[AudioControlUI] Panel {(isPanelOpen ? "opened" : "closed")}");
        }
        
        /// <summary>
        /// Close the panel
        /// </summary>
        public void ClosePanel()
        {
            isPanelOpen = false;
            if (audioControlPanel != null)
            {
                audioControlPanel.SetActive(false);
            }
            
            // Lock cursor again
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        // Slider event handlers
        private void OnMasterVolumeChanged(float value)
        {
            if (audioManager != null)
            {
                audioManager.SetMasterVolume(value);
            }
        }
        
        private void OnMusicVolumeChanged(float value)
        {
            if (audioManager != null)
            {
                audioManager.SetMusicVolume(value);
            }
        }
        
        private void OnFootstepVolumeChanged(float value)
        {
            if (audioManager != null)
            {
                audioManager.SetFootstepVolume(value);
            }
        }
        
        private void OnSFXVolumeChanged(float value)
        {
            if (audioManager != null)
            {
                audioManager.SetSFXVolume(value);
            }
        }
        
        private void OnMuteToggleChanged(bool isMuted)
        {
            if (audioManager != null)
            {
                audioManager.SetMuted(isMuted);
            }
        }
        
        /// <summary>
        /// Update UI values to match current audio manager settings
        /// Useful if audio settings are changed elsewhere
        /// </summary>
        public void RefreshUI()
        {
            if (audioManager == null) return;
            
            if (masterVolumeSlider != null)
                masterVolumeSlider.value = audioManager.masterVolume;
            if (musicVolumeSlider != null)
                musicVolumeSlider.value = audioManager.musicVolume;
            if (footstepVolumeSlider != null)
                footstepVolumeSlider.value = audioManager.footstepVolume;
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.value = audioManager.sfxVolume;
        }
        
        private void OnDestroy()
        {
            // Clean up event listeners
            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.RemoveAllListeners();
            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.RemoveAllListeners();
            if (footstepVolumeSlider != null)
                footstepVolumeSlider.onValueChanged.RemoveAllListeners();
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            if (muteToggle != null)
                muteToggle.onValueChanged.RemoveAllListeners();
        }
    }
}