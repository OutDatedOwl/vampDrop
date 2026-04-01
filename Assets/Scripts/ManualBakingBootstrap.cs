using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Vampire.Rice
{
    /// <summary>
    /// Bootstraps ECS entities from MonoBehaviour authoring components in the main scene.
    ///
    /// WHY THIS EXISTS:
    ///   Baker<T> only runs on GameObjects inside a Unity SubScene.
    ///   This project uses regular scene GameObjects (no SubScene), so we manually
    ///   create the ECS entities here — but ONLY if they don't already exist,
    ///   preventing the duplicate-spawner bug from scene reloads.
    /// </summary>
    public class ManualBakingBootstrap : MonoBehaviour
    {
        void Start()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                // Debug.LogError("[ManualBakingBootstrap] No ECS world found!");
                return;
            }

            var em = world.EntityManager;

            BootstrapSpawner(em);
            BootstrapSpawnPoints(em);
            BootstrapPlayer(em);

            // Debug.Log("[ManualBakingBootstrap] ✅ ECS bootstrap complete.");
        }

        // ─── Spawner ──────────────────────────────────────────────────────────

        private void BootstrapSpawner(EntityManager em)
        {
            // Guard: only create if no RiceSpawner entity exists yet
            var existingQuery = em.CreateEntityQuery(typeof(RiceSpawner));
            int existing = existingQuery.CalculateEntityCount();
            existingQuery.Dispose();

            if (existing > 0)
            {
                // Debug.Log($"[ManualBakingBootstrap] RiceSpawner already exists ({existing}), skipping.");
                return;
            }

            var spawnerAuthoring = FindFirstObjectByType<RiceSpawnerAuthoring>();
            if (spawnerAuthoring == null)
            {
                // Debug.LogError("[ManualBakingBootstrap] No RiceSpawnerAuthoring found in scene!");
                return;
            }

            if (spawnerAuthoring.RicePrefab == null)
            {
                // Debug.LogError("[ManualBakingBootstrap] RiceSpawnerAuthoring.RicePrefab is NULL!");
                return;
            }

            // Build a minimal ECS prefab entity for rice
            var riceAuthoring = spawnerAuthoring.RicePrefab.GetComponent<RiceAuthoring>();
            if (riceAuthoring == null)
            {
                // Debug.LogError("[ManualBakingBootstrap] Rice prefab is missing RiceAuthoring component!");
                return;
            }

            var prefabEntity = em.CreateEntity();
            em.SetName(prefabEntity, "RiceGrain_Prefab");
            em.AddComponentData(prefabEntity, new RiceEntity { CollectionRadius = riceAuthoring.CollectionRadius });
            em.AddComponentData(prefabEntity, LocalTransform.FromPosition(0, 0, 0));
            em.AddComponentData(prefabEntity, new RiceHighlighted());
            em.SetComponentEnabled<RiceHighlighted>(prefabEntity, false);
            em.AddComponentData(prefabEntity, new Unity.Entities.Prefab());

            var spawnerEntity = em.CreateEntity();
            em.SetName(spawnerEntity, "RiceSpawner");
            em.AddComponentData(spawnerEntity, new RiceSpawner
            {
                Prefab = prefabEntity,
                Count  = spawnerAuthoring.Count,
                Seed   = spawnerAuthoring.Seed == 0 ? 1u : spawnerAuthoring.Seed
            });

            // Debug.Log($"[ManualBakingBootstrap] ✅ RiceSpawner created. Prefab={spawnerAuthoring.RicePrefab.name}, Count={spawnerAuthoring.Count}");
        }

        // ─── Spawn Points ─────────────────────────────────────────────────────

        private void BootstrapSpawnPoints(EntityManager em)
        {
            // Guard: only create if no RiceSpawnPoint entities exist yet
            var existingQuery = em.CreateEntityQuery(typeof(RiceSpawnPoint));
            int existing = existingQuery.CalculateEntityCount();
            existingQuery.Dispose();

            if (existing > 0)
            {
                // Debug.Log($"[ManualBakingBootstrap] RiceSpawnPoints already exist ({existing}), skipping.");
                return;
            }

            var spawnPoints = FindObjectsByType<RiceSpawnPointAuthoring>(FindObjectsSortMode.None);
            if (spawnPoints.Length == 0)
            {
                // Debug.LogError("[ManualBakingBootstrap] No RiceSpawnPointAuthoring found in scene!");
                return;
            }

            foreach (var sp in spawnPoints)
            {
                Bounds bounds = sp.GetSpawnBounds();

                // Calculate floor Y from assigned floor objects
                float floorY = sp.ManualFloorY;
                if (sp.FloorObjects != null && sp.FloorObjects.Length > 0)
                {
                    float total = 0f;
                    int count = 0;
                    foreach (var floor in sp.FloorObjects)
                    {
                        if (floor == null) continue;
                        var r = floor.GetComponent<Renderer>();
                        if (r != null) { total += r.bounds.max.y; count++; }
                    }
                    if (count > 0) floorY = total / count;
                }

                var entity = em.CreateEntity();
                em.SetName(entity, $"RiceSpawnPoint_{sp.ZoneName}");
                em.AddComponentData(entity, new RiceSpawnPoint
                {
                    Center           = (float3)bounds.center,
                    Size             = (float3)bounds.size,
                    Count            = sp.Count,
                    SpawnOnFloor     = true,
                    FloorY           = floorY + sp.SpawnHeightOffset,
                    ObstacleLayerMask = sp.ObstacleLayerMask.value,
                    CheckRadius      = sp.CheckRadius,
                    MaxRetries       = sp.MaxRetries
                });

                // Debug.Log($"[ManualBakingBootstrap] ✅ SpawnPoint '{sp.ZoneName}' — Count={sp.Count}, FloorY={floorY + sp.SpawnHeightOffset:F3}, Size={bounds.size}");
            }
        }

        // ─── Player ───────────────────────────────────────────────────────────

        private void BootstrapPlayer(EntityManager em)
        {
            var playerAuthoring = FindFirstObjectByType<Player.PlayerAuthoring>();
            if (playerAuthoring == null) return;

            // Clean up any stale player entities from previous scene loads
            var existingQuery = em.CreateEntityQuery(typeof(Player.PlayerData));
            var existing = existingQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (existing.Length > 0)
            {
                // Debug.Log($"[ManualBakingBootstrap] Cleaning up {existing.Length} stale player entities.");
                foreach (var e in existing) em.DestroyEntity(e);
            }
            existing.Dispose();
            existingQuery.Dispose();

            var playerEntity = em.CreateEntity();
            em.SetName(playerEntity, "Player_ECS");
            em.AddComponentData(playerEntity, new Player.PlayerData
            {
                MoveSpeed        = 0,
                CollectionRadius = playerAuthoring.CollectionRadius,
                RiceCollected    = 0
            });
            em.AddComponentData(playerEntity, LocalTransform.FromPositionRotationScale(
                playerAuthoring.transform.position,
                playerAuthoring.transform.rotation,
                1f
            ));

            // Debug.Log($"[ManualBakingBootstrap] ✅ Player ECS entity created at {playerAuthoring.transform.position}");
        }
    }

    /// <summary>
    /// Holds an ECS Entity reference on a MonoBehaviour GameObject.
    /// </summary>
    public class EntityHolder : MonoBehaviour
    {
        public Entity Entity;
    }
}
