using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using StarterAssets;
using TrustNoOne.AI;
using V0.UI;
using V0.Interaction;

namespace V0.Cinematics
{
    /// <summary>
    /// Attached to the LastTrigger GameObject at the exit door/porch.
    /// Cutscene Sequence (The Backstab Betrayal):
    /// 1. Player triggers LastTrigger -> Player controls freeze, letterbox bars fade in.
    /// 2. Violent stab from behind -> Camera shakes, player groans, and camera collapses down onto the wooden floor tilted.
    /// 3. Stranger steps into view over the fallen player with the knife in hand.
    /// 4. Subtitle: "[Stranger]: \"You are not leaving.\""
    /// 5. Screen slowly fades out to pitch black.
    /// 6. Loads the ending scene.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LastTriggerCutscene : MonoBehaviour
    {
        [Header("Stranger (Enemy 2) Reference")]
        [SerializeField] private DeceiverAI _stranger;

        [Header("Stab & Fall Physics")]
        [Tooltip("Stage 1 stumble duration (knees buckle, seconds)")]
        [SerializeField] private float _stumbleDuration = 1.0f;

        [Tooltip("Stage 2 full collapse duration onto the floor (seconds)")]
        [SerializeField] private float _fallDuration = 2.0f;

        [Tooltip("Sideways tilt angle of the camera lying on the floor")]
        [SerializeField] private float _fallTiltAngle = 65.0f;

        [Tooltip("Floor height of camera relative to player base when collapsed")]
        [SerializeField] private float _collapsedCameraY = 0.20f;

        [Header("Audio")]
        [Tooltip("Drop dead / body collapse sound played when player falls to the floor (Auto-finds DropDeadSound.mp3)")]
        [SerializeField] private AudioClip _dropDeadSound;
        [SerializeField] private AudioClip _stabSound;
        [SerializeField] private AudioClip _groanSound;
        [SerializeField] private AudioSource _audioSource;

        [Header("Dialogue Settings")]
        [TextArea(1, 3)]
        [SerializeField] private string _dialogueText = "[Stranger]: \"You are not leaving.\"";
        [SerializeField] private Color _dialogueColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private float _dialogueDuration = 4.5f;

        [Header("Scene Transition")]
        [Tooltip("Name of the scene to load after the cutscene fades out")]
        [SerializeField] private string _endingSceneName = "GoodEnding";

        [Tooltip("Slow gradual fade duration to pitch black")]
        [SerializeField] private float _slowFadeDuration = 5.0f;

        [Header("GameObject(s) To SetActive(true)")]
        [Tooltip("Drag & drop any GameObject to activate when cutscene triggers/ends")]
        [SerializeField] private GameObject _objectToActivate;
        [SerializeField] private List<GameObject> _additionalObjectsToActivate = new List<GameObject>();

        [Header("Cinematic UI References")]
        [SerializeField] private CanvasGroup _letterboxCanvasGroup;
        [SerializeField] private Text _subtitleText;

        [Header("Player References")]
        [SerializeField] private FirstPersonController _playerController;
        [SerializeField] private StarterAssetsInputs _playerInputs;

        [Header("Settings")]
        [Tooltip("Allow pressing Space or Escape to skip")]
        [SerializeField] private bool _allowSkip = true;

        [Tooltip("Trigger only once")]
        [SerializeField] private bool _playOnce = true;

        private bool _hasTriggered = false;
        private bool _isPlaying = false;
        private Coroutine _cutsceneCoroutine;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
            }

            AutoWireReferences();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasTriggered && _playOnce) return;
            if (_isPlaying) return;

            if (other.CompareTag("Player") || other.GetComponent<FirstPersonController>() != null || other.GetComponentInParent<FirstPersonController>() != null)
            {
                _hasTriggered = true;
                _cutsceneCoroutine = StartCoroutine(PlayBetrayalRoutine());
            }
        }

        private void Update()
        {
            if (_isPlaying && _allowSkip)
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Keyboard.current != null && (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame || UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame))
                {
                    Debug.Log("<color=yellow>[LastTriggerCutscene]</color> Skipped by player.");
                    SkipCutscene();
                }
#else
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
                {
                    Debug.Log("<color=yellow>[LastTriggerCutscene]</color> Skipped by player.");
                    SkipCutscene();
                }
