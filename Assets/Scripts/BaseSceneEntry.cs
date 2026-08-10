using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire.DropPuzzle
{
    /// <summary>
    /// Place on a trigger collider at the house/stairs in FPS_Collect scene.
    /// F key enters the Base scene. On tutorial step 4, TutorialManager
    /// handles the transition (comic plays first, then loads Base).
    /// </summary>
    public class BaseSceneEntry : MonoBehaviour
    {
        [Header("Scene")]
        public string baseSceneName = "Base";

        [Header("UI")]
        public string enterPrompt = "Press [F] to enter the house";

        private bool playerInRange;
        private GUIStyle _guiStyle;

        private TutorialManager Tutorial => TutorialManager.Instance;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) playerInRange = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player")) playerInRange = false;
        }

        private void Update()
        {
            if (!playerInRange) return;
            if (!Input.GetKeyDown(KeyCode.F)) return;

            // Block entry before quest 4 (Go into the House) is active
            if (Tutorial != null && Tutorial.tutorialActive && Tutorial.tutorialStep < 4) return;

            TryEnterBase();
        }

        private void TryEnterBase()
        {
            if (Tutorial != null && Tutorial.tutorialActive && Tutorial.tutorialStep == 4)
            {
                // TutorialManager triggers the comic which loads Base as its next scene
                Tutorial.NotifyEnteredBase();
            }
            else
            {
                SceneManager.LoadScene(baseSceneName);
            }
        }

        private void OnGUI()
        {
            if (!playerInRange) return;
            if (Tutorial != null && Tutorial.tutorialActive && Tutorial.tutorialStep < 4) return;

            if (_guiStyle == null)
            {
                _guiStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 20,
                    alignment = TextAnchor.MiddleCenter
                };
                _guiStyle.normal.textColor = Color.green;
            }

            GUI.Label(new Rect(Screen.width / 2f - 250, Screen.height - 100, 500, 60),
                enterPrompt, _guiStyle);
        }
    }
}
