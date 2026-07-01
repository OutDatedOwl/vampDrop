using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using Vampire.DropPuzzle;
using Vampire.Player;

namespace Vampire
{
    [RequireComponent(typeof(UIDocument))]
    public class SnerdBaseShopUI : MonoBehaviour
    {
        public static SnerdBaseShopUI Instance { get; private set; }

        [Header("Visuals — drag assets in Inspector")]
        [Tooltip("Background image for the shop screen")]
        public Sprite backgroundSprite;
        [Tooltip("Snerd character sprite shown on the left side")]
        public Sprite snerdSprite;

        private UIDocument      _doc;
        private VisualElement   _root;
        private Label           _currencyLabel, _riceLabel, _riceballLabel, _craftableLabel;
        private Button          _craftButton;
        private VisualElement   _upgradesContainer;
        private FPSController   _fps;
        private bool            _isOpen;
        public bool             IsOpen => _isOpen;

        // ── Upgrade data ───────────────────────────────────────────────────────
        private struct UpgradeDef
        {
            public string name;
            public string category;
            public Func<string>  getStatus;
            public Func<int>     getCost;
            public Func<bool>    isAvailable;
            public Func<bool>    isMaxed;
            public Action        purchase;
        }

        private List<UpgradeDef> _defs;
        private List<(Label status, Label cost, Button btn)> _rows;

        // ── Lifecycle ──────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _doc = GetComponent<UIDocument>();
        }

