using System.Collections.Generic;
using UnityEngine;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace V0.Interaction
{
    /// <summary>
    /// Player interaction component. Raycasts forward from player camera
    /// and invokes IInteractable.Interact() when the interact input is triggered.
    /// Shows Free Outline ONLY when the player is in interaction range AND looking directly at the object.
    /// </summary>
    [RequireComponent(typeof(StarterAssetsInputs))]
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [Tooltip("Max reach distance for interaction and outline visibility")]
        [SerializeField] private float _interactDistance = 3.0f;

        [Tooltip("Layer mask for interactable objects")]
        [SerializeField] private LayerMask _interactableLayer;

        [Header("Outline / Highlight Settings (Free Outline)")]
        [Tooltip("Enable outline highlight when looking at an interactable in range")]
        [SerializeField] private bool _enableOutline = true;

        [Tooltip("The URP Rendering Layer bit for Free Outline. Default is 2 (Light Layer 1 / Layer 2).")]
        [SerializeField] private uint _outlineRenderingLayer = 2;

        [Tooltip("Print debug logs to console when items are highlighted")]
        [SerializeField] private bool _debugLogs = true;

        [Header("References")]
        [SerializeField] private Camera _playerCamera;
        [SerializeField] private StarterAssetsInputs _input;

        private IInteractable _currentInteractable;
        private IInteractable _lastInteractable;

        // Tracks original rendering layer masks so we can cleanly restore them
        private readonly Dictionary<Renderer, uint> _highlightedRenderers = new Dictionary<Renderer, uint>();
        private readonly List<Renderer> _cachedTargetRenderers = new List<Renderer>();

        public IInteractable CurrentInteractable => _currentInteractable;

        private void Awake()
        {
            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
            }

            // Auto-assign Interactable layer if unassigned (0)
            if (_interactableLayer.value == 0)
            {
                _interactableLayer = LayerMask.GetMask("Interactable");
                if (_interactableLayer.value == 0)
                {
                    _interactableLayer = 1 << 6; // Layer 6: Interactable
                }
            }
        }

        private void Start()
        {
            V0.UI.InteractionPromptUI.GetOrCreate();
        }

        private void Update()
        {
            UpdateCurrentInteractable();
            UpdateOutlines();
            UpdatePromptUI();
            HandleInteraction();
        }

        private void UpdatePromptUI()
        {
            if (_currentInteractable != null)
            {
                string prompt = _currentInteractable.InteractionPrompt;
                V0.UI.InteractionPromptUI.Instance?.ShowPrompt(prompt);
            }
            else
            {
                V0.UI.InteractionPromptUI.Instance?.HidePrompt();
            }
        }

        private void UpdateCurrentInteractable()
        {
            if (_playerCamera == null)
            {
                _playerCamera = Camera.main;
                if (_playerCamera == null) return;
            }

            Ray ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);

            // 1. Direct raycast
            if (Physics.Raycast(ray, out RaycastHit hit, _interactDistance, _interactableLayer, QueryTriggerInteraction.Ignore))
            {
                _currentInteractable = hit.collider.GetComponentInParent<IInteractable>();
            }
            // 2. Slight sphere cast (radius 0.15m) so small pickups like flashlights/keys are easy to target
            else if (Physics.SphereCast(ray, 0.15f, out RaycastHit sphereHit, _interactDistance, _interactableLayer, QueryTriggerInteraction.Ignore))
            {
                _currentInteractable = sphereHit.collider.GetComponentInParent<IInteractable>();
            }
            else
            {
                _currentInteractable = null;
            }
        }

        private void UpdateOutlines()
        {
            if (!_enableOutline)
            {
                if (_highlightedRenderers.Count > 0) ClearAllOutlines();
                return;
            }

            // High performance: Only update renderers when the looked-at interactable actually changes!
            if (_currentInteractable == _lastInteractable) return;
            _lastInteractable = _currentInteractable;

            // Clear previous highlight
            ClearAllOutlines();

            // Highlight new target
            if (_currentInteractable != null)
            {
                MonoBehaviour mb = _currentInteractable as MonoBehaviour;
                if (mb != null)
                {
                    _cachedTargetRenderers.Clear();
                    mb.GetComponentsInChildren(true, _cachedTargetRenderers);

                    foreach (Renderer r in _cachedTargetRenderers)
                    {
                        if (r != null && r.enabled && !_highlightedRenderers.ContainsKey(r))
                        {
                            _highlightedRenderers[r] = r.renderingLayerMask;
                            r.renderingLayerMask |= _outlineRenderingLayer;

                            if (_debugLogs)
                            {
                                Debug.Log($"<color=cyan>[PlayerInteraction]</color> Highlighted: <b>{r.gameObject.name}</b>");
                            }
                        }
                    }
                }
            }
        }

        private void ClearAllOutlines()
        {
            foreach (var kvp in _highlightedRenderers)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.renderingLayerMask = kvp.Value;
                }
            }
            _highlightedRenderers.Clear();
            _cachedTargetRenderers.Clear();
        }

        private void HandleInteraction()
        {
            if (_input == null) return;

            if (_input.interact)
            {
                _input.interact = false;

                if (_currentInteractable != null)
                {
                    _currentInteractable.Interact();
                }
            }
        }

        private void OnDisable()
        {
            ClearAllOutlines();
        }

        private void OnDrawGizmosSelected()
        {
            Camera cam = _playerCamera != null ? _playerCamera : Camera.main;
            if (cam != null)
            {
                Gizmos.color = _currentInteractable != null ? Color.green : Color.red;
                Gizmos.DrawRay(cam.transform.position, cam.transform.forward * _interactDistance);
            }
        }
    }
}
