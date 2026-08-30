using UnityEngine;

namespace TrustNoOne.AI
{
    /// <summary>
    /// Enemy Health System (Ghost / Enemies):
    /// - Max Health: 100 HP
    /// </summary>
    public class EnemyHealth : MonoBehaviour
    {
        [Header("Enemy Health Attributes")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _currentHealth = 100f;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public bool IsDead => _currentHealth <= 0f;

        public event System.Action<float, float> OnHealthChanged;
        public event System.Action OnDied;

        private void Awake()
        {
            _currentHealth = _maxHealth;
        }

        public void TakeDamage(float damage)
        {
            if (IsDead) return;

            _currentHealth = Mathf.Max(0f, _currentHealth - damage);
            Debug.Log($"<color=orange>[EnemyHealth]</color> {gameObject.name} took {damage} damage! Remaining Health: {_currentHealth}/{_maxHealth}");

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            Debug.Log($"<color=red>[EnemyHealth]</color> {gameObject.name} was defeated!");
            OnDied?.Invoke();
        }
    }
}