        private void Start()
        {
            _fps  = FindObjectOfType<FPSController>();

            // UIDocument must stay enabled so the visual tree is never rebuilt.
            // We show/hide by toggling display on the root element instead.
            _root = _doc.rootVisualElement.Q("snerd-shop-root");
            if (_root == null)
            {
                // Fallback: root element IS the visualTreeAsset root
                _root = _doc.rootVisualElement;
                Debug.LogWarning("[SnerdBaseShopUI] snerd-shop-root not found, using doc root");
            }

            ApplySprite("shop-background", backgroundSprite);
            ApplySprite("snerd-image",     snerdSprite);

            _currencyLabel  = _root.Q<Label>("currency-label");
            _riceLabel      = _root.Q<Label>("rice-label");
            _riceballLabel  = _root.Q<Label>("riceball-label");
            _craftableLabel = _root.Q<Label>("craftable-label");
            _craftButton    = _root.Q<Button>("craft-button");
            _upgradesContainer = _root.Q("upgrades-container");

            _craftButton?.RegisterCallback<ClickEvent>(_ => OnCraftClicked());
            _root.Q<Button>("exit-button")?.RegisterCallback<ClickEvent>(_ => Close());

            // Hide via display — keeps the visual tree and all callbacks intact
            _root.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            if (!_isOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            RefreshStats();
        }

        // ── Public API ─────────────────────────────────────────────────────────
        public void Open()
        {
            // Late-find in case FPSController wasn't ready at Start()
            if (_fps == null) _fps = FindObjectOfType<FPSController>();

            _isOpen = true;
            _root.style.display = DisplayStyle.Flex;
            EscapeMenuManager.PushEscBlock();
            if (_fps != null) _fps.enabled = false;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible   = true;

            // Build (or rebuild) upgrades the first time, or whenever re-opened so costs stay fresh
            if (_defs == null) BuildUpgradeDefs();
            BuildUpgradeRows();
            RefreshStats();
        }

        public void Close()
        {
            _isOpen = false;
            _root.style.display = DisplayStyle.None;
            EscapeMenuManager.PopEscBlock();
            if (_fps != null) _fps.enabled = true;
            // Always restore cursor — even if FPS controller was null
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible   = false;
        }

        // ── Crafting ───────────────────────────────────────────────────────────
        private void OnCraftClicked()
        {
            RiceCraftingSystem.Instance?.CraftRiceBalls();
        }

        // ── Stats ──────────────────────────────────────────────────────────────
        private void RefreshStats()
        {
            var pd = PlayerDataManager.Instance;
            if (pd == null) return;

            if (_currencyLabel != null)
                _currencyLabel.text = $"${pd.TotalCurrency * 0.01f:F2}";
            if (_riceLabel != null)
                _riceLabel.text = $"Rice: {pd.RiceGrains}";
            if (_riceballLabel != null)
                _riceballLabel.text = $"Riceballs: {pd.Inventory.GetTotalBalls()}";

            int  craftable = RiceCraftingSystem.Instance?.GetCraftableCount() ?? 0;
            bool crafting  = RiceCraftingSystem.Instance?.IsCrafting()        ?? false;

            if (_craftableLabel != null)
                _craftableLabel.text = crafting ? "Crafting..." : craftable > 0 ? $"Can craft: {craftable}" : "Need 5 rice to craft";
            _craftButton?.SetEnabled(craftable > 0 && !crafting);

            RefreshUpgradeRows();
        }

        // ── Upgrades ───────────────────────────────────────────────────────────
        private void BuildUpgradeDefs()
        {
            var shop = UpgradeShop.Instance;
            var pd   = PlayerDataManager.Instance;
            _defs = new List<UpgradeDef>();

            if (shop == null || pd == null) return;

            // ─ Crafting quality ─────────────────────────────────────
            Def("Unlock Good Quality (2×)", "CRAFTING",
                () => pd.Crafting.goodChance == 0 ? "Locked" : $"{pd.Crafting.goodChance * 100:F0}%",
                () => 250,
                () => pd.Crafting.goodChance == 0,
                () => pd.Crafting.goodChance > 0,
                () => shop.UnlockGoodQuality());

            Def("Good Chance +5%", "CRAFTING",
                () => $"{pd.Crafting.goodChance * 100:F0}%",
                () => 150 + (int)((pd.Crafting.goodChance - 0.2f) * 100 / 5) * 50,
                () => pd.Crafting.goodChance > 0 && pd.Crafting.goodChance < 0.5f,
                () => pd.Crafting.goodChance >= 0.5f,
                () => shop.BuyGoodQualityUpgrade());

            Def("Unlock Great Quality (4×)", "CRAFTING",
                () => pd.Crafting.greatChance == 0 ? "Locked" : $"{pd.Crafting.greatChance * 100:F0}%",
                () => 600,
                () => pd.Crafting.goodChance >= 0.3f && pd.Crafting.greatChance == 0,
                () => pd.Crafting.greatChance > 0,
                () => shop.UnlockGreatQuality());

            Def("Great Chance +2%", "CRAFTING",
                () => $"{pd.Crafting.greatChance * 100:F0}%",
                () => 300 + (int)((pd.Crafting.greatChance - 0.05f) * 100 / 2) * 100,
                () => pd.Crafting.greatChance > 0 && pd.Crafting.greatChance < 0.2f,
                () => pd.Crafting.greatChance >= 0.2f,
                () => shop.BuyGreatQualityUpgrade());

            Def("Crafting Speed +20%", "CRAFTING",
                () => $"{pd.Crafting.craftingSpeedMultiplier:F1}×",
                () => 200 + (int)((pd.Crafting.craftingSpeedMultiplier - 1.0f) * 5) * 100,
                () => pd.Crafting.craftingSpeedMultiplier < 3.0f,
                () => pd.Crafting.craftingSpeedMultiplier >= 3.0f,
                () => shop.BuyCraftingSpeedUpgrade());

            // ─ FPS collector ────────────────────────────────────────
            Def("Pickup Radius +0.25", "FPS",
                () => $"{pd.FPSCollector.pickupRadius:F2}",
                () => 100 + (int)((pd.FPSCollector.pickupRadius - 1.5f) / 0.25f) * 50,
                () => pd.FPSCollector.pickupRadius < 5.0f,
                () => pd.FPSCollector.pickupRadius >= 5.0f,
                () => shop.BuyPickupRadiusUpgrade());

            Def("Move Speed +10%", "FPS",
                () => $"{pd.FPSCollector.moveSpeedMultiplier:F1}×",
                () => 150 + (int)((pd.FPSCollector.moveSpeedMultiplier - 1.0f) * 10) * 75,
                () => pd.FPSCollector.moveSpeedMultiplier < 2.0f,
                () => pd.FPSCollector.moveSpeedMultiplier >= 2.0f,
                () => shop.BuyMoveSpeedUpgrade());

            Def("Unlock Multi-Pickup", "FPS",
                () => $"{pd.FPSCollector.maxSimultaneousPickups}×",
                () => 400,
                () => pd.FPSCollector.maxSimultaneousPickups == 1,
                () => pd.FPSCollector.maxSimultaneousPickups > 1,
                () => shop.UnlockMultiPickup());

            // ─ Drop puzzle ──────────────────────────────────────────
            Def("x2 Gate Chance +5%", "DROP",
                () => $"{pd.DropPuzzle.x2GateChance * 100:F0}%",
                () => 100 + (int)(pd.DropPuzzle.x2GateChance * 100 / 5) * 50,
                () => pd.DropPuzzle.x2GateChance < 0.5f,
                () => pd.DropPuzzle.x2GateChance >= 0.5f,
                () => shop.BuyX2GateUpgrade());

            Def("Unlock x3 Gates", "DROP",
                () => pd.DropPuzzle.x3GateChance > 0 ? "Unlocked" : "Locked",
                () => 500,
                () => pd.DropPuzzle.x2GateChance >= 0.1f && pd.DropPuzzle.x3GateChance == 0,
                () => pd.DropPuzzle.x3GateChance > 0,
                () => shop.UnlockX3Gates());

            Def("+5 Starting Balls", "DROP",
                () => $"{pd.DropPuzzle.startingBalls}",
                () => 200 + ((pd.DropPuzzle.startingBalls - 20) / 5) * 100,
                () => pd.DropPuzzle.startingBalls < 50,
                () => pd.DropPuzzle.startingBalls >= 50,
                () => shop.BuyExtraBalls());

            Def("Unlock Bonus Balls", "DROP",
                () => pd.DropPuzzle.bonusPointBallChance > 0 ? $"{pd.DropPuzzle.bonusPointBallChance * 100:F0}%" : "Locked",
                () => 300,
                () => pd.DropPuzzle.bonusPointBallChance == 0,
                () => pd.DropPuzzle.bonusPointBallChance > 0,
                () => shop.UnlockBonusPointBalls());
        }

        private void Def(string name, string category, Func<string> getStatus, Func<int> getCost,
            Func<bool> isAvailable, Func<bool> isMaxed, Action purchase)
        {
            _defs.Add(new UpgradeDef
            {
                name = name, category = category,
                getStatus = getStatus, getCost = getCost,
                isAvailable = isAvailable, isMaxed = isMaxed,
                purchase = purchase
            });
        }

        private void BuildUpgradeRows()
        {
            if (_upgradesContainer == null || _defs == null) return;
            _upgradesContainer.Clear();
            _rows = new List<(Label, Label, Button)>();

            string lastCat = null;
            foreach (var def in _defs)
            {
                // Category separator
                if (def.category != lastCat)
                {
                    lastCat = def.category;
                    var catLbl = new Label(def.category);
                    catLbl.style.color       = new StyleColor(new Color(1f, 0.84f, 0.35f));
                    catLbl.style.fontSize    = 12;
                    catLbl.style.marginTop   = 10;
                    catLbl.style.marginBottom = 3;
                    catLbl.style.unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold);
                    _upgradesContainer.Add(catLbl);
                }

                // Row
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems    = Align.Center;
                row.style.marginBottom  = 5;

                var nameLbl = new Label(def.name);
                nameLbl.style.flexGrow  = 1;
                nameLbl.style.fontSize  = 13;
                nameLbl.style.color     = new StyleColor(Color.white);

                var statusLbl = new Label();
                statusLbl.style.width    = 80;
                statusLbl.style.fontSize = 11;
                statusLbl.style.color    = new StyleColor(new Color(0.65f, 0.9f, 1f));

                var costLbl = new Label();
                costLbl.style.width    = 68;
                costLbl.style.fontSize = 11;
                costLbl.style.color    = new StyleColor(new Color(1f, 0.84f, 0.35f));

                // Capture def for the lambda
                var captured = def;
                var btn = new Button(() => { captured.purchase(); RefreshStats(); });
                btn.style.width  = 66;
                btn.style.height = 26;
                btn.style.fontSize = 11;

                row.Add(nameLbl);
                row.Add(statusLbl);
                row.Add(costLbl);
                row.Add(btn);
                _upgradesContainer.Add(row);
                _rows.Add((statusLbl, costLbl, btn));
            }

            RefreshUpgradeRows();
        }

