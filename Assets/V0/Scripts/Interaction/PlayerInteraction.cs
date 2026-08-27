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
    /// Dynamically highlights interactable objects using URP Rendering Layer Masks with Free Outline.
    /// </summary>
    [RequireComponent(typeof(StarterAssetsInputs))]
    public class PlayerInteraction : MonoBehaviour
    {
        public enum HighlightTrigger
        {
            CrosshairHover,
            ProximityRadius,
            Both
        }

        [Header("Interaction Settings")]
        [Tooltip("Max reach distance for interaction")]
        [SerializeField] private float _interactDistance = 3.0f;

        [Tooltip("Layer mask for interactable objects")]
        [SerializeField] private LayerMask _interactableLayer;

        [Header("Outline / Highlight Settings (Free Outline)")]
        [Tooltip("Enable dynamic outline highlighting on interactable objects")]
        [SerializeField] private bool _enableOutline = true;

        [Tooltip("When to show outline: CrosshairHover (aiming at it), ProximityRadius (near it), or Both")]
        [SerializeField] private HighlightTrigger _highlightMode = HighlightTrigger.Both;

        [Tooltip("Distance around player to highlight interactables when using ProximityRadius or Both")]
        [SerializeField] private float _proximityRadius = 4.0f;

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
                    // Fallback to Layer 6
                    _interactableLayer = 1 << 6;
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

            if (Physics.Raycast(ray, out RaycastHit hit, _interactDistance, _interactableLayer, QueryTriggerInteraction.Ignore))
            {
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                _currentInteractable = interactable;
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

            // 1. Crosshair Hover target (aiming directly at an interactable)
            if (_highlightMode == HighlightTrigger.CrosshairHover || _highlightMode == HighlightTrigger.Both)
            {
                if (_currentInteractable != null)
                {
                    MonoBehaviour mb = _currentInteractable as MonoBehaviour;
                    if (mb != null)
                    {
                        AddRenderersToActive(mb.gameObject);
                    }
                }
            }

            // 2. Proximity check (walking close to any interactable in the scene)
            if (_highlightMode == HighlightTrigger.ProximityRadius || _highlightMode == HighlightTrigger.Both)
            {
                Vector3 origin = _playerCamera != null ? _playerCamera.transform.position : transform.position;
                Collider[] hits = Physics.OverlapSphere(origin, _proximityRadius, _interactableLayer);

                foreach (Collider col in hits)
                {
                    IInteractable interactable = col.GetComponentInParent<IInteractable>();
                    if (interactable != null)
                    {
                        MonoBehaviour mb = interactable as MonoBehaviour;
                        if (mb != null)
                        {
                            AddRenderersToActive(mb.gameObject);
                        }
                    }
                }
            }

            // Apply outline layer to all active renderers
            foreach (Renderer r in _currentlyActiveRenderers)
            {
                if (r == null) continue;
                if (!_highlightedRenderers.ContainsKey(r))
                {
                    _highlightedRenderers[r] = r.renderingLayerMask;
                    r.renderingLayerMask |= _outlineRenderingLayer;

                    if (_debugLogs)
                    {
                        Debug.Log($"<color=cyan>[PlayerInteraction]</color> Highlighted: <b>{r.gameObject.name}</b> (New mask: {r.renderingLayerMask})");
                    }
                }
            }

            // Remove outline from renderers no longer targeted / in range
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

            if (_enableOutline && (_highlightMode == HighlightTrigger.ProximityRadius || _highlightMode == HighlightTrigger.Both))
            {
                Gizmos.color = Color.cyan;
                Vector3 origin = cam != null ? cam.transform.position : transform.position;
                Gizmos.DrawWireSphere(origin, _proximityRadius);
            }
        }
    }
}
