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

        [Header("Detection & Combat")]
        [Tooltip("Distance at which the enemy notices the player")]
        [SerializeField] private float _detectionRadius = 12f;

        [Tooltip("Distance at which the enemy stops and attacks the player")]
        [SerializeField] private float _attackRadius = 2f;

        [Tooltip("Cooldown between consecutive attacks (seconds)")]
        [SerializeField] private float _attackCooldown = 2f;

        [Header("Movement Speeds")]
        [SerializeField] private float _patrolSpeed = 2f;
        [SerializeField] private float _chaseSpeed = 4f;

        [Header("Patrol Settings")]
        [Tooltip("Optional predefined patrol points. If empty, enemy roams randomly near spawn.")]
        [SerializeField] private Transform[] _patrolWaypoints;
        [SerializeField] private float _patrolWanderRadius = 15f;
        [SerializeField] private float _patrolWaitDuration = 2.5f;

        [Header("References (Auto-detected if unassigned)")]
        [SerializeField] private Transform _player;
        [SerializeField] private Animator _animator;

        private NavMeshAgent _agent;
        private Vector3 _spawnPosition;
        private int _currentWaypointIndex;
        private float _lastAttackTime = -999f;
        private float _patrolWaitTimer;
        private bool _isWaitingAtPoint;

        // Animator hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _spawnPosition = transform.position;

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    _player = playerObj.transform;
                }
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
                // Try finding player if spawned dynamically
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

        private void HandlePatrol(float distanceToPlayer)
        {
            // Player spotted -> Chase
            if (distanceToPlayer <= _detectionRadius)
            {
                _isWaitingAtPoint = false;
                _currentState = State.Chase;
                _agent.isStopped = false;
                _agent.speed = _chaseSpeed;
                return;
            }

            _agent.speed = _patrolSpeed;

            // Check if reached current destination
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
            // Within attack range -> Attack
            if (distanceToPlayer <= _attackRadius)
            {
                _currentState = State.Attack;
                _agent.isStopped = true;
                return;
            }

            // Player escaped beyond detection -> Return to patrol
            if (distanceToPlayer > _detectionRadius * 1.4f)
            {
                _currentState = State.Patrol;
                _agent.speed = _patrolSpeed;
                SetNextPatrolDestination();
                return;
            }

            // Continue chasing player
            _agent.isStopped = false;
            _agent.speed = _chaseSpeed;
            _agent.SetDestination(_player.position);
        }

        private void HandleAttack(float distanceToPlayer)
        {
            _agent.isStopped = true;

            // Smoothly look at player horizontally while attacking
            Vector3 lookDirection = (_player.position - transform.position);
            lookDirection.y = 0f;
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 8f);
            }

            // If player moved outside attack range -> Resume chase
            if (distanceToPlayer > _attackRadius * 1.2f)
            {
                _currentState = State.Chase;
                _agent.isStopped = false;
                return;
            }

            // Attack cooldown check
            if (Time.time >= _lastAttackTime + _attackCooldown)
            {
                PerformAttack();
            }
        }

        private void PerformAttack()
        {
            _lastAttackTime = Time.time;

            if (_animator != null)
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
                // Roam randomly on NavMesh
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
            if (_animator == null) return;

            float currentSpeed = _agent.velocity.magnitude;
            bool isMoving = currentSpeed > 0.1f && !_agent.isStopped;

            _animator.SetFloat(SpeedHash, currentSpeed);
            _animator.SetBool(IsMovingHash, isMoving);
        }

        private void OnDrawGizmosSelected()
        {
            // Yellow = Detection / Chase radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);

            // Red = Attack radius
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _attackRadius);
        }
    }
}
