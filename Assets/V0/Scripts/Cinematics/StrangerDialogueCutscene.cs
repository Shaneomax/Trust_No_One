using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using DG.Tweening;
using StarterAssets;
using V0.Interaction;
using V0.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace V0.Cinematics
{
    /// <summary>
    /// Triggered when the player reaches SecondTrigger in front of the chained room.
    /// Supports a fully customizable list of cinematic shots and dialogue lines (just like Entry Trigger),
    /// complete with camera switching (e.g. Cam_ChainedRoom, DoorShut_Cam), custom durations,
    /// subtitle text, and the door slam & lock event.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class StrangerDialogueCutscene : MonoBehaviour
    {
        [System.Serializable]
        public class DialogueShot
        {
            [Tooltip("Descriptive name for this shot")]
            public string shotName = "Shot";

            [Tooltip("Cinemachine virtual camera for this shot (e.g. Cam_ChainedRoom, DoorShut_Cam)")]
            public CinemachineCamera virtualCamera;

            [Tooltip("How long this shot and its subtitle remain active on screen (seconds)")]
            public float duration = 5.0f;

            [Tooltip("Subtitle dialogue text displayed during this shot")]
            [TextArea(2, 4)]
            public string subtitleText = "";

            [Tooltip("Color tint for the subtitle text")]
            public Color textColor = new Color(1f, 0.88f, 0.6f);

            [Tooltip("If true, triggers the front door auto-slam and lock during this shot")]
            public bool triggerDoorSlam = false;

            [Tooltip("Shake the camera for impact tremor")]
            public bool shakeCamera = false;
        }

        [Header("Cinematic Shots & Dialogue")]
        [Tooltip("List of shots and dialogue lines played in sequence with customizable durations and cameras")]
        [SerializeField] private List<DialogueShot> _shots = new List<DialogueShot>();

        [Header("Main Front Door Reference")]
        [Tooltip("The main entrance door that slams shut and locks")]
        [SerializeField] private DoorInteractable _mainFrontDoor;

        [Header("Cinemachine Player Camera")]
        [Tooltip("The player's first-person virtual camera (restored when cutscene ends)")]
        [SerializeField] private CinemachineVirtualCameraBase _playerFollowCamera;

        [Header("Audio (Optional)")]
        [Tooltip("Loud heavy slam audio played when the front door shuts")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _doorSlamAudio;

        [Header("Timing & Blends")]
        [Tooltip("Cinemachine transition blend duration (seconds)")]
        [SerializeField] private float _cameraBlendDuration = 2.0f;

        [Header("Cinematic UI References")]
        [SerializeField] private CanvasGroup _letterboxCanvasGroup;
        [SerializeField] private Text _subtitleText;

        [Header("Player Control References")]
        [SerializeField] private FirstPersonController _playerController;
        [SerializeField] private PlayerInteraction _playerInteraction;
        [SerializeField] private StarterAssetsInputs _playerInputs;

        [Header("Settings")]
        [Tooltip("Allow pressing Space or Escape to skip")]
        [SerializeField] private bool _allowSkip = true;

        [Tooltip("Trigger only once")]
        [SerializeField] private bool _playOnce = true;

        private bool _hasTriggered = false;
        private bool _isPlaying = false;
        private Coroutine _cutsceneCoroutine;
        private CinemachineBlendDefinition _originalBlend;
        private CinemachineBrain _cachedBrain;

        /// <summary>
        /// True once the SecondTrigger cutscene (stranger introduction) has fired.
        /// Used by other scripts (e.g. DoorInteractable) to gate dialogue.
        /// </summary>
        public static bool HasMet { get; private set; } = false;

        public event Action OnCutsceneStarted;
        public event Action OnCutsceneCompleted;

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
            if (_hasTriggered && _playOnce) return;

            if (other.CompareTag("Player") || other.GetComponent<FirstPersonController>() != null || other.GetComponentInParent<FirstPersonController>() != null)
            {
                StartCutscene();
            }
        }

        private void Update()
        {
            if (_isPlaying && _allowSkip)
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame))
                {
                    Debug.Log("<color=yellow>[StrangerDialogueCutscene]</color> Skipped by player.");
                    SkipCutscene();
                }
#else
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
                {
                    Debug.Log("<color=yellow>[StrangerDialogueCutscene]</color> Skipped by player.");
                    SkipCutscene();
                }
#endif
            }
        }

        public void StartCutscene()
        {
            if (_isPlaying) return;
            _hasTriggered = true;
            _isPlaying = true;
            HasMet = true;  // signal to the rest of the game that player has met the stranger

            OnCutsceneStarted?.Invoke();
            DoorBangingAudio.Instance?.StopBanging();
            Debug.Log("<color=cyan>[StrangerDialogueCutscene]</color> Starting Stranger Dialogue Cutscene!");

            // Configure Cinemachine Brain blend
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                _cachedBrain = mainCam.GetComponent<CinemachineBrain>();
                if (_cachedBrain != null)
                {
                    _originalBlend = _cachedBrain.DefaultBlend;
                    _cachedBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, _cameraBlendDuration);
                }
            }

            // Freeze player movement & camera
            SetPlayerControlsActive(false);

            // Animate letterbox black bars in
            ShowLetterbox(true);

            // Run cutscene sequence
            if (_cutsceneCoroutine != null) StopCoroutine(_cutsceneCoroutine);
            _cutsceneCoroutine = StartCoroutine(CutsceneRoutine());
        }

        private IEnumerator CutsceneRoutine()
        {
            // Reset all virtual camera priorities
            ResetAllCameraPriorities();

            for (int i = 0; i < _shots.Count; i++)
            {
                DialogueShot shot = _shots[i];
                if (shot == null) continue;

                Debug.Log($"<color=cyan>[StrangerDialogueCutscene]</color> Playing shot {i + 1}/{_shots.Count}: '{shot.shotName}' ({shot.duration}s)");

                // Activate this virtual camera
                if (shot.virtualCamera != null)
                {
                    shot.virtualCamera.Priority.Value = 50 + i;
                }

                // If this shot triggers the door slam event
                if (shot.triggerDoorSlam)
                {
                    Debug.Log("<color=red>[StrangerDialogueCutscene]</color> Door Slam Triggered!");
                    if (_audioSource != null && _doorSlamAudio != null)
                    {
                        _audioSource.clip = _doorSlamAudio;
                        _audioSource.Play();
                    }

                    if (_mainFrontDoor != null)
                    {
                        _mainFrontDoor.ForceSlamAndLock(_doorSlamAudio);
                    }
                }

                // Camera tremor if enabled
                if (shot.shakeCamera && shot.virtualCamera != null)
                {
                    shot.virtualCamera.transform.DOShakePosition(1.0f, strength: new Vector3(0.06f, 0.05f, 0.06f), vibrato: 14);
                }

                // Display subtitle text
                if (_subtitleText != null)
                {
                    _subtitleText.DOKill();
                    if (!string.IsNullOrEmpty(shot.subtitleText))
                    {
                        _subtitleText.text = shot.subtitleText;
                        _subtitleText.color = new Color(shot.textColor.r, shot.textColor.g, shot.textColor.b, 0f);
                        _subtitleText.DOFade(1f, 0.6f).SetDelay(0.2f);
                    }
                    else
                    {
                        _subtitleText.DOFade(0f, 0.3f);
                    }
                }

                // Wait for the full specified duration
                yield return new WaitForSeconds(Mathf.Max(shot.duration, 1.0f));

                // Fade out text before next shot
                if (_subtitleText != null && !string.IsNullOrEmpty(shot.subtitleText))
                {
                    _subtitleText.DOFade(0f, 0.4f);
                }
            }

            // Cutscene sequence complete
            EndCutscene();
        }

        public void EndCutscene()
        {
            if (!_isPlaying) return;
            _isPlaying = false;

            if (_cutsceneCoroutine != null)
            {
                StopCoroutine(_cutsceneCoroutine);
                _cutsceneCoroutine = null;
            }

            // Ensure front door is locked even if skipped early
            if (_mainFrontDoor != null && !_mainFrontDoor.IsLocked)
            {
                _mainFrontDoor.ForceSlamAndLock();
            }

            // Fade out subtitles and letterbox immediately
            if (_subtitleText != null)
            {
                _subtitleText.DOKill();
                _subtitleText.DOFade(0f, 0.3f);
            }
            ShowLetterbox(false);

            // Subtle cinematic DOTween fade transition back to player view
            FadeScreen.Instance.FadeOutAndIn(0.35f, 0.08f, 0.45f, () =>
            {
                // Reset Cinemachine Brain blend to Cut
                if (_cachedBrain != null)
                {
                    _cachedBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
                }

                // Reset camera priorities so player camera immediately takes back control
                ResetAllCameraPriorities();

                if (_playerFollowCamera != null)
                {
                    _playerFollowCamera.Priority.Value = 10;
                }

                // Restore player controls cleanly
                SetPlayerControlsActive(true);
            });

            OnCutsceneCompleted?.Invoke();
            ObjectiveManager.SetObjective("Search for chainsaw to break the chain");
            Debug.Log("<color=green>[StrangerDialogueCutscene]</color> Cutscene finished! Front door is locked. Player in control.");

            if (_playOnce)
            {
                Collider col = GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
        }

        public void SkipCutscene()
        {
            EndCutscene();
        }

        private void ResetAllCameraPriorities()
        {
            foreach (DialogueShot shot in _shots)
            {
                if (shot != null && shot.virtualCamera != null)
                {
                    shot.virtualCamera.Priority.Value = 0;
                }
            }
        }

        private void ShowLetterbox(bool show)
        {
            if (_letterboxCanvasGroup != null)
            {
                _letterboxCanvasGroup.DOKill();
                _letterboxCanvasGroup.DOFade(show ? 1f : 0f, 0.6f).SetEase(Ease.InOutSine);
            }
        }

        private void SetPlayerControlsActive(bool active)
        {
            if (_playerController == null || _playerInputs == null)
            {
                AutoFindReferences();
            }

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

            if (_playerInteraction != null) _playerInteraction.enabled = active;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void AutoFindReferences()
        {
            if (_playerController == null)
            {
                _playerController = FindFirstObjectByType<FirstPersonController>();
            }

            if (_playerInteraction == null && _playerController != null)
            {
                _playerInteraction = _playerController.GetComponent<PlayerInteraction>();
            }

            if (_playerInputs == null && _playerController != null)
            {
                _playerInputs = _playerController.GetComponent<StarterAssetsInputs>();
            }

            if (_playerFollowCamera == null)
            {
                GameObject followCamObj = GameObject.Find("PlayerFollowCamera");
                if (followCamObj != null)
                {
                    _playerFollowCamera = followCamObj.GetComponent<CinemachineVirtualCameraBase>();
                }
            }

            if (_mainFrontDoor == null)
            {
                GameObject frontDoorObj = GameObject.Find("SM_Door_Front_01");
                if (frontDoorObj != null)
                {
                    _mainFrontDoor = frontDoorObj.GetComponent<DoorInteractable>();
                    if (_mainFrontDoor == null) _mainFrontDoor = frontDoorObj.GetComponentInParent<DoorInteractable>();
                }
            }

            if (_letterboxCanvasGroup == null)
            {
                GameObject lbObj = GameObject.Find("CinematicLetterboxCanvas");
                if (lbObj != null)
                {
                    _letterboxCanvasGroup = lbObj.GetComponent<CanvasGroup>();
                    _subtitleText = lbObj.GetComponentInChildren<Text>();
                }
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }
        }

        private void OnDestroy()
        {
            if (_letterboxCanvasGroup != null) _letterboxCanvasGroup.DOKill();
            if (_subtitleText != null) _subtitleText.DOKill();
        }
    }
}
