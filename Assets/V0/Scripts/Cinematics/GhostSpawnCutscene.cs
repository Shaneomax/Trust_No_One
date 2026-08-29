using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Triggered when the player steps on GhostTrigger (outside bedroom door after picking up bedroom key).
    /// Focuses camera downstairs on the foggy grand entrance where the ghost awakens and becomes active.
    /// Stranger warns the player not to get caught and teaches stealth mechanics (crouching, avoiding sprinting).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class GhostSpawnCutscene : MonoBehaviour
    {
        [System.Serializable]
        public class DialogueShot
        {
            [Tooltip("Descriptive name for this shot")]
            public string shotName = "Shot";

            [Tooltip("Cinemachine virtual camera for this shot (e.g. Cam_GhostGrandEntrance)")]
            public CinemachineCamera virtualCamera;

            [Tooltip("How long this shot and its subtitle remain active on screen (seconds)")]
            public float duration = 5.0f;

            [Tooltip("Subtitle dialogue text displayed during this shot")]
            [TextArea(2, 4)]
            public string subtitleText = "";

            [Tooltip("Color tint for the subtitle text")]
            public Color textColor = new Color(1f, 0.88f, 0.6f);

            [Tooltip("If true, activates the Ghost GameObject during this shot")]
            public bool activateGhost = false;

            [Tooltip("Shake the camera for horror impact tremor")]
            public bool shakeCamera = false;
        }

        [Header("Cinematic Shots & Dialogue")]
        [Tooltip("List of shots and dialogue lines played in sequence with customizable durations and cameras")]
        [SerializeField] private List<DialogueShot> _shots = new List<DialogueShot>();

        [Header("Ghost Reference")]
        [Tooltip("The Ghost GameObject to set active during the cutscene")]
        [SerializeField] private GameObject _ghostGameObject;

        [Header("Fog System (Grand Entrance)")]
        [Tooltip("ParticleSystem for visible fog surrounding the ghost at the grand entrance")]
        [SerializeField] private ParticleSystem _ghostSpawnFog;

        [Header("Cinemachine Player Camera")]
        [Tooltip("The player's first-person virtual camera (restored when cutscene ends)")]
        [SerializeField] private CinemachineVirtualCameraBase _playerFollowCamera;

        [Header("Audio (Optional)")]
        [Tooltip("Eerie stinger / ghost screech audio played during ghost spawn")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _ghostSpawnStinger;

        [Header("Timing & Blends")]
        [Tooltip("Cinemachine transition blend duration (seconds)")]
        [SerializeField] private float _cameraBlendDuration = 2.5f;

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
        private TrustNoOne.AI.EnemyAI _ghostEnemyAI;
        private Animator _ghostAnimator;

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
                    Debug.Log("<color=yellow>[GhostSpawnCutscene]</color> Skipped by player.");
                    SkipCutscene();
                }
#else
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
                {
                    Debug.Log("<color=yellow>[GhostSpawnCutscene]</color> Skipped by player.");
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
            Debug.Log("<color=cyan>[GhostSpawnCutscene]</color> Starting Ghost Spawn Cutscene at Grand Entrance!");

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
            ResetAllCameraPriorities();

            for (int i = 0; i < _shots.Count; i++)
            {
                DialogueShot shot = _shots[i];
                if (shot == null) continue;

                Debug.Log($"<color=cyan>[GhostSpawnCutscene]</color> Playing shot {i + 1}/{_shots.Count}: '{shot.shotName}' ({shot.duration}s)");

                // Activate this virtual camera
                if (shot.virtualCamera != null)
                {
                    shot.virtualCamera.Priority.Value = 60 + i;
                }

                // If this shot activates the Ghost
                if (shot.activateGhost && _ghostGameObject != null)
                {
                    _ghostGameObject.SetActive(true);

                    // Ensure Ghost is in Idle animation during cutscene
                    if (_ghostEnemyAI == null) _ghostEnemyAI = _ghostGameObject.GetComponent<TrustNoOne.AI.EnemyAI>();
                    if (_ghostEnemyAI != null) _ghostEnemyAI.SetCutsceneMode(true);

                    if (_ghostAnimator == null) _ghostAnimator = _ghostGameObject.GetComponentInChildren<Animator>();
                    if (_ghostAnimator != null) _ghostAnimator.SetFloat("Speed", 0f);

                    // Start visible fog around ghost
                    if (_ghostSpawnFog != null)
                    {
                        _ghostSpawnFog.gameObject.SetActive(true);
                        _ghostSpawnFog.Play();
                    }

                    Debug.Log("<color=red>[GhostSpawnCutscene]</color> Ghost has materialized in the Grand Entrance fog (Idle animation active)!");

                    if (_audioSource != null && _ghostSpawnStinger != null)
                    {
                        _audioSource.clip = _ghostSpawnStinger;
                        _audioSource.Play();
                    }
                }

                // Camera tremor if enabled
                if (shot.shakeCamera && shot.virtualCamera != null)
                {
                    shot.virtualCamera.transform.DOShakePosition(1.2f, strength: new Vector3(0.06f, 0.05f, 0.06f), vibrato: 12);
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

            // Ensure Ghost is active and resume hunting mode
            if (_ghostGameObject != null)
            {
                if (!_ghostGameObject.activeSelf) _ghostGameObject.SetActive(true);

                if (_ghostEnemyAI == null) _ghostEnemyAI = _ghostGameObject.GetComponent<TrustNoOne.AI.EnemyAI>();
                if (_ghostEnemyAI != null) _ghostEnemyAI.SetCutsceneMode(false);
            }

            // Fog naturally stops emitting and dissipates
            if (_ghostSpawnFog != null)
            {
                _ghostSpawnFog.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            // Reset Cinemachine Brain blend to Cut so camera doesn't slowly rotate/pan on its own
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
            Debug.Log("<color=green>[GhostSpawnCutscene]</color> Cutscene completed! Ghost is now hunting. Player in control.");

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

            if (_ghostGameObject == null)
            {
                _ghostGameObject = GameObject.Find("Ghost");
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