#endif
            }
        }

        public void SkipCutscene()
        {
            if (!_isPlaying) return;
            _isPlaying = false;

            if (_cutsceneCoroutine != null)
            {
                StopCoroutine(_cutsceneCoroutine);
            }

            if (_letterboxCanvasGroup != null)
            {
                _letterboxCanvasGroup.DOKill();
                _letterboxCanvasGroup.alpha = 0f;
            }

            if (_subtitleText != null)
            {
                _subtitleText.DOKill();
                _subtitleText.text = "";
            }

            TriggerObjectActivation();

            if (FadeScreen.Instance != null)
            {
                FadeScreen.Instance.FadeToBlack(0.5f, () =>
                {
                    SceneManager.LoadScene(_endingSceneName);
                });
            }
            else
            {
                SceneManager.LoadScene(_endingSceneName);
            }
        }

        private IEnumerator PlayBetrayalRoutine()
        {
            _isPlaying = true;
            Debug.Log("<color=red><b>[LastTriggerCutscene]</b> Starting Backstab Betrayal Cutscene...</color>");

            AutoWireReferences();

            // 1. Lock Player Movement & Flashlight
            SetPlayerControlsActive(false);

            // 2. Fade in Letterbox Bars
            if (_letterboxCanvasGroup != null)
            {
                _letterboxCanvasGroup.DOKill();
                _letterboxCanvasGroup.DOFade(1f, 0.6f).SetEase(Ease.InOutSine);
            }

            // 3. Stab Impact & Shock Jolt
            if (_stabSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_stabSound);
            }

            // Play Drop Dead sound as player is struck and collapses!
            if (_dropDeadSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_dropDeadSound, 1.0f);
            }

            GameObject camTarget = _playerController != null ? _playerController.CinemachineCameraTarget : null;

            if (camTarget != null)
            {
                camTarget.transform.DOKill();
                camTarget.transform.DOShakePosition(0.4f, new Vector3(0.2f, 0.2f, 0.35f), 25, 90, false, true);
                camTarget.transform.DOShakeRotation(0.4f, new Vector3(18f, 12f, 15f), 25);
            }

            yield return new WaitForSeconds(0.3f);

            if (_groanSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_groanSound);
            }

            // 4. Stage 1: Stumble / Knees Buckle
            if (camTarget != null)
            {
                camTarget.transform.DOLocalMoveY(0.85f, _stumbleDuration).SetEase(Ease.OutQuad);
                camTarget.transform.DOLocalRotate(new Vector3(25f, 0f, 18f), _stumbleDuration).SetEase(Ease.InOutSine);
            }

            // Stranger begins stepping up towards the player
            if (_stranger != null && _playerController != null)
            {
                _stranger.ApproachPlayer(_playerController.transform, 2.8f);
            }

            yield return new WaitForSeconds(_stumbleDuration);

            // 5. Stage 2: Heavy Physical Collapse Down Onto the Floor Planks
            if (camTarget != null)
            {
                camTarget.transform.DOLocalMoveY(_collapsedCameraY, _fallDuration).SetEase(Ease.InCubic);
                camTarget.transform.DOLocalRotate(new Vector3(10f, 0f, _fallTiltAngle), _fallDuration).SetEase(Ease.OutBounce);
            }

            yield return new WaitForSeconds(_fallDuration + 0.3f);

            // 6. Subtitle: "[Stranger]: \"You are not leaving.\""
            if (_subtitleText != null)
            {
                _subtitleText.DOKill();
                _subtitleText.text = _dialogueText;
                _subtitleText.color = _dialogueColor;
                _subtitleText.DOFade(1f, 0.5f);
            }

            yield return new WaitForSeconds(1.0f);

            // 7. Slow, Atmospheric Fade Out to Pitch Black
            bool fadeDone = false;
            FadeScreen.Instance.FadeToBlack(_slowFadeDuration, () => fadeDone = true);

            // Hold dialogue while screen slowly fades to pitch black
            yield return new WaitForSeconds(_dialogueDuration);

            if (_subtitleText != null)
            {
                _subtitleText.DOFade(0f, 1.5f);
            }

            yield return new WaitUntil(() => fadeDone);
            yield return new WaitForSeconds(0.6f);

            // Activate any optional linked GameObjects
            TriggerObjectActivation();

            // 8. Load Ending Scene
            Debug.Log($"<color=green>[LastTriggerCutscene]</color> Setting LastTrigger Ending and Loading Scene: '{_endingSceneName}'");
            EndingManager.CurrentEnding = EndingType.LastTrigger;
            SceneManager.LoadScene(_endingSceneName);
        }

        private void TriggerObjectActivation()
        {
            if (_objectToActivate != null)
            {
                _objectToActivate.SetActive(true);
                Debug.Log($"<color=green><b>[LastTriggerCutscene]</b></color> Activated GameObject: <b>{_objectToActivate.name}</b>");
            }

            if (_additionalObjectsToActivate != null)
            {
                foreach (GameObject go in _additionalObjectsToActivate)
                {
                    if (go != null)
                    {
                        go.SetActive(true);
                        Debug.Log($"<color=green><b>[LastTriggerCutscene]</b></color> Activated GameObject: <b>{go.name}</b>");
                    }
                }
            }
        }

        private void SetPlayerControlsActive(bool active)
        {
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
                if (!active)
                {
                    CharacterController cc = _playerController.GetComponent<CharacterController>();
                    if (cc != null) cc.Move(Vector3.zero);
                }
            }
        }

        public void AutoWireReferences()
        {
            if (_stranger == null) _stranger = Object.FindFirstObjectByType<DeceiverAI>();
            if (_playerController == null) _playerController = Object.FindFirstObjectByType<FirstPersonController>();
            if (_playerController == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("PlayerCapsule");
                if (playerObj != null) _playerController = playerObj.GetComponentInChildren<FirstPersonController>();
            }

            if (_playerInputs == null && _playerController != null) _playerInputs = _playerController.GetComponent<StarterAssetsInputs>();

            if (_letterboxCanvasGroup == null)
            {
                GameObject canvasObj = GameObject.Find("CinematicLetterboxCanvas");
                if (canvasObj != null)
                {
                    _letterboxCanvasGroup = canvasObj.GetComponent<CanvasGroup>();
                    if (_subtitleText == null) _subtitleText = canvasObj.GetComponentInChildren<Text>();
                }
            }

            if (_dropDeadSound == null)
            {
                #if UNITY_EDITOR
                _dropDeadSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/V0/Audio/DropDeadSound.mp3");
                #endif
                if (_dropDeadSound == null)
                {
                    AudioClip[] allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
                    foreach (var c in allClips)
                    {
                        if (c.name.ToLower().Contains("dropdead") || c.name.ToLower().Contains("drop_dead"))
                        {
                            _dropDeadSound = c;
                            break;
                        }
                    }
                }
            }
        }
    }
}
