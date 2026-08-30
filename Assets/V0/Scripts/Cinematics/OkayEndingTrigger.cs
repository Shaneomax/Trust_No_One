using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using DG.Tweening;
using StarterAssets;
using TrustNoOne.AI;
using V0.UI;
using V0.Interaction;

namespace V0.Cinematics
{
    /// <summary>
    /// Attached to the OkayEnding trigger volume on the road/pathway.
    /// Cutscene Sequence (Second Ending):
    /// 1. Player steps into OkayEnding trigger -> Player is frozen, letterbox bars fade in.
    /// 2. Stranger angrily shouts: "STOP!"
    /// 3. Stranger aggressively hunts and approaches the player.
    /// 4. Player's view tracks the approaching Stranger.
    /// 5. When Stranger reaches the player, he lets out an Evil Laugh.
    /// 6. Screen fades to black and loads the GoodEnding scene.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class OkayEndingTrigger : MonoBehaviour
    {
        [Header("Stranger (Enemy 2) Reference")]
        [SerializeField] private DeceiverAI _stranger;

        [Header("Scene Transition")]
        [Tooltip("Name of the scene to load after cutscene finishes")]
        [SerializeField] private string _endingSceneName = "GoodEnding";
        [SerializeField] private float _fadeDuration = 1.8f;

        [Header("Dialogue & Audio")]
        [TextArea(1, 3)]
        [SerializeField] private string _shoutText = "[Stranger]: \"STOP!\"";
        [SerializeField] private Color _shoutColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private float _shoutDuration = 2.5f;

        [TextArea(1, 3)]
        [SerializeField] private string _laughText = "[Stranger]: *Evil Laugh* \"You really thought you could escape me?!\"";
        [SerializeField] private Color _laughColor = new Color(1f, 0.4f, 0.4f);
        [SerializeField] private float _laughDuration = 4.0f;

        [SerializeField] private AudioClip _shoutAudio;
        [SerializeField] private AudioClip _evilLaughAudio;
        [SerializeField] private AudioSource _audioSource;

        [Header("Chase Settings")]
        [Tooltip("Move speed of the Stranger when rushing towards the player")]
        [SerializeField] private float _strangerRunSpeed = 4.8f;

        [Tooltip("If Stranger is far away inside the house, spawn/warp him closer along the path so the cutscene is immediate and dramatic")]
        [SerializeField] private bool _spawnCloseIfFar = true;
        [SerializeField] private float _spawnDistance = 14.0f;

        [Header("Cinematic UI References")]
        [SerializeField] private CanvasGroup _letterboxCanvasGroup;
        [SerializeField] private Text _subtitleText;

        [Header("Player References")]
        [SerializeField] private FirstPersonController _playerController;
        [SerializeField] private StarterAssetsInputs _playerInputs;

        private bool _hasTriggered = false;
        private bool _isPlaying = false;

        [Header("Settings")]
        [Tooltip("Allow pressing Space or Escape to skip cutscene")]
        [SerializeField] private bool _allowSkip = true;

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
            if (_hasTriggered || _isPlaying) return;

            if (other.CompareTag("Player") || other.GetComponent<FirstPersonController>() != null || other.GetComponentInParent<FirstPersonController>() != null)
            {
                _hasTriggered = true;
                _cutsceneCoroutine = StartCoroutine(PlayOkayEndingRoutine());
            }
        }

        private void Update()
        {
            if (_isPlaying && _allowSkip)
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    Debug.Log("<color=yellow>[OkayEndingTrigger]</color> Cutscene skipped by player.");
                    SkipCutscene();
                }
#else
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    Debug.Log("<color=yellow>[OkayEndingTrigger]</color> Cutscene skipped by player.");
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

