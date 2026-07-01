using UnityEngine;

namespace Vampire.DropPuzzle
{
    /// <summary>
    /// Three-phase camera for the drop puzzle:
    ///   Assessment  — zoomed out, shows the entire puzzle, "Ready?" prompt
    ///   ZoomIn      — smooth lerp toward the dropper after player confirms
    ///   Drop        — zooms back out to watch all the action
    ///
    /// Attach to Main Camera in the DropPuzzle scene.
    /// Works with orthographic cameras.  Perspective cameras are supported via FOV.
    /// </summary>
    public class DropPuzzleCameraController : MonoBehaviour
    {
        // ── Camera controller singleton ───────────────────────────────────────
        public static DropPuzzleCameraController Instance { get; private set; }

        /// <summary>
        /// Returns false during the Assessment phase so the dropper won't fire
        /// when the player presses Space to confirm they're ready.
        /// </summary>
        public static bool AllowDrop =>
            Instance == null || Instance._phase != Phase.Assessment;

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Assessment (overview)")]
        [Tooltip("Extra world-units of padding around the puzzle bounds")]
        public float BoundsPadding = 3f;

        [Header("Ready-to-drop (zoomed in)")]
        [Tooltip("Fraction of the assessment ortho size to zoom to (0.5 = half as wide)")]
        [Range(0.2f, 0.9f)]
        public float ReadyZoomFraction = 0.55f;
        [Tooltip("World-space offset from the dropper position when zoomed in")]
        public Vector2 ReadyOffset = new Vector2(0f, 1.5f);

        [Header("Transitions")]
        [Tooltip("Lerp speed for camera position and ortho size")]
        public float TransitionSpeed = 2.5f;

        [Header("Prompt UI")]
        public string AssessmentPrompt = "Press [SPACE] when ready to drop";

        // ── Private state ─────────────────────────────────────────────────────

        private enum Phase { Assessment, ZoomIn, ReadyToDrop, Drop }
        private Phase _phase = Phase.Assessment;

        private Camera _cam;

        // Cached positions/sizes for each phase
        private Vector3 _overviewPos;
        private float   _overviewOrtho;
        private Vector3 _readyPos;
        private float   _readyOrtho;

        // Current lerp targets
        private Vector3 _targetPos;
        private float   _targetOrtho;

        // GUI
        private GUIStyle _promptStyle;
        private bool     _stylesBuilt;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;

            // Prevent camera from lerping toward Vector3.zero before InitAfterLoad runs
            _targetPos   = transform.position;
            _targetOrtho = _cam != null && _cam.orthographic ? _cam.orthographicSize : 5f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DropperControllerECS.OnAnyDropStarted -= OnDropStarted;
            PuzzlePrefabLoader.OnPuzzleLoaded     -= OnPuzzleLoaded;
        }

        private void Start()
        {
            DropperControllerECS.OnAnyDropStarted += OnDropStarted;
            PuzzlePrefabLoader.OnPuzzleLoaded     += OnPuzzleLoaded;

            // Puzzle may have already loaded before Start() if execution order placed
            // PuzzlePrefabLoader first — snap immediately in that case.
            if (PuzzlePrefabLoader.IsPuzzleReady)
                SnapToOverview();
        }

        private void OnPuzzleLoaded()
        {
            SnapToOverview();
        }

        private void SnapToOverview()
        {
            CalculatePositions();
            transform.position = _overviewPos;
            SetOrtho(_overviewOrtho);
            _targetPos   = _overviewPos;
            _targetOrtho = _overviewOrtho;
            _phase       = Phase.Assessment;
        }

        // ── Update ────────────────────────────────────────────────────────────

        private void Update()
        {
            // Smooth lerp toward current target
            transform.position = Vector3.Lerp(
                transform.position, _targetPos,
                TransitionSpeed * Time.deltaTime);

            if (_cam.orthographic)
                _cam.orthographicSize = Mathf.Lerp(
                    _cam.orthographicSize, _targetOrtho,
                    TransitionSpeed * Time.deltaTime);

            switch (_phase)
            {
                case Phase.Assessment:
                    // Space captured here → go to zoom-in; DropperControllerECS is gated out
                    if (Input.GetKeyDown(KeyCode.Space))
                        BeginZoomIn();
                    break;

                case Phase.ZoomIn:
                    // Finish zoom-in when close enough
                    if (Vector3.Distance(transform.position, _targetPos) < 0.15f)
                        _phase = Phase.ReadyToDrop;
                    break;

                case Phase.ReadyToDrop:
                    // AllowDrop is now true — DropperControllerECS will fire on Space
                    break;

                case Phase.Drop:
                    break;
            }
        }

