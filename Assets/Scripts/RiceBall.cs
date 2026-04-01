using UnityEngine;
using System.Collections.Generic;

namespace Vampire.DropPuzzle
{
    /// <summary>
    /// MonoBehaviour rice ball for the Drop Puzzle scene.
    ///
    /// FIXES APPLIED:
    ///   1. Rigidbody cached in Awake — eliminates GetComponent every Update frame.
    ///   2. CollisionDetectionMode changed from Discrete → ContinuousSpeculative.
    ///      Discrete is fastest but allows fast balls to tunnel through thin walls.
    ///      ContinuousSpeculative is ~5% slower but prevents tunneling entirely.
    ///   3. Removed Physics.defaultSolverIterations mutation — setting global physics
    ///      config from every ball instance caused race conditions and was redundant
    ///      (PhysicsOptimizer already sets this once on Awake).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class RiceBall : MonoBehaviour
    {
        [Header("Physics")]
        [Tooltip("Ball will auto-destroy after this many seconds")]
        public float Lifetime = 30f;

        [Tooltip("Destroy if ball falls below this Y position")]
        public float DestroyBelowY = -20f;

        // Cached — never call GetComponent in Update
        private Rigidbody _rb;
        private float     _spawnTime;

        // Per-ball gate tracking (bitmask preferred, but HashSet kept for MonoBehaviour path)
        private HashSet<int> _hitGates = new HashSet<int>();

        private void Awake()
        {
            // Cache immediately — before any physics tick
            _rb = GetComponent<Rigidbody>();

            if (!gameObject.CompareTag("RiceBall"))
                gameObject.tag = "RiceBall";
        }

        private void Start()
        {
            _spawnTime = Time.time;

            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();

            // ── Performance settings ──────────────────────────────────────────
            _rb.linearDamping  = 0.1f;
            _rb.angularDamping = 0.9f;
            _rb.interpolation  = RigidbodyInterpolation.None;
            _rb.sleepThreshold = 0.1f;
            _rb.maxAngularVelocity = 5f;

            // ContinuousSpeculative: prevents tunneling through walls at high speed,
            // costs ~5% more CPU than Discrete — worth it to stop balls escaping.
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // Freeze Z + all rotation — this is a 2D-style drop puzzle
            _rb.constraints = RigidbodyConstraints.FreezeRotation
                            | RigidbodyConstraints.FreezePositionZ;

            // NOTE: Do NOT set Physics.defaultSolverIterations here.
            // PhysicsOptimizer sets it once on Awake. Setting it per-ball
            // causes redundant global mutations every time a ball is activated.
        }

        private void Update()
        {
            // Lifetime expiry
            if (Time.time - _spawnTime > Lifetime)
            {
                ReturnToPool();
                return;
            }

            // Kill zone
            if (transform.position.y < DestroyBelowY)
            {
                ReturnToPool();
                return;
            }

            // Force sleep when nearly stationary (saves solver iterations)
            if (!_rb.IsSleeping() && _rb.linearVelocity.sqrMagnitude < 0.01f)
                _rb.Sleep();
        }

        /// <summary>
        /// Return to pool instead of destroying — avoids GC alloc.
        /// Falls back to Destroy if pool/controller not available.
        /// </summary>
        private void ReturnToPool()
        {
            _hitGates.Clear(); // Reset gate tracking for reuse
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            if (DropperController.BallPool != null)
            {
                gameObject.SetActive(false);
                DropperController.BallPool.Push(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public bool HasHitGate(int gateInstanceId)  => _hitGates.Contains(gateInstanceId);
        public void MarkGateHit(int gateInstanceId) => _hitGates.Add(gateInstanceId);
    }
}
