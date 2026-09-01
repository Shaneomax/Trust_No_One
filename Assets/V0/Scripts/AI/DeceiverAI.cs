using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;
using V0.Interaction;

namespace TrustNoOne.AI
{
    /// <summary>
    /// Enemy 2 (The Deceiver):
    /// Acts like a friendly / helpful NPC follower to deceive the player into false safety.
    /// - Follows the player at a natural distance using NavMesh.
    /// - Stands still playing Idle animation when the player stops.
    /// - Walks smoothly matching pace when following.
    /// - Automatically stops and plays DoorOpening animation to open closed doors in his path!
    /// - Never attacks or deals damage (pure psychological deception).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class DeceiverAI : MonoBehaviour
    {
        [Header("Follow Settings")]
        [Tooltip("Target distance to maintain from the player")]
        [SerializeField] private float _followDistance = 2.5f;

        [Tooltip("Movement walking speed when following player")]
        [SerializeField] private float _walkSpeed = 2.0f;

        [Tooltip("Smooth turn speed when facing player while idle")]
        [SerializeField] private float _lookAtSpeed = 4.0f;

        [Header("Door Interaction Settings")]
        [Tooltip("Distance to closed door to initiate opening interaction")]
        [SerializeField] private float _doorDetectDistance = 2.4f;

        [Tooltip("Full duration of the DoorOpening animation before opening the door (seconds)")]
        [SerializeField] private float _doorOpenAnimationDuration = 1.8f;

        [Tooltip("Can Enemy2 also force open locked doors? (false = only normal/unlocked closed doors)")]
        [SerializeField] private bool _canOpenLockedDoors = false;

        [Header("Cutscene & Target Destinations")]
        [Tooltip("Drag & Drop the Knife or destination spot where Enemy 2 should go during the cutscene.")]
        [SerializeField] private Transform _knifeDestination;

        [Header("Hand Knife Reference")]
        [Tooltip("The knife GameObject attached to Enemy 2's hand (set active when picked up)")]
        [SerializeField] private GameObject _handKnife;

        [Header("Footsteps Audio")]
        [Tooltip("Footstep audio clip for Enemy 2 (Auto-finds FootStep.mp3 if null)")]
        [SerializeField] private AudioClip _footstepAudioClip;
        [SerializeField] private float _footstepVolume = 0.60f;
        [SerializeField] private float _footstepInterval = 0.52f;

        [Header("References (Auto-detected if unassigned)")]
        [SerializeField] private Transform _player;
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _modelTransform;

        private NavMeshAgent _agent;
        private AudioSource _footstepAudioSource;
        private float _footstepTimer = 0f;
        private bool _isOpeningDoor = false;
        private bool _isStationary = false;
        private bool _isNavigatingToDestination = false;
        private DoorInteractable _currentDoorTarget;
        private float _doorCooldownTimer = 0f;
        private readonly HashSet<DoorInteractable> _openedDoors = new HashSet<DoorInteractable>();

        public bool IsStationary
        {
            get => _isStationary;
            set => _isStationary = value;
        }

        public Transform KnifeDestination
        {
            get => _knifeDestination;
            set => _knifeDestination = value;
        }

        public GameObject HandKnife
        {
            get => _handKnife;
            set => _handKnife = value;
        }

        // Animator parameter hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private bool _hasSpeedParam;

        // Zero-GC raycast buffer
        private readonly RaycastHit[] _groundHitBuffer = new RaycastHit[8];

        private class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();
            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            // CRITICAL: Disable Root Motion so NavMeshAgent controls movement
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }

            if (_modelTransform == null && _animator != null && _animator.transform != transform)
            {
                _modelTransform = _animator.transform;
            }

            // Configure NavMeshAgent defaults
            if (_agent != null)
            {
                _agent.speed = _walkSpeed;
                _agent.stoppingDistance = _followDistance;
                _agent.acceleration = 16f;
                _agent.angularSpeed = 360f;
                _agent.autoBraking = true;
            }

