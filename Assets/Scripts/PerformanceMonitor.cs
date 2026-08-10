using UnityEngine;
using Unity.Entities;

namespace Vampire.DropPuzzle
{
    /// <summary>
    /// Simple FPS and entity count monitor to identify performance bottlenecks
    /// </summary>
    public class PerformanceMonitor : MonoBehaviour
    {
        private float deltaTime = 0.0f;
        private EntityManager entityManager;
        private EntityQuery _ballQuery;
        private GUIStyle _guiStyle;
        
        private void Start()
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _ballQuery = entityManager.CreateEntityQuery(typeof(RiceBallTag));
        }

        private void OnDestroy()
        {
            if (_ballQuery != default) _ballQuery.Dispose();
        }

        private void Update()
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

            if (Time.frameCount % 180 == 0)
            {
                float fps = 1.0f / deltaTime;
                int ballCount = _ballQuery.CalculateEntityCount();
                Debug.Log($"[PERFORMANCE] FPS: {fps:F1} | Balls: {ballCount} | Frame Time: {deltaTime * 1000f:F1}ms");
                if (fps < 30f)
                    Debug.LogWarning($"[PERFORMANCE] ⚠️ LOW FPS! {fps:F1} fps with {ballCount} balls");
            }
        }

        private void OnGUI()
        {
            if (_guiStyle == null)
            {
                _guiStyle = new GUIStyle();
                _guiStyle.alignment = TextAnchor.UpperLeft;
            }

            int h = Screen.height;
            _guiStyle.fontSize = h * 2 / 50;

            float fps = 1.0f / deltaTime;
            _guiStyle.normal.textColor = fps < 20f ? Color.red : fps < 40f ? Color.yellow : Color.green;

            GUI.Label(new Rect(10, 10, Screen.width, h * 2 / 100),
                $"{fps:0.} FPS ({deltaTime * 1000f:0.0} ms)", _guiStyle);
        }
    }
}