        private IEnumerator PlayOkayEndingRoutine()
        {
            _isPlaying = true;
            Debug.Log("<color=red><b>[OkayEndingTrigger]</b> Starting Second Ending Cutscene!</color>");

            AutoWireReferences();

            // 1. Lock Player Movement & Controls
            SetPlayerControlsActive(false);

            // 2. Fade in Letterbox Bars
            if (_letterboxCanvasGroup != null)
            {
                _letterboxCanvasGroup.DOKill();
                _letterboxCanvasGroup.DOFade(1f, 0.6f).SetEase(Ease.InOutSine);
            }

            yield return new WaitForSeconds(0.4f);

            // 3. Position Stranger outside the house if needed and command him to STAND STILL
            if (_stranger != null && _playerController != null)
            {
                float dist = Vector3.Distance(_stranger.transform.position, _playerController.transform.position);
                if (_spawnCloseIfFar && dist > 20.0f)
                {
                    Vector3 toPlayer = (_playerController.transform.position - _stranger.transform.position).normalized;
                    Vector3 candidatePos = _playerController.transform.position - toPlayer * _spawnDistance;

                    if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, 8.0f, NavMesh.AllAreas))
                    {
                        NavMeshAgent agent = _stranger.GetComponent<NavMeshAgent>();
                        if (agent != null) agent.Warp(hit.position);
                        else _stranger.transform.position = hit.position;
                    }
                }

                // Command Enemy 2 to stand 100% still in Idle stance facing the player
                _stranger.StandStill(_playerController.transform.position);
            }

            // 4. Stranger Shouts "STOP!"
            if (_subtitleText != null)
            {
                _subtitleText.DOKill();
                _subtitleText.text = _shoutText;
                _subtitleText.color = _shoutColor;
                _subtitleText.DOFade(1f, 0.3f);
            }

            if (_shoutAudio != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_shoutAudio);
            }

            // 5. Camera tracks and focuses on Stranger standing still outside
            float shoutElapsed = 0f;
            while (shoutElapsed < _shoutDuration)
            {
                shoutElapsed += Time.deltaTime;
                if (_stranger != null)
                {
                    TrackStrangerWithCamera(_stranger.transform, Time.deltaTime * 6f);
                }
                yield return null;
            }

            // Hide shout text
            if (_subtitleText != null)
            {
                _subtitleText.DOFade(0f, 0.3f);
            }

            // 6. Evil Laugh dialogue & audio
            if (_evilLaughAudio != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_evilLaughAudio);
            }

            if (_subtitleText != null)
            {
                _subtitleText.DOKill();
                _subtitleText.text = _laughText;
                _subtitleText.color = _laughColor;
                _subtitleText.DOFade(1f, 0.4f);
            }

            // Lock camera firmly on Stranger standing still outside during Evil Laugh
            float laughTimer = 0f;
            while (laughTimer < _laughDuration)
            {
                laughTimer += Time.deltaTime;
                if (_stranger != null)
                {
                    TrackStrangerWithCamera(_stranger.transform, Time.deltaTime * 8f);
                }
                yield return null;
            }

            // 8. Fade Screen to Black
            bool fadeDone = false;
            FadeScreen.Instance.FadeToBlack(_fadeDuration, () => fadeDone = true);
            yield return new WaitUntil(() => fadeDone);

            yield return new WaitForSeconds(0.4f);

            // 9. Load Ending Scene
            Debug.Log($"<color=green>[OkayEndingTrigger]</color> Setting Okay Ending and Loading Scene: '{_endingSceneName}'");
            EndingManager.CurrentEnding = EndingType.Okay;
            SceneManager.LoadScene(_endingSceneName);
        }

        private void TrackStrangerWithCamera(Transform strangerTransform, float lerpSpeed)
        {
            if (_playerController == null || strangerTransform == null) return;

            Vector3 eyePos = _playerController.CinemachineCameraTarget != null
                ? _playerController.CinemachineCameraTarget.transform.position
                : _playerController.transform.position + Vector3.up * 1.6f;

            Vector3 targetLookPos = strangerTransform.position + Vector3.up * 1.55f;
            Vector3 dir = (targetLookPos - eyePos).normalized;

            float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float targetPitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;

            float startYaw = _playerController.transform.eulerAngles.y;
            float startPitch = _playerController.CinemachineCameraTarget != null ? _playerController.CinemachineCameraTarget.transform.localEulerAngles.x : 0f;
            if (startPitch > 180f) startPitch -= 360f;

            float curYaw = Mathf.LerpAngle(startYaw, targetYaw, lerpSpeed);
            float curPitch = Mathf.Lerp(startPitch, targetPitch, lerpSpeed);

            _playerController.ResetLookOrientation(curPitch, curYaw);
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
        }
    }
}