            // Set ghost/enemy colliders to triggers so player does not get jammed
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            foreach (Collider c in colliders)
            {
                if (c != null) c.isTrigger = true;
            }

            CheckAnimatorParameters();
            CachePlayer();
        }

        private void OnDisable()
        {
            if (_footstepAudioSource != null && _footstepAudioSource.isPlaying)
            {
                _footstepAudioSource.Stop();
            }
        }

        private void Start()
        {
            CachePlayer();

            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }

            if (_agent != null)
            {
                if (!_agent.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                    {
                        _agent.Warp(hit.position);
                    }
                }
                _agent.isStopped = false;
                _agent.speed = _walkSpeed;
                _agent.stoppingDistance = _followDistance;
            }
        }

        private void CheckAnimatorParameters()
        {
            if (_animator == null) return;

            foreach (AnimatorControllerParameter param in _animator.parameters)
            {
                if (param.nameHash == SpeedHash) _hasSpeedParam = true;
            }
        }

        private void CachePlayer()
        {
            if (_player == null)
            {
                // 1. Check tag "Player"
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    _player = playerObj.transform;
                    return;
                }

                // 2. Check FirstPersonController
                StarterAssets.FirstPersonController fpc = Object.FindFirstObjectByType<StarterAssets.FirstPersonController>();
                if (fpc != null)
                {
                    _player = fpc.transform;
                    return;
                }

                // 3. Check name PlayerCapsule
                GameObject pc = GameObject.Find("PlayerCapsule");
                if (pc != null)
                {
                    _player = pc.transform;
                }
            }
        }

        private void Update()
        {
            UpdateFootsteps();

            // If navigating to a specific destination (e.g. knife during cutscene), suppress follow player logic!
            if (_isNavigatingToDestination)
            {
                return;
            }

            if (_player == null)
            {
                CachePlayer();
                return;
            }

            if (_doorCooldownTimer > 0f)
            {
                _doorCooldownTimer -= Time.deltaTime;
            }

            // If in stationary mode (e.g. standing beside the knife after cutscene), remain idle
            if (_isStationary)
            {
                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                }
                UpdateAnimator(0f);
                return;
            }

            // If currently performing the door opening animation, stand 100% still and wait until finished
            if (_isOpeningDoor)
            {
                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                }
                return;
            }

            // 1. Check for closed doors near Enemy2 that block his path or need opening
            if (_doorCooldownTimer <= 0f && CheckForDoorAhead(out DoorInteractable closedDoor))
            {
                StartCoroutine(OpenDoorSequence(closedDoor));
                return;
            }

            // 2. Follow player logic
            if (_agent != null && _agent.isOnNavMesh)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

                if (distanceToPlayer > _followDistance + 0.3f)
                {
                    // Player moved away: follow player
                    _agent.isStopped = false;
                    _agent.speed = _walkSpeed;
                    _agent.SetDestination(_player.position);

                    float currentSpeed = _agent.velocity.magnitude > 0.1f ? _walkSpeed : 0.5f;
                    UpdateAnimator(currentSpeed);
                }
                else
                {
                    // Reached player: stand still and face player smoothly
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;

                    UpdateAnimator(0f);

                    // Look at player while idling
                    Vector3 dirToPlayer = (_player.position - transform.position);
                    dirToPlayer.y = 0f;
                    if (dirToPlayer.sqrMagnitude > 0.01f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(dirToPlayer);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * _lookAtSpeed);
                    }
                }
            }
        }

        private void LateUpdate()
        {
            SnapModelToGround();
        }

        /// <summary>
        /// Detects if there is a closed door in front of Enemy2 that needs to be opened.
        /// Strictly ignores already-open doors.
        /// </summary>
        private bool CheckForDoorAhead(out DoorInteractable door)
        {
            door = null;
            if (_isOpeningDoor) return false;

            Vector3 origin = transform.position + Vector3.up * 1.0f;
            Vector3 forward = transform.forward;

            // Tight sphere check directly in front
            Collider[] hits = Physics.OverlapSphere(origin + forward * (_doorDetectDistance * 0.45f), _doorDetectDistance * 0.55f, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider col in hits)
            {
                DoorInteractable d = col.GetComponentInParent<DoorInteractable>();
                if (d != null)
                {
                    // If door is ALREADY OPEN, never play animation
                    if (d.IsOpen)
                    {
                        _openedDoors.Add(d);
                        continue;
                    }

                    // Must be facing the door
                    Vector3 toDoor = (d.transform.position - transform.position);
                    toDoor.y = 0f;
                    if (toDoor.sqrMagnitude > 0.01f && Vector3.Dot(forward, toDoor.normalized) < 0.3f)
                    {
                        continue;
                    }

                    if (!_openedDoors.Contains(d))
                    {
                        if (!d.IsLocked || _canOpenLockedDoors)
                        {
                            door = d;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Sequences the DoorOpening animation:
        /// 1. Freezes Enemy2 in place with 0 position drift.
        /// 2. Plays DoorOpening animation cleanly.
        /// 3. AFTER animation completes, door opens.
        /// 4. CrossFades back to Idle before moving to prevent half-open animation glitches.
        /// 5. Resumes following the player!
        /// </summary>
        private IEnumerator OpenDoorSequence(DoorInteractable door)
        {
            if (door == null || door.IsOpen || _openedDoors.Contains(door))
            {
                yield break;
            }

            _isOpeningDoor = true;
            _currentDoorTarget = door;
            _openedDoors.Add(door);
            _doorCooldownTimer = 4.0f;

            // 1. Force Speed to 0 so walk cycle does not blend in
            if (_animator != null && _hasSpeedParam)
            {
                _animator.SetFloat(SpeedHash, 0f);
            }

            // 2. Completely halt NavMeshAgent and capture position
            Vector3 lockedPos = transform.position;
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
                _agent.velocity = Vector3.zero;
            }

            // 3. Turn smoothly to face the door
            Vector3 doorDir = (door.transform.position - transform.position);
            doorDir.y = 0f;
            if (doorDir.sqrMagnitude > 0.01f)
            {
                Quaternion faceDoorRot = Quaternion.LookRotation(doorDir);
                float turnTimer = 0f;
                while (turnTimer < 0.25f)
                {
                    turnTimer += Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(transform.rotation, faceDoorRot, turnTimer / 0.25f);
                    transform.position = lockedPos;
                    if (_agent != null && _agent.isOnNavMesh) _agent.velocity = Vector3.zero;
                    yield return null;
                }
            }

            // 4. Play DoorOpening animation cleanly via CrossFade
            if (_animator != null)
            {
                _animator.CrossFadeInFixedTime("DoorOpening", 0.12f);
            }

            Debug.Log($"<color=cyan>[DeceiverAI]</color> Playing DoorOpening animation for: <b>{door.gameObject.name}</b>");

            // 5. Wait for the DoorOpening animation to play to completion while standing 100% still
            float animElapsed = 0f;
            while (animElapsed < _doorOpenAnimationDuration)
            {
                animElapsed += Time.deltaTime;
                transform.position = lockedPos; // Absolute position lock (0 drift!)
                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                }
                yield return null;
            }

            // 6. AFTER animation finishes: Open the door!
            if (door != null && !door.IsOpen)
            {
                door.Interact();
                Debug.Log($"<color=green>[DeceiverAI]</color> Door opened: <b>{door.gameObject.name}</b>");
            }

            // 7. CrossFade back to Idle pose BEFORE enabling movement (eliminates half-open drifting pose!)
            if (_animator != null)
            {
                _animator.CrossFadeInFixedTime("Idle", 0.2f);
            }

            // Stand still for a brief moment while the door swings open
            float settleTimer = 0f;
            while (settleTimer < 0.35f)
            {
                settleTimer += Time.deltaTime;
                transform.position = lockedPos;
                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                }
                yield return null;
            }

            // 8. Resume following the player smoothly
            _isOpeningDoor = false;
            _doorCooldownTimer = 3.5f;

            if (_agent != null && _agent.isOnNavMesh && _player != null)
            {
                _agent.isStopped = false;
                _agent.speed = _walkSpeed;
                _agent.SetDestination(_player.position);
            }
        }

        private void UpdateAnimator(float speed)
        {
            if (_animator == null) return;

            if (_hasSpeedParam)
            {
                _animator.SetFloat(SpeedHash, speed);

                if (speed <= 0.05f)
                {
                    AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
                    if (state.IsName("Walking") && !state.IsName("DoorOpening"))
                    {
                        _animator.CrossFadeInFixedTime("Idle", 0.15f);
                    }
                }
            }
            else
            {
                // Fallback direct state playback if Speed parameter is missing
                if (speed > 0.1f)
                {
                    if (!_animator.GetCurrentAnimatorStateInfo(0).IsName("Walking"))
                    {
                        _animator.CrossFade("Walking", 0.2f);
                    }
                }
                else
                {
                    if (!_animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && !_animator.GetCurrentAnimatorStateInfo(0).IsName("DoorOpening"))
                    {
                        _animator.CrossFade("Idle", 0.2f);
                    }
                }
            }
        }

        private void SnapModelToGround()
        {
            if (_modelTransform != null && _modelTransform != transform)
            {
                _modelTransform.localPosition = Vector3.zero;
            }
        }

        /// <summary>
        /// Commands Enemy 2 to navigate directly to the target destination (e.g. Knife).
        /// - Follows NavMesh path directly to the destination.
        /// - Stops exactly 2 meters away from the knife (like player distance).
        /// - If he encounters any closed door in front of him along the path, he opens it with animation.
        /// - Once he passes through the door, onDoorPassed is fired (to close the door).
        /// - When he arrives 2m from destination, he faces the knife, plays Idle, and becomes permanently stationary.
        /// </summary>
        public void MoveToDestination(Transform destination, System.Action onDoorPassed, System.Action onArrived)
        {
            StopAllCoroutines();
            StartCoroutine(MoveToDestinationRoutine(destination, onDoorPassed, onArrived));
        }

        private IEnumerator MoveToDestinationRoutine(Transform destination, System.Action onDoorPassed, System.Action onArrived)
        {
            _isOpeningDoor = false;
            _isStationary = false;
            _isNavigatingToDestination = true;

            // Resolve target destination
            Transform targetTransform = destination != null ? destination : _knifeDestination;
            if (targetTransform == null)
            {
                GameObject knife = GameObject.Find("SM_Knife");
                if (knife != null) targetTransform = knife.transform;
            }

            if (targetTransform == null)
            {
                Debug.LogError("<color=red>[DeceiverAI]</color> MoveToDestination failed: No Knife or Destination Transform provided!");
                _isNavigatingToDestination = false;
                yield break;
            }

            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();
            }

            if (_agent == null)
            {
                _isNavigatingToDestination = false;
                yield break;
            }

            // Ensure agent is on NavMesh
            if (!_agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, 5.0f, NavMesh.AllAreas))
                {
                    _agent.Warp(navHit.position);
                }
            }

            Vector3 targetPos = targetTransform.position;
            _agent.isStopped = false;
            _agent.speed = _walkSpeed;
            _agent.stoppingDistance = 0.25f; // Close pickup reach distance to knife!
            _agent.SetDestination(targetPos);
            UpdateAnimator(_walkSpeed);

            Debug.Log($"<color=cyan><b>[DeceiverAI]</b></color> Moving to knife (stopping at pickup distance ~1.0m): <b>{targetTransform.gameObject.name}</b> at {targetPos}");

            DoorInteractable lastOpenedDoor = null;
            bool doorPassedCalled = false;

            while (true)
            {
                // 1. Check for closed doors directly in front of Enemy 2 along his path
                if (_doorCooldownTimer <= 0f && CheckForDoorAhead(out DoorInteractable closedDoor))
                {
                    lastOpenedDoor = closedDoor;
                    yield return StartCoroutine(OpenDoorSequence(closedDoor));

                    // After door opens, re-engage navigation to destination!
                    if (_agent != null && _agent.isOnNavMesh)
                    {
                        _agent.isStopped = false;
                        _agent.speed = _walkSpeed;
                        _agent.stoppingDistance = 0.25f;
                        _agent.SetDestination(targetPos);
                        UpdateAnimator(_walkSpeed);
                    }
                }

                // 2. Track when Enemy 2 passes through the door into the room
                if (!doorPassedCalled && lastOpenedDoor != null)
                {
                    float distToDoor = Vector3.Distance(transform.position, lastOpenedDoor.transform.position);
                    if (distToDoor > 1.6f) // Enemy 2 has traversed through the door
                    {
                        doorPassedCalled = true;
                        onDoorPassed?.Invoke();
                    }
                }

                // 3. Keep moving towards destination (stopping at reach/pickup distance ~1.0m)
                if (_agent != null && _agent.isOnNavMesh)
                {
                    float distToDestination = Vector3.Distance(transform.position, targetPos);

                    // Update movement animation
                    float currentSpeed = _agent.velocity.magnitude > 0.1f ? _walkSpeed : 0.5f;
                    UpdateAnimator(currentSpeed);

                    if ((distToDestination <= 1.1f || (_agent.hasPath && _agent.remainingDistance <= 1.1f)) && !_agent.pathPending)
                    {
                        // Reached knife pickup distance! Stop here beside the table!
                        _agent.isStopped = true;
                        _agent.ResetPath();
                        _agent.velocity = Vector3.zero;
                        break;
                    }
                }

                yield return null;
            }

            // If door passed wasn't invoked yet, invoke now
            if (!doorPassedCalled)
            {
                onDoorPassed?.Invoke();
            }

            // 4. Stand at pickup distance, smoothly face the knife, and switch to Idle animation
            if (_animator != null)
            {
                if (_hasSpeedParam) _animator.SetFloat(SpeedHash, 0f);
                _animator.CrossFadeInFixedTime("Idle", 0.15f);
            }

            Vector3 toKnife = (targetPos - transform.position);
            toKnife.y = 0f;
            if (toKnife.sqrMagnitude > 0.01f)
            {
                Quaternion faceRot = Quaternion.LookRotation(toKnife);
                float turnTimer = 0f;
                while (turnTimer < 0.5f)
                {
                    turnTimer += Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(transform.rotation, faceRot, turnTimer / 0.5f);
                    yield return null;
                }
            }

            _isStationary = true;
            _isNavigatingToDestination = false;
            _isOpeningDoor = false;

            if (_animator != null)
            {
                if (_hasSpeedParam) _animator.SetFloat(SpeedHash, 0f);
                _animator.CrossFadeInFixedTime("Idle", 0.15f);
            }

            Debug.Log("<color=green><b>[DeceiverAI]</b></color> Arrived at knife pickup distance (~1.0m) and entered permanent stationary Idle.");
            onArrived?.Invoke();
        }

        /// <summary>
        /// Commands Enemy 2 to rapidly pursue/approach the player during the OkayEnding cutscene.
        /// </summary>
        public void ApproachPlayer(Transform playerTransform, float speed = 4.0f, System.Action onArrived = null)
        {
            StopAllCoroutines();
            StartCoroutine(ApproachPlayerRoutine(playerTransform, speed, onArrived));
        }

        private IEnumerator ApproachPlayerRoutine(Transform playerTransform, float speed, System.Action onArrived)
        {
            _isStationary = false;
            _isOpeningDoor = false;
            _isNavigatingToDestination = true;

            if (_agent == null) _agent = GetComponent<NavMeshAgent>();

            if (_agent != null)
            {
                if (!_agent.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                    {
                        _agent.Warp(hit.position);
                    }
                }

                _agent.isStopped = false;
                _agent.speed = speed;
                _agent.stoppingDistance = 1.8f;
                _agent.SetDestination(playerTransform.position);
                UpdateAnimator(speed);
            }

            Debug.Log($"<color=red><b>[DeceiverAI]</b></color> Approaching player aggressively at speed {speed}!");

            while (_agent != null && _agent.isOnNavMesh)
            {
                _agent.SetDestination(playerTransform.position);
                UpdateAnimator(speed);

                // Handle any doors along the path
                if (_doorCooldownTimer <= 0f && CheckForDoorAhead(out DoorInteractable door))
                {
                    yield return StartCoroutine(OpenDoorSequence(door));
                    if (_agent != null && _agent.isOnNavMesh)
                    {
                        _agent.isStopped = false;
                        _agent.speed = speed;
                        _agent.stoppingDistance = 1.8f;
                        _agent.SetDestination(playerTransform.position);
                    }
                }

                float dist = Vector3.Distance(transform.position, playerTransform.position);
                if (dist <= 2.0f && !_agent.pathPending)
                {
                    _agent.isStopped = true;
                    _agent.ResetPath();
                    _agent.velocity = Vector3.zero;
                    break;
                }

                yield return null;
            }

            // Smoothly face player
            Vector3 toPlayer = (playerTransform.position - transform.position);
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toPlayer);
                float timer = 0f;
                while (timer < 0.5f)
                {
                    timer += Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, timer / 0.5f);
                    yield return null;
                }
            }

            _isStationary = true;
            _isNavigatingToDestination = false;
            _isOpeningDoor = false;

            if (_animator != null)
            {
                if (_hasSpeedParam) _animator.SetFloat(SpeedHash, 0f);
                _animator.CrossFadeInFixedTime("Idle", 0.15f);
            }

            Debug.Log("<color=green><b>[DeceiverAI]</b></color> Arrived directly in front of player!");
            onArrived?.Invoke();
        }

        /// <summary>
        /// Commands Enemy 2 to play the knife pickup animation:
        /// 1. Plays the 'PickUP' animation state.
        /// 2. Halfway through the pickup gesture, disables/destroys tableKnife and enables handKnife.
        /// 3. Transitions cleanly back to Idle.
        /// </summary>
        public void PlayPickupKnife(GameObject tableKnife, GameObject handKnife = null, float animDuration = 2.2f, System.Action onCompleted = null)
        {
            StopAllCoroutines();
            StartCoroutine(PickupKnifeRoutine(tableKnife, handKnife, animDuration, onCompleted));
        }

        private IEnumerator PickupKnifeRoutine(GameObject tableKnife, GameObject handKnife, float animDuration, System.Action onCompleted)
        {
            _isStationary = true;
            _isOpeningDoor = false;
            _isNavigatingToDestination = false;

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }

            GameObject resolvedHandKnife = handKnife != null ? handKnife : _handKnife;
            if (resolvedHandKnife == null)
            {
                Transform[] allChildren = GetComponentsInChildren<Transform>(true);
                foreach (Transform child in allChildren)
                {
                    if (child.name.ToLower().Contains("knife") && child.gameObject != tableKnife)
                    {
                        resolvedHandKnife = child.gameObject;
                        break;
                    }
                }
            }

            // 1. Trigger PickUP animation
            if (_animator != null)
            {
                _animator.SetFloat(SpeedHash, 0f);

                bool hasPickUpParam = false;
                foreach (var p in _animator.parameters)
                {
                    if (p.name == "PickUp") { hasPickUpParam = true; break; }
                }

                if (hasPickUpParam)
                {
                    _animator.SetTrigger("PickUp");
                }

                // Check state name variations and force Play if needed
                _animator.CrossFadeInFixedTime("PickUP", 0.1f, 0, 0f);
            }

            Debug.Log("<color=yellow>[DeceiverAI]</color> Playing PickUP animation for knife...");

            // 2. Wait until hand reaches the table (~42% of animation duration)
            float swapTime = animDuration * 0.42f;
            yield return new WaitForSeconds(swapTime);

            // 3. Swap knife: Hide/destroy table knife and activate hand knife!
            if (tableKnife != null)
            {
                tableKnife.SetActive(false);
                Destroy(tableKnife, 0.1f);
                Debug.Log("<color=green>[DeceiverAI]</color> Table knife destroyed/hidden.");
            }

            if (resolvedHandKnife != null)
            {
                resolvedHandKnife.SetActive(true);
                Debug.Log($"<color=green>[DeceiverAI]</color> Hand knife '{resolvedHandKnife.name}' activated in Enemy 2's hand!");
            }

            // 4. Wait for remaining pickup animation duration
            float remaining = Mathf.Max(0.1f, animDuration - swapTime);
            yield return new WaitForSeconds(remaining);

            // 5. Crossfade back to Idle
            if (_animator != null)
            {
                _animator.CrossFadeInFixedTime("Idle", 0.2f);
            }

            yield return new WaitForSeconds(0.3f);

            onCompleted?.Invoke();
        }

        /// <summary>
        /// Resumes normal following behavior after cutscenes.
        /// </summary>
        public void ResumeFollowingPlayer()
        {
            StopAllCoroutines();
            _isStationary = false;
            _isNavigatingToDestination = false;
            _isOpeningDoor = false;
            _doorCooldownTimer = 0f;

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.speed = _walkSpeed;
                _agent.stoppingDistance = _followDistance;
                if (_player != null)
                {
                    _agent.SetDestination(_player.position);
                }
            }
            Debug.Log("<color=green>[DeceiverAI]</color> Resumed following player!");
        }

        /// <summary>
        /// Commands Enemy 2 to stand 100% still in Idle stance (e.g. after coming outside the house in Okay Ending).
        /// </summary>
        public void StandStill(Vector3? lookAtTarget = null)
        {
            StopAllCoroutines();
            _isStationary = true;
            _isNavigatingToDestination = false;
            _isOpeningDoor = false;

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
                _agent.ResetPath();
            }

            if (_animator != null)
            {
                if (_hasSpeedParam) _animator.SetFloat(SpeedHash, 0f);
                _animator.CrossFadeInFixedTime("Idle", 0.15f);
            }

            if (lookAtTarget.HasValue)
            {
                Vector3 dir = (lookAtTarget.Value - transform.position);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(dir);
                }
            }

            Debug.Log("<color=yellow>[DeceiverAI]</color> Enemy 2 is now standing still in Idle stance outside.");
        }

        private void UpdateFootsteps()
        {
            if (_footstepAudioSource == null)
            {
                _footstepAudioSource = gameObject.AddComponent<AudioSource>();
                _footstepAudioSource.playOnAwake = false;
                _footstepAudioSource.spatialBlend = 1.0f; // 3D Spatial Audio
                _footstepAudioSource.minDistance = 1.5f;
                _footstepAudioSource.maxDistance = 18.0f;
                _footstepAudioSource.rolloffMode = AudioRolloffMode.Linear;
                _footstepAudioSource.loop = true;
            }

            if (_footstepAudioClip == null)
            {
                _footstepAudioClip = Resources.Load<AudioClip>("Audio/FootStep")
                                  ?? Resources.Load<AudioClip>("FootStep");
#if UNITY_EDITOR
                if (_footstepAudioClip == null)
                {
                    _footstepAudioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/V0/Audio/FootStep.mp3");
                }
#endif
            }

            if (_footstepAudioClip != null && _footstepAudioSource.clip != _footstepAudioClip)
            {
                _footstepAudioSource.clip = _footstepAudioClip;
            }

            if (_isStationary || _isOpeningDoor || _agent == null || !_agent.isOnNavMesh || V0.Interaction.FlashlightController.IsGlobalCutscene)
            {
                if (_footstepAudioSource.isPlaying) _footstepAudioSource.Stop();
                return;
            }

            float currentSpeed = _agent.velocity.magnitude;
            bool isMoving = currentSpeed > 0.25f && !_agent.isStopped;

            if (isMoving && _footstepAudioSource.clip != null)
            {
                _footstepAudioSource.pitch = 0.90f; // Heavier stride for stranger
                _footstepAudioSource.volume = _footstepVolume;

                if (!_footstepAudioSource.isPlaying)
                {
                    _footstepAudioSource.Play();
                }
            }
            else
            {
                if (_footstepAudioSource.isPlaying)
                {
                    _footstepAudioSource.Stop();
                }
            }
        }
    }
}
