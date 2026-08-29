using UnityEngine;
using DG.Tweening;
using System;
using System.Collections.Generic;

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

        [Header("Ghost & Trigger Activation")]
        [Tooltip("List of GameObjects to SetActive(true) when this key is picked up. Drag any triggers, spawners, etc. here.")]
        [SerializeField] private List<GameObject> _objectsToActivateOnPickup = new List<GameObject>();

        [Tooltip("Optional reference to the Ghost GameObject. If null, auto-finds 'Ghost' in the scene.")]
        [SerializeField] private GameObject _ghostToActivate;

        [Tooltip("Activate the Ghost immediately on key pickup? (False recommended so Ghost spawns during GhostTrigger cutscene)")]
        [SerializeField] private bool _spawnGhostOnPickup = false;

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

            // Capture locals for lambda closure
            string capturedKeyId = _keyId;
            List<GameObject> capturedObjects = new List<GameObject>(_objectsToActivateOnPickup);
            GameObject capturedGhost = _ghostToActivate;
            bool capturedSpawnGhost = _spawnGhostOnPickup;

            Sequence pickupSeq = DOTween.Sequence();
            pickupSeq.Append(transform.DOMove(targetPosition, _pickupDuration).SetEase(_moveEase));
            pickupSeq.Join(transform.DOScale(transform.localScale * 0.8f, _pickupDuration).SetEase(_moveEase));
            pickupSeq.OnComplete(() =>
            {
                // Register this specific key as collected
                if (!string.IsNullOrEmpty(capturedKeyId))
                {
                    _collectedKeyIds.Add(capturedKeyId);
                }
                _collectedKeyInstances.Add(this);

                OnKeyCollected?.Invoke(capturedKeyId);
                Debug.Log($"<color=yellow>[KeyPickup]</color> Collected key: '{capturedKeyId}'");

                // Activate all objects in the list on pickup
                if (capturedObjects != null && capturedObjects.Count > 0)
                {
                    foreach (GameObject obj in capturedObjects)
                    {
                        if (obj != null)
                        {
                            obj.SetActive(true);
                            Debug.Log($"<color=green>[KeyPickup]</color> Key '{capturedKeyId}' collected! Activated '{obj.name}'!");
                        }
                    }
                }
                // Fallback: DrawingRoomKey auto-finds and activates GhostTrigger if no list assigned
                else if (string.Equals(capturedKeyId, "DrawingRoomKey", StringComparison.OrdinalIgnoreCase))
                {
                    ActivateGhostTrigger();
                }

                // Update Game Objective based on item collected
                string lowerKey = capturedKeyId != null ? capturedKeyId.ToLower() : "";
                if (lowerKey.Contains("bed") || lowerKey.Contains("crowbar") || lowerKey.Contains("haligan"))
                {
                    V0.UI.ObjectiveManager.SetObjective("Retrieve Master key from the bedroom");
                }
                else if (lowerKey.Contains("drawing") || lowerKey.Contains("master"))
                {
                    V0.UI.ObjectiveManager.SetObjective("Get the Chainsaw");
                }
                else if (lowerKey.Contains("chain"))
                {
                    V0.UI.ObjectiveManager.SetObjective("Free the man");
                }

                // Spawn / Activate Ghost immediately (if enabled)
                if (capturedSpawnGhost && capturedGhost != null)
                {
                    capturedGhost.SetActive(true);
                    Debug.Log("<color=red>[KeyPickup]</color> Ghost has awakened and is now hunting!");
                }

                Destroy(gameObject);
            });
        }

        /// <summary>
        /// Reliably searches the scene for 'GhostTrigger' (including inactive objects) and sets it active.
        /// </summary>
        public static void ActivateGhostTrigger()
        {
            UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            GameObject[] rootObjects = activeScene.GetRootGameObjects();
            foreach (GameObject root in rootObjects)
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in transforms)
                {
                    if (t.name == "GhostTrigger")
                    {
                        t.gameObject.SetActive(true);
                        Debug.Log("<color=green>[KeyPickup]</color> DrawingRoomKey picked up -> Successfully activated inactive 'GhostTrigger'!");
                        return;
                    }
                }
            }

            Debug.LogWarning("[KeyPickup] DrawingRoomKey picked up, but could not find 'GhostTrigger' in scene!");
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
