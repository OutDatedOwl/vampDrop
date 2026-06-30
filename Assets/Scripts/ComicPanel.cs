using UnityEngine;
using System;

namespace Vampire
{
    public enum CharacterSide { Left, Right, None }

    [Serializable]
    public class ComicPanel
    {
        [Header("Visuals")]
        public Sprite background;
        public Sprite character;
        public CharacterSide characterSide = CharacterSide.Left;

        [Header("Dialogue")]
        [TextArea(2, 5)]
        public string dialogueText;

        [Header("Timing")]
        [Tooltip("Auto-advance after this many seconds. 0 = wait for player input.")]
        public float autoDuration = 0f;

        [Header("Audio")]
        public AudioClip sfx;
        public AudioClip music;
        [Range(0f, 1f)]
        public float musicVolume = 0.5f;
    }
}
