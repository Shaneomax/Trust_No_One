using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using DG.Tweening;
using StarterAssets;
using TrustNoOne.AI;
using V0.Interaction;
using V0.UI;

namespace V0.Cinematics
{
    /// <summary>
    /// Triggered when the player reaches the FirstWait trigger in front of the interior room.
    /// Cutscene Flow:
    /// 1. Player freezes and smoothly looks at the Stranger (Enemy 2).
    /// 2. Stranger speaks: "Wait here, I'll go get the truck key."
    /// 3. Stranger walks to the room door, plays the DoorOpening animation, and opens the door.
    /// 4. Stranger walks through the doorway into the room toward the Knife destination.
    /// 5. As soon as the Stranger is inside the room, the door closes shut behind him.
    /// 6. Stranger stops beside the knife in Idle pose and remains stationary.
    /// 7. Cutscene ends and player control is restored!
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FirstWaitStrangerCutscene : MonoBehaviour
    {
        [Header("Stranger (Enemy 2) Reference")]
        [Tooltip("The Deceiver NPC. Auto-detects if left empty.")]
        [SerializeField] private DeceiverAI _stranger;

        [Header("Door Reference")]
        [Tooltip("The room door that Stranger opens and enters. Auto-detects closest door if empty.")]
        [SerializeField] private DoorInteractable _roomDoor;

        [Header("Knife / Destination Reference")]
        [Tooltip("Drag & Drop the Knife or destination spot where Stranger should stand. Auto-finds SM_Knife if empty.")]
        [SerializeField] private Transform _knifeDestination;

        [Header("Dialogue & Timing")]
        [TextArea(2, 4)]
        [SerializeField] private string _dialogueText = "Wait here, I'll go get the truck key.";
        [SerializeField] private Color _dialogueColor = new Color(1f, 0.88f, 0.6f);
        [SerializeField] private float _dialogueDuration = 3.5f;

        [Header("Camera Framing")]
        [Tooltip("Optional Cinemachine camera for framing this cutscene (e.g. Cam_FirstWait). If unassigned, smoothly rotates player camera toward Stranger.")]
        [SerializeField] private CinemachineCamera _cutsceneCamera;
        [SerializeField] private CinemachineVirtualCameraBase _playerFollowCamera;

        [Header("UI References")]
        [SerializeField] private CanvasGroup _letterboxCanvasGroup;
        [SerializeField] private Text _subtitleText;

        [Header("Player Control References")]
        [SerializeField] private FirstPersonController _playerController;
        [SerializeField] private PlayerInteraction _playerInteraction;
        [SerializeField] private StarterAssetsInputs _playerInputs;

        [Header("Settings")]
        [SerializeField] private bool _playOnce = true;

        private bool _hasTriggered = false;
        private bool _isPlaying = false;
        private Coroutine _cutsceneRoutine;

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

            if (other.CompareTag("Player") || other.GetComponent<FirstPersonController>() != null)
            {
                _hasTriggered = true;
                _cutsceneRoutine = StartCoroutine(PlayCutsceneRoutine());
            }
        }

