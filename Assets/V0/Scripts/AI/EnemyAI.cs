using System.Collections;
using UnityEngine;
using UnityEngine.AI;

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
        private Vector3 _spawnPosition;
        private int _currentWaypointIndex;
        private float _lastAttackTime = -999f;
        private float _patrolWaitTimer;
        private bool _isWaitingAtPoint;

        // Animator parameter hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        private bool _hasSpeedParam;
        private bool _hasAttackParam;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _spawnPosition = transform.position;

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_modelTransform == null && _animator != null)
            {
                _modelTransform = _animator.transform;
            }

            CheckAnimatorParameters();

            if (_player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    _player = playerObj.transform;
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

        /// <summary>
        /// Snaps model feet down to the actual stair steps or floor surface.
        /// Prevents floating when descending stairs on angled NavMesh ramps.
        /// </summary>
        private void SnapModelToGround()
        {
            if (!_enableGroundSnapping || _modelTransform == null) return;

            Ray ray = new Ray(transform.position + Vector3.up * 1f, Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(ray, 3f, _groundLayers, QueryTriggerInteraction.Ignore);

            if (hits.Length == 0) return;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                // Ignore self and child colliders
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                    continue;

                float targetLocalY = (hit.point.y - transform.position.y) + _feetOffset;
                Vector3 localPos = _modelTransform.localPosition;
                localPos.y = Mathf.Lerp(localPos.y, targetLocalY, Time.deltaTime * 20f);
                _modelTransform.localPosition = localPos;
                break;
            }
        }

        private void HandlePatrol(float distanceToPlayer)
        {
            if (distanceToPlayer <= _detectionRadius)
            {
                _isWaitingAtPoint = false;
                _currentState = State.Chase;
                _agent.isStopped = false;
                _agent.speed = _chaseSpeed;
                return;
            }

            _agent.speed = _patrolSpeed;

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
                _agent.speed = _patrolSpeed;
                SetNextPatrolDestination();
                return;
            }

            _agent.isStopped = false;
            _agent.speed = _chaseSpeed;
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
                _agent.speed = _chaseSpeed;
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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRadius);
        }
    }
}
