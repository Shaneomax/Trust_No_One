using System;
using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;
using StarterAssets;
using V0.Interaction;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace V0.Cinematics
{
    /// <summary>
    /// Coordinates the atmospheric horror wake-up intro sequence.
    /// Uses DOTween for smooth eyelid fade-in/out and a multi-stage physical struggle
    /// simulating an unconscious player hauling themselves up from the dirt to their feet.
    /// </summary>
    public class WakeUpSequenceController : MonoBehaviour
    {
        [Header("Cinemachine Cameras")]
        [Tooltip("The camera representing the player lying on the ground, tilted in the field")]
        [SerializeField] private CinemachineVirtualCameraBase _wakeUpCamera;

        [Tooltip("The standard player first-person follow camera")]
        [SerializeField] private CinemachineVirtualCameraBase _playerFollowCamera;

        [Header("Player References")]
        [SerializeField] private FirstPersonController _playerController;
        [SerializeField] private PlayerInteraction _playerInteraction;
        [SerializeField] private StarterAssetsInputs _playerInputs;

        [Header("UI Eyelid / Blackout Overlay")]
        [Tooltip("CanvasGroup controlling the black screen overlay for eye blinking")]
        [SerializeField] private CanvasGroup _blackoutCanvasGroup;

        [Header("Cinematic Subtitles / Thoughts (Preset in Inspector)")]
        [Tooltip("UI Text element to display waking thoughts/subtitles")]
        [SerializeField] private UnityEngine.UI.Text _subtitleText;

        [Tooltip("Thought when eyes first flutter open")]
        [SerializeField] private string _line1FirstBlink = "...Ugh... my head hurts...";

        [Tooltip("Thought when head drops back into dirt after failed lift")]
        [SerializeField] private string _line2FailedLift = "...Where... where am I...?";

        [Tooltip("Thought when standing up on feet")]
        [SerializeField] private string _line3Standing = "...There's a house up ahead. I need to find help.";

        [Header("Eyelid Blink Settings (DOTween Smooth Fades)")]
        [Tooltip("Seconds the screen remains pitch black before first eyelid flutter")]
        [SerializeField] private float _initialBlackoutHold = 1.0f;

        [Tooltip("Duration for eyelids to flutter partially open on first attempt")]
        [SerializeField] private float _firstBlinkOpenDuration = 1.2f;

        [Tooltip("How open the eyelids get on first blink (0 = fully open, 1 = shut)")]
        [Range(0.1f, 0.8f)]
        [SerializeField] private float _firstBlinkOpenAlpha = 0.5f;

        [Tooltip("Duration for heavy eyelids to snap shut again")]
        [SerializeField] private float _firstBlinkCloseDuration = 0.45f;

        [Tooltip("Pause between first and second eyelid flutter")]
        [SerializeField] private float _blinkPauseDuration = 0.6f;

        [Tooltip("Duration for eyes to open on second attempt")]
        [SerializeField] private float _secondBlinkOpenDuration = 1.1f;

        [Header("Physical Struggle Settings")]
        [Tooltip("Camera height off the ground while lying unconscious")]
        [SerializeField] private float _groundHeight = 0.35f;

        [Tooltip("Natural head roll tilt while cheek rests on dirt (degrees)")]
        [SerializeField] private float _groundRestRoll = 15f;

        [Tooltip("Pitch angle looking slightly up towards horizon while on ground (degrees)")]
        [SerializeField] private float _groundRestPitch = -4f;

        [Tooltip("Camera height when pushed up onto hands and knees")]
        [SerializeField] private float _kneelingHeight = 0.85f;

        [Tooltip("Player attempts to lift head first, but weakness causes head to drop back down with a thud")]
        [SerializeField] private bool _enableFailedHeadLift = true;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _groggyBreathOrGroan;
        [SerializeField] private AudioClip _windAmbience;

        [Header("Developer Settings")]
        [Tooltip("Play intro sequence automatically on scene start")]
        [SerializeField] private bool _playOnStart = true;

        [Tooltip("Allow pressing Space or Escape to skip intro during testing")]
        [SerializeField] private bool _allowSkipInEditor = true;

        private Coroutine _sequenceCoroutine;
        private bool _isSequenceRunning;
        private Sequence _activeTweenSequence;

        public event Action OnSequenceCompleted;

        private void Awake()
        {
            AutoFindReferences();
        }

        private void Start()
        {
            if (_playOnStart)
            {
                StartWakeUpSequence();
            }
            else
            {
                CompleteSequenceImmediately();
            }
        }

        private void Update()
        {
            if (_isSequenceRunning && _allowSkipInEditor)
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    Debug.Log("<color=yellow>[WakeUpSequence]</color> Skipped by player.");
                    SkipSequence();
                }
#else
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    Debug.Log("<color=yellow>[WakeUpSequence]</color> Skipped by player.");
                    SkipSequence();
                }
#endif
            }
        }

        private void AutoFindReferences()
        {
            if (_playerController == null)
            {
                _playerController = FindFirstObjectByType<FirstPersonController>();
            }

            if (_playerController != null)
            {
                if (_playerInteraction == null)
                {
                    _playerInteraction = _playerController.GetComponent<PlayerInteraction>();
                }
                if (_playerInputs == null)
                {
                    _playerInputs = _playerController.GetComponent<StarterAssetsInputs>();
                }
            }

            if (_playerFollowCamera == null)
            {
                GameObject camObj = GameObject.Find("PlayerFollowCamera");
                if (camObj != null)
                {
                    _playerFollowCamera = camObj.GetComponent<CinemachineVirtualCameraBase>();
                }
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }
        }

        /// <summary>
        /// Begins the full cinematic wake-up sequence.
        /// </summary>
        public void StartWakeUpSequence()
        {
            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
            }

            _sequenceCoroutine = StartCoroutine(WakeUpRoutine());
        }

        private IEnumerator WakeUpRoutine()
        {
            _isSequenceRunning = true;

            // Hide Objective UI during the wake-up cinematic cutscene
            V0.UI.ObjectiveManager.SetVisible(false);

            // 1. Lock down player movement and interaction
            SetPlayerControlsActive(false);

            // Determine target standing position (player's eye level) and ground position
            Transform playerT = _playerController != null ? _playerController.transform : transform;
            Vector3 standingPos = playerT.position + Vector3.up * 1.375f;
            Quaternion standingRot = playerT.rotation;

            // Ground pose: cheek resting in dirt, looking down the path towards the house (NOT outside the map)
            Vector3 groundPos = playerT.position + Vector3.up * _groundHeight;
            Quaternion groundRot = Quaternion.Euler(_groundRestPitch, standingRot.eulerAngles.y, _groundRestRoll);

            // 2. Position WakeUp camera and activate
            if (_wakeUpCamera != null)
            {
                _wakeUpCamera.transform.position = groundPos;
                _wakeUpCamera.transform.rotation = groundRot;
                _wakeUpCamera.gameObject.SetActive(true);
                _wakeUpCamera.Priority.Value = 30;
            }

            if (_playerFollowCamera != null)
            {
                _playerFollowCamera.Priority.Value = 10;
            }

            // 3. Pitch black overlay
            if (_blackoutCanvasGroup != null)
            {
                _blackoutCanvasGroup.gameObject.SetActive(true);
                _blackoutCanvasGroup.alpha = 1f;
            }

            // Play wind / creepy ambience
            if (_audioSource != null && _windAmbience != null)
            {
                _audioSource.clip = _windAmbience;
                _audioSource.loop = true;
                _audioSource.Play();
            }

            // Wait in total darkness (unconscious)
            yield return new WaitForSeconds(_initialBlackoutHold);

            // 4. First Eyelid Flutter with DOTween (eyes struggle open to 50%, then snap shut)
            if (_blackoutCanvasGroup != null)
            {
                _activeTweenSequence?.Kill();
                _activeTweenSequence = DOTween.Sequence();

                _activeTweenSequence.Append(_blackoutCanvasGroup.DOFade(_firstBlinkOpenAlpha, _firstBlinkOpenDuration).SetEase(Ease.InOutSine));
                _activeTweenSequence.AppendInterval(0.35f);
                _activeTweenSequence.Append(_blackoutCanvasGroup.DOFade(1f, _firstBlinkCloseDuration).SetEase(Ease.InOutSine));

                yield return _activeTweenSequence.WaitForCompletion();
            }

            // Groggy groan or heavy waking breath
            if (_audioSource != null && _groggyBreathOrGroan != null)
            {
                _audioSource.PlayOneShot(_groggyBreathOrGroan);
            }

            // Subtitle Line 1: First waking thought
            yield return ShowSubtitleAndWait(_line1FirstBlink, 2.5f);

            yield return new WaitForSeconds(_blinkPauseDuration);

            // 5. Second Eyelid Flutter (eyes open wider, blur clears)
            if (_blackoutCanvasGroup != null)
            {
                _activeTweenSequence?.Kill();
                _activeTweenSequence = DOTween.Sequence();

                _activeTweenSequence.Append(_blackoutCanvasGroup.DOFade(0.18f, _secondBlinkOpenDuration).SetEase(Ease.InOutSine));
                // Subtle flutter as eyes adjust to gray daylight
                _activeTweenSequence.Append(_blackoutCanvasGroup.DOFade(0.32f, 0.2f).SetEase(Ease.InOutSine));
                _activeTweenSequence.Append(_blackoutCanvasGroup.DOFade(0.12f, 0.25f).SetEase(Ease.InOutSine));
            }

            yield return new WaitForSeconds(0.5f);

            // 6. Multi-Stage Physical Struggle (Smooth, organic cinematic rise from the ground)
            if (_wakeUpCamera != null)
            {
                Transform camT = _wakeUpCamera.transform;

                // -------------------------------------------------------------
                // STAGE A: Groggy Head Stir / Partial Lift (Smooth InOutSine)
                // -------------------------------------------------------------
                if (_enableFailedHeadLift)
                {
                    Vector3 headLiftPos = groundPos + Vector3.up * 0.15f;
                    Vector3 headLiftRot = new Vector3(_groundRestPitch + 6f, standingRot.eulerAngles.y, _groundRestRoll * 0.4f);

                    // Player gently strains to lift head off dirt
                    camT.DOMove(headLiftPos, 1.1f).SetEase(Ease.InOutSine);
                    camT.DORotate(headLiftRot, 1.1f).SetEase(Ease.InOutSine);
                    yield return new WaitForSeconds(1.15f);

                    // Head gently rests back down into dirt
                    camT.DOMove(groundPos, 0.85f).SetEase(Ease.InOutSine);
                    camT.DORotate(groundRot.eulerAngles, 0.85f).SetEase(Ease.InOutSine);
                    yield return new WaitForSeconds(0.9f);

                    if (_blackoutCanvasGroup != null)
                    {
                        _blackoutCanvasGroup.DOFade(0.4f, 0.3f).SetEase(Ease.InOutSine).OnComplete(() => _blackoutCanvasGroup.DOFade(0.12f, 0.4f).SetEase(Ease.InOutSine));
                    }

                    // Subtitle Line 2: Failed lift confusion
                    ShowSubtitle(_line2FailedLift, 2.2f);
                    yield return new WaitForSeconds(0.8f);
                }

                // -------------------------------------------------------------
                // STAGE B: Smoothly Pushing Up Onto Hands and Knees
                // -------------------------------------------------------------
                Vector3 kneelPos = playerT.position + Vector3.up * _kneelingHeight;
                Vector3 kneelRot = new Vector3(8f, standingRot.eulerAngles.y, 0f);

                // Smooth, steady push upward
                camT.DOMove(kneelPos, 1.8f).SetEase(Ease.InOutSine);
                camT.DORotate(kneelRot, 1.8f).SetEase(Ease.InOutSine);
                yield return new WaitForSeconds(1.85f);

                // Pause on knees: Gentle natural head sway looking at surroundings
                camT.DORotate(new Vector3(6f, standingRot.eulerAngles.y + 4f, 0f), 1.0f).SetEase(Ease.InOutSine);
                yield return new WaitForSeconds(1.05f);
                camT.DORotate(kneelRot, 0.9f).SetEase(Ease.InOutSine);
                yield return new WaitForSeconds(0.95f);

                // -------------------------------------------------------------
                // STAGE C: Smoothly Hauling Up to Feet
                // -------------------------------------------------------------
                // Fluid, uninterrupted rising to full standing height
                camT.DOMove(standingPos, 2.0f).SetEase(Ease.InOutSine);
                camT.DORotate(standingRot.eulerAngles, 2.0f).SetEase(Ease.InOutSine);

                // Smoothly fade away remaining darkness
                if (_blackoutCanvasGroup != null)
                {
                    _blackoutCanvasGroup.DOFade(0f, 1.5f).SetEase(Ease.InOutSine);
                }
                yield return new WaitForSeconds(2.05f);

                // Align player look orientation smoothly before handover
                if (_playerController != null)
                {
                    _playerController.ResetLookOrientation(0f, standingRot.eulerAngles.y);
                }

                // Subtitle Line 3: Spotted the house / need help
                yield return ShowSubtitleAndWait(_line3Standing, 2.5f);
            }

            // 7. Sequence complete: Seamless handoff to gameplay!
            CompleteSequenceImmediately();
        }

        /// <summary>
        /// Instantly concludes the intro and enables full player gameplay controls.
        /// </summary>
        public void CompleteSequenceImmediately()
        {
            _isSequenceRunning = false;

            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }

            _activeTweenSequence?.Kill();

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                CinemachineBrain brain = mainCam.GetComponent<CinemachineBrain>();
                if (brain != null)
                {
                    brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
                }
            }

            if (_wakeUpCamera != null)
            {
                _wakeUpCamera.transform.DOKill();
                _wakeUpCamera.Priority.Value = 0;
                _wakeUpCamera.gameObject.SetActive(false);
            }

            if (_playerFollowCamera != null)
            {
                _playerFollowCamera.Priority.Value = 10;
                _playerFollowCamera.gameObject.SetActive(true);
            }

            // Hide blackout canvas
            if (_blackoutCanvasGroup != null)
            {
                _blackoutCanvasGroup.DOKill();
                _blackoutCanvasGroup.alpha = 0f;
                _blackoutCanvasGroup.gameObject.SetActive(false);
            }

            // Enable player controls
            SetPlayerControlsActive(true);

            V0.UI.ObjectiveManager.SetObjective("Seek Help from the House");
            OnSequenceCompleted?.Invoke();
            Debug.Log("<color=green>[WakeUpSequence]</color> Player is awake and in control!");
        }

        /// <summary>
        /// Shows a subtitle, waits for it to fully appear, display and fade out, then returns.
        /// Guarantees previous subtitle is completely finished before starting the next.
        /// </summary>
        private IEnumerator ShowSubtitleAndWait(string text, float displayDuration)
        {
            if (_subtitleText == null || string.IsNullOrEmpty(text)) yield break;

            _subtitleText.DOKill(true);
            _subtitleText.text = text;
            _subtitleText.color = new Color(_subtitleText.color.r, _subtitleText.color.g, _subtitleText.color.b, 0f);

            const float fadeInDur  = 0.5f;
            const float fadeOutDur = 0.6f;

            // Fade in
            bool fadeInDone = false;
            _subtitleText.DOFade(1f, fadeInDur).SetEase(Ease.OutQuad).OnComplete(() => fadeInDone = true);
            yield return new WaitUntil(() => fadeInDone);

            // Hold
            yield return new WaitForSeconds(displayDuration);

            // Fade out
            bool fadeOutDone = false;
            _subtitleText.DOFade(0f, fadeOutDur).SetEase(Ease.InQuad).OnComplete(() => fadeOutDone = true);
            yield return new WaitUntil(() => fadeOutDone);
        }

        private void ShowSubtitle(string text, float duration)
        {
            if (_subtitleText == null || string.IsNullOrEmpty(text)) return;

            _subtitleText.DOKill();
            _subtitleText.text = text;
            _subtitleText.color = new Color(_subtitleText.color.r, _subtitleText.color.g, _subtitleText.color.b, 0f);

            Sequence subSeq = DOTween.Sequence();
            subSeq.Append(_subtitleText.DOFade(1f, 0.5f).SetEase(Ease.OutQuad));
            subSeq.AppendInterval(duration);
            subSeq.Append(_subtitleText.DOFade(0f, 0.7f).SetEase(Ease.InQuad));
        }

        private void SkipSequence()
        {
            CompleteSequenceImmediately();
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
                if (active)
                {
                    _playerController.ResetLookOrientation();
                }
            }

            if (_playerInteraction != null)
            {
                _playerInteraction.enabled = active;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDestroy()
        {
            _activeTweenSequence?.Kill();
            if (_wakeUpCamera != null)
            {
                _wakeUpCamera.transform.DOKill();
            }
            if (_blackoutCanvasGroup != null)
            {
                _blackoutCanvasGroup.DOKill();
            }
        }
    }
}
