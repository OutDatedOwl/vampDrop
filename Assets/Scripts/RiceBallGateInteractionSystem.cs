using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;

namespace Vampire.DropPuzzle
{
    /// <summary>
    /// Hybrid system — bridges ECS balls with MonoBehaviour gates.
    ///
    /// SYNC-POINT FIX:
    ///   All structural ECS changes (CreateEntity, DestroyEntity, SetComponentData)
    ///   are now batched into an EntityCommandBuffer and played back ONCE at the end
    ///   of FixedUpdate. This eliminates the ~1-second hiccup that occurred when
    ///   multiple balls hit a multiplier gate in the same frame, each triggering
    ///   a direct CreateEntity() call that forced a full ECS world sync.
    /// </summary>
    public class RiceBallGateInteractionSystem : MonoBehaviour
    {
        private EntityQuery     _ballQuery;
        private EntityManager   _em;
        private MultiplierGate[] _multiplierGates;
        private GoalGate[]       _goalGates;

        // ── Cached archetype so CreateEntity doesn't re-resolve types every call ──
        private EntityArchetype _ballArchetype;

        // ── Spawn cap: prevents a mass-gate hit from creating hundreds of entities ──
        // in a single FixedUpdate, which would stall the world on archetype resizing.
        private const int MaxSpawnsPerFrame = 50;
        private int _spawnsThisFrame;

        private void Start()
        {
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;

            _ballQuery = _em.CreateEntityQuery(
                typeof(LocalTransform),
                typeof(RiceBallPhysics),
                typeof(RiceBallGateTracker),
                typeof(RiceBallTag)
            );

            _ballArchetype = _em.CreateArchetype(
                typeof(LocalTransform),
                typeof(RiceBallPhysics),
                typeof(RiceBallTag),
                typeof(RiceBallType),
                typeof(RiceBallGateTracker),
                typeof(RiceBallLifetime)
            );

            RefreshGates();
            Debug.Log($"[GateInteraction] Found {_multiplierGates.Length} multiplier gates, {_goalGates.Length} goal gates");
        }

        public void RefreshGates()
        {
            _multiplierGates = FindObjectsByType<MultiplierGate>(FindObjectsSortMode.None);
            _goalGates       = FindObjectsByType<GoalGate>(FindObjectsSortMode.None);
        }

        private void FixedUpdate()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "DropPuzzle") return;
            if (_ballQuery == null || _ballQuery.IsEmpty) return;

            _spawnsThisFrame = 0;

            // ── Snapshot ball data ────────────────────────────────────────────
            var entities   = _ballQuery.ToEntityArray(Allocator.Temp);
            var transforms = _ballQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var trackers   = _ballQuery.ToComponentDataArray<RiceBallGateTracker>(Allocator.Temp);

            // ── ECB via EndFixedStepSimulationEntityCommandBufferSystem ───────
            // Unity batches the playback with other ECS structural changes at the
            // end of the fixed-step group — no manual Playback/Dispose needed.
            var ecbSystem = World.DefaultGameObjectInjectionWorld
                .GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
            var ecb = ecbSystem.CreateCommandBuffer();

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity    = entities[i];
                float3 ballPos   = transforms[i].Position;
                int    hitMask   = trackers[i].HitGatesMask;
                bool   maskDirty = false;

                // ── Multiplier gates ──────────────────────────────────────────
                for (int g = 0; g < _multiplierGates.Length; g++)
                {
                    var gate = _multiplierGates[g];
                    if (gate == null || !gate.gameObject.activeInHierarchy) continue;

                    int gateBit = 1 << g;
                    if ((hitMask & gateBit) != 0) continue; // already hit

                    var col = gate.GetComponent<Collider>();
                    if (col == null) continue;

                    if (col.bounds.Contains((Vector3)ballPos))
                    {
                        SpawnMultipliedBalls(ref ecb, entity, gate, ballPos);
                        hitMask  |= gateBit;
                        maskDirty = true;
                    }
                }

                // ── Goal gates ────────────────────────────────────────────────
                bool ballDestroyed = false;
                foreach (var gate in _goalGates)
                {
                    if (gate == null || !gate.gameObject.activeInHierarchy) continue;

                    var col = gate.GetComponent<Collider>();
                    if (col == null) continue;

                    if (col.bounds.Contains((Vector3)ballPos))
                    {
                        if (PlayerDataManager.Instance != null)
                            PlayerDataManager.Instance.AddCurrency(1, "Goal scored");

                        ecb.DestroyEntity(entity);
                        ballDestroyed = true;
                        break;
                    }
                }

                // Write updated tracker back via ECB (no per-entity sync point)
                if (!ballDestroyed && maskDirty)
                    ecb.SetComponent(entity, new RiceBallGateTracker { HitGatesMask = hitMask });
            }

            // ECB owned by EndFixedStepSimulationEntityCommandBufferSystem —
            // do NOT call ecb.Playback() or ecb.Dispose() here.

            entities.Dispose();
            transforms.Dispose();
            trackers.Dispose();
        }

        private void SpawnMultipliedBalls(ref EntityCommandBuffer ecb, Entity original,
                                          MultiplierGate gate, float3 ballPos)
        {
            var physics  = _em.GetComponentData<RiceBallPhysics>(original);
            var origType = _em.GetComponentData<RiceBallType>(original);
            var origTracker = _em.GetComponentData<RiceBallGateTracker>(original);
            var origLifetime = _em.GetComponentData<RiceBallLifetime>(original);

            float r = physics.Radius;
            int   extra = gate.Multiplier - 1;

            for (int i = 0; i < extra; i++)
            {
                // Per-frame cap: prevent hundreds of CreateEntity calls in one FixedUpdate
                // (e.g. 50 balls hitting an x10 gate = 450 spawns → archetype resize spike).
                if (_spawnsThisFrame >= MaxSpawnsPerFrame) break;
                _spawnsThisFrame++;
                float spread   = UnityEngine.Random.Range(-r * 3f, r * 3f);
                float3 spawnPos = ballPos + new float3(spread, r * 2.2f * (i + 1), 0f);

                // CreateEntity via ECB — deferred, no sync point
                Entity newBall = ecb.CreateEntity(_ballArchetype);

                ecb.SetComponent(newBall, LocalTransform.FromPositionRotationScale(
                    spawnPos, quaternion.identity, r * 2f));

                ecb.SetComponent(newBall, new RiceBallPhysics
                {
                    Position             = spawnPos,
                    Velocity             = new float3(spread * 0.5f, 0f, 0f),
                    Radius               = r,
                    Mass                 = physics.Mass,
                    Bounciness           = physics.Bounciness,
                    Friction             = physics.Friction,
                    IsSleeping           = false,
                    SleepVelocityThreshold = 0.015f
                });

                ecb.SetComponent(newBall, origType);
                ecb.SetComponent(newBall, origTracker);   // inherits gate hit mask
                ecb.SetComponent(newBall, origLifetime);
            }
        }

        private void OnDestroy()
        {
            if (_ballQuery != default)
                _ballQuery.Dispose();
        }
    }
}
