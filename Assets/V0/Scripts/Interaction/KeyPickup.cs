using UnityEngine;
using DG.Tweening;
using System;

namespace V0.Interaction
{
    /// <summary>
    /// Put this on any world key that the player can pick up by pressing E.
    /// Uses DOTween to smoothly fly towards the player on pickup (same as FlashlightPickup).
    /// Once picked up, registers the key in memory and activates the Ghost.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class KeyPickup : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [Tooltip("The prompt displayed when looking at the key")]
        [SerializeField] private string _interactionPrompt = "Take Key";

        [Tooltip("Unique ID or name for this key (e.g. DrawingRoomKey, BedroomKey)")]
        [SerializeField] private string _keyId = "DrawingRoomKey";

        [Header("Pickup Animation")]
        [Tooltip("Duration to move towards the player")]
        [SerializeField] private float _pickupDuration = 0.35f;

        [Tooltip("Easing curve for moving towards the player")]
        [SerializeField] private Ease _moveEase = Ease.InQuad;

        [Tooltip("Height offset added to player position (chest height)")]
        [SerializeField] private float _playerHeightOffset = 1.0f;

        [Header("Ghost Activation Trigger")]
        [Tooltip("Optional reference to the Ghost GameObject. If null, auto-finds 'Ghost' in the scene.")]
        [SerializeField] private GameObject _ghostToActivate;

        [Tooltip("Activate the Ghost when this key is picked up?")]
        [SerializeField] private bool _spawnGhostOnPickup = true;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioClip _pickupSound;

        private static readonly System.Collections.Generic.HashSet<string> _collectedKeyIds = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Collections.Generic.HashSet<KeyPickup> _collectedKeyInstances = new System.Collections.Generic.HashSet<KeyPickup>();

        private bool _isBeingPickedUp = false;

        /// <summary>
        /// Global event fired when any key is collected.
        /// </summary>
        public static event Action<string> OnKeyCollected;

        /// <summary>
        /// Checks if a specific key ID (e.g. "DrawingRoomKey", "AtticKey") has been collected.
        /// </summary>
        public static bool HasKey(string keyId)
        {
            if (string.IsNullOrEmpty(keyId)) return false;
            return _collectedKeyIds.Contains(keyId);
        }

        /// <summary>
        /// Checks if a specific KeyPickup reference has been collected.
        /// </summary>
        public static bool HasKey(KeyPickup key)
        {
            if (key == null) return false;
            return _collectedKeyInstances.Contains(key) || (!string.IsNullOrEmpty(key.KeyId) && _collectedKeyIds.Contains(key.KeyId));
        }

        /// <summary>
        /// Returns true if the player holds at least one key.
        /// </summary>
        public static bool HasAnyKey => _collectedKeyIds.Count > 0;

        public string InteractionPrompt => _interactionPrompt;
        public string KeyId => _keyId;

        private void Reset()
        {
            // Ensure collider exists
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
                sphere.radius = 0.25f;
            }

            // Ensure layer is set to Interactable (Layer 6)
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1)
            {
                gameObject.layer = interactableLayer;
            }
        }

        private void Awake()
        {
            // Auto-find ghost if not assigned
            if (_ghostToActivate == null && _spawnGhostOnPickup)
            {
                GameObject foundGhost = GameObject.Find("Ghost");
                if (foundGhost != null)
                {
                    _ghostToActivate = foundGhost;
                }
            }
        }

        public void Interact()
        {
            if (_isBeingPickedUp) return;
            _isBeingPickedUp = true;

            // Disable all colliders immediately so it can't be interacted with multiple times
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider c in colliders)
            {
                if (c != null) c.enabled = false;
            }

            // Find player position
            Transform playerTransform = null;
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                Camera mainCam = Camera.main;
                if (mainCam != null) playerTransform = mainCam.transform;
            }

            Vector3 targetPosition = playerTransform != null
                ? playerTransform.position + Vector3.up * _playerHeightOffset
                : transform.position;

            // Play pickup audio if assigned
            if (_pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(_pickupSound, transform.position, 1.0f);
            }

            // Smoothly move towards player & scale down slightly (same animation as Flashlight)
            Sequence pickupSeq = DOTween.Sequence();
            pickupSeq.Append(transform.DOMove(targetPosition, _pickupDuration).SetEase(_moveEase));
            pickupSeq.Join(transform.DOScale(transform.localScale * 0.8f, _pickupDuration).SetEase(_moveEase));
            pickupSeq.OnComplete(() =>
            {
                // Register this specific key as collected
                if (!string.IsNullOrEmpty(_keyId))
                {
                    _collectedKeyIds.Add(_keyId);
                }
                _collectedKeyInstances.Add(this);

                OnKeyCollected?.Invoke(_keyId);
                Debug.Log($"<color=yellow>[KeyPickup]</color> Collected key: '{_keyId}' ({gameObject.name})");

                // Spawn / Activate Ghost
                if (_spawnGhostOnPickup && _ghostToActivate != null)
                {
                    _ghostToActivate.SetActive(true);
                    Debug.Log("<color=red>[KeyPickup]</color> Ghost has awakened and is now hunting!");
                }

                Destroy(gameObject);
            });
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
