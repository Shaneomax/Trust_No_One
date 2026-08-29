using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using StarterAssets;
using TrustNoOne.AI;
using V0.UI;
using V0.Interaction;

namespace V0.Cinematics
{
    /// <summary>
    /// Attached to the SecondWait trigger point in front of the dining room / table.
    /// Cutscene Sequence:
    /// 1. Player steps into SecondWait -> Player freezes, letterbox fades in, camera frames Stranger.
    /// 2. Stranger plays the 'PickUP' animation to grab the knife from the table.
    /// 3. Table knife is destroyed/hidden, and hand knife on Enemy 2's hand is activated (SetActive true).
    /// 4. Player asks: "What's that for...?"
    /// 5. Stranger replies: "I got the truck key. Let's get out of this house before my wife returns."
    /// 6. Letterbox fades out, player controls restored, Stranger resumes following player.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SecondWaitStrangerCutscene : MonoBehaviour
    {
        [Header("Stranger (Enemy 2) Reference")]
        [SerializeField] private DeceiverAI _stranger;

        [Header("Knife References")]
        [Tooltip("The knife on the table to destroy/hide")]
        [SerializeField] private GameObject _tableKnife;

        [Tooltip("The knife GameObject attached to Enemy 2's hand (set active when picked up)")]
        [SerializeField] private GameObject _handKnife;

        [Header("Animation Settings")]
        [SerializeField] private float _pickupAnimDuration = 2.2f;

        [Header("Dialogue Settings")]
        [TextArea(1, 3)]
        [SerializeField] private string _playerDialogue = "[Player]: \"What's that for...?\"";
        [SerializeField] private Color _playerDialogueColor = new Color(0.85f, 0.9f, 1f);
        [SerializeField] private float _playerDialogueDuration = 2.8f;

        [TextArea(1, 3)]
        [SerializeField] private string _strangerDialogue = "[Stranger]: \"I got the truck key. Let's get out of this house before my wife returns.\"";
        [SerializeField] private Color _strangerDialogueColor = new Color(1f, 0.88f, 0.6f);
        [SerializeField] private float _strangerDialogueDuration = 4.5f;

        [Header("Cinematic UI References")]
        [SerializeField] private CanvasGroup _letterboxCanvasGroup;
        [SerializeField] private Text _subtitleText;

        [Header("Player References")]
        [SerializeField] private FirstPersonController _playerController;
        [SerializeField] private StarterAssetsInputs _playerInputs;

        [Header("Settings")]
        [SerializeField] private bool _playOnce = true;

        private bool _hasTriggered = false;
        private bool _isPlaying = false;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            AutoWireReferences();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasTriggered && _playOnce) return;
            if (_isPlaying) return;

