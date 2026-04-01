using UnityEngine;
using TMPro;

namespace Vampire.DropPuzzle
{
    /// <summary>
    /// Displays the current quest using TMP canvas elements.
    ///
    /// OPTIMISED:
    ///   - OnGUI fallback removed entirely. It was allocating 4 new GUIStyle
    ///     objects + 2 strings every single frame (4 KB/frame as seen in profiler).
    ///     The TMP panel (questPanel) is the correct display path.
    ///   - Progress text is only rebuilt when the value string actually changes.
    ///   - Completion checkmark uses string.Concat instead of interpolation
    ///     to avoid an extra allocation.
    /// </summary>
    public class QuestUI : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI questTitleText;
        public TextMeshProUGUI questDescriptionText;
        public TextMeshProUGUI questProgressText;
        public GameObject      questPanel;

        private QuestManager _qm;
        private string       _lastProgressStr = null;

        // ── Lifecycle ─────────────────────────────────────────────────────

        private void Start()
        {
            _qm = QuestManager.Instance;

            if (_qm != null)
            {
                _qm.OnQuestStarted   += OnQuestStarted;
                _qm.OnQuestCompleted += OnQuestCompleted;
                _qm.OnQuestProgress  += OnQuestProgress;
                RefreshQuestDisplay();
            }
            else
            {
                if (questPanel != null) questPanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_qm == null) _qm = QuestManager.Instance;
            if (_qm != null) RefreshQuestDisplay();
        }

        private void OnDestroy()
        {
            if (_qm != null)
            {
                _qm.OnQuestStarted   -= OnQuestStarted;
                _qm.OnQuestCompleted -= OnQuestCompleted;
                _qm.OnQuestProgress  -= OnQuestProgress;
            }
        }

        // ── Quest event handlers ──────────────────────────────────────────

        private void OnQuestStarted(QuestManager.Quest quest)
        {
            CancelInvoke(nameof(HideQuestPanel));
            if (questPanel != null) questPanel.SetActive(true);
            UpdateQuestDisplay(quest);
        }

        private void OnQuestProgress(QuestManager.Quest quest)
        {
            UpdateQuestDisplay(quest);
        }

        private void OnQuestCompleted(QuestManager.Quest quest)
        {
            if (questTitleText != null)
                questTitleText.text = "✅ " + quest.title;
        }

        // ── Display helpers ───────────────────────────────────────────────

        private void RefreshQuestDisplay()
        {
            if (_qm == null) _qm = QuestManager.Instance;
            if (_qm == null) return;

            var q = _qm.currentQuest;
            if (q != null && !q.isComplete && !string.IsNullOrEmpty(q.title))
                OnQuestStarted(q);
        }

        private void UpdateQuestDisplay(QuestManager.Quest quest)
        {
            if (questTitleText != null)
                questTitleText.text = quest.title;

            if (questDescriptionText != null)
                questDescriptionText.text = quest.description;

            // Only rebuild progress string when it actually changes
            if (questProgressText != null && _qm != null)
            {
                string progress = _qm.GetCurrentQuestProgress();
                if (progress != _lastProgressStr)
                {
                    _lastProgressStr        = progress;
                    questProgressText.text  = progress;
                }
            }
        }

        private void HideQuestPanel()
        {
            if (questPanel != null) questPanel.SetActive(false);
        }
    }
}
