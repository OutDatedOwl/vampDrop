using UnityEngine;
using System;
using System.Collections.Generic;
using TMPro;

namespace Vampire
{
    public enum CharacterSide { Left, Right, None }

    [Serializable]
    public class ComicPanel
    {
        [Header("Element Settings")]
        public string elementName = "Element";
        public Sprite sprite;
        public LayerType layer = LayerType.Midground;

        [Header("Size Mode")]
        [Tooltip("How this element should be sized")]
        public ElementSizeMode sizeMode = ElementSizeMode.FitToSprite;

        [Tooltip("Custom size in pixels (only used if sizeMode = Custom)")]
        public Vector2 customSize = new Vector2(500, 500);

        [Header("Position & Size")]
        [Tooltip("Normalized position (0-1), where 0.5,0.5 is center")]
        public Vector2 position = new Vector2(0.5f, 0.5f);

        [Tooltip("Scale multiplier (1 = original size)")]
        public float scale = 1f;

        [Header("Animation")]
        public PanelAnimation animation = PanelAnimation.None;

        [Tooltip("Delay before this element appears (seconds)")]
        public float appearDelay = 0f;

        [Tooltip("Duration of animation (seconds)")]
        public float animationDuration = 1f;

        [Header("Animation Settings")]
        [Tooltip("For pan animations: how far to pan (screen heights/widths)")]
        public float panDistance = 0.3f;

        [Tooltip("For zoom animations: start/end scale")]
        public float zoomScale = 0.5f;

        [Header("Dialogue Text (Optional)")]
        [Tooltip("Text rendered over this element. Assign blank_text as the sprite for a speech-bubble background.")]
        [TextArea(2, 5)]
        public string dialogueText = "";

        [Tooltip("Font size for dialogue text")]
        [Range(8, 96)]
        public int fontSize = 28;

        [Tooltip("Text colour")]
        public Color textColor = Color.black;

        [Tooltip("Text alignment inside the element")]
        public TextAlignmentOptions textAlignment = TextAlignmentOptions.Center;

        [Tooltip("Inner padding in pixels: X=left  Y=right  Z=top  W=bottom")]
        public Vector4 textPadding = new Vector4(24, 24, 16, 16);

        // Runtime data
        [NonSerialized] public GameObject gameObject;
        [NonSerialized] public RectTransform rectTransform;
        [NonSerialized] public UnityEngine.UI.Image imageComponent;
        [NonSerialized] public TextMeshProUGUI textComponent;
        [NonSerialized] public float animationStartTime;
        [NonSerialized] public bool isAnimating;
        [NonSerialized] public Vector2 animationStartPos;
        [NonSerialized] public Vector2 animationEndPos;
        [NonSerialized] public float animationStartScale;
        [NonSerialized] public float animationEndScale;
        [NonSerialized] public Color animationStartColor;
        [NonSerialized] public Color animationEndColor;
        [NonSerialized] public GameObject curtainLeft;  // For CurtainOpen animation
        [NonSerialized] public GameObject curtainRight; // For CurtainOpen animation
    }
}
