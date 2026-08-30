using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using StarterAssets;
using V0.UI;
using V0.Interaction;

namespace V0.Cinematics
{
    /// <summary>
    /// Attached to the GoodEnding trigger volume.
    /// When the player enters, plays the stranger's final chilling threat dialogue,
    /// then loads the GoodEnding scene.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GoodEndingTrigger : MonoBehaviour
    {
        [System.Serializable]
        public class DialogueLine
        {
            [TextArea(1, 3)]
            public string text;
            public Color color = new Color(1f, 0.88f, 0.6f);
            public float duration = 4.5f;
        }

        [Header("Ending Dialogue")]
        [Tooltip("List of dialogue lines shown before the scene transition")]
        [SerializeField] private List<DialogueLine> _lines = new List<DialogueLine>()
        {
            new DialogueLine()
            {
                text = "[Stranger]: \"Don't leave me... please... you can't just leave me here!\"",
                color = new Color(1f, 0.88f, 0.6f),
                duration = 4.5f
            },
            new DialogueLine()
            {
                text = "[Stranger]: \"One day... I WILL get out of here... and I will haunt you for the rest of your life!\"",
                color = new Color(1f, 0.3f, 0.3f),
                duration = 5.5f
            },
            new DialogueLine()
            {
                text = "[Player]: *stumbles back in terror, runs for the truck*",
                color = new Color(0.85f, 0.85f, 0.9f),
                duration = 3.5f
            }
        };

        [Header("Scene Transition")]
        [Tooltip("Exact name of the Good Ending scene to load")]
        [SerializeField] private string _goodEndingSceneName = "GoodEnding";

        [Tooltip("Delay (seconds) after last dialogue line before the fade and scene load")]
        [SerializeField] private float _transitionDelay = 1.0f;

        [Tooltip("Fade to black duration (seconds)")]
        [SerializeField] private float _fadeDuration = 1.5f;

        [Header("Cinematic UI References")]
        [SerializeField] private CanvasGroup _letterboxCanvasGroup;
        [SerializeField] private Text _subtitleText;

        [Header("Player Control References")]
        [SerializeField] private FirstPersonController _playerController;
        [SerializeField] private StarterAssetsInputs _playerInputs;

        private bool _hasTriggered = false;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            AutoFindReferences();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasTriggered) return;
            if (other.CompareTag("Player") || other.GetComponent<FirstPersonController>() != null || other.GetComponentInParent<FirstPersonController>() != null)
            {
                _hasTriggered = true;
                StartCoroutine(GoodEndingRoutine());
            }
        }

        private IEnumerator GoodEndingRoutine()
        {
            // Freeze player
            SetPlayerControlsActive(false);

            // Fade in letterbox bars
            if (_letterboxCanvasGroup != null)
            {
                _letterboxCanvasGroup.DOKill();
                _letterboxCanvasGroup.DOFade(1f, 0.6f).SetEase(Ease.InOutSine);
            }

            yield return new WaitForSeconds(0.5f);

            // Play each dialogue line
            for (int i = 0; i < _lines.Count; i++)
            {
                DialogueLine line = _lines[i];
                if (line == null || string.IsNullOrEmpty(line.text)) continue;

                if (_subtitleText != null)
                {
                    _subtitleText.DOKill();
                    _subtitleText.text = line.text;
                    _subtitleText.color = new Color(line.color.r, line.color.g, line.color.b, 0f);
                    _subtitleText.DOFade(1f, 0.4f);
                }

                yield return new WaitForSeconds(line.duration);

                if (_subtitleText != null)
                {
                    _subtitleText.DOKill();
                    _subtitleText.DOFade(0f, 0.4f);
                }

                yield return new WaitForSeconds(0.2f);
            }

            yield return new WaitForSeconds(_transitionDelay);

            // Fade to black using FadeScreen singleton
            bool done = false;
            FadeScreen.Instance.FadeToBlack(_fadeDuration, () => done = true);
            yield return new WaitUntil(() => done);

            yield return new WaitForSeconds(0.1f);

            Debug.Log($"<color=green>[GoodEndingTrigger]</color> Setting Good Ending and Loading scene: '{_goodEndingSceneName}'");
            EndingManager.CurrentEnding = EndingType.Good;
            SceneManager.LoadScene(_goodEndingSceneName);
        }

        private void SetPlayerControlsActive(bool active)
        {
            // Disable flashlight during cutscene, restore after
            FlashlightController.SetGlobalCutsceneMode(!active);

            if (_playerInputs != null)
            {
                _playerInputs.cursorLocked = true;
                _playerInputs.cursorInputForLook = active;
                _playerInputs.ResetInputs();
            }
            if (_playerController != null)
            {
                _playerController.enabled = active;
                if (active) _playerController.ResetLookOrientation();
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void AutoFindReferences()
        {
            if (_playerController == null)
                _playerController = FindFirstObjectByType<FirstPersonController>();
            if (_playerInputs == null && _playerController != null)
                _playerInputs = _playerController.GetComponent<StarterAssetsInputs>();

            GameObject canvasObj = GameObject.Find("CinematicLetterboxCanvas");
            if (canvasObj != null)
            {
                if (_letterboxCanvasGroup == null) _letterboxCanvasGroup = canvasObj.GetComponent<CanvasGroup>();
                if (_subtitleText == null) _subtitleText = canvasObj.GetComponentInChildren<Text>();
            }
        }
    }
}
