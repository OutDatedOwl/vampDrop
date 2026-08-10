using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using System.Collections.Generic;

namespace Vampire.DropPuzzle
{
    /// <summary>
    /// Boost Block — applies a one-time horizontal velocity impulse to ECS rice balls
    /// that pass through its trigger collider.
    ///
    /// Place in the marble-roll track between the curve and the ramp.
    /// Uses the same bounds-check pattern as RiceBallGateInteractionSystem —
    /// no Physics.OverlapSphere, no per-frame sync points.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BoostBlock : MonoBehaviour
    {
        [Header("Boost Settings")]
        [Tooltip("Velocity added in the boost direction (world space)")]
        public Vector3 BoostVelocity = new Vector3(8f, 0f, 0f);

        [Tooltip("Cap: ball speed along the boost axis won't exceed this after the boost")]
        public float MaxSpeedAlongBoostAxis = 20f;

        [Header("Visual")]
        public Color GizmoColor = new Color(1f, 0.5f, 0f, 0.8f);

        // ── ECS refs ──────────────────────────────────────────────────────────
        private EntityManager   _em;
        private EntityQuery     _ballQuery;

        // Tracks Entity.Index values we've already boosted this session.
        // Persists for the drop session lifetime so balls don't get boosted twice
        // if they linger inside the bounds across multiple FixedUpdate ticks.
        private readonly HashSet<int> _boostedEntities = new HashSet<int>();

        private Collider _col;

        private void Start()
        {
            _col = GetComponent<Collider>();
            if (!_col.isTrigger) _col.isTrigger = true;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            _em = world.EntityManager;
            _ballQuery = _em.CreateEntityQuery(
                typeof(RiceBallPhysics),
                typeof(RiceBallTag)
            );
        }

        private void FixedUpdate()
        {
            if (_ballQuery == null || _ballQuery.IsEmpty) return;

            Bounds bounds = _col.bounds;
            float3 boost  = (float3)BoostVelocity;
            float3 boostDir = math.lengthsq(boost) > 0f
                ? math.normalize(boost)
                : new float3(1f, 0f, 0f);

            var entities = _ballQuery.ToEntityArray(Allocator.Temp);
            var physicsArr = _ballQuery.ToComponentDataArray<RiceBallPhysics>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                int idx = entities[i].Index;
                if (_boostedEntities.Contains(idx)) continue;

                // Use RiceBallPhysics.Position — the authoritative physics position.
                // LocalTransform can lag one frame behind.
                if (!bounds.Contains((Vector3)physicsArr[i].Position)) continue;

                _boostedEntities.Add(idx);

                var physics = physicsArr[i];

                // Wake the ball first — sleeping balls have their velocity ignored by the physics system
                physics.IsSleeping = false;

                // Add boost, then clamp the component along the boost direction
                float3 newVel    = physics.Velocity + boost;
                float  projected = math.dot(newVel, boostDir);
                if (projected > MaxSpeedAlongBoostAxis)
                    newVel -= boostDir * (projected - MaxSpeedAlongBoostAxis);

                physics.Velocity = newVel;
                _em.SetComponentData(entities[i], physics);
            }

            entities.Dispose();
            physicsArr.Dispose();
        }

        private void OnDestroy()
        {
            if (_ballQuery != default)
                _ballQuery.Dispose();
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor;
            var col = GetComponent<Collider>();
            if (col != null)
                Gizmos.DrawWireCube(transform.position, col.bounds.size);

            // Arrow showing boost direction
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, BoostVelocity.normalized * 1.5f);
        }
    }
}
