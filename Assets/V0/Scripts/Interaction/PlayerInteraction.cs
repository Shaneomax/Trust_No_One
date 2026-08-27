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

        // Tracks original rendering layer masks so we can cleanly restore them
        private readonly Dictionary<Renderer, uint> _highlightedRenderers = new Dictionary<Renderer, uint>();
        private readonly HashSet<Renderer> _currentlyActiveRenderers = new HashSet<Renderer>();

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

        private void Update()
        {
            UpdateCurrentInteractable();
            UpdateOutlines();
            HandleInteraction();
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
                ClearAllOutlines();
                return;
            }

            _currentlyActiveRenderers.Clear();

            // ONLY highlight when player is in interaction range AND looking directly at the object!
            if (_currentInteractable != null)
            {
                MonoBehaviour mb = _currentInteractable as MonoBehaviour;
                if (mb != null)
                {
                    AddRenderersToActive(mb.gameObject);
                }
            }

            // Apply outline layer to the active target
            foreach (Renderer r in _currentlyActiveRenderers)
            {
                if (r == null) continue;
                if (!_highlightedRenderers.ContainsKey(r))
                {
                    _highlightedRenderers[r] = r.renderingLayerMask;
                    r.renderingLayerMask |= _outlineRenderingLayer;

                    if (_debugLogs)
                    {
                        Debug.Log($"<color=cyan>[PlayerInteraction]</color> Highlighted: <b>{r.gameObject.name}</b>");
                    }
                }
            }

            // Remove outline when looking away or stepping out of range
            List<Renderer> toRemove = null;
            foreach (var kvp in _highlightedRenderers)
            {
                Renderer r = kvp.Key;
                if (r == null)
                {
                    toRemove ??= new List<Renderer>();
                    toRemove.Add(r);
                    continue;
                }

                if (!_currentlyActiveRenderers.Contains(r))
                {
                    r.renderingLayerMask = kvp.Value; // Restore original mask
                    toRemove ??= new List<Renderer>();
                    toRemove.Add(r);

                    if (_debugLogs)
                    {
                        Debug.Log($"<color=gray>[PlayerInteraction]</color> Un-highlighted: <b>{r.gameObject.name}</b>");
                    }
                }
            }

            if (toRemove != null)
            {
                foreach (Renderer r in toRemove)
                {
                    _highlightedRenderers.Remove(r);
                }
            }
        }

        private void AddRenderersToActive(GameObject target)
        {
            if (target == null) return;
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r != null && r.enabled)
                {
                    _currentlyActiveRenderers.Add(r);
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
            _currentlyActiveRenderers.Clear();
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