        private void RefreshUpgradeRows()
        {
            if (_defs == null || _rows == null) return;
            var pd = PlayerDataManager.Instance;
            int currency = pd?.TotalCurrency ?? 0;

            for (int i = 0; i < _defs.Count && i < _rows.Count; i++)
            {
                var def = _defs[i];
                var (statusLbl, costLbl, btn) = _rows[i];

                bool maxed     = def.isMaxed();
                bool available = def.isAvailable();
                int  cost      = def.getCost();
                bool canAfford = currency >= cost;

                statusLbl.text  = maxed ? "MAXED" : def.getStatus();
                statusLbl.style.color = new StyleColor(maxed ? Color.cyan : new Color(0.65f, 0.9f, 1f));

                costLbl.text = maxed || !available ? "" : $"${cost * 0.01f:F2}";

                if (maxed)          { btn.text = "✓";      btn.SetEnabled(false); }
                else if (!available){ btn.text = "Locked";  btn.SetEnabled(false); }
                else if (!canAfford){ btn.text = "Need $";  btn.SetEnabled(false); }
                else                { btn.text = "Buy";     btn.SetEnabled(true);  }
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private void ApplySprite(string elementName, Sprite sprite)
        {
            if (sprite == null) return;
            var el = _root?.Q(elementName);
            if (el != null) el.style.backgroundImage = new StyleBackground(sprite);
        }
    }
}
