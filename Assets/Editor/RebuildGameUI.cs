using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public class RebuildGameUI
{
    [MenuItem("Tools/VAMP4/Rebuild Game UI")]
    public static void Execute()
    {
        var canvasGO = GameObject.Find("UI");
        if (canvasGO == null) { Debug.LogError("[RebuildGameUI] No 'UI' GameObject found!"); return; }
        // Debug.Log("[RebuildGameUI] Found UI canvas, starting rebuild...");

        // ── Canvas settings ──────────────────────────────────────────────
        var canvas = canvasGO.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
        }

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // ── Colours ──────────────────────────────────────────────────────
        Color panelBg    = new Color(0.05f, 0.05f, 0.08f, 0.82f);
        Color accentGold = new Color(1.00f, 0.80f, 0.20f, 1.00f);
        Color textWhite  = new Color(0.95f, 0.95f, 0.95f, 1.00f);
        Color textDim    = new Color(0.70f, 0.70f, 0.70f, 1.00f);
        Color textGreen  = new Color(0.40f, 1.00f, 0.50f, 1.00f);

        // ════════════════════════════════════════════════════════════════
        // 1. RICE COUNTER — top-left
        // ════════════════════════════════════════════════════════════════
        // Debug.Log("[RebuildGameUI] Rebuilding RiceCounterUI...");
        var riceT = canvasGO.transform.Find("RiceCounterUI");
        if (riceT != null)
        {
            var riceGO = riceT.gameObject;
            // RiceCounterUI is a TMP text object — don't add Image to it, just style the text
            var rt = riceGO.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0, 1);
            rt.anchorMax        = new Vector2(0, 1);
            rt.pivot            = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(20, -20);
            rt.sizeDelta        = new Vector2(260, 56);

            var tmp = riceGO.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text             = "🌾  Rice: 0";
                tmp.fontSize         = 26;
                tmp.fontStyle        = FontStyles.Bold;
                tmp.color            = accentGold;
                tmp.alignment        = TextAlignmentOptions.MidlineLeft;
                tmp.margin           = new Vector4(14, 0, 8, 0);
                tmp.enableAutoSizing = false;
            }
            // Debug.Log("[RebuildGameUI] ✅ RiceCounterUI done.");
        }
        else Debug.LogWarning("[RebuildGameUI] RiceCounterUI not found.");

        // ════════════════════════════════════════════════════════════════
        // 2. DAY/NIGHT PANEL — top-centre
        // ════════════════════════════════════════════════════════════════
        // Debug.Log("[RebuildGameUI] Rebuilding DayNightPanel...");
        var dnT = canvasGO.transform.Find("DayNightPanel");
        if (dnT != null)
        {
            var dnGO = dnT.gameObject;
            var img = dnGO.GetComponent<Image>();
            if (img == null) img = dnGO.AddComponent<Image>();
            img.color = panelBg;

            var rt = dnGO.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 1);
            rt.anchorMax        = new Vector2(0.5f, 1);
            rt.pivot            = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -20);
            rt.sizeDelta        = new Vector2(220, 64);

            // TimeOfDay — left half
            var todT = dnT.Find("TimeOfDay");
            if (todT != null)
            {
                var todRT = todT.GetComponent<RectTransform>();
                todRT.anchorMin = new Vector2(0, 0);
                todRT.anchorMax = new Vector2(0.52f, 1);
                todRT.offsetMin = new Vector2(12, 4);
                todRT.offsetMax = new Vector2(0, -4);

                var tmp = todT.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text             = "DAY";
                    tmp.fontSize         = 22;
                    tmp.fontStyle        = FontStyles.Bold;
                    tmp.color            = new Color(1f, 0.9f, 0.4f);
                    tmp.alignment        = TextAlignmentOptions.MidlineLeft;
                    tmp.enableAutoSizing = false;
                }
            }

            // Countdown — right half
            var cdT = dnT.Find("CountdownText");
            if (cdT != null)
            {
                var cdRT = cdT.GetComponent<RectTransform>();
                cdRT.anchorMin = new Vector2(0.52f, 0);
                cdRT.anchorMax = new Vector2(1, 1);
                cdRT.offsetMin = new Vector2(0, 4);
                cdRT.offsetMax = new Vector2(-12, -4);

                var tmp = cdT.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text             = "01:00";
                    tmp.fontSize         = 28;
                    tmp.fontStyle        = FontStyles.Bold;
                    tmp.color            = textWhite;
                    tmp.alignment        = TextAlignmentOptions.MidlineRight;
                    tmp.enableAutoSizing = false;
                }
            }
            // Debug.Log("[RebuildGameUI] ✅ DayNightPanel done.");
        }
        

        // ════════════════════════════════════════════════════════════════
        // 3. QUEST PANEL — top-right
        // ════════════════════════════════════════════════════════════════
        // Debug.Log("[RebuildGameUI] Rebuilding QuestPanel...");
        var questPanelT = canvasGO.transform.Find("QuestPanel");
        if (questPanelT != null)
        {
            var questPanelGO = questPanelT.gameObject;
            var img = questPanelGO.GetComponent<Image>();
            if (img == null) img = questPanelGO.AddComponent<Image>();
            img.color = new Color(0.06f, 0.06f, 0.10f, 0.88f);

            var rt = questPanelGO.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(1, 1);
            rt.anchorMax        = new Vector2(1, 1);
            rt.pivot            = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-20, -20);
            rt.sizeDelta        = new Vector2(300, 120);

            // Wire QuestUI references
            var questUI = Object.FindFirstObjectByType<Vampire.DropPuzzle.QuestUI>(FindObjectsInactive.Include);
            if (questUI != null) questUI.questPanel = questPanelGO;

            // Quests container
            var questsT = questPanelT.Find("Quests");
            if (questsT != null)
            {
                var questsRT = questsT.GetComponent<RectTransform>();
                if (questsRT == null) questsRT = questsT.gameObject.AddComponent<RectTransform>();
                questsRT.anchorMin = Vector2.zero;
                questsRT.anchorMax = Vector2.one;
                questsRT.offsetMin = new Vector2(12, 8);
                questsRT.offsetMax = new Vector2(-12, -8);

                // Title
                var titleT = questsT.Find("CurrentQuest");
                if (titleT != null)
                {
                    var titleRT = titleT.GetComponent<RectTransform>();
                    titleRT.anchorMin        = new Vector2(0, 1);
                    titleRT.anchorMax        = new Vector2(1, 1);
                    titleRT.pivot            = new Vector2(0, 1);
                    titleRT.anchoredPosition = new Vector2(0, 0);
                    titleRT.sizeDelta        = new Vector2(0, 28);

                    var tmp = titleT.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text             = "QUEST";
                        tmp.fontSize         = 17;
                        tmp.fontStyle        = FontStyles.Bold;
                        tmp.color            = accentGold;
                        tmp.alignment        = TextAlignmentOptions.MidlineLeft;
                        tmp.enableAutoSizing = false;
                    }
                    if (questUI != null) questUI.questTitleText = titleT.GetComponent<TextMeshProUGUI>();
                }

                // Description
                var descT = questsT.Find("QuestDesc");
                if (descT != null)
                {
                    var descRT = descT.GetComponent<RectTransform>();
                    descRT.anchorMin        = new Vector2(0, 1);
                    descRT.anchorMax        = new Vector2(1, 1);
                    descRT.pivot            = new Vector2(0, 1);
                    descRT.anchoredPosition = new Vector2(0, -30);
                    descRT.sizeDelta        = new Vector2(0, 40);

                    var tmp = descT.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text             = "Collect rice from the field";
                        tmp.fontSize         = 13;
                        tmp.fontStyle        = FontStyles.Normal;
                        tmp.color            = textDim;
                        tmp.alignment        = TextAlignmentOptions.TopLeft;
                        tmp.textWrappingMode = TextWrappingModes.Normal;
                        tmp.enableAutoSizing = false;
                    }
                    if (questUI != null) questUI.questDescriptionText = descT.GetComponent<TextMeshProUGUI>();
                }

                // Progress
                var progT = questsT.Find("QuestProg");
                if (progT != null)
                {
                    var progRT = progT.GetComponent<RectTransform>();
                    progRT.anchorMin        = new Vector2(0, 0);
                    progRT.anchorMax        = new Vector2(1, 0);
                    progRT.pivot            = new Vector2(0, 0);
                    progRT.anchoredPosition = new Vector2(0, 0);
                    progRT.sizeDelta        = new Vector2(0, 24);

                    var tmp = progT.GetComponent<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text             = "0 / 50";
                        tmp.fontSize         = 15;
                        tmp.fontStyle        = FontStyles.Bold;
                        tmp.color            = textGreen;
                        tmp.alignment        = TextAlignmentOptions.MidlineLeft;
                        tmp.enableAutoSizing = false;
                    }
                    if (questUI != null) questUI.questProgressText = progT.GetComponent<TextMeshProUGUI>();
                }
            }
            // Debug.Log("[RebuildGameUI] ✅ QuestPanel done.");
        }
        else Debug.LogWarning("[RebuildGameUI] QuestPanel not found.");

        // ════════════════════════════════════════════════════════════════
        // 4. CROSSHAIR — dead centre
        // ════════════════════════════════════════════════════════════════
        var crosshairGO = GameObject.Find("CrosshairUI");
        if (crosshairGO != null)
        {
            var rt = crosshairGO.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }
        }

        // ════════════════════════════════════════════════════════════════
        // 5. COMPLETION + WARNING — hidden, centred
        // ════════════════════════════════════════════════════════════════
        foreach (var panelName in new[] { "Completion", "WarningPanel" })
        {
            var t = canvasGO.transform.Find(panelName);
            if (t == null) continue;
            t.gameObject.SetActive(false);
            var rt = t.GetComponent<RectTransform>();
            if (rt == null) continue;
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        // Debug.Log("[RebuildGameUI] ✅ All UI rebuilt successfully.");
    }
}