            if (other.CompareTag("Player") || other.GetComponent<FirstPersonController>() != null || other.GetComponentInParent<FirstPersonController>() != null)
            {
                _hasTriggered = true;
                StartCoroutine(PlayCutsceneRoutine());
            }
        }

        private IEnumerator PlayCutsceneRoutine()
        {
            _isPlaying = true;
            Debug.Log("<color=cyan><b>[SecondWaitCutscene]</b> Starting Knife Pickup Cutscene...</color>");

            AutoWireReferences();

            // 1. Lock Player Movement & Controls
            SetPlayerControlsActive(false);

            // 2. Fade in Letterbox Bars
            if (_letterboxCanvasGroup != null)
            {
                _letterboxCanvasGroup.DOKill();
                _letterboxCanvasGroup.DOFade(1f, 0.5f).SetEase(Ease.InOutSine);
            }

            // 3. Smoothly align player look towards Stranger
            if (_stranger != null)
            {
                yield return StartCoroutine(SmoothLookAtStranger(_stranger.transform, 0.6f));
            }

            // 4. Play Stranger Knife Pickup Animation
            bool pickupDone = false;
            if (_stranger != null)
            {
                _stranger.PlayPickupKnife(_tableKnife, _handKnife, _pickupAnimDuration, onCompleted: () =>
                {
                    pickupDone = true;
                });
            }
            else
            {
                pickupDone = true;
            }

            // Track Stranger with camera while he picks up the knife
            float animTimer = 0f;
            while (!pickupDone && animTimer < _pickupAnimDuration + 0.5f)
            {
                animTimer += Time.deltaTime;
                if (_stranger != null)
                {
                    TrackStrangerWithCamera(_stranger.transform, Time.deltaTime * 6f);
                }
                yield return null;
            }

            yield return new WaitForSeconds(0.3f);

            // 5. Line 1: Player asks: "What's that for...?"
            if (_subtitleText != null)
            {
                _subtitleText.DOKill();
                _subtitleText.text = _playerDialogue;
                _subtitleText.color = _playerDialogueColor;
                _subtitleText.DOFade(1f, 0.3f);
            }

            float line1Timer = 0f;
            while (line1Timer < _playerDialogueDuration)
            {
                line1Timer += Time.deltaTime;
                if (_stranger != null)
                {
                    TrackStrangerWithCamera(_stranger.transform, Time.deltaTime * 6f);
                }
                yield return null;
            }

            // 6. Line 2: Stranger replies: "I got the truck key. Let's get out of this house before my wife returns."
            if (_subtitleText != null)
            {
                _subtitleText.DOKill();
                _subtitleText.text = _strangerDialogue;
                _subtitleText.color = _strangerDialogueColor;
                _subtitleText.DOFade(1f, 0.3f);
            }

            float line2Timer = 0f;
            while (line2Timer < _strangerDialogueDuration)
            {
                line2Timer += Time.deltaTime;
                if (_stranger != null)
                {
                    TrackStrangerWithCamera(_stranger.transform, Time.deltaTime * 6f);
                }
                yield return null;
            }

            // Hide Subtitle
            if (_subtitleText != null)
            {
                _subtitleText.DOFade(0f, 0.4f);
            }

            // 7. End Cutscene: Fade out letterbox & restore player controls
            if (_letterboxCanvasGroup != null)
            {
                _letterboxCanvasGroup.DOKill();
                _letterboxCanvasGroup.DOFade(0f, 0.6f);
            }

            yield return new WaitForSeconds(0.4f);

            // Resume Stranger AI so he can follow the player
            if (_stranger != null)
            {
                _stranger.ResumeFollowingPlayer();
            }

            SetPlayerControlsActive(true);
            _isPlaying = false;

            Debug.Log("<color=green><b>[SecondWaitCutscene]</b> Cutscene Complete! Stranger holding knife and ready to follow.</color>");

            if (_playOnce)
            {
                gameObject.SetActive(false);
            }
        }

        private IEnumerator SmoothLookAtStranger(Transform strangerTransform, float duration)
        {
            if (_playerController == null) AutoWireReferences();
            if (_playerController == null || strangerTransform == null) yield break;

            Vector3 eyePos = _playerController.CinemachineCameraTarget != null
                ? _playerController.CinemachineCameraTarget.transform.position
                : _playerController.transform.position + Vector3.up * 1.6f;

            Vector3 targetLookPos = strangerTransform.position + Vector3.up * 1.35f;
            Vector3 dir = (targetLookPos - eyePos).normalized;

            float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float targetPitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;

            float elapsed = 0f;
            float startYaw = _playerController.transform.eulerAngles.y;
            float startPitch = _playerController.CinemachineCameraTarget != null ? _playerController.CinemachineCameraTarget.transform.localEulerAngles.x : 0f;
            if (startPitch > 180f) startPitch -= 360f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                float curYaw = Mathf.LerpAngle(startYaw, targetYaw, t);
                float curPitch = Mathf.Lerp(startPitch, targetPitch, t);

                _playerController.ResetLookOrientation(curPitch, curYaw);
                yield return null;
            }

            _playerController.ResetLookOrientation(targetPitch, targetYaw);
        }

        private void TrackStrangerWithCamera(Transform strangerTransform, float lerpSpeed)
        {
            if (_playerController == null || strangerTransform == null) return;

            Vector3 eyePos = _playerController.CinemachineCameraTarget != null
                ? _playerController.CinemachineCameraTarget.transform.position
                : _playerController.transform.position + Vector3.up * 1.6f;

            Vector3 targetLookPos = strangerTransform.position + Vector3.up * 1.35f;
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

            // Auto-find Table Knife if unassigned
            if (_tableKnife == null)
            {
                GameObject k = GameObject.Find("SM_Knife");
                if (k != null) _tableKnife = k;
            }

            // Auto-find Hand Knife inside Enemy 2 if unassigned
            if (_handKnife == null && _stranger != null)
            {
                if (_stranger.HandKnife != null)
                {
                    _handKnife = _stranger.HandKnife;
                }
                else
                {
                    Transform[] children = _stranger.GetComponentsInChildren<Transform>(true);
                    foreach (Transform tr in children)
                    {
                        if (tr.name.ToLower().Contains("knife") && tr.gameObject != _tableKnife)
                        {
                            _handKnife = tr.gameObject;
                            _stranger.HandKnife = _handKnife;
                            break;
                        }
                    }
                }
            }
        }
    }
}
