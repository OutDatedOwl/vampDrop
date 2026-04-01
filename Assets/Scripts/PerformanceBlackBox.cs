using UnityEngine;
using Unity.Entities;
using Unity.Profiling;
using System.Text;

namespace Vampire.DropPuzzle
{
    public class PerformanceBlackBox : MonoBehaviour
    {
        [Header("Thresholds")]
        public float chokeThresholdMs = 16.6f; // Target 60fps
        public int gcAllocThresholdBytes = 1024; // 1KB is a lot for ECS
        public int entityChangeThreshold = 100;
        public float jitterThresholdMs = 5f;

        [Header("Logging")]
        public float logCooldown = 0.5f;

        private ProfilerRecorder _gcAllocRecorder;
        private ProfilerRecorder _mainThreadRecorder;
        private ProfilerRecorder _renderThreadRecorder;
        private ProfilerRecorder _batchesRecorder;

        private EntityManager _em;
        private int _lastEntityCount;
        private int _lastGCCount;
        private float _lastLogTime;
        private float _avgFrameMs;
        
        // New Global Alloc Tracker
        private long _lastTotalAlloc;

        void OnEnable()
        {
            _gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");
            _mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            _renderThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 15);
            _batchesRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");

            _lastGCCount = System.GC.CollectionCount(0);
            _lastTotalAlloc = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        }

        void OnDisable()
        {
            _gcAllocRecorder.Dispose();
            _mainThreadRecorder.Dispose();
            _renderThreadRecorder.Dispose();
            _batchesRecorder.Dispose();
        }

        void Start()
        {
            if (World.DefaultGameObjectInjectionWorld != null)
                _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        void Update()
        {
            float frameMs = Time.unscaledDeltaTime * 1000f;
            _avgFrameMs = Mathf.Lerp(_avgFrameMs, frameMs, 0.1f);
            float jitter = Mathf.Abs(frameMs - _avgFrameMs);

            // 1. Check GC & Allocations
            long gcAlloc = _gcAllocRecorder.Valid ? _gcAllocRecorder.LastValue : 0;
            int gcCount = System.GC.CollectionCount(0);
            int gcDelta = gcCount - _lastGCCount;
            _lastGCCount = gcCount;

            // 2. Global Allocation Delta (More accurate than Recorder sometimes)
            long totalAlloc = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            long allocDelta = totalAlloc - _lastTotalAlloc;
            _lastTotalAlloc = totalAlloc;

            // 3. Thread Timings
            float mainMs = GetMs(_mainThreadRecorder);
            float renderMs = GetMs(_renderThreadRecorder);

            // 4. Trigger Check
            bool isSpike = frameMs > chokeThresholdMs || 
                           allocDelta > gcAllocThresholdBytes || 
                           gcDelta > 0 || 
                           jitter > jitterThresholdMs;

            if (isSpike && Time.time > _lastLogTime + logCooldown)
            {
                // Move heavy ECS counting INSIDE the spike check to save perf
                int currentEntities = (_em != default && _em.World.IsCreated) ? _em.UniversalQuery.CalculateEntityCount() : 0;
                int entityDelta = Mathf.Abs(currentEntities - _lastEntityCount);
                _lastEntityCount = currentEntities;

                LogSpike(frameMs, jitter, allocDelta, gcDelta, entityDelta, currentEntities, mainMs, renderMs);
                _lastLogTime = Time.time;
            }
        }

        float GetMs(ProfilerRecorder recorder) => recorder.Valid ? recorder.LastValue * 1e-6f : 0f;

        void LogSpike(float frameMs, float jitter, long allocDelta, int gcDelta, int entityDelta, int totalEntities, float mainMs, float renderMs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<color=#FF00FF><b>[PERF SPIKE @ {Time.time:F2}s]</b></color>");

            if (gcDelta > 0) sb.AppendLine("<color=red><b>CAUSE: GC COLLECTION HITCH</b></color>");
            else if (allocDelta > gcAllocThresholdBytes) sb.AppendLine($"<color=orange><b>CAUSE: HIGH ALLOCATION ({allocDelta / 1024f:F2} KB)</b></color>");
            else if (mainMs > chokeThresholdMs) sb.AppendLine("<b>CAUSE: MAIN THREAD STALL</b>");

            sb.AppendLine($"• Frame: {frameMs:F2}ms | Main: {mainMs:F2}ms | Render: {renderMs:F2}ms");
            sb.AppendLine($"• Alloc this frame: {allocDelta / 1024f:F2} KB");
            sb.AppendLine($"• Entities: {totalEntities} (Δ{entityDelta}) | Batches: {_batchesRecorder.LastValue}");

            Debug.LogWarning(sb.ToString());
        }
    }
}