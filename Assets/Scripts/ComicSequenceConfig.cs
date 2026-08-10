using UnityEngine;
using System.Collections.Generic;

namespace Vampire
{
    [CreateAssetMenu(fileName = "NewComicSequence", menuName = "Vampire/Comic Sequence Config", order = 1)]
    public class ComicSequenceConfig : ScriptableObject
    {
        [Header("Sequence Info")]
        public string sequenceId = "new_sequence";
        public string sequenceName = "New Comic Sequence";

        [Header("Panels")]
        public List<ComicPanel> panels = new List<ComicPanel>();

        [Header("Transition")]
        [Tooltip("Scene to load after this comic completes")]
        public string nextSceneName = "FPS_Collect";

        [Header("Audio")]
        [Tooltip("Background music that loops for the whole sequence")]
        public AudioClip sequenceMusic;
        [Range(0f, 1f)]
        public float musicVolume = 0.5f;
    }
}
