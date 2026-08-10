using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using System.Collections.Generic;

namespace Vampire.DropPuzzle
{
    /// <summary>
    /// Boss Health — attach to the boss target object (with a Collider).
    ///
    /// ECS rice balls that fly into the boss bounds deal damage once each.
    /// Damage per ball = quality value:
    ///   Fine (TypeID 0) = 1  |  Good (TypeID 1) = 2
    ///   Great (TypeID 2) = 3  |  Excellent (TypeID 3) = 5
    ///
    /// Boss is defeated when HP reaches 0.  Fires OnBossDefeated event and
    /// awards BossRewardCurrency to PlayerDataManager.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BossHealth : MonoBehaviour
    {
        [Header("Boss Settings")]
        public int MaxHp = 100;
        public int BossRewardCurrency = 5000;

        [Header("Visual Feedback")]
        public Color HpBarColor = new Color(0.8f, 0.1f, 0.1f);
        public Color HpBarBgColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        // Singleton — checked by other systems to know if a boss fight is active
        public static BossHealth Instance { get; private set; }

        public int CurrentHp { get; private set; }
        public bool IsDefeated { get; private set; }

        public event System.Action OnBossDefeated;
        public event System.Action<int, int> OnDamageTaken; // (damage, ballTypeId)

        // ── ECS refs ──────────────────────────────────────────────────────────
        private EntityManager   _em;
        private EntityQuery     _ballQuery;

        // Per-ball hit tracking — persists for the whole drop session
        private readonly HashSet<int> _hitEntities = new HashSet<int>();

        private Collider _col;

        // ── GUI state ────────────────────────────────────────────────────────
        private GUIStyle _barBgStyle;
        private GUIStyle _barFillStyle;
        private GUIStyle _labelStyle;
        private bool     _stylesBuilt;
        private float    _hitFlashTimer;
        private string   _defeatedText;

        private void Awake()
        {
            Instance = this;
            CurrentHp = MaxHp;
        }

        private void Start()
        {
            _col = GetComponent<Collider>();
            if (!_col.isTrigger) _col.isTrigger = true;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            _em = world.EntityManager;
            _ballQuery = _em.CreateEntityQuery(
                typeof(LocalTransform),
                typeof(RiceBallPhysics),
                typeof(RiceBallType),
                typeof(RiceBallTag)
            );
        }

        private void FixedUpdate()
        {
            if (IsDefeated || _ballQuery == null || _ballQuery.IsEmpty) return;

            Bounds bounds = _col.bounds;

            var entities   = _ballQuery.ToEntityArray(Allocator.Temp);
            var transforms = _ballQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var types      = _ballQuery.ToComponentDataArray<RiceBallType>(Allocator.Temp);

            // ECB for deferred ball destruction (no sync point mid-loop)
            var ecbSystem = World.DefaultGameObjectInjectionWorld
                .GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer();

            for (int i = 0; i < entities.Length; i++)
            {
                int idx = entities[i].Index;
                if (_hitEntities.Contains(idx)) continue;

                if (!bounds.Contains((Vector3)transforms[i].Position)) continue;

                _hitEntities.Add(idx);

                int damage = DamageForType(types[i].TypeID);
                ApplyDamage(damage, types[i].TypeID);

                ecb.DestroyEntity(entities[i]);

                if (IsDefeated) break;
            }

            entities.Dispose();
            transforms.Dispose();
            types.Dispose();
        }

        private void ApplyDamage(int damage, int typeId)
        {
            if (IsDefeated) return;

            CurrentHp -= damage;
            _hitFlashTimer = 0.25f;
            OnDamageTaken?.Invoke(damage, typeId);

            if (CurrentHp <= 0)
            {
                CurrentHp  = 0;
                IsDefeated = true;
                _defeatedText = "BOSS DEFEATED!";

                if (PlayerDataManager.Instance != null)
                    PlayerDataManager.Instance.AddCurrency(BossRewardCurrency, "Boss Defeated");

                OnBossDefeated?.Invoke();
                Debug.Log($"[BossHealth] Boss defeated! Rewarded ${BossRewardCurrency * 0.01f:F2}");
            }
        }

        private static int DamageForType(int typeId) => typeId switch
        {
            0 => 1,  // Fine
            1 => 2,  // Good
            2 => 3,  // Great
            3 => 5,  // Excellent
            _ => 1
        };

        // ── HUD ──────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            BuildStyles();

            float barW  = 500f;
            float barH  = 36f;
            float barX  = (Screen.width - barW) / 2f;
            float barY  = 20f;

            // Background
            GUI.Box(new Rect(barX - 2, barY - 2, barW + 4, barH + 4 + 30), "", _barBgStyle);

            // Title
            GUI.Label(new Rect(barX, barY, barW, 22), "BOSS HP", _labelStyle);
            barY += 22;

            // HP fill
            float pct = (float)CurrentHp / MaxHp;
            if (!IsDefeated && _hitFlashTimer > 0f)
            {
                _hitFlashTimer -= Time.deltaTime;
                // Flash white on hit
                GUI.color = Color.white;
                GUI.Box(new Rect(barX, barY, barW, barH), "", _barFillStyle);
                GUI.color = Color.white;
            }

            GUI.color = Color.Lerp(Color.red, HpBarColor, pct);
            GUI.Box(new Rect(barX, barY, barW * pct, barH), "", _barFillStyle);
            GUI.color = Color.white;

            // HP text centred on bar
            GUI.Label(new Rect(barX, barY, barW, barH),
                IsDefeated ? _defeatedText : $"{CurrentHp} / {MaxHp}",
                _labelStyle);
        }

        private void BuildStyles()
        {
            if (_stylesBuilt) return;
            _stylesBuilt = true;

            _barBgStyle = new GUIStyle(GUI.skin.box);
            _barBgStyle.normal.background = MakeTexture(new Color(0.1f, 0.1f, 0.1f, 0.85f));

            _barFillStyle = new GUIStyle(GUI.skin.box);
            _barFillStyle.normal.background = MakeTexture(HpBarColor);

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize  = 18;
            _labelStyle.fontStyle = FontStyle.Bold;
            _labelStyle.alignment = TextAnchor.MiddleCenter;
            _labelStyle.normal.textColor = Color.white;
        }

        private static Texture2D MakeTexture(Color c)
        {
            var tex = new Texture2D(2, 2);
            tex.SetPixels(new[] { c, c, c, c });
            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_ballQuery != default) _ballQuery.Dispose();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            var col = GetComponent<Collider>();
            if (col != null) Gizmos.DrawWireCube(transform.position, col.bounds.size);
        }
    }
}
