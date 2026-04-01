using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Transforms;

namespace Vampire.Rice
{
    /// <summary>
    /// Pre-generates all candidate spawn positions and rotations in a Burst-compiled job.
    /// The main thread then only handles UnityEngine.Physics.CheckSphere calls (which cannot run in jobs).
    /// </summary>
    [BurstCompile]
    struct GenerateCandidatePositionsJob : IJob
    {
        public Random Random;
        public float3 Center;
        public float3 Size;
        public float FloorY;
        public bool SpawnOnFloor;
        public int Count;
        public int MaxRetries;

        [WriteOnly] public NativeArray<float3> OutPositions;   // Count * MaxRetries candidates
        [WriteOnly] public NativeArray<quaternion> OutRotations; // Count * MaxRetries candidates

        public void Execute()
        {
            int total = Count * MaxRetries;
            for (int i = 0; i < total; i++)
            {
                var offset = Random.NextFloat3(-0.5f, 0.5f) * Size;
                if (SpawnOnFloor) offset.y = 0f;

                var pos = Center + offset;
                if (SpawnOnFloor) pos.y = FloorY;
                OutPositions[i] = pos;

                OutRotations[i] = quaternion.EulerXYZ(math.radians(new float3(
                    Random.NextFloat(-10f, 10f),
                    Random.NextFloat(-180f, 180f),
                    Random.NextFloat(-10f, 10f)
                )));
            }
        }
    }

    public partial struct RiceSpawnSystem : ISystem
    {
        private bool hasCheckedRequirements;
        private EntityQuery spawnerQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RiceSpawner>();
            state.RequireForUpdate<RiceSpawnPoint>();
            spawnerQuery = state.GetEntityQuery(ComponentType.ReadOnly<RiceSpawner>());
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!hasCheckedRequirements)
            {
                hasCheckedRequirements = true;
            }

            int spawnerCount = spawnerQuery.CalculateEntityCount();
            if (spawnerCount != 1) return;

            Entity spawnerEntity = SystemAPI.GetSingletonEntity<RiceSpawner>();
            if (SystemAPI.HasComponent<RiceSpawned>(spawnerEntity)) return;

            var spawner = SystemAPI.GetSingleton<RiceSpawner>();
            if (spawner.Prefab == Entity.Null)
            {
                // UnityEngine.Debug.LogError("[RiceSpawnSystem] Prefab is NULL! Assign the Rice prefab to RiceSpawnerAuthoring");
                state.EntityManager.AddComponent<RiceSpawned>(spawnerEntity);
                return;
            }

            var spawnQuery = SystemAPI.QueryBuilder().WithAll<RiceSpawnPoint>().Build();
            var spawnPoints = spawnQuery.ToComponentDataArray<RiceSpawnPoint>(Allocator.Temp);
            if (spawnPoints.Length == 0)
            {
                // UnityEngine.Debug.LogError("[RiceSpawnSystem] No RiceSpawnPoint found! Add RiceSpawnPointAuthoring to a GameObject in the scene");
                spawnPoints.Dispose();
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var random = new Random(spawner.Seed == 0 ? 1u : spawner.Seed);

            for (int pointIdx = 0; pointIdx < spawnPoints.Length; pointIdx++)
            {
                var point = spawnPoints[pointIdx];

                if (point.Count <= 0)
                {
                    // UnityEngine.Debug.LogError($"[RiceSpawnSystem] ❌ Spawn point {pointIdx} has Count={point.Count}! Set Count > 0 in RiceSpawnPointAuthoring Inspector");
                    continue;
                }

                bool checkObstacles = point.ObstacleLayerMask != 0;
                int maxRetries = math.max(1, point.MaxRetries);

                // UnityEngine.Debug.Log($"[RiceSpawnSystem] ✅ Spawning {point.Count} rice in spawn point {pointIdx} — obstacle checking: {checkObstacles}, maxRetries: {maxRetries}");

                if (checkObstacles)
                {
                    var testColliders = UnityEngine.Physics.OverlapSphere(point.Center, point.CheckRadius, point.ObstacleLayerMask);
                    if (testColliders.Length > 0)
                    {
                        // UnityEngine.Debug.LogWarning($"[RiceSpawnSystem] ⚠️ OBSTACLE DETECTED at spawn center! Found {testColliders.Length} colliders. Set ObstacleLayerMask to 'Nothing' or exclude floor layer.");
                    }
                }

                // --- Burst job: generate all candidate positions/rotations off the main thread ---
                int totalCandidates = point.Count * maxRetries;
                var candidatePositions = new NativeArray<float3>(totalCandidates, Allocator.TempJob);
                var candidateRotations = new NativeArray<quaternion>(totalCandidates, Allocator.TempJob);

                new GenerateCandidatePositionsJob
                {
                    Random = random,
                    Center = point.Center,
                    Size = point.Size,
                    FloorY = point.FloorY,
                    SpawnOnFloor = point.SpawnOnFloor,
                    Count = point.Count,
                    MaxRetries = maxRetries,
                    OutPositions = candidatePositions,
                    OutRotations = candidateRotations
                }.Run(); // Burst-compiled, runs synchronously but benefits from SIMD/vectorisation

                // Advance the random state past the candidates we just consumed
                random = new Random(random.NextUInt());

                // --- Main thread: physics checks only (UnityEngine.Physics is not thread-safe) ---
                int successfulSpawns = 0;
                int skippedDueToObstacles = 0;

                for (int i = 0; i < point.Count; i++)
                {
                    bool foundValidPosition = false;

                    for (int attempt = 0; attempt < maxRetries; attempt++)
                    {
                        var pos = candidatePositions[i * maxRetries + attempt];

                        bool hasObstacle = checkObstacles &&
                            UnityEngine.Physics.CheckSphere(pos, point.CheckRadius, point.ObstacleLayerMask);

                        if (!hasObstacle)
                        {
                            var instance = ecb.Instantiate(spawner.Prefab);
                            ecb.SetComponent(instance, LocalTransform.FromPositionRotationScale(
                                pos,
                                candidateRotations[i * maxRetries + attempt],
                                spawner.Scale
                            ));
                            successfulSpawns++;
                            foundValidPosition = true;
                            break;
                        }
                    }

                    if (!foundValidPosition)
                        skippedDueToObstacles++;
                }

                candidatePositions.Dispose();
                candidateRotations.Dispose();

               
            }

            ecb.AddComponent<RiceSpawned>(spawnerEntity);
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            spawnPoints.Dispose();
        }
    }
}
