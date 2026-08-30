using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;
using V0.Interaction;
using V0.UI;

namespace V0.Player
{
    /// <summary>
    /// Player Health & Damage System:
    /// - Max Health: 100 HP
    /// - Ghost Attacks: 25 Damage per attack
    /// - Low Health (< 30% HP): Player is injured and sprints slower
    /// - Auto-Regeneration: Regenerates health over time ONLY when safe (not during searching/hunting mode)
    /// - Death: Triggers game over sequence and restarts scene smoothly
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        [Header("Health Attributes")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _currentHealth = 100f;
        [Tooltip("Health threshold (30 HP) below which player is injured and sprints slower")]
        [SerializeField] private float _lowHealthThreshold = 30f;

        [Header("Auto-Regeneration")]
        [Tooltip("Health points regenerated per second when safe")]
        [SerializeField] private float _regenRate = 6.0f;
        [Tooltip("Delay in seconds after taking damage before health starts regenerating")]
        [SerializeField] private float _regenDelay = 3.5f;

        [Header("Scene Transition on Death")]
        [Tooltip("Scene loaded when player dies")]
        [SerializeField] private string _mainMenuSceneName = "MainMenu";
        [SerializeField] private float _deathFadeDuration = 2.0f;

        [Header("Audio & Feedback")]
        [SerializeField] private AudioClip _hurtSound;
        [SerializeField] private AudioSource _audioSource;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public float HealthPercent => Mathf.Clamp01(_currentHealth / _maxHealth);
        public bool IsLowHealth => _currentHealth <= _lowHealthThreshold;
        public bool IsDead => _currentHealth <= 0f;

        public event System.Action<float, float> OnHealthChanged; // (current, max)
        public event System.Action<float> OnDamaged;              // (amount)
        public event System.Action OnDied;

        private float _timeSinceDamaged = 0f;
        private bool _isDead = false;
        private FirstPersonController _fpc;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _currentHealth = _maxHealth;
            _fpc = GetComponent<FirstPersonController>();

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                    _audioSource.playOnAwake = false;
                    _audioSource.spatialBlend = 0f;
                }
            }
        }

        private void Start()
        {
            DamageIndicatorUI.GetOrCreate();
            DamageIndicatorUI.Instance?.UpdateHealthDisplay(_currentHealth, _maxHealth);
        }

        private void Update()
        {
            if (_isDead) return;

            _timeSinceDamaged += Time.deltaTime;

            // Auto-Regeneration logic
            // Check if player is safe: NOT in searching or hunting mode!
            bool isSearchingOrHunting = DetectionIndicatorUI.Instance != null && 
                DetectionIndicatorUI.Instance.CurrentState != DetectionIndicatorUI.DetectionState.None;

            if (!isSearchingOrHunting && _timeSinceDamaged >= _regenDelay && _currentHealth < _maxHealth)
            {
                float prevHealth = _currentHealth;
                _currentHealth = Mathf.Min(_maxHealth, _currentHealth + _regenRate * Time.deltaTime);

                if (Mathf.Abs(_currentHealth - prevHealth) > 0.01f)
                {
                    OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
                    DamageIndicatorUI.Instance?.UpdateHealthDisplay(_currentHealth, _maxHealth);
                }
            }
        }

        /// <summary>
        /// Applies damage to the player from ghost attacks or hazards.
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (_isDead || FlashlightController.IsGlobalCutscene) return;

            _currentHealth = Mathf.Max(0f, _currentHealth - damage);
            _timeSinceDamaged = 0f;

            Debug.Log($"<color=red><b>[PlayerHealth]</b></color> Player took {damage} damage! Remaining Health: {_currentHealth:F0}/{_maxHealth:F0}");

            // Play hurt sound if assigned
            if (_hurtSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_hurtSound, 0.9f);
            }

            // Trigger UI damage flash & blood vignette
            DamageIndicatorUI.Instance?.OnPlayerHit(damage, HealthPercent, IsLowHealth);

            OnDamaged?.Invoke(damage);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// Heals the player by a specified amount.
        /// </summary>
        public void Heal(float amount)
        {
            if (_isDead) return;

            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            DamageIndicatorUI.Instance?.UpdateHealthDisplay(_currentHealth, _maxHealth);
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            Debug.Log("<color=red><b>[PlayerHealth]</b> PLAYER DIED! Restarting game...</color>");

            OnDied?.Invoke();

            if (_fpc != null)
            {
                _fpc.StopMovement();
                _fpc.enabled = false;
            }

            StartCoroutine(DeathSequenceRoutine());
        }

        private IEnumerator DeathSequenceRoutine()
        {
            FlashlightController.SetGlobalCutsceneMode(true);

            // Screen fades to black
            bool fadeDone = false;
            if (FadeScreen.Instance != null)
            {
                FadeScreen.Instance.FadeToBlack(_deathFadeDuration, () => fadeDone = true);
            }
            else
            {
                fadeDone = true;
            }

            yield return new WaitForSeconds(_deathFadeDuration + 0.2f);

            // Unlock cursor for the Main Menu
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Load MainMenu scene
            Debug.Log($"<color=green>[PlayerHealth]</color> Loading Main Menu scene: '{_mainMenuSceneName}'");
            SceneManager.LoadScene(_mainMenuSceneName);
        }
    }
}
