using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
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
    /// Triggered when the player reaches the front porch (FirstTrigger).
    /// Focuses a Cinemachine camera on the chained room door, plays the trapped man's
    /// desperate screams for help with door impact shaking, displays dialogue subtitles,
    /// and smoothly returns control to the player.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ChainedRoomCutscene : MonoBehaviour
    {
        [Header("Cinemachine Cameras")]
        [Tooltip("The virtual camera positioned in front of the chained room door")]
        [SerializeField] private CinemachineCamera _chainedRoomCamera;

        [Tooltip("The player's first-person virtual camera (restored when cutscene ends)")]
        [SerializeField] private CinemachineVirtualCameraBase _playerFollowCamera;

        [Header("Dialogue Subtitles (Preset in Inspector)")]
        [Tooltip("Line 1: The trapped man screaming from behind the chained door")]
        [TextArea(1, 3)]
        [SerializeField] private string _trappedManDialogue = "[Muffled Voice]: \"PLEASE! SOMEBODY HELP ME! I'M LOCKED IN HERE!\"";

        [Tooltip("Line 2: Player's reaction / thought")]
        [TextArea(1, 3)]
        [SerializeField] private string _playerReactionDialogue = "[Player]: \"Someone's trapped in that room... I need to check it.\"";

        [Header("Timing & Blends")]
        [Tooltip("Duration to hold camera on chained room (seconds)")]
        [SerializeField] private float _shotDuration = 6.5f;

        [Tooltip("Cinemachine transition blend duration (seconds)")]
        [SerializeField] private float _cameraBlendDuration = 2.5f;

        [Header("Audio (Optional)")]
        [Tooltip("Audio clip for the scream and door banging")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _screamingBangingAudio;

        [Header("Cinematic UI References")]
        [SerializeField] private CanvasGroup _letterboxCanvasGroup;
        [SerializeField] private Text _subtitleText;

        [Header("Player Control References")]
        [SerializeField] private FirstPersonController _playerController;
        [SerializeField] private PlayerInteraction _playerInteraction;
        [SerializeField] private StarterAssetsInputs _playerInputs;

        [Header("Settings")]
        [Tooltip("Allow pressing Space, Escape, or E to skip")]
        [SerializeField] private bool _allowSkip = true;

        [Tooltip("Trigger only once")]
        [SerializeField] private bool _playOnce = true;

        private bool _hasTriggered = false;
        private bool _isPlaying = false;
        private Coroutine _cutsceneCoroutine;
        private CinemachineBlendDefinition _originalBlend;
        private CinemachineBrain _cachedBrain;

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
                if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
                {
                    Debug.Log("<color=yellow>[ChainedRoomCutscene]</color> Skipped by player.");
                    SkipCutscene();
                }
#else
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
                {
                    Debug.Log("<color=yellow>[ChainedRoomCutscene]</color> Skipped by player.");
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

            OnCutsceneStarted?.Invoke();
            Debug.Log("<color=cyan>[ChainedRoomCutscene]</color> Starting Chained Room Cutscene!");

            // Configure Cinemachine Brain blend for slow, cinematic glide
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
            // Activate Chained Room camera with high priority (100) so Cinemachine smoothly transitions
            if (_chainedRoomCamera != null)
            {
                _chainedRoomCamera.Priority.Value = 100;
            }

            // Play scream / door impact audio
            if (_audioSource != null && _screamingBangingAudio != null)
            {
                _audioSource.clip = _screamingBangingAudio;
                _audioSource.Play();
            }

            // Subtitle Line 1: The Trapped Man Screaming
            if (_subtitleText != null)
            {
                _subtitleText.DOKill();
                _subtitleText.text = _trappedManDialogue;
                _subtitleText.color = new Color(1f, 0.4f, 0.4f, 0f); // Tense red-white tint
                _subtitleText.DOFade(1f, 0.6f).SetDelay(0.4f);
            }

            // Door banging camera tremor
            if (_chainedRoomCamera != null)
            {
                _chainedRoomCamera.transform.DOShakePosition(1.5f, strength: new Vector3(0.04f, 0.03f, 0.04f), vibrato: 10).SetDelay(0.6f);
            }

            yield return new WaitForSeconds(3.5f);

            // Subtitle Line 2: Player's Thought / Reaction
            if (_subtitleText != null)
            {
                _subtitleText.DOKill();
                _subtitleText.text = _playerReactionDialogue;
                _subtitleText.color = new Color(0.9f, 0.9f, 0.85f, 0f); // Soft white tint
                _subtitleText.DOFade(1f, 0.5f);
            }

            yield return new WaitForSeconds(3.2f);

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

            // Reset camera priorities so player camera smoothly takes back control
            if (_chainedRoomCamera != null)
            {
                _chainedRoomCamera.Priority.Value = 0;
            }

            if (_playerFollowCamera != null)
            {
                _playerFollowCamera.Priority.Value = 10;
            }

            // Fade out subtitles and letterbox
            if (_subtitleText != null)
            {
                _subtitleText.DOKill();
                _subtitleText.DOFade(0f, 0.6f);
            }
            ShowLetterbox(false);

            // Restore player controls
            SetPlayerControlsActive(true);

            OnCutsceneCompleted?.Invoke();
            Debug.Log("<color=green>[ChainedRoomCutscene]</color> Cutscene finished! Player in control.");

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

            if (_playerController != null) _playerController.enabled = active;
            if (_playerInteraction != null) _playerInteraction.enabled = active;
            if (_playerInputs != null)
            {
                _playerInputs.cursorLocked = true;
                _playerInputs.cursorInputForLook = active;
                _playerInputs.move = Vector2.zero;
                _playerInputs.sprint = false;
            }

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
