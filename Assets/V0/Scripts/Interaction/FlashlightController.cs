using UnityEngine;
using StarterAssets;

namespace V0.Interaction
{
    /// <summary>
    /// Attach this to PlayerCapsule.
    /// Manages the child flashlight GameObject and toggles the light via StarterAssetsInputs.
    /// </summary>
    public class FlashlightController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to StarterAssetsInputs on the player")]
        [SerializeField] private StarterAssetsInputs _input;

        [Header("Flashlight Child Object")]
        [Tooltip("The child flashlight GameObject on the player (held in hand / on camera)")]
        [SerializeField] private GameObject _flashlightObject;

        [Tooltip("The Light component for the flashlight. If left empty, automatically searches children.")]
        [SerializeField] private Light _flashlightLight;

        private bool _hasFlashlight = false;
        private bool _isLightOn = false;

        public bool HasFlashlight => _hasFlashlight;
        public bool IsLightOn => _isLightOn;

        private void Start()
        {
            if (_input == null)
            {
                _input = GetComponent<StarterAssetsInputs>();
            }

            // Keep child flashlight disabled until player picks it up in the world
            if (_flashlightObject != null)
            {
                _flashlightObject.SetActive(false);
            }

            if (_flashlightLight != null)
            {
                _flashlightLight.enabled = false;
            }
        }

        /// <summary>
        /// Called when the player interacts with a flashlight in the world.
        /// </summary>
        public void PickupFlashlight()
        {
            _hasFlashlight = true;

            // Enable child flashlight GameObject on the player
            if (_flashlightObject != null)
            {
                _flashlightObject.SetActive(true);
            }

            // Automatically find light component if not manually assigned
            if (_flashlightLight == null && _flashlightObject != null)
            {
                _flashlightLight = _flashlightObject.GetComponentInChildren<Light>(true);
            }

            // Turn light on upon pickup
            SetLight(true);
            Debug.Log("Player picked up the flashlight!");
        }

        private void Update()
        {
            if (!_hasFlashlight || _input == null) return;

            if (_input.flashLight)
            {
                _input.flashLight = false; // Reset input trigger
                SetLight(!_isLightOn);
            }
        }

        public void SetLight(bool state)
        {
            _isLightOn = state;

            if (_flashlightLight != null)
            {
                _flashlightLight.enabled = _isLightOn;
            }

            Debug.Log(_isLightOn ? "Flashlight turned ON" : "Flashlight turned OFF");
        }
    }
}