        private IEnumerator PlayCutsceneRoutine()
        {
            _isPlaying = true;
            Debug.Log("<color=cyan><b>[FirstWaitCutscene]</b> Starting Stranger Wait Cutscene...</color>");

            AutoWireReferences();

            // 1. Lock Player Movement & Inputs
            LockPlayerControls(true);

            // 2. Fade in Letterbox Bars
            if (_letterboxCanvasGroup != null)
            {
                _letterboxCanvasGroup.DOKill();
                _letterboxCanvasGroup.DOFade(1f, 0.5f);
            }

            // 3. Smoothly rotate player camera / view directly to look at the Stranger
            if (_stranger != null)
            {
                yield return StartCoroutine(SmoothLookAtStranger(_stranger.transform, 0.8f));
            }

            // 4. Show Subtitle Dialogue: "Wait here, I'll go get the truck key."
            if (_subtitleText != null)
            {
                _subtitleText.text = _dialogueText;
                _subtitleText.color = _dialogueColor;
                _subtitleText.DOKill();
                _subtitleText.DOFade(1f, 0.4f);
            }

            // Wait for dialogue line to deliver
            yield return new WaitForSeconds(_dialogueDuration);

            // Hide Subtitle
            if (_subtitleText != null)
            {
                _subtitleText.DOFade(0f, 0.5f);
            }

            // 5. Command Stranger to navigate directly to the Knife position (opens any closed doors on his way)
            bool doorClosed = false;
            bool strangerArrived = false;

            if (_stranger != null && _knifeDestination != null)
            {
                _stranger.MoveToDestination(
                    _knifeDestination,
                    onDoorPassed: () =>
                    {
                        if (!doorClosed)
                        {
                            doorClosed = true;
                            if (_roomDoor != null)
                            {
                                Debug.Log("<color=yellow>[FirstWaitCutscene]</color> Stranger entered room. Closing door behind him!");
                                _roomDoor.CloseDoor();
                            }
                        }
                    },
                    onArrived: () =>
                    {
                        strangerArrived = true;
                        Debug.Log("<color=green>[FirstWaitCutscene]</color> Stranger arrived at knife destination and entered idle.");
                    }
                );
            }
            else
            {
                doorClosed = true;
                strangerArrived = true;
            }

            // Wait until the door is closed and stranger has reached the knife destination
            float maxWait = 14.0f;
            float elapsed = 0f;
            while ((!doorClosed || !strangerArrived) && elapsed < maxWait)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Brief moment after door shuts
            yield return new WaitForSeconds(0.6f);

            // 6. End Cutscene: Fade out letterbox, restore player controls
            if (_letterboxCanvasGroup != null)
            {
                _letterboxCanvasGroup.DOKill();
                _letterboxCanvasGroup.DOFade(0f, 0.6f);
            }

            yield return new WaitForSeconds(0.4f);

            LockPlayerControls(false);
            _isPlaying = false;

            Debug.Log("<color=green><b>[FirstWaitCutscene]</b> Cutscene Complete! Player restored.</color>");

            if (_playOnce)
            {
                gameObject.SetActive(false);
            }
        }

        private IEnumerator SmoothLookAtStranger(Transform strangerTransform, float duration)
        {
            if (_playerController == null || strangerTransform == null) yield break;

            Vector3 eyePos = _playerController.CinemachineCameraTarget != null
                ? _playerController.CinemachineCameraTarget.transform.position
                : _playerController.transform.position + Vector3.up * 1.6f;

            Vector3 targetLookPos = strangerTransform.position + Vector3.up * 1.5f;
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

        private void LockPlayerControls(bool locked)
        {
            if (_playerController != null)
            {
                _playerController.enabled = !locked;
            }

            if (_playerInputs != null)
            {
                _playerInputs.move = Vector2.zero;
                _playerInputs.look = Vector2.zero;
                _playerInputs.sprint = false;
                _playerInputs.jump = false;
                _playerInputs.cursorLocked = !locked;
                _playerInputs.cursorInputForLook = !locked;
            }

            if (_playerInteraction != null)
            {
                _playerInteraction.enabled = !locked;
            }
        }

        public void AutoWireReferences()
        {
            if (_stranger == null)
            {
                _stranger = Object.FindFirstObjectByType<DeceiverAI>();
            }

            if (_playerController == null)
            {
                _playerController = Object.FindFirstObjectByType<FirstPersonController>();
            }

            if (_playerController != null)
            {
                if (_playerInteraction == null) _playerInteraction = _playerController.GetComponent<PlayerInteraction>();
                if (_playerInputs == null) _playerInputs = _playerController.GetComponent<StarterAssetsInputs>();
            }

            if (_playerFollowCamera == null)
            {
                GameObject camObj = GameObject.Find("PlayerFollowCamera");
                if (camObj != null) _playerFollowCamera = camObj.GetComponent<CinemachineVirtualCameraBase>();
            }

            if (_letterboxCanvasGroup == null)
            {
                GameObject canvasObj = GameObject.Find("CinematicLetterboxCanvas");
                if (canvasObj != null)
                {
                    _letterboxCanvasGroup = canvasObj.GetComponent<CanvasGroup>();
                    _subtitleText = canvasObj.GetComponentInChildren<Text>();
                }
            }

            // Find closest door to FirstWait if unassigned
            if (_roomDoor == null)
            {
                DoorInteractable[] doors = Object.FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
                float closestDist = float.MaxValue;
                foreach (DoorInteractable d in doors)
                {
                    float dist = Vector3.Distance(transform.position, d.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        _roomDoor = d;
                    }
                }
            }

            // Find Knife destination if unassigned
            if (_knifeDestination == null)
            {
                GameObject knife = GameObject.Find("SM_Knife");
                if (knife != null)
                {
                    _knifeDestination = knife.transform;
                }
            }
        }
    }
}
