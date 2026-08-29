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
    /// Trigger-activated cinematic cutscene that showcases establishing shots of the house and map.
    /// Supports Cinemachine Virtual Camera priority blending, dynamic camera dollies,
    /// cinematic 21:9 letterbox bars, atmospheric subtitle cards, and skip functionality.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HouseFlyoverCutscene : MonoBehaviour
    {
        [System.Serializable]
        public class CinematicShot
        {
            [Tooltip("Descriptive name for this shot (e.g. 'House Approach', 'Barn Overview', 'Upper Window')")]
            public string shotName = "Shot";

            [Tooltip("The Cinemachine virtual camera for this shot")]
            public CinemachineCamera virtualCamera;

            [Tooltip("How long this shot remains active (seconds)")]
            public float duration = 4.0f;

            [Tooltip("Optional subtitle text displayed during this shot")]
            [TextArea(1, 3)]
            public string subtitleText = "";
        }

        [Header("Cinematic Shots")]
        [Tooltip("List of camera shots played in sequence")]
        [SerializeField] private List<CinematicShot> _shots = new List<CinematicShot>();

        [Header("Cinemachine Player Camera")]
        [Tooltip("The player's first-person virtual camera (restored when cutscene ends)")]
        [SerializeField] private CinemachineVirtualCameraBase _playerFollowCamera;

        [Header("Player Control References")]
        [SerializeField] private FirstPersonController _playerController;
        [SerializeField] private PlayerInteraction _playerInteraction;
        [SerializeField] private StarterAssetsInputs _playerInputs;

        [Header("Cinematic UI / Letterbox")]
        [Tooltip("CanvasGroup controlling the top and bottom cinematic black bars")]
        [SerializeField] private CanvasGroup _letterboxCanvasGroup;

        [Tooltip("Text component for displaying cinematic subtitles / location cards")]
        [SerializeField] private Text _subtitleText;

        [Header("Audio (Optional)")]
        [Tooltip("Atmospheric drone / music stinger played during cutscene")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _cutsceneMusicOrAmbience;

        [Header("Cinematic Blends & Timing")]
        [Tooltip("Smooth blend duration between camera angles (seconds)")]
        [SerializeField] private float _cameraBlendDuration = 2.5f;

        [Tooltip("Blend style for transitions (EaseInOut is cinematic and smooth)")]
        [SerializeField] private CinemachineBlendDefinition.Styles _blendStyle = CinemachineBlendDefinition.Styles.EaseInOut;

        [Tooltip("Allow pressing Space, Escape, or E to skip cutscene")]
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
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void Awake()
        {
            AutoFindReferences();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasTriggered && _playOnce) return;

            // Check if player entered the trigger
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
                    Debug.Log("<color=yellow>[HouseFlyover]</color> Cutscene skipped by player.");
                    SkipCutscene();
                }
#else
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
                {
                    Debug.Log("<color=yellow>[HouseFlyover]</color> Cutscene skipped by player.");
                    SkipCutscene();
                }
#endif
            }
        }

        /// <summary>
        /// Starts the cinematic flyover cutscene sequence.
        /// </summary>
        public void StartCutscene()
        {
            if (_isPlaying) return;
            _hasTriggered = true;
            _isPlaying = true;

            OnCutsceneStarted?.Invoke();
            Debug.Log("<color=cyan>[HouseFlyover]</color> Starting House & Map Cinematic Cutscene!");

            // 1. Configure CinemachineBrain for slow, majestic cinematic blending
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                _cachedBrain = mainCam.GetComponent<CinemachineBrain>();
                if (_cachedBrain != null)
                {
                    _originalBlend = _cachedBrain.DefaultBlend;
                    _cachedBrain.DefaultBlend = new CinemachineBlendDefinition(_blendStyle, _cameraBlendDuration);
                }
            }

            // 2. Freeze player controls
            SetPlayerControlsActive(false);

            // 3. Animate Letterbox Bars In smoothly
            ShowLetterbox(true);

            // 4. Play cutscene music / ambience
            if (_audioSource != null && _cutsceneMusicOrAmbience != null)
            {
                _audioSource.clip = _cutsceneMusicOrAmbience;
                _audioSource.Play();
            }

            // 5. Run shot sequence coroutine
            if (_cutsceneCoroutine != null) StopCoroutine(_cutsceneCoroutine);
            _cutsceneCoroutine = StartCoroutine(PlayShotsRoutine());
        }

        private IEnumerator PlayShotsRoutine()
        {
            // Reset all virtual camera priorities
            ResetAllCameraPriorities();

            // Play each shot in order
            for (int i = 0; i < _shots.Count; i++)
            {
                CinematicShot shot = _shots[i];
                if (shot == null) continue;

                Debug.Log($"<color=cyan>[HouseFlyover]</color> Playing shot {i + 1}/{_shots.Count}: '{shot.shotName}' ({shot.duration}s)");

                // Activate this virtual camera with highest priority
                if (shot.virtualCamera != null)
                {
                    shot.virtualCamera.Priority.Value = 30 + i;
                }

                // Smoothly display subtitle text
                if (_subtitleText != null)
                {
                    _subtitleText.DOKill();
                    if (!string.IsNullOrEmpty(shot.subtitleText))
                    {
                        _subtitleText.text = shot.subtitleText;
                        _subtitleText.color = new Color(_subtitleText.color.r, _subtitleText.color.g, _subtitleText.color.b, 0f);
                        _subtitleText.DOFade(1f, 0.8f).SetDelay(0.3f);
                    }
                    else
                    {
                        _subtitleText.DOFade(0f, 0.4f);
                    }
                }

                // Wait for the shot duration (allowing slow cinematic camera glide)
                yield return new WaitForSeconds(Mathf.Max(shot.duration, _cameraBlendDuration + 1.0f));

                // Fade out current subtitle before next shot
                if (_subtitleText != null && !string.IsNullOrEmpty(shot.subtitleText))
                {
                    _subtitleText.DOFade(0f, 0.5f);
                }
            }

            // Sequence complete -> restore to player camera
            EndCutscene();
        }

        /// <summary>
        /// Restores player control and returns camera back to first person.
        /// </summary>
        public void EndCutscene()
        {
            if (!_isPlaying) return;
            _isPlaying = false;

            if (_cutsceneCoroutine != null)
            {
                StopCoroutine(_cutsceneCoroutine);
                _cutsceneCoroutine = null;
            }

            // Fade out subtitles and letterbox bars immediately
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

                // Reset camera priorities so PlayerFollowCamera takes over
                ResetAllCameraPriorities();
                if (_playerFollowCamera != null)
                {
                    _playerFollowCamera.Priority.Value = 10;
                }

                // Re-enable player movement & interaction cleanly
                SetPlayerControlsActive(true);
            });

            OnCutsceneCompleted?.Invoke();
            Debug.Log("<color=green>[HouseFlyover]</color> Cinematic Cutscene completed! Player control restored.");

            // Disable trigger collider if play once
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
            foreach (CinematicShot shot in _shots)
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

            if (_playerInteraction != null)
            {
                _playerInteraction.enabled = active;
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
                }
            }
        }

        private void OnDestroy()
        {
            if (_letterboxCanvasGroup != null) _letterboxCanvasGroup.DOKill();
            if (_subtitleText != null) _subtitleText.DOKill();
        }
    }
}
