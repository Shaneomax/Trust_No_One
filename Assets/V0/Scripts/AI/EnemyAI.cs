using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;
using StarterAssets;
using V0.Interaction;
using V0.UI;

namespace TrustNoOne.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAI : MonoBehaviour
    {
        public enum State
        {
            Patrol,
            Chase,
            Search,
            Attack
        }

        [Header("Current State")]
        [SerializeField] private State _currentState = State.Patrol;

        [Header("Vision & Line of Sight")]
        [Tooltip("Maximum distance enemy can spot the player visually")]
        [SerializeField] private float _detectionRadius = 14f;

        [Tooltip("Vision cone angle (degrees) in front of the enemy")]
        [Range(30f, 360f)]
        [SerializeField] private float _fieldOfView = 120f;

        [Tooltip("Height of enemy's eyes from feet for raycasting")]
        [SerializeField] private float _eyeHeight = 1.4f;

        [Tooltip("Close proximity awareness: within this distance, enemy senses player 360 degrees")]
        [SerializeField] private float _closeProximityRadius = 2.2f;

        [Tooltip("Layers that block line of sight (walls, furniture, closed doors, tables)")]
        [SerializeField] private LayerMask _obstructionLayers = ~0;

        [Header("Hearing & Footstep Detection")]
        [Tooltip("Maximum distance the ghost can hear the player sprinting (loud footsteps)")]
        [SerializeField] private float _sprintHearingRadius = 14f;

        [Tooltip("Maximum distance the ghost can hear the player normal walking (standard footsteps in close proximity)")]
        [SerializeField] private float _walkHearingRadius = 4.5f;

        [Tooltip("Can the ghost hear footsteps through walls? (false = walls and floors muffle footsteps)")]
        [SerializeField] private bool _hearThroughWalls = false;

        [Header("Search / Lost Sight Settings")]
        [Tooltip("Time enemy spends searching at player's last known or heard position before resuming patrol")]
        [SerializeField] private float _searchDuration = 3.5f;

        [Tooltip("Movement speed when walking to investigate last known spot")]
        [SerializeField] private float _searchSpeed = 1.6f;

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
        [SerializeField] private float _doorPhaseDistance = 2.2f;

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

        [Header("Spawn Grace Period (Player Headstart)")]
        [Tooltip("Headstart time in seconds after spawning where the ghost ignores the player, giving the player time to run and hide")]
        [SerializeField] private float _spawnGracePeriod = 3.5f;
        private float _spawnGraceTimer = 0f;

        [Header("Audio & Scream upon Detection")]
        [Tooltip("AudioSource component for playing scream and horror sounds")]
        [SerializeField] private AudioSource _audioSource;

        [Tooltip("Audio clip played when the ghost spots the player and stands still screaming (Auto-finds Ghost_Scream.mp3)")]
        [SerializeField] private AudioClip _screamSound;

        [Tooltip("Duration the ghost stands still screaming before sprinting to chase (seconds)")]
        [SerializeField] private float _screamDuration = 1.2f;

        [Header("Heartbeat Audio (Hunting & Searching)")]
        [Tooltip("Heartbeat audio clip played when ghost is hunting or searching (Auto-finds Heart_Beat.mp3)")]
        [SerializeField] private AudioClip _heartbeatAudioClip;
        [Range(0f, 1f)]
        [SerializeField] private float _chaseHeartbeatVolume = 0.85f;
        [Range(0f, 1f)]
        [SerializeField] private float _searchHeartbeatVolume = 0.50f;

        private AudioSource _heartbeatAudioSource;

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

        // Scream & Alert reaction state
        private bool _isScreaming = false;
        private Coroutine _screamCoroutine;

        // Player input & movement cache for hearing and push prevention
        private StarterAssetsInputs _playerInputs;
        private CharacterController _playerCharController;
        private FirstPersonController _playerFPC;

        // Line of Sight & Search State
        private bool _canSeePlayer;
        private Vector3 _lastKnownPlayerPosition;
        private float _searchTimer;
        private float _lostSightGraceTimer;
        private bool _hasReachedLastKnownPos;
        private float _searchLookTimer;
        private float _searchLookAngle;

        // Preallocated raycast buffers for Zero-GC allocations in WebGL
        private readonly RaycastHit[] _raycastHitBuffer = new RaycastHit[16];
        private readonly RaycastHit[] _groundHitBuffer = new RaycastHit[8];

        private class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();
            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }

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

            // Ensure EnemyHealth is attached (100 HP)
            if (GetComponent<EnemyHealth>() == null)
            {
                gameObject.AddComponent<EnemyHealth>();
            }

            // Make all ghost colliders triggers so the player's CharacterController passes straight through
            Collider[] ghostColliders = GetComponentsInChildren<Collider>(true);
            foreach (Collider gc in ghostColliders)
            {
                if (gc != null)
                {
                    gc.isTrigger = true;
                }
            }

            // Lock Rigidbody so physics collisions/shoves cannot move the ghost
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_modelTransform == null && _animator != null)
            {
                _modelTransform = _animator.transform;
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                    _audioSource.spatialBlend = 1f;
                    _audioSource.minDistance = 3f;
                    _audioSource.maxDistance = 25f;
                    _audioSource.rolloffMode = AudioRolloffMode.Linear;
                }
            }

            CacheRenderers();
            CheckAnimatorParameters();
            InitializeDoors();

            CachePlayerReferences();
        }

        private void CachePlayerReferences()
        {
            if (_player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    _player = playerObj.transform;
                }
            }

            if (_player != null)
            {
                _playerInputs = _player.GetComponent<StarterAssetsInputs>();
                _playerCharController = _player.GetComponent<CharacterController>();
                _playerFPC = _player.GetComponent<FirstPersonController>();
            }
        }

        private void InitializeDoors()
        {
            _trackedDoors.Clear();

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
                DoorInteractable[] foundDoors = Object.FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
                foreach (DoorInteractable door in foundDoors)
                {
                    RegisterDoorObject(door.gameObject);
                }
            }
        }

        private void RegisterDoorObject(GameObject go)
        {
            DoorInteractable doorInteractable = go.GetComponentInChildren<DoorInteractable>(true)
                                            ?? go.GetComponentInParent<DoorInteractable>();

            if (doorInteractable == null) return;

            TrackedDoor tracked = new TrackedDoor
            {
                RootObject = go,
                Interactable = doorInteractable,
                CenterTransform = doorInteractable.transform
            };

            Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in colliders)
            {
                if (!tracked.ChildColliders.Contains(c))
                {
                    tracked.ChildColliders.Add(c);
                }
            }

            Collider[] doorCols = doorInteractable.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in doorCols)
            {
                if (!tracked.ChildColliders.Contains(c))
                {
                    tracked.ChildColliders.Add(c);
                }
            }

            // Also check parent for box colliders (e.g. SM_Door_interior_01 or BoxColliderObject)
            if (doorInteractable.transform.parent != null)
            {
                Collider[] parentCols = doorInteractable.transform.parent.GetComponentsInChildren<Collider>(true);
                foreach (Collider c in parentCols)
                {
                    if (!tracked.ChildColliders.Contains(c))
                    {
                        tracked.ChildColliders.Add(c);
                    }
                }
            }

            _trackedDoors.Add(tracked);
        }

        /// <summary>
        /// Freezes enemy AI during cutscenes so the ghost stands in place playing its idle animation.
        /// </summary>
        public void SetCutsceneMode(bool inCutscene)
        {
            if (inCutscene)
            {
                if (_screamCoroutine != null)
                {
                    StopCoroutine(_screamCoroutine);
                    _screamCoroutine = null;
                }
                _isScreaming = false;

                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = true;
                    _agent.velocity = Vector3.zero;
                }
                if (_animator != null)
                {
                    _animator.SetFloat(SpeedHash, 0f);
                }
                DetectionIndicatorUI.SetGlobalState(DetectionIndicatorUI.DetectionState.None);
                enabled = false;
            }
            else
            {
                enabled = true;
                _spawnGraceTimer = _spawnGracePeriod;
                _currentState = State.Patrol;
                if (_agent != null && _agent.isOnNavMesh)
                {
                    _agent.isStopped = false;
                    if (!_isPhasing) _agent.speed = _patrolSpeed;
                    SetNextPatrolDestination();
                }
                DetectionIndicatorUI.SetGlobalState(DetectionIndicatorUI.DetectionState.None);
                Debug.Log($"<color=green>[EnemyAI]</color> Ghost spawned/resumed with {_spawnGracePeriod:F1}s headstart for player!");
            }
        }

        private void OnEnable()
        {
            _spawnGraceTimer = _spawnGracePeriod;
            _currentState = State.Patrol;
            DetectionIndicatorUI.SetGlobalState(DetectionIndicatorUI.DetectionState.None);
        }

        private void OnDisable()
        {
            if (_screamCoroutine != null)
            {
                StopCoroutine(_screamCoroutine);
                _screamCoroutine = null;
            }
            _isScreaming = false;
            if (_heartbeatAudioSource != null && _heartbeatAudioSource.isPlaying)
            {
                _heartbeatAudioSource.Stop();
            }
            DetectionIndicatorUI.SetGlobalState(DetectionIndicatorUI.DetectionState.None);
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
            _currentState = State.Patrol;
            _spawnGraceTimer = _spawnGracePeriod;
            _agent.speed = _patrolSpeed;
            _agent.stoppingDistance = _attackRadius * 0.8f;
            SetNextPatrolDestination();
            DetectionIndicatorUI.SetGlobalState(DetectionIndicatorUI.DetectionState.None);
        }

        private void Update()
        {
            if (_player == null)
            {
                CachePlayerReferences();
                return;
            }

            // Spawn Grace Period (Give player guaranteed 5-6s headstart to run and hide!)
            if (_spawnGraceTimer > 0f)
            {
                _spawnGraceTimer -= Time.deltaTime;
                _canSeePlayer = false;

                // Lock in quiet Patrol mode and keep UI indicator hidden
                _currentState = State.Patrol;
                DetectionIndicatorUI.SetGlobalState(DetectionIndicatorUI.DetectionState.None);

                UpdateDoorPhasing();
                HandlePatrol(Vector3.Distance(transform.position, _player.position));
                UpdateAnimator();
                return;
            }

            // 1. Evaluate Line of Sight to the player
            _canSeePlayer = EvaluateLineOfSight(out float distanceToPlayer);

            if (_canSeePlayer)
            {
                _lastKnownPlayerPosition = _player.position;
                _lostSightGraceTimer = 0f;
            }
            else
            {
                // 2. If enemy CANNOT see the player, check if it HEARS the player!
                EvaluateHearing(distanceToPlayer);
            }

            // 3. Check door proximity for phasing
            UpdateDoorPhasing();

            // 4. Update UI Detection Indicator (Yellow for Search, Red for Detected/Chase, None for Patrol)
            UpdateDetectionUI();

            // 5. Update Player Panic Heartbeat Audio (Pounding in Chase/Attack, Tense in Search, Silent in Patrol)
            UpdateHeartbeatAudio();

            // If standing still screaming upon spotting player, face player and wait
            if (_isScreaming)
            {
                Vector3 lookDir = (_player.position - transform.position);
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
                }
                UpdateAnimator();
                return;
            }

            // 5. State Machine
            switch (_currentState)
            {
                case State.Patrol:
                    HandlePatrol(distanceToPlayer);
                    break;

                case State.Chase:
                    HandleChase(distanceToPlayer);
                    break;

                case State.Search:
                    HandleSearch();
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

        #region Hearing & Footstep Detection

        /// <summary>
        /// Evaluates whether the ghost hears the player's footsteps.
        /// - Sprinting: Loud footsteps heard from up to _sprintHearingRadius (18m).
        /// - Normal Walking: Standard footsteps heard from up to _walkHearingRadius (10m).
        /// - Crouch Walking / Standing: Completely silent — ghost cannot hear!
        /// </summary>
        private void EvaluateHearing(float distanceToPlayer)
        {
            // If already chasing or attacking, no need to listen for footsteps
            if (_currentState == State.Chase || _currentState == State.Attack) return;

            // 1. CROUCH CHECK: Crouch walking is completely silent!
            if (IsPlayerCrouching()) return;

            // 2. MOVEMENT CHECK: Is the player actively moving? (Standing still is silent)
            if (!IsPlayerMoving(out bool isSprinting)) return;

            // 3. RANGE CHECK: Determine effective hearing radius based on sprint vs normal walk
            float effectiveHearingRadius = isSprinting ? _sprintHearingRadius : _walkHearingRadius;
            if (distanceToPlayer > effectiveHearingRadius) return;

            // 4. WALL OCCLUSION CHECK (if enabled)
            if (!_hearThroughWalls)
            {
                Vector3 eyePos = transform.position + Vector3.up * _eyeHeight;
                Vector3 dirToPlayer = _player.position - eyePos;
                if (Physics.Raycast(eyePos, dirToPlayer.normalized, distanceToPlayer, _obstructionLayers, QueryTriggerInteraction.Ignore))
                {
                    return; // Wall muffles the sound
                }
            }

            // Ghost hears the footsteps! Head directly to investigate where the noise came from
            _lastKnownPlayerPosition = _player.position;
            _currentState = State.Search;
            _searchTimer = _searchDuration;
            _hasReachedLastKnownPos = false;
            _agent.isStopped = false;
            if (!_isPhasing) _agent.speed = _searchSpeed;
            _agent.SetDestination(_lastKnownPlayerPosition);

            string moveType = isSprinting ? "sprinting" : "normal walking";
            Debug.Log($"<color=orange>[EnemyAI]</color> Ghost heard {moveType} footsteps {distanceToPlayer:F1}m away (radius: {effectiveHearingRadius:F0}m)! Investigating sound location.");
        }

        private bool IsPlayerCrouching()
        {
            if (_player == null) return false;

            if (_playerFPC == null)
            {
                _playerFPC = _player.GetComponent<FirstPersonController>();
            }
            if (_playerFPC != null && _playerFPC.IsCrouching)
            {
                return true;
            }

            if (_playerInputs == null)
            {
                _playerInputs = _player.GetComponent<StarterAssetsInputs>();
            }
            if (_playerInputs != null && _playerInputs.crouch)
            {
                return true;
            }

            return false;
        }

        private bool IsPlayerMoving(out bool isSprinting)
        {
            isSprinting = false;
            if (_player == null) return false;

            if (_playerInputs == null)
            {
                _playerInputs = _player.GetComponent<StarterAssetsInputs>();
            }
            if (_playerCharController == null)
            {
                _playerCharController = _player.GetComponent<CharacterController>();
            }

            // Input movement check
            bool hasMoveInput = _playerInputs != null && _playerInputs.move.sqrMagnitude > 0.01f;

            // Physical speed check
            float physicalSpeed = 0f;
            if (_playerCharController != null)
            {
                Vector3 horizontalVel = new Vector3(_playerCharController.velocity.x, 0, _playerCharController.velocity.z);
                physicalSpeed = horizontalVel.magnitude;
            }

            bool isPhysicallyMoving = physicalSpeed > 0.15f;

            // If neither moving input nor physical motion, player is standing still (silent)
            if (!hasMoveInput && !isPhysicallyMoving)
            {
                return false;
            }

            // Check if sprinting (Sprint input held OR high physical speed > 4.5m/s)
            bool sprintInput = _playerInputs != null && _playerInputs.sprint;
            isSprinting = sprintInput || physicalSpeed > 4.5f;

            return true;
        }

        #endregion

        #region Line of Sight & Vision Cone

        /// <summary>
        /// Checks if player is within distance, inside field of view,
        /// and not blocked by walls, furniture, tables, or closed doors.
        /// </summary>
        private bool EvaluateLineOfSight(out float distanceToPlayer)
        {
            distanceToPlayer = Vector3.Distance(transform.position, _player.position);

            if (distanceToPlayer > _detectionRadius)
            {
                return false;
            }

            // Check if player is crouching behind cover
            bool isCrouching = false;
            if (_playerFPC != null)
            {
                isCrouching = _playerFPC.IsCrouching;
            }
            else if (_playerInputs != null)
            {
                isCrouching = _playerInputs.crouch;
            }

            Vector3 eyePos = transform.position + Vector3.up * _eyeHeight;

            // When crouching, target point is low to ground (0.45m instead of 0.9m)
            // Low obstacles like half-walls, desks, crates, and window frames will intercept the raycast
            float targetHeight = isCrouching ? 0.45f : 0.9f;
            Vector3 playerTarget = _player.position + Vector3.up * targetHeight;
            Vector3 dirToPlayer = playerTarget - eyePos;

            // Check field of view angle (crouching also sharpens stealth by reducing close-proximity 360-degree awareness)
            float effectiveProximity = isCrouching ? (_closeProximityRadius * 0.6f) : _closeProximityRadius;
            if (distanceToPlayer > effectiveProximity)
            {
                float angle = Vector3.Angle(transform.forward, dirToPlayer);
                if (angle > _fieldOfView * 0.5f)
                {
                    return false; // Player is outside peripheral vision cone
                }
            }

            // Raycast check for walls, tables, and closed doors (Zero-GC NonAlloc)
            Ray ray = new Ray(eyePos, dirToPlayer.normalized);
            int hitCount = Physics.RaycastNonAlloc(ray, _raycastHitBuffer, distanceToPlayer, _obstructionLayers, QueryTriggerInteraction.Ignore);

            if (hitCount == 0) return true;

            System.Array.Sort(_raycastHitBuffer, 0, hitCount, RaycastHitDistanceComparer.Instance);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _raycastHitBuffer[i];

                // Ignore self and child colliders
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                    continue;

                // Reached player without obstacle
                if (hit.transform == _player || hit.transform.IsChildOf(_player))
                {
                    return true;
                }

                // Ghost supernatural line of sight passes through doors (open OR closed) so ghost pursues through doors!
                DoorInteractable door = hit.collider.GetComponentInParent<DoorInteractable>();
                if (door != null)
                {
                    continue;
                }

                // Blocked by a solid wall, table, or furniture!
                return false;
            }

            return true;
        }

        #endregion

        #region State Handlers

        private void HandlePatrol(float distanceToPlayer)
        {
            // If enemy sees player -> immediately scream and stand still, then Chase!
            if (_canSeePlayer)
            {
                _isWaitingAtPoint = false;
                TriggerSpotPlayerScream(distanceToPlayer);
                return;
            }

            // Normal Patrol movement
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
            // Close enough to attack and still has line of sight -> Attack
            if (distanceToPlayer <= _attackRadius && _canSeePlayer)
            {
                _currentState = State.Attack;
                _agent.isStopped = true;
                return;
            }

            // Player is visible -> Run straight towards player
            if (_canSeePlayer)
            {
                _agent.isStopped = false;
                if (!_isPhasing) _agent.speed = _chaseSpeed;
                _agent.SetDestination(_player.position);
            }
            else
            {
                // Player broke line of sight (ran behind wall, under table, etc.)
                _lostSightGraceTimer += Time.deltaTime;

                // After brief 0.35s confirmation, switch to searching last known spot
                if (_lostSightGraceTimer >= 0.35f)
                {
                    Debug.Log("<color=yellow>[EnemyAI]</color> Lost sight of player! Heading to last known position.");
                    _currentState = State.Search;
                    _searchTimer = _searchDuration;
                    _hasReachedLastKnownPos = false;
                    _agent.isStopped = false;
                    if (!_isPhasing) _agent.speed = _searchSpeed;
                    _agent.SetDestination(_lastKnownPlayerPosition);
                }
            }
        }

        private void HandleSearch()
        {
            // If player is spotted (or found while searching) -> scream, then Chase / Attack!
            if (_canSeePlayer)
            {
                Debug.Log("<color=red>[EnemyAI]</color> Found player while searching!");
                float dist = Vector3.Distance(transform.position, _player.position);
                TriggerSpotPlayerScream(dist);
                return;
            }

            // Move to where the player was last seen or heard
            if (!_hasReachedLastKnownPos)
            {
                if (!_isPhasing) _agent.speed = _searchSpeed;
                _agent.SetDestination(_lastKnownPlayerPosition);

                if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.3f)
                {
                    _hasReachedLastKnownPos = true;
                    _agent.isStopped = true;
                }
            }
            else
            {
                // Reached position: look around cautiously
                _agent.isStopped = true;
                _searchTimer -= Time.deltaTime;

                // Turn head/body left and right periodically
                _searchLookTimer -= Time.deltaTime;
                if (_searchLookTimer <= 0f)
                {
                    _searchLookTimer = Random.Range(1.2f, 2.2f);
                    _searchLookAngle = Random.Range(-65f, 65f);
                }

                Quaternion targetRot = Quaternion.Euler(0f, transform.eulerAngles.y + _searchLookAngle, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3f);

                // If search time expires without seeing player -> resume patrol
                if (_searchTimer <= 0f)
                {
                    Debug.Log("<color=green>[EnemyAI]</color> Player not found. Resuming patrol.");
                    _currentState = State.Patrol;
                    _agent.isStopped = false;
                    if (!_isPhasing) _agent.speed = _patrolSpeed;
                    SetNextPatrolDestination();
                }
            }
        }

        private void HandleAttack(float distanceToPlayer)
        {
            _agent.isStopped = true;

            // Look directly at player
            Vector3 lookDirection = (_player.position - transform.position);
            lookDirection.y = 0f;
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
            }

            // If player moves away or breaks line of sight -> Chase or Search
            if (distanceToPlayer > _attackRadius * 1.2f || !_canSeePlayer)
            {
                if (_canSeePlayer)
                {
                    _currentState = State.Chase;
                    _agent.isStopped = false;
                    if (!_isPhasing) _agent.speed = _chaseSpeed;
                }
                else
                {
                    _currentState = State.Search;
                    _searchTimer = _searchDuration;
                    _hasReachedLastKnownPos = false;
                    _agent.isStopped = false;
                    if (!_isPhasing) _agent.speed = _searchSpeed;
                    _agent.SetDestination(_lastKnownPlayerPosition);
                }
                return;
            }

            // Attack cooldown logic
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

            // Deal 25 damage to player
            if (V0.Player.PlayerHealth.Instance != null)
            {
                V0.Player.PlayerHealth.Instance.TakeDamage(_attackDamage);
            }
            else if (_player != null)
            {
                var ph = _player.GetComponent<V0.Player.PlayerHealth>() ?? _player.GetComponentInChildren<V0.Player.PlayerHealth>();
                if (ph != null)
                {
                    ph.TakeDamage(_attackDamage);
                }
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

            float currentSpeed = (_agent.isStopped || _isScreaming) ? 0f : _agent.velocity.magnitude;
            _animator.SetFloat(SpeedHash, currentSpeed);
        }

        #endregion

        #region Scream & Alert Sequence

        /// <summary>
        /// Triggers the ghost to stand still and scream / roar before chasing the player.
        /// </summary>
        private void TriggerSpotPlayerScream(float distanceToPlayer)
        {
            if (_isScreaming) return;

            if (_screamCoroutine != null) StopCoroutine(_screamCoroutine);
            _screamCoroutine = StartCoroutine(SpotPlayerScreamRoutine(distanceToPlayer));
        }

        private IEnumerator SpotPlayerScreamRoutine(float initialDistance)
        {
            _isScreaming = true;

            // Immediately halt movement
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }

            // Set Idle / Scream stance in Animator
            if (_animator != null)
            {
                _animator.SetFloat(SpeedHash, 0f);
            }

            // Auto-resolve scream sound if null
            if (_screamSound == null)
            {
                #if UNITY_EDITOR
                _screamSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/V0/Audio/Ghost_Scream.mp3");
                #endif
                if (_screamSound == null)
                {
                    AudioClip[] allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
                    foreach (var c in allClips)
                    {
                        if (c.name.ToLower().Contains("ghost_scream") || c.name.ToLower().Contains("scream"))
                        {
                            _screamSound = c;
                            break;
                        }
                    }
                }
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1.0f; // 3D Spatial Audio
                _audioSource.minDistance = 2.0f;
                _audioSource.maxDistance = 28.0f;
            }

            // Play scream sound
            if (_screamSound != null)
            {
                if (_audioSource != null)
                {
                    _audioSource.PlayOneShot(_screamSound, 1.0f);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(_screamSound, transform.position, 1.0f);
                }
            }

            // Trigger Red Detected UI
            DetectionIndicatorUI.SetGlobalState(DetectionIndicatorUI.DetectionState.Detected);

            Debug.Log("<color=red>[EnemyAI]</color> Ghost SPOTTED the player! Screaming in horror before chasing!");

            // Stand still facing the player for the duration of the scream
            float timer = 0f;
            while (timer < _screamDuration)
            {
                timer += Time.deltaTime;
                if (_player != null)
                {
                    Vector3 lookDir = (_player.position - transform.position);
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.01f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
                    }
                }
                yield return null;
            }

            _isScreaming = false;

            // Now begin sprinting / chasing player!
            if (_player != null)
            {
                float currentDistance = Vector3.Distance(transform.position, _player.position);
                if (currentDistance <= _attackRadius && _canSeePlayer)
                {
                    _currentState = State.Attack;
                    if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
                }
                else
                {
                    _currentState = State.Chase;
                    if (_agent != null && _agent.isOnNavMesh)
                    {
                        _agent.isStopped = false;
                        if (!_isPhasing) _agent.speed = _chaseSpeed;
                        _agent.SetDestination(_player.position);
                    }
                }
            }
        }

        private void UpdateDetectionUI()
        {
            if (!enabled || !gameObject.activeInHierarchy)
            {
                DetectionIndicatorUI.SetGlobalState(DetectionIndicatorUI.DetectionState.None);
                return;
            }

            if (_currentState == State.Chase || _currentState == State.Attack || _isScreaming)
            {
                DetectionIndicatorUI.SetGlobalState(DetectionIndicatorUI.DetectionState.Detected);
            }
            else if (_currentState == State.Search)
            {
                DetectionIndicatorUI.SetGlobalState(DetectionIndicatorUI.DetectionState.Searching);
            }
            else
            {
                DetectionIndicatorUI.SetGlobalState(DetectionIndicatorUI.DetectionState.None);
            }
        }

        private void UpdateHeartbeatAudio()
        {
            if (_heartbeatAudioSource == null)
            {
                // Create dedicated 2D stereo AudioSource for the player's heartbeat
                GameObject playerObj = _player != null ? _player.gameObject : GameObject.FindWithTag("Player");
                if (playerObj != null)
                {
                    _heartbeatAudioSource = playerObj.GetComponent<AudioSource>();
                    if (_heartbeatAudioSource == null)
                    {
                        _heartbeatAudioSource = playerObj.AddComponent<AudioSource>();
                    }
                }
                else
                {
                    _heartbeatAudioSource = GetComponent<AudioSource>();
                    if (_heartbeatAudioSource == null)
                    {
                        _heartbeatAudioSource = gameObject.AddComponent<AudioSource>();
                    }
                }

                _heartbeatAudioSource.playOnAwake = false;
                _heartbeatAudioSource.spatialBlend = 0f; // 2D Stereo inside player's head
                _heartbeatAudioSource.loop = true;
            }

            if (_heartbeatAudioClip == null)
            {
                #if UNITY_EDITOR
                _heartbeatAudioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/V0/Audio/Heart_Beat.mp3");
                #endif
                if (_heartbeatAudioClip == null)
                {
                    _heartbeatAudioClip = Resources.Load<AudioClip>("Heart_Beat");
                    if (_heartbeatAudioClip == null)
                    {
                        AudioClip[] allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
                        foreach (var c in allClips)
                        {
                            if (c.name.ToLower().Contains("heart"))
                            {
                                _heartbeatAudioClip = c;
                                break;
                            }
                        }
                    }
                }
            }

            if (_heartbeatAudioClip != null && _heartbeatAudioSource.clip != _heartbeatAudioClip)
            {
                _heartbeatAudioSource.clip = _heartbeatAudioClip;
            }

            // Stop heartbeat immediately if ghost is disabled, inactive, in cutscene, or in spawn grace
            if (!enabled || !gameObject.activeInHierarchy || V0.Interaction.FlashlightController.IsGlobalCutscene || _spawnGraceTimer > 0f)
            {
                if (_heartbeatAudioSource.isPlaying) _heartbeatAudioSource.Stop();
                return;
            }

            if (_currentState == State.Chase || _currentState == State.Attack || _isScreaming)
            {
                // Hunting / Chasing: Fast, loud pounding heartbeat
                _heartbeatAudioSource.pitch = 1.20f;
                _heartbeatAudioSource.volume = _chaseHeartbeatVolume;
                if (!_heartbeatAudioSource.isPlaying && _heartbeatAudioSource.clip != null)
                {
                    _heartbeatAudioSource.Play();
                }
            }
            else if (_currentState == State.Search)
            {
                // Searching / Suspicious: Moderate tense heartbeat
                _heartbeatAudioSource.pitch = 1.0f;
                _heartbeatAudioSource.volume = _searchHeartbeatVolume;
                if (!_heartbeatAudioSource.isPlaying && _heartbeatAudioSource.clip != null)
                {
                    _heartbeatAudioSource.Play();
                }
            }
            else
            {
                // Patrol / Peaceful: Stop heartbeat
                if (_heartbeatAudioSource.isPlaying)
                {
                    _heartbeatAudioSource.Stop();
                }
            }
        }

        #endregion

        #region Door Phasing (Go Through Closed Doors)

        private void UpdateDoorPhasing()
        {
            if (_trackedDoors.Count == 0)
            {
                InitializeDoors();
            }

            TrackedDoor nearestDoor = null;
            float minDistance = float.MaxValue;

            Vector3 ghostPos = transform.position;

            foreach (TrackedDoor door in _trackedDoors)
            {
                if (door == null || door.Interactable == null || door.CenterTransform == null) continue;

                float dist = Vector3.Distance(ghostPos, door.CenterTransform.position);

                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestDoor = door;
                }
            }

            if (nearestDoor != null && minDistance <= _doorPhaseDistance)
            {
                // Door is OPEN -> do not phase! Move normally through doorway
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
            else if (_isPhasing && minDistance > _doorPhaseDistance + 0.6f)
            {
                EndDoorPhasing();
            }
        }

        private void StartDoorPhasing(TrackedDoor door)
        {
            _isPhasing = true;
            _currentPhasingDoor = door;
            _agent.speed = _doorPhasingSpeed;

            // Temporarily convert door colliders to triggers so the ghost's NavMeshAgent can walk through them
            foreach (Collider col in door.ChildColliders)
            {
                if (col != null)
                {
                    col.isTrigger = true;
                }
            }

            if (_modelTransform != null)
            {
                _phaseSequence?.Kill();
                _phaseSequence = DOTween.Sequence();

                _phaseSequence.Append(_modelTransform.DOScale(new Vector3(0.25f, 1.12f, 0.25f), 0.35f).SetEase(Ease.InOutSine));
                _phaseSequence.Join(_modelTransform.DOShakePosition(2f, new Vector3(0.06f, 0.02f, 0.06f), 10, 90, false, false));

                foreach (var kvp in _originalColors)
                {
                    Material mat = kvp.Key;
                    Color orig = kvp.Value;
                    Color spectralColor = new Color(orig.r * 0.4f, orig.g * 0.7f, orig.b * 1.3f, orig.a);
                    _phaseSequence.Join(mat.DOColor(spectralColor, "_BaseColor", 0.35f));
                }
            }

            Debug.Log("<color=cyan>[EnemyAI]</color> Ghost is phasing through closed door!");
        }

        private void EndDoorPhasing()
        {
            _isPhasing = false;

            if (_currentPhasingDoor != null)
            {
                // Restore door colliders to solid
                foreach (Collider col in _currentPhasingDoor.ChildColliders)
                {
                    if (col != null)
                    {
                        col.isTrigger = false;
                    }
                }
            }
            _currentPhasingDoor = null;

            if (_modelTransform != null)
            {
                _phaseSequence?.Kill();
                _phaseSequence = DOTween.Sequence();

                _phaseSequence.Append(_modelTransform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack));

                foreach (var kvp in _originalColors)
                {
                    Material mat = kvp.Key;
                    _phaseSequence.Join(mat.DOColor(kvp.Value, "_BaseColor", 0.35f));
                }
            }

            // Restore appropriate speed
            if (_currentState == State.Chase) _agent.speed = _chaseSpeed;
            else if (_currentState == State.Search) _agent.speed = _searchSpeed;
            else _agent.speed = _patrolSpeed;
        }

        #endregion

        #region Ground Snapping (Stairs & Slope Alignment)

        private void SnapModelToGround()
        {
            if (!_enableGroundSnapping || _modelTransform == null) return;

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

        #endregion

        private void OnDestroy()
        {
            _phaseSequence?.Kill();
        }

        private void OnDrawGizmosSelected()
        {
            // Yellow = Vision range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);

            // Blue = Sprint hearing range (loud footsteps)
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _sprintHearingRadius);

            // Cyan = Walk hearing range (normal footsteps)
            Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _walkHearingRadius);

            // Red = Attack range
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRadius);

            // Green = Last known/heard player position (during search)
            if (_currentState == State.Search)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_lastKnownPlayerPosition, 0.8f);
                Gizmos.DrawLine(transform.position, _lastKnownPlayerPosition);
            }

            // Vision Cone in Scene view
            Vector3 eyePos = transform.position + Vector3.up * _eyeHeight;
            Vector3 leftRay = Quaternion.Euler(0, -_fieldOfView * 0.5f, 0) * transform.forward;
            Vector3 rightRay = Quaternion.Euler(0, _fieldOfView * 0.5f, 0) * transform.forward;
            Gizmos.color = _canSeePlayer ? Color.red : Color.white;
            Gizmos.DrawRay(eyePos, leftRay * _detectionRadius);
            Gizmos.DrawRay(eyePos, rightRay * _detectionRadius);
        }
    }
}