        // ── Phase transitions ─────────────────────────────────────────────────

        private void BeginZoomIn()
        {
            _phase       = Phase.ZoomIn;
            _targetPos   = _readyPos;
            _targetOrtho = _readyOrtho;
        }

        private void OnDropStarted()
        {
            // Zoom back out to watch the action
            _phase       = Phase.Drop;
            _targetPos   = _overviewPos;
            _targetOrtho = _overviewOrtho;
        }

        // ── Position calculation ──────────────────────────────────────────────

        private void CalculatePositions()
        {
            Bounds b       = GetPuzzleBounds();
            float  aspect  = _cam.aspect;
            float  camZ    = transform.position.z;

            // Overview: frame the entire puzzle
            float neededH = b.extents.y + BoundsPadding;
            float neededW = (b.extents.x + BoundsPadding) / aspect;
            _overviewOrtho = Mathf.Max(neededH, neededW, 6f);
            _overviewPos   = new Vector3(b.center.x, b.center.y, camZ);

            // Ready-to-drop: zoom in on the dropper
            var dropper = FindObjectOfType<DropperControllerECS>();
            Vector3 dropperPos = dropper != null ? dropper.transform.position : b.center;
            _readyOrtho = _overviewOrtho * ReadyZoomFraction;
            _readyPos   = new Vector3(
                dropperPos.x + ReadyOffset.x,
                dropperPos.y + ReadyOffset.y,
                camZ);

            Debug.Log($"[CameraController] bounds={b.center} size={b.size} " +
                      $"overviewOrtho={_overviewOrtho:F1} readyOrtho={_readyOrtho:F1}");
        }

        private Bounds GetPuzzleBounds()
        {
            bool started = false;
            Bounds b     = default;

            // Prefer bounds from the live puzzle instance — works regardless of tags
            var puzzle = PuzzlePrefabLoader.CurrentPuzzle;
            if (puzzle != null)
            {
                foreach (var col in puzzle.GetComponentsInChildren<Collider>())
                {
                    if (!started) { b = col.bounds; started = true; }
                    else b.Encapsulate(col.bounds);
                }

                // Also encapsulate renderers so visual-only objects aren't clipped
                foreach (var rend in puzzle.GetComponentsInChildren<Renderer>())
                {
                    if (!started) { b = rend.bounds; started = true; }
                    else b.Encapsulate(rend.bounds);
                }
            }

            // Fallback: Wall-tagged colliders anywhere in the scene
            if (!started)
            {
                foreach (var col in FindObjectsOfType<Collider>())
                {
                    if (!col.CompareTag("Wall")) continue;
                    if (!started) { b = col.bounds; started = true; }
                    else b.Encapsulate(col.bounds);
                }
            }

            if (!started)
                b = new Bounds(Vector3.zero, new Vector3(20f, 30f, 1f));

            return b;
        }

        // ── GUI ───────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_phase == Phase.Assessment)
                DrawPrompt(AssessmentPrompt);
            else if (_phase == Phase.ReadyToDrop)
                DrawPrompt("Press [SPACE] to drop!");
        }

        private void DrawPrompt(string msg)
        {
            BuildStyles();
            float w = 480f, h = 60f;
            GUI.Box(new Rect((Screen.width - w) / 2f, Screen.height - h - 80f, w, h),
                msg, _promptStyle);
        }

        private void BuildStyles()
        {
            if (_stylesBuilt) return;
            _stylesBuilt = true;

            _promptStyle = new GUIStyle(GUI.skin.box);
            _promptStyle.fontSize  = 26;
            _promptStyle.fontStyle = FontStyle.Bold;
            _promptStyle.alignment = TextAnchor.MiddleCenter;
            _promptStyle.normal.textColor = Color.white;
            _promptStyle.normal.background = MakeTex(new Color(0f, 0f, 0f, 0.78f));
        }

        private void SetOrtho(float size)
        {
            if (_cam.orthographic) _cam.orthographicSize = size;
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(2, 2);
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            return t;
        }

        // ── Context menu helpers ──────────────────────────────────────────────

        [ContextMenu("Recalculate Camera Positions")]
        public void EditorRecalculate() => CalculatePositions();
    }
}
