using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;
using V0.Interaction;

namespace TrustNoOne.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAI : MonoBehaviour
    {
        public enum State
        {
            Patrol,
            Chase,
            Attack
        }

        [Header("Current State")]
        [SerializeField] private State _currentState = State.Patrol;

        [Header("Detection")]
        [Tooltip("Distance at which the enemy notices the player")]
        [SerializeField] private float _detectionRadius = 12f;

        [Header("Combat & Attack Logic")]
        [Tooltip("Distance at which the enemy stops and attacks the player")]
        [SerializeField] private float _attackRadius = 2f;

        [Tooltip("Cooldown between consecutive attacks (seconds)")]
        [SerializeField] private float _attackCooldown = 2f;

        [Tooltip("Damage dealt per attack")]
        [SerializeField] private float _attackDamage = 25f;

        [Header("Movement Speeds")]
        [Tooltip("Speed when patrolling (plays Walk animation)")]
        [SerializeField] private float _patrolSpeed = 1.2f;

        [Tooltip("Speed when chasing player (plays Run animation)")]
        [SerializeField] private float _chaseSpeed = 3.5f;

        [Header("Door References & Phasing")]
        [Tooltip("Drag your door GameObjects (or parent walls) here. If left empty, will auto-detect all doors in scene.")]
        [SerializeField] private List<GameObject> _doorGameObjects = new List<GameObject>();

        [Tooltip("How close the ghost needs to be to a door to start phasing through it (meters)")]
        [SerializeField] private float _doorPhaseDistance = 1.8f;

        [Tooltip("Slowed speed when slipping through a closed door")]
        [SerializeField] private float _doorPhasingSpeed = 0.45f;

        [Header("Stairs & Ground Snapping (Fixes Floating)")]
        [Tooltip("Smoothly snaps the model feet to actual stairs/floor geometry")]
        [SerializeField] private bool _enableGroundSnapping = true;

        [Tooltip("Fine-tune vertical feet position (negative lowers feet, positive raises feet)")]
        [SerializeField] private float _feetOffset = 0f;

        [Tooltip("Layers considered ground/stairs")]
        [SerializeField] private LayerMask _groundLayers = ~0;

        [Header("Patrol Settings")]
        [Tooltip("Optional predefined patrol points. If empty, enemy roams randomly near spawn.")]
        [SerializeField] private Transform[] _patrolWaypoints;
        [SerializeField] private float _patrolWanderRadius = 15f;
        [SerializeField] private float _patrolWaitDuration = 2.5f;

        [Header("References (Auto-detected if unassigned)")]
        [SerializeField] private Transform _player;
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _modelTransform;

        private NavMeshAgent _agent;
        private Collider _ownCollider;
        private Vector3 _spawnPosition;
        private int _currentWaypointIndex;
        private float _lastAttackTime = -999f;
        private float _patrolWaitTimer;
        private bool _isWaitingAtPoint;

        // Door tracking with child collider search
        private class TrackedDoor
        {
            public GameObject RootObject;
            public DoorInteractable Interactable;
            public Transform CenterTransform;
            public readonly List<Collider> ChildColliders = new List<Collider>();
        }

        private readonly List<TrackedDoor> _trackedDoors = new List<TrackedDoor>();
        private bool _isPhasing;
        private TrackedDoor _currentPhasingDoor;
        private Sequence _phaseSequence;

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly Dictionary<Material, Color> _originalColors = new Dictionary<Material, Color>();

        // Animator parameter hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        private bool _hasSpeedParam;
        private bool _hasAttackParam;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _ownCollider = GetComponent<Collider>();
            _spawnPosition = transform.position;

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_modelTransform == null && _animator != null)
            {
                _modelTransform = _animator.transform;
            }

            CacheRenderers();
            CheckAnimatorParameters();
            InitializeDoors();

            if (_player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    _player = playerObj.transform;
                }
            }
        }

        private void InitializeDoors()
        {
            _trackedDoors.Clear();

            // 1. If user assigned specific door gameobjects in inspector
            if (_doorGameObjects != null && _doorGameObjects.Count > 0)
            {
                foreach (GameObject go in _doorGameObjects)
                {
                    if (go == null) continue;
                    RegisterDoorObject(go);
                }
            }
            else
            {
                // 2. Auto-find all DoorInteractable in the entire scene
                DoorInteractable[] foundDoors = Object.FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
                foreach (DoorInteractable door in foundDoors)
                {
                    RegisterDoorObject(door.gameObject);
                }
            }

            Debug.Log($"<color=cyan>[EnemyAI]</color> Initialized {_trackedDoors.Count} doors with all child colliders.");
        }

        private void RegisterDoorObject(GameObject go)
        {
            // Search on self, children, or parent for DoorInteractable
            DoorInteractable doorInteractable = go.GetComponentInChildren<DoorInteractable>(true)
                                            ?? go.GetComponentInParent<DoorInteractable>();

            if (doorInteractable == null) return;

            TrackedDoor tracked = new TrackedDoor
            {
                RootObject = go,
                Interactable = doorInteractable,
                CenterTransform = doorInteractable.transform
            };

            // Search self and ALL children recursively for colliders
            Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in colliders)
            {
                if (!tracked.ChildColliders.Contains(c))
                {
                    tracked.ChildColliders.Add(c);
                }
            }

            // Also check doorInteractable GameObject children
            Collider[] doorCols = doorInteractable.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in doorCols)
            {
                if (!tracked.ChildColliders.Contains(c))
                {
                    tracked.ChildColliders.Add(c);
                }
            }

            _trackedDoors.Add(tracked);
        }

        private void CacheRenderers()
        {
            GetComponentsInChildren(true, _renderers);
            foreach (Renderer r in _renderers)
            {
                foreach (Material m in r.materials)
                {
                    if (m != null && m.HasProperty("_BaseColor") && !_originalColors.ContainsKey(m))
                    {
                        _originalColors[m] = m.GetColor("_BaseColor");
                    }
                }
            }
        }

        private void CheckAnimatorParameters()
        {
            if (_animator == null) return;

            foreach (AnimatorControllerParameter param in _animator.parameters)
            {
                if (param.nameHash == SpeedHash) _hasSpeedParam = true;
                if (param.nameHash == AttackHash) _hasAttackParam = true;
            }
        }

        private void Start()
        {
            _agent.speed = _patrolSpeed;
            _agent.stoppingDistance = _attackRadius * 0.8f;
            SetNextPatrolDestination();
        }

        private void Update()
        {
            if (_player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _player = p.transform;
                return;
            }

            float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

            // Check distance to all tracked doors
            UpdateDoorPhasing();

            switch (_currentState)
            {
                case State.Patrol:
                    HandlePatrol(distanceToPlayer);
                    break;

                case State.Chase:
                    HandleChase(distanceToPlayer);
                    break;

                case State.Attack:
                    HandleAttack(distanceToPlayer);
                    break;
            }

            UpdateAnimator();
        }

        private void LateUpdate()
        {
            SnapModelToGround();
        }

        #region Door Phasing (Go Through Closed Doors)

        /// <summary>
        /// Continuously checks distance to all known doors.
        /// When near a closed door, slows down and triggers DOTween ghost phasing.
        /// </summary>
        private void UpdateDoorPhasing()
        {
            TrackedDoor nearestDoor = null;
            float minDistance = float.MaxValue;

            Vector2 ghost2D = new Vector2(transform.position.x, transform.position.z);

            // Find the closest door to the ghost
            foreach (TrackedDoor door in _trackedDoors)
            {
                if (door == null || door.Interactable == null || door.CenterTransform == null) continue;

                Vector2 door2D = new Vector2(door.CenterTransform.position.x, door.CenterTransform.position.z);
                float dist = Vector2.Distance(ghost2D, door2D);

                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestDoor = door;
                }
            }

            // Check if the ghost is within door range
            if (nearestDoor != null && minDistance <= _doorPhaseDistance)
            {
                // If the door is OPEN -> DO NOT PHASE. Walk through normally at full speed!
                if (nearestDoor.Interactable.IsOpen)
                {
                    if (_isPhasing)
                    {
                        EndDoorPhasing();
                    }
                }
                else
                {
                    // Door is CLOSED -> Phase through with slow speed and DOTween animation
                    if (!_isPhasing || _currentPhasingDoor != nearestDoor)
                    {
                        StartDoorPhasing(nearestDoor);
                    }
                    _agent.speed = _doorPhasingSpeed;
                }
            }
            else if (_isPhasing && minDistance > _doorPhaseDistance + 0.3f)
            {
                // Moved safely past the door -> End phasing
                EndDoorPhasing();
            }
        }

        private void StartDoorPhasing(TrackedDoor door)
        {
            _isPhasing = true;
            _currentPhasingDoor = door;
            _agent.speed = _doorPhasingSpeed;

            // Ignore collision with ALL child colliders of this door/wall
            if (_ownCollider != null)
            {
                foreach (Collider col in door.ChildColliders)
                {
                    if (col != null)
                    {
                        Physics.IgnoreCollision(_ownCollider, col, true);
                    }
                }
            }

            // DOTween Phasing Animation
            if (_modelTransform != null)
            {
                _phaseSequence?.Kill();
                _phaseSequence = DOTween.Sequence();

                // 1. Slim down into an ethereal mist
                _phaseSequence.Append(_modelTransform.DOScale(new Vector3(0.25f, 1.12f, 0.25f), 0.35f).SetEase(Ease.InOutSine));

                // 2. Ghostly vibration while passing through
                _phaseSequence.Join(_modelTransform.DOShakePosition(2f, new Vector3(0.06f, 0.02f, 0.06f), 10, 90, false, false));

                // 3. Spectral blue/shadow color tint on materials
                foreach (var kvp in _originalColors)
                {
                    Material mat = kvp.Key;
                    Color orig = kvp.Value;
                    Color spectralColor = new Color(orig.r * 0.4f, orig.g * 0.7f, orig.b * 1.3f, orig.a);
                    _phaseSequence.Join(mat.DOColor(spectralColor, "_BaseColor", 0.35f));
                }
            }
        }

        private void EndDoorPhasing()
        {
            _isPhasing = false;

            // Re-enable collisions
            if (_ownCollider != null && _currentPhasingDoor != null)
            {
                foreach (Collider col in _currentPhasingDoor.ChildColliders)
                {
                    if (col != null)
                    {
                        Physics.IgnoreCollision(_ownCollider, col, false);
                    }
                }
            }
            _currentPhasingDoor = null;

            // Restore scale and original colors with DOTween
            if (_modelTransform != null)
            {
                _phaseSequence?.Kill();
                _phaseSequence = DOTween.Sequence();

                // Restore full scale with slight bounce
                _phaseSequence.Append(_modelTransform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack));

                // Restore material colors
                foreach (var kvp in _originalColors)
                {
                    Material mat = kvp.Key;
                    _phaseSequence.Join(mat.DOColor(kvp.Value, "_BaseColor", 0.35f));
                }
            }

            // Restore normal speed
            _agent.speed = (_currentState == State.Chase) ? _chaseSpeed : _patrolSpeed;
        }

        #endregion

        #region Ground Snapping (Stairs & Slope Alignment)

        private void SnapModelToGround()
        {
            if (!_enableGroundSnapping || _modelTransform == null) return;

            Ray ray = new Ray(transform.position + Vector3.up * 1f, Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(ray, 3f, _groundLayers, QueryTriggerInteraction.Ignore);

            if (hits.Length == 0) return;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                    continue;

                float targetLocalY = (hit.point.y - transform.position.y) + _feetOffset;
                Vector3 localPos = _modelTransform.localPosition;
                localPos.y = Mathf.Lerp(localPos.y, targetLocalY, Time.deltaTime * 20f);
                _modelTransform.localPosition = localPos;
                break;
            }
        }

        #endregion

        #region State Handlers

        private void HandlePatrol(float distanceToPlayer)
        {
            if (distanceToPlayer <= _detectionRadius)
            {
                _isWaitingAtPoint = false;
                _currentState = State.Chase;
                _agent.isStopped = false;
                if (!_isPhasing) _agent.speed = _chaseSpeed;
                return;
            }

            if (!_isPhasing) _agent.speed = _patrolSpeed;

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.2f)
            {
                if (!_isWaitingAtPoint)
                {
                    _isWaitingAtPoint = true;
                    _patrolWaitTimer = _patrolWaitDuration;
                }
                else
                {
                    _patrolWaitTimer -= Time.deltaTime;
                    if (_patrolWaitTimer <= 0f)
                    {
                        _isWaitingAtPoint = false;
                        SetNextPatrolDestination();
                    }
                }
            }
        }

        private void HandleChase(float distanceToPlayer)
        {
            if (distanceToPlayer <= _attackRadius)
            {
                _currentState = State.Attack;
                _agent.isStopped = true;
                return;
            }

            if (distanceToPlayer > _detectionRadius * 1.4f)
            {
                _currentState = State.Patrol;
                if (!_isPhasing) _agent.speed = _patrolSpeed;
                SetNextPatrolDestination();
                return;
            }

            _agent.isStopped = false;
            if (!_isPhasing) _agent.speed = _chaseSpeed;
            _agent.SetDestination(_player.position);
        }

        private void HandleAttack(float distanceToPlayer)
        {
            _agent.isStopped = true;

            Vector3 lookDirection = (_player.position - transform.position);
            lookDirection.y = 0f;
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
            }

            if (distanceToPlayer > _attackRadius * 1.2f)
            {
                _currentState = State.Chase;
                _agent.isStopped = false;
                if (!_isPhasing) _agent.speed = _chaseSpeed;
                return;
            }

            if (Time.time >= _lastAttackTime + _attackCooldown)
            {
                PerformAttack();
            }
        }

        private void PerformAttack()
        {
            _lastAttackTime = Time.time;

            Debug.Log($"<color=red>[EnemyAI]</color> Ghost attacked player! Dealt {_attackDamage} damage.");

            if (_animator != null && _hasAttackParam)
            {
                _animator.SetTrigger(AttackHash);
            }
        }

        private void SetNextPatrolDestination()
        {
            _agent.isStopped = false;

            if (_patrolWaypoints != null && _patrolWaypoints.Length > 0)
            {
                _agent.SetDestination(_patrolWaypoints[_currentWaypointIndex].position);
                _currentWaypointIndex = (_currentWaypointIndex + 1) % _patrolWaypoints.Length;
            }
            else
            {
                Vector3 randomDirection = Random.insideUnitSphere * _patrolWanderRadius;
                randomDirection += _spawnPosition;

                if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, _patrolWanderRadius, NavMesh.AllAreas))
                {
                    _agent.SetDestination(hit.position);
                }
            }
        }

        private void UpdateAnimator()
        {
            if (_animator == null || !_hasSpeedParam) return;

            float currentSpeed = _agent.isStopped ? 0f : _agent.velocity.magnitude;
            _animator.SetFloat(SpeedHash, currentSpeed);
        }

        #endregion

        private void OnDestroy()
        {
            _phaseSequence?.Kill();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRadius);

            // Draw cyan spheres for all tracked door trigger zones
            Gizmos.color = Color.cyan;
            foreach (TrackedDoor door in _trackedDoors)
            {
                if (door?.CenterTransform != null)
                {
                    Gizmos.DrawWireSphere(door.CenterTransform.position, _doorPhaseDistance);
                }
            }
        }
    }
}
