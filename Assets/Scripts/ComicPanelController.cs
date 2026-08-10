using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Vampire
{
    public class ComicPanelController : MonoBehaviour
    {
        [Header("Image Slots")]
        public Image background;
        public Image characterLeft;
        public Image characterRight;

        [Header("Dialogue Slots")]
        public TMP_Text dialogueText;
        public GameObject dialogueBox;

        [Header("Transition")]
        public CanvasGroup canvasGroup;

        [HideInInspector] public bool entranceComplete;

        private string fullText;
        private Image activeCharacter;
        private Vector2 characterEndPos;
        private CanvasGroup dialogueGroup;

        private const float CharsPerSecond = 38f;

        public void Populate(ComicPanel data)
        {
            entranceComplete = false;
            fullText = data.dialogueText;

            if (background != null)
            {
                background.sprite = data.background;
                background.color = new Color(1, 1, 1, 0);
                background.gameObject.SetActive(data.background != null);
            }

            bool hasChar = data.character != null && data.characterSide != CharacterSide.None;
            activeCharacter = null;

            if (characterLeft != null)
            {
                characterLeft.sprite = data.character;
                characterLeft.color = new Color(1, 1, 1, 0);
                bool active = hasChar && data.characterSide == CharacterSide.Left;
                characterLeft.gameObject.SetActive(active);
                if (active) activeCharacter = characterLeft;
            }
            if (characterRight != null)
            {
                characterRight.sprite = data.character;
                characterRight.color = new Color(1, 1, 1, 0);
                bool active = hasChar && data.characterSide == CharacterSide.Right;
                characterRight.gameObject.SetActive(active);
                if (active) activeCharacter = characterRight;
            }

            bool hasText = !string.IsNullOrEmpty(fullText);
            if (dialogueText != null) dialogueText.text = "";
            if (dialogueBox != null)
            {
                dialogueBox.SetActive(hasText);
                dialogueGroup = dialogueBox.GetComponent<CanvasGroup>();
                if (dialogueGroup == null) dialogueGroup = dialogueBox.AddComponent<CanvasGroup>();
                dialogueGroup.alpha = 0f;
            }

            if (activeCharacter != null)
                characterEndPos = activeCharacter.rectTransform.anchoredPosition;
        }

        public IEnumerator PlayEntrance(bool backgroundChanged = true)
        {
            // 1. Background — only fade if it changed, otherwise snap visible
            if (background != null && background.gameObject.activeSelf)
            {
                if (backgroundChanged)
                    yield return StartCoroutine(FadeImage(background, 0f, 1f, 0.55f));
                else
                    background.color = Color.white;
            }

            yield return new WaitForSeconds(backgroundChanged ? 0.15f : 0f);

            // 2. Character slides and fades in
            if (activeCharacter != null)
            {
                float slideDir = activeCharacter == characterLeft ? -70f : 70f;
                Vector2 startPos = characterEndPos + new Vector2(slideDir, -25f);
                activeCharacter.rectTransform.anchoredPosition = startPos;

                float dur = 0.5f;
                for (float t = 0f; t < dur; t += Time.deltaTime)
                {
                    float p = Smooth(t / dur);
                    activeCharacter.color = new Color(1, 1, 1, p);
                    activeCharacter.rectTransform.anchoredPosition = Vector2.Lerp(startPos, characterEndPos, p);
                    yield return null;
                }
                activeCharacter.color = Color.white;
                activeCharacter.rectTransform.anchoredPosition = characterEndPos;
            }

            yield return new WaitForSeconds(0.2f);

            // 3. Dialogue box fades in, then typewriter
            if (dialogueBox != null && dialogueBox.activeSelf && dialogueGroup != null)
            {
                yield return StartCoroutine(FadeCanvasGroup(dialogueGroup, 0f, 1f, 0.3f));
                yield return StartCoroutine(Typewriter());
            }

            entranceComplete = true;
        }

        public void SkipEntrance()
        {
            StopAllCoroutines();

            if (background != null && background.gameObject.activeSelf)
                background.color = Color.white;

            if (activeCharacter != null)
            {
                activeCharacter.color = Color.white;
                activeCharacter.rectTransform.anchoredPosition = characterEndPos;
            }

            if (dialogueGroup != null) dialogueGroup.alpha = 1f;
            if (dialogueText != null) dialogueText.text = fullText;

            entranceComplete = true;
        }

        public IEnumerator FadeOut(float duration)
        {
            if (canvasGroup == null) yield break;
            for (float t = duration; t > 0f; t -= Time.deltaTime)
            {
                canvasGroup.alpha = Smooth(t / duration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        // Fades only character and dialogue — background stays visible for same-bg transitions.
        public IEnumerator FadeOutContent(float duration)
        {
            for (float t = duration; t > 0f; t -= Time.deltaTime)
            {
                float a = Smooth(t / duration);
                if (activeCharacter != null) activeCharacter.color = new Color(1, 1, 1, a);
                if (dialogueGroup != null)   dialogueGroup.alpha = a;
                yield return null;
            }
            if (activeCharacter != null) activeCharacter.color = new Color(1, 1, 1, 0f);
            if (dialogueGroup != null)   dialogueGroup.alpha = 0f;
        }

        private IEnumerator Typewriter()
        {
            if (dialogueText == null || string.IsNullOrEmpty(fullText)) yield break;
            float delay = 1f / CharsPerSecond;
            for (int i = 1; i <= fullText.Length; i++)
            {
                dialogueText.text = fullText.Substring(0, i);
                yield return new WaitForSeconds(delay);
            }
        }

        private IEnumerator FadeImage(Image img, float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                img.color = new Color(1, 1, 1, Mathf.Lerp(from, to, Smooth(t / duration)));
                yield return null;
            }
            img.color = new Color(1, 1, 1, to);
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, Smooth(t / duration));
                yield return null;
            }
            group.alpha = to;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
