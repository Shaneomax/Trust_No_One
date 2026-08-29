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

        [Tooltip("Delay into the DoorOpening animation when the door actually swings open (seconds)")]
        [SerializeField] private float _doorOpenTriggerDelay = 0.7f;

        [Tooltip("Total duration of the DoorOpening animation sequence (seconds)")]
        [SerializeField] private float _doorOpenTotalDuration = 1.6f;

        [Tooltip("Can Enemy2 also force open locked doors? (false = only normal/unlocked closed doors)")]
        [SerializeField] private bool _canOpenLockedDoors = false;

        [Header("Stairs & Ground Snapping")]
        [Tooltip("Smoothly aligns feet with stairs and floor geometry")]
        [SerializeField] private bool _enableGroundSnapping = true;
        [SerializeField] private float _feetOffset = 0f;
        [SerializeField] private LayerMask _groundLayers = ~0;

        [Header("References (Auto-detected if unassigned)")]
        [SerializeField] private Transform _player;
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _modelTransform;

        private NavMeshAgent _agent;
        private bool _isOpeningDoor = false;
        private DoorInteractable _currentDoorTarget;
        private float _doorCooldownTimer = 0f;
        private readonly HashSet<DoorInteractable> _openedDoors = new HashSet<DoorInteractable>();

        // Animator parameter hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int OpenDoorHash = Animator.StringToHash("OpenDoor");

        private bool _hasSpeedParam;
        private bool _hasOpenDoorParam;

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
                if (param.nameHash == OpenDoorHash) _hasOpenDoorParam = true;
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
            if (_player == null)
            {
                CachePlayer();
                return;
            }

            if (_doorCooldownTimer > 0f)
            {
                _doorCooldownTimer -= Time.deltaTime;
            }

            // If currently performing the door opening animation, wait until finished
            if (_isOpeningDoor)
            {
                UpdateAnimator(0f);
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
        /// </summary>
        private bool CheckForDoorAhead(out DoorInteractable door)
        {
            door = null;
            if (_isOpeningDoor) return false;

            Vector3 origin = transform.position + Vector3.up * 1.0f;
            Vector3 forward = transform.forward;

            // 1. Check colliders in front of Enemy2
            Collider[] hits = Physics.OverlapSphere(origin + forward * 0.9f, _doorDetectDistance, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider col in hits)
            {
                DoorInteractable d = col.GetComponentInParent<DoorInteractable>();
                if (d != null && !d.IsOpen && !_openedDoors.Contains(d))
                {
                    if (!d.IsLocked || _canOpenLockedDoors)
                    {
                        door = d;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Sequences the DoorOpening animation, faces the door, and opens it smoothly.
        /// </summary>
        private IEnumerator OpenDoorSequence(DoorInteractable door)
        {
            _isOpeningDoor = true;
            _currentDoorTarget = door;
            _openedDoors.Add(door);
            _doorCooldownTimer = 4.0f;

            // 1. Stop NavMeshAgent
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }

            // 2. Turn smoothly to face the door
            Vector3 doorDir = (door.transform.position - transform.position);
            doorDir.y = 0f;
            if (doorDir.sqrMagnitude > 0.01f)
            {
                Quaternion faceDoorRot = Quaternion.LookRotation(doorDir);
                float turnTimer = 0f;
                while (turnTimer < 0.35f)
                {
                    turnTimer += Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(transform.rotation, faceDoorRot, turnTimer / 0.35f);
                    yield return null;
                }
            }

            // 3. Trigger DoorOpening animation ONCE
            if (_animator != null)
            {
                if (_hasOpenDoorParam)
                {
                    _animator.ResetTrigger(OpenDoorHash);
                    _animator.SetTrigger(OpenDoorHash);
                }
                else
                {
                    _animator.Play("DoorOpening", 0, 0f);
                }
            }

            Debug.Log($"<color=cyan>[DeceiverAI]</color> Enemy 2 is opening door: <b>{door.gameObject.name}</b>");

            // 4. Wait for hand reach moment in animation
            yield return new WaitForSeconds(_doorOpenTriggerDelay);

            // 5. Open the door!
            if (door != null && !door.IsOpen)
            {
                door.Interact();
            }

            // 6. Wait for animation to finish cleanly
            float remainingDuration = Mathf.Max(0.2f, _doorOpenTotalDuration - _doorOpenTriggerDelay);
            yield return new WaitForSeconds(remainingDuration);

            // 7. Resume following
            _isOpeningDoor = false;
            _doorCooldownTimer = 3.0f;

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
            if (!_enableGroundSnapping || _modelTransform == null || _modelTransform == transform) return;

            Ray ray = new Ray(transform.position + Vector3.up * 1f, Vector3.down);
            int hitCount = Physics.RaycastNonAlloc(ray, _groundHitBuffer, 3f, _groundLayers, QueryTriggerInteraction.Ignore);

            if (hitCount == 0) return;

            System.Array.Sort(_groundHitBuffer, 0, hitCount, RaycastHitDistanceComparer.Instance);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _groundHitBuffer[i];
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                    continue;

                float targetLocalY = (hit.point.y - transform.position.y) + _feetOffset;
                Vector3 localPos = _modelTransform.localPosition;
                localPos.y = Mathf.Lerp(localPos.y, targetLocalY, Time.deltaTime * 20f);
                _modelTransform.localPosition = localPos;
                break;
            }
        }
    }
}
