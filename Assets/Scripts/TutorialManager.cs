using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Vampire;

namespace Vampire.DropPuzzle
{
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("Tutorial State")]
        public bool tutorialActive = true;
        public int tutorialStep = 0;

        [Header("Scene Names")]
        public string FPSSceneName = "FPS_Collect";
        public string BaseSceneName = "Base";

        [Header("Tutorial Puzzles")]
        [Tooltip("Tutorial puzzle JSON file used in ball drop scene")]
        public TextAsset TutorialPuzzle;

        [Header("Tutorial NPCs")]
        [Tooltip("Snerd NPC in FPS scene — hidden until quest 1 completes")]
        public GameObject SnerdNPCInFPS;

        [Header("Shop Configuration")]
        [Tooltip("BuyZone GameObject — enabled after tutorial completes")]
        public GameObject BuyZone;
        [Tooltip("Flink character — enabled after tutorial completes")]
        public GameObject FlinkCharacter;

        [Header("Comics")]
        [Tooltip("Shown after step 2: Talk to Snerd in FPS — returns to FPS scene")]
        public ComicSequenceConfig snerdFPSComic;
        [Tooltip("Shown after step 4: Enter the house — loads Base scene")]
        public ComicSequenceConfig enterHouseComic;
        [Tooltip("Shown after step 7: Talk to Snerd again after crafting — stays in Base")]
        public ComicSequenceConfig postCraftComic;

        // Quest IDs
        private const string QUEST_RICE_10      = "tut_rice_10";
        private const string QUEST_SNERD_FPS    = "tut_snerd_fps";
        private const string QUEST_RICE_50      = "tut_rice_50";
        private const string QUEST_GO_BASE      = "tut_go_base";
        private const string QUEST_SNERD_BASE_1 = "tut_snerd_base_1";
        private const string QUEST_CRAFT        = "tut_craft";
        private const string QUEST_SNERD_BASE_2 = "tut_snerd_base_2";
        private const string QUEST_GO_OUTSIDE   = "tut_go_outside";
        private const string QUEST_DROP         = "tut_drop";
        private const string QUEST_MONEY        = "tut_money";

        private QuestManager questManager;
        private DayNightCycleManager cycleManager;
        private PlayerDataManager playerData;

        private Vector3 _savedFPSPosition;
        private Quaternion _savedFPSRotation;
        private bool _hasSavedFPSPosition;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (BuyZone != null) BuyZone.SetActive(false);
            if (FlinkCharacter != null) FlinkCharacter.SetActive(false);
            if (SnerdNPCInFPS != null) SnerdNPCInFPS.SetActive(false);
        }

        private void Start()
        {
            questManager  = QuestManager.Instance;
            cycleManager  = DayNightCycleManager.Instance;
            playerData    = PlayerDataManager.Instance;

            if (questManager != null)
                questManager.OnQuestCompleted += OnQuestCompleted;

            if (tutorialActive)
                StartCoroutine(StartTutorial());
        }

        private void OnDestroy()
        {
            if (questManager != null)
                questManager.OnQuestCompleted -= OnQuestCompleted;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == FPSSceneName && _hasSavedFPSPosition)
                StartCoroutine(RestorePlayerPosition());
        }

        private IEnumerator RestorePlayerPosition()
        {
            yield return null; // wait one frame for player to initialise
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.SetPositionAndRotation(_savedFPSPosition, _savedFPSRotation);
                if (cc != null) cc.enabled = true;
            }
            _hasSavedFPSPosition = false;
        }

        public void SavePlayerPosition(Vector3 position, Quaternion rotation)
        {
            _savedFPSPosition   = position;
            _savedFPSRotation   = rotation;
            _hasSavedFPSPosition = true;
        }

        private IEnumerator StartTutorial()
        {
            yield return new WaitForSeconds(1f);

            if (questManager == null) yield break;

            questManager.ResetAllQuests();

            if (cycleManager != null)
            {
                cycleManager.enabled = false;
                cycleManager.currentTime = DayNightCycleManager.TimeOfDay.Day;
            }

            SetupTutorialQuests();

            tutorialStep = 1;
            questManager.StartQuest(QUEST_RICE_10);
        }

        private void SetupTutorialQuests()
        {
            questManager.AddQuest(QUEST_RICE_10,      "Collect Rice",            "Collect 10 rice grains",                     QuestManager.QuestType.CollectRice,   10);
            questManager.AddQuest(QUEST_SNERD_FPS,    "Talk to Snerd",           "Find Snerd and talk to him",                 QuestManager.QuestType.TalkToSnerd,   1);
            questManager.AddQuest(QUEST_RICE_50,      "Collect More Rice",       "Collect 50 rice grains",                     QuestManager.QuestType.CollectRice,   50);
            questManager.AddQuest(QUEST_GO_BASE,      "Go into the House",       "Enter the house at the end of the field",    QuestManager.QuestType.GoToBase,      1);
            questManager.AddQuest(QUEST_SNERD_BASE_1, "Talk to Snerd",           "Find Snerd inside and talk to him",          QuestManager.QuestType.TalkToSnerd,   1);
            questManager.AddQuest(QUEST_CRAFT,        "Craft Riceballs",         "Craft 5 riceballs at Snerd (Press [E])",     QuestManager.QuestType.CraftRiceBalls,5);
            questManager.AddQuest(QUEST_SNERD_BASE_2, "Talk to Snerd",           "Talk to Snerd again",                        QuestManager.QuestType.TalkToSnerd,   1);
            questManager.AddQuest(QUEST_GO_OUTSIDE,   "Go Outside",              "Head outside at night",                      QuestManager.QuestType.VisitBallDrop, 1);
            questManager.AddQuest(QUEST_DROP,         "Drop the Riceballs",      "Complete a ball drop",                       QuestManager.QuestType.DropRiceBalls, 1);
            questManager.AddQuest(QUEST_MONEY,        "Earn Money",              "Collect $0.50 from ball drops",              QuestManager.QuestType.CollectCurrency,50);
        }

        private void OnQuestCompleted(QuestManager.Quest quest)
        {
            switch (quest.questId)
            {
                case QUEST_RICE_10:
                    if (SnerdNPCInFPS != null) SnerdNPCInFPS.SetActive(true);
                    tutorialStep = 2;
                    questManager.StartQuest(QUEST_SNERD_FPS);
                    break;

                case QUEST_SNERD_FPS:
                    tutorialStep = 3;
                    if (playerData != null)
                        playerData.RiceGrains = Mathf.Max(0, playerData.RiceGrains - 10);
                    questManager.StartQuest(QUEST_RICE_50);
                    TriggerComic(snerdFPSComic, FPSSceneName);
                    break;

                case QUEST_RICE_50:
                    tutorialStep = 4;
                    questManager.StartQuest(QUEST_GO_BASE);
                    break;

                case QUEST_GO_BASE:
                    tutorialStep = 5;
                    questManager.StartQuest(QUEST_SNERD_BASE_1);
                    TriggerComic(enterHouseComic, BaseSceneName);
                    break;

                case QUEST_SNERD_BASE_1:
                    tutorialStep = 6;
                    questManager.StartQuest(QUEST_CRAFT);
                    break;

                case QUEST_CRAFT:
                    tutorialStep = 7;
                    questManager.StartQuest(QUEST_SNERD_BASE_2);
                    break;

                case QUEST_SNERD_BASE_2:
                    tutorialStep = 8;
                    questManager.StartQuest(QUEST_GO_OUTSIDE);
                    ForceNight();
                    TriggerComic(postCraftComic, BaseSceneName);
                    break;

                case QUEST_GO_OUTSIDE:
                    tutorialStep = 9;
                    questManager.StartQuest(QUEST_DROP);
                    break;

                case QUEST_DROP:
                    tutorialStep = 10;
                    questManager.StartQuest(QUEST_MONEY);
                    break;

                case QUEST_MONEY:
                    tutorialStep = 11;
                    CompleteTutorial();
                    break;
            }
        }

        // ── Public notify methods ─────────────────────────────────────────────

        public void NotifyRiceCollected(int amount = 1)
        {
            if (tutorialStep == 1 || tutorialStep == 3)
                questManager.UpdateQuestProgress(QuestManager.QuestType.CollectRice, amount);
        }

        public void NotifySnerdTalkedFPS()
        {
            if (tutorialStep == 2 && questManager.currentQuest?.questId == QUEST_SNERD_FPS)
                questManager.UpdateQuestProgress(QuestManager.QuestType.TalkToSnerd, 1);
        }

        public void NotifyEnteredBase()
        {
            if (tutorialStep == 4 && questManager.currentQuest?.questId == QUEST_GO_BASE)
                questManager.UpdateQuestProgress(QuestManager.QuestType.GoToBase, 1);
        }

        public void NotifySnerdTalkedBase1()
        {
            if (tutorialStep == 5 && questManager.currentQuest?.questId == QUEST_SNERD_BASE_1)
                questManager.UpdateQuestProgress(QuestManager.QuestType.TalkToSnerd, 1);
        }

        public void NotifySnerdTalkedBase2()
        {
            if (tutorialStep == 7 && questManager.currentQuest?.questId == QUEST_SNERD_BASE_2)
                questManager.UpdateQuestProgress(QuestManager.QuestType.TalkToSnerd, 1);
        }

        public void NotifyRiceBallsCrafted(int amount = 1)
        {
            if (tutorialStep == 6 && questManager.currentQuest?.questId == QUEST_CRAFT)
                questManager.UpdateQuestProgress(QuestManager.QuestType.CraftRiceBalls, amount);
        }

        public void NotifyBallDropVisit()
        {
            if (tutorialStep == 8 && questManager.currentQuest?.questId == QUEST_GO_OUTSIDE)
                questManager.UpdateQuestProgress(QuestManager.QuestType.VisitBallDrop, 1);
        }

        public void NotifyRiceBallsDropped()
        {
            if (tutorialStep == 9 && questManager.currentQuest?.questId == QUEST_DROP)
                questManager.UpdateQuestProgress(QuestManager.QuestType.DropRiceBalls, 1);
        }

        public void NotifyCurrencyEarned(int amount)
        {
            if (tutorialStep == 10 && questManager.currentQuest?.questId == QUEST_MONEY)
                questManager.UpdateQuestProgress(QuestManager.QuestType.CollectCurrency, amount);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void TriggerComic(ComicSequenceConfig comic, string nextScene)
        {
            if (comic == null)
            {
                if (!string.IsNullOrEmpty(nextScene))
                    SceneManager.LoadScene(nextScene);
                return;
            }
            ComicSceneManager.NextSceneOverride = nextScene;
            ComicSceneLoader.LoadComic(comic);
        }

        private void ForceNight()
        {
            if (cycleManager == null) return;
            cycleManager.enabled = true;
            cycleManager.isPaused = false;
            cycleManager.ForcePhase(DayNightCycleManager.TimeOfDay.Night);
        }

        private void CompleteTutorial()
        {
            tutorialActive = false;

            if (PlayerDataManager.Instance != null)
                PlayerDataManager.Instance.TutorialCompleted = true;

            if (BuyZone != null) BuyZone.SetActive(true);
            if (FlinkCharacter != null) FlinkCharacter.SetActive(true);
        }

        public void SetupTutorialPuzzle()
        {
            if (!tutorialActive) return;
            var gridLoader = Object.FindObjectOfType<GridPuzzleLoader>();
            if (gridLoader != null && TutorialPuzzle != null)
            {
                gridLoader.PuzzleJsonFile = TutorialPuzzle;
                gridLoader.LoadAndBuildPuzzle();
            }
        }

        public void SkipTutorial()
        {
            tutorialActive = false;
            ForceNight();
        }
    }
}
