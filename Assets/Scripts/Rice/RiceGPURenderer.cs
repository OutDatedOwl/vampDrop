using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vampire.Rice
{
    /// <summary>
    /// Adds render components to rice ECS entities so they are visible.
    ///
    /// ARCHITECTURE NOTE:
    ///   Rice entities are spawned by RiceSpawnSystem with only RiceEntity + LocalTransform.
    ///   This system runs in PresentationSystemGroup and adds MaterialMeshInfo + all
    ///   required Hybrid Renderer components so DOTS renders them via GPU instancing.
    ///
    ///   The query targets entities WITHOUT MaterialMeshInfo, so it only processes
    ///   each entity ONCE (on first appearance). After that the query is empty = zero cost.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class RiceGPURenderer : SystemBase
    {
        private EntityQuery _uninitializedQuery;  // rice with no render components yet
        private EntityQuery _parentedQuery;        // rice still parented (needs detach)

        private RenderMeshArray  _renderMeshArray;
        private RenderMeshDescription _renderDesc;
        private bool   _renderDataReady;
        private Material _cachedMaterial;
        private Mesh     _cachedMesh;

        protected override void OnCreate()
        {
            // Rice that exists but has no renderer yet
            _uninitializedQuery = GetEntityQuery(new EntityQueryDesc
            {
                All  = new ComponentType[] { typeof(RiceEntity), typeof(LocalTransform) },
                None = new ComponentType[] { typeof(MaterialMeshInfo) }
            });

            // Rice that still has a Parent component (from prefab hierarchy)
            _parentedQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(RiceEntity), typeof(Parent) }
            });

            _renderDesc = new RenderMeshDescription(
                ShadowCastingMode.Off,
                receiveShadows: false,
                lightProbeUsage: LightProbeUsage.Off
            );
        }

        protected override void OnUpdate()
        {
            var config = RiceRenderingConfig.Instance;
            if (config == null)
            {
                return;
            }

            if (config.RiceMesh == null)
            {
                // Debug.LogError("[RiceGPURenderer] RiceMesh is NULL on RiceRenderingConfig! Assign a mesh.");
                return;
            }

            if (config.RiceMaterial == null)
            {
                // Debug.LogError("[RiceGPURenderer] RiceMaterial is NULL on RiceRenderingConfig! Assign a material.");
                return;
            }

            // ── Step 1: Detach any parented rice (batch, no loop) ────────────
            if (!_parentedQuery.IsEmpty)
            {
                EntityManager.RemoveComponent<Parent>(_parentedQuery);
            }

            // ── Step 2: Skip if nothing needs rendering ──────────────────────
            if (_uninitializedQuery.IsEmpty) return;

            int count = _uninitializedQuery.CalculateEntityCount();
            // Debug.Log($"[RiceGPURenderer] Adding render components to {count} rice entities...");

            // ── Step 3: Rebuild render data if config changed ────────────────
            if (!_renderDataReady || _cachedMaterial != config.RiceMaterial || _cachedMesh != config.RiceMesh)
            {
                _renderMeshArray = new RenderMeshArray(
                    new[] { config.RiceMaterial },
                    new[] { config.RiceMesh }
                );
                _cachedMaterial   = config.RiceMaterial;
                _cachedMesh       = config.RiceMesh;
                _renderDataReady  = true;
                // Debug.Log($"[RiceGPURenderer] Render data built. Mesh={config.RiceMesh.name}, Mat={config.RiceMaterial.name}, Scale={config.Scale}");
            }

            // ── Step 4: Add render + set scale ───────────────────────────────
            // RenderMeshUtility.AddComponents does structural changes, so we must
            // iterate a snapshot of the entity array (not a live query iterator).
            var entities = _uninitializedQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                // Skip the prefab template entity itself
                if (EntityManager.HasComponent<Unity.Entities.Prefab>(entity)) continue;

                RenderMeshUtility.AddComponents(
                    entity,
                    EntityManager,
                    _renderDesc,
                    _renderMeshArray,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)
                );

                // Set scale from config (e.g. 0.03)
                var t = EntityManager.GetComponentData<LocalTransform>(entity);
                t.Scale = config.Scale;
                EntityManager.SetComponentData(entity, t);
            }

            entities.Dispose();

            // Debug.Log($"[RiceGPURenderer] ✅ Done. Rice should now be visible.");
        }
    }
}
