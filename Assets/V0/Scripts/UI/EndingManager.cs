using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using DG.Tweening;

namespace V0.UI
{
    public enum EndingType
    {
        Good,        // GoodEnding trigger
        Okay,        // OkayEnding trigger
        LastTrigger  // LastTrigger cutscene (Backstab betrayal)
    }

    /// <summary>
    /// Lightweight, Zero-GC Ending Screen Controller:
    /// All UI elements are baked into the scene at compile time for maximum performance on WebGL & low-end PCs.
    /// Sets active appropriate ending text and runs smooth fading transitions.
    /// </summary>
    public class EndingManager : MonoBehaviour
    {
        public static EndingType CurrentEnding = EndingType.Good;

        [Header("Ending Narratives (Customizable)")]
        [TextArea(2, 3)]
        [SerializeField] private string _goodEndingText = "You successfully escaped the terror.";
        [TextArea(2, 3)]
        [SerializeField] private string _okayEndingText = "You might have save yourself now But oneday He will hunt you down.";
        [TextArea(2, 3)]
        [SerializeField] private string _lastTriggerText = "Have a Good Night sleep. Because you are not waking up";

        [Header("Thank You Screen Settings")]
        [SerializeField] private string _thankYouTitle = "Thank you for playing!";
        [SerializeField] private string _creatorCreditsText = "Created by: Anik Pal";
        [SerializeField] private string _continueButtonText = "CONTINUE";

        [Header("Pre-baked UI References in Scene")]
        [SerializeField] private CanvasGroup _narrativeGroup;
        [SerializeField] private Text _narrativeText;
        [SerializeField] private CanvasGroup _thankYouGroup;
        [SerializeField] private Text _thankYouTitleText;
        [SerializeField] private Text _creatorCreditsTextUI;
        [SerializeField] private Button _continueButton;

        [Header("Timing")]
        [SerializeField] private float _textFadeInDuration = 1.6f;
        [SerializeField] private float _textHoldDuration = 4.5f;
        [SerializeField] private float _textFadeOutDuration = 1.2f;
        [SerializeField] private float _thankYouFadeInDuration = 1.5f;

        private bool _isThankYouActive = false;
        private bool _hasClickedContinue = false;

        private void Awake()
        {
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = Color.black;
            }

            AutoWireIfMissing();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveAllListeners();
                _continueButton.onClick.AddListener(OnContinueClicked);
            }

            StartCoroutine(PlayEndingSequence());
        }

        private void Update()
        {
            if (_isThankYouActive && !_hasClickedContinue)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame))
                {
                    OnContinueClicked();
                }
#else
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape))
                {
                    OnContinueClicked();
                }
#endif
            }
        }

        private IEnumerator PlayEndingSequence()
        {
            if (_thankYouGroup != null)
            {
                _thankYouGroup.alpha = 0f;
                _thankYouGroup.gameObject.SetActive(false);
            }

            if (_narrativeText != null)
            {
                switch (CurrentEnding)
                {
                    case EndingType.Good:
                        _narrativeText.text = _goodEndingText;
                        break;
                    case EndingType.Okay:
                        _narrativeText.text = _okayEndingText;
                        break;
                    case EndingType.LastTrigger:
                        _narrativeText.text = _lastTriggerText;
                        break;
                }
            }

            if (_thankYouTitleText != null) _thankYouTitleText.text = _thankYouTitle;
            if (_creatorCreditsTextUI != null) _creatorCreditsTextUI.text = _creatorCreditsText;

            yield return new WaitForSeconds(0.5f);

            // 1. Fade in narrative text on black screen
            if (_narrativeGroup != null)
            {
                _narrativeGroup.DOKill();
                _narrativeGroup.alpha = 0f;
                _narrativeGroup.DOFade(1f, _textFadeInDuration).SetEase(Ease.InOutSine);
            }

            // 2. Hold text on screen for reading
            yield return new WaitForSeconds(_textHoldDuration);

            // 3. Fade out narrative text
            if (_narrativeGroup != null)
            {
                _narrativeGroup.DOFade(0f, _textFadeOutDuration).SetEase(Ease.InOutSine);
            }
            yield return new WaitForSeconds(_textFadeOutDuration + 0.3f);

            // 4. Fade in "Thank you for playing!" & Continue button
            if (_thankYouGroup != null)
            {
                _thankYouGroup.gameObject.SetActive(true);
                _thankYouGroup.DOKill();
                _thankYouGroup.alpha = 0f;
                _thankYouGroup.DOFade(1f, _thankYouFadeInDuration).SetEase(Ease.InOutSine);
            }

            _isThankYouActive = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void OnContinueClicked()
        {
            if (_hasClickedContinue) return;
            _hasClickedContinue = true;

            if (_continueButton != null) _continueButton.interactable = false;

            // Fade out and return to MainMenu scene
            if (_thankYouGroup != null)
            {
                _thankYouGroup.DOKill();
                _thankYouGroup.DOFade(0f, 0.8f).OnComplete(() =>
                {
                    Debug.Log("<color=green>[EndingManager]</color> Returning to MainMenu scene.");
                    SceneManager.LoadScene("MainMenu");
                });
            }
            else
            {
                SceneManager.LoadScene("MainMenu");
            }
        }

        public void AutoWireIfMissing()
        {
            if (_narrativeGroup == null)
            {
                Transform nT = transform.Find("EndingCanvas/NarrativePanel") ?? transform.Find("NarrativePanel");
                if (nT != null)
                {
                    _narrativeGroup = nT.GetComponent<CanvasGroup>();
                    _narrativeText = nT.GetComponentInChildren<Text>(true);
                }
            }

            if (_thankYouGroup == null)
            {
                Transform tT = transform.Find("EndingCanvas/ThankYouPanel") ?? transform.Find("ThankYouPanel");
                if (tT != null)
                {
                    _thankYouGroup = tT.GetComponent<CanvasGroup>();
                    _continueButton = tT.GetComponentInChildren<Button>(true);

                    Text[] texts = tT.GetComponentsInChildren<Text>(true);
                    if (texts.Length > 0 && _thankYouTitleText == null) _thankYouTitleText = texts[0];
                    if (texts.Length > 1 && _creatorCreditsTextUI == null) _creatorCreditsTextUI = texts[1];
                }
            }
        }
    }
}
