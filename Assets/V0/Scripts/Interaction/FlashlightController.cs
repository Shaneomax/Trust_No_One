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

        private static FlashlightController _instance;
        public static FlashlightController Instance => _instance;

        private bool _hasFlashlight = false;
        private bool _isLightOn = false;
        private bool _isInCutscene = false;
        private bool _wasActiveBeforeCutscene = false;

        public bool HasFlashlight => _hasFlashlight;
        public bool IsLightOn => _isLightOn;
        public bool IsInCutscene => _isInCutscene;

        private void Awake()
        {
            _instance = this;

            if (_input == null)
            {
                _input = GetComponent<StarterAssetsInputs>();
            }

            if (_flashlightObject == null)
            {
                Transform t = transform.Find("Flashligh") ?? transform.Find("Flashlight") ?? transform.Find("FlashLight");
                if (t != null)
                {
                    _flashlightObject = t.gameObject;
                }
            }

            if (_flashlightLight == null && _flashlightObject != null)
            {
                _flashlightLight = _flashlightObject.GetComponentInChildren<Light>(true);
            }
        }

        private void Start()
        {
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
        /// Global helper to disable/hide the flashlight during cutscenes.
        /// </summary>
        public static void SetGlobalCutsceneMode(bool inCutscene)
        {
            if (_instance != null)
            {
                _instance.SetCutsceneMode(inCutscene);
            }
            else
            {
                FlashlightController controller = Object.FindFirstObjectByType<FlashlightController>();
                if (controller != null)
                {
                    controller.SetCutsceneMode(inCutscene);
                }
            }
        }

        /// <summary>
        /// Temporarily disables the flashlight GameObject and Light during cutscenes,
        /// and restores it cleanly when the cutscene ends.
        /// </summary>
        public void SetCutsceneMode(bool inCutscene)
        {
            _isInCutscene = inCutscene;

            if (inCutscene)
            {
                _wasActiveBeforeCutscene = _hasFlashlight && _flashlightObject != null && _flashlightObject.activeSelf;

                if (_flashlightObject != null)
                {
                    _flashlightObject.SetActive(false);
                }

                if (_flashlightLight != null)
                {
                    _flashlightLight.enabled = false;
                }

                Debug.Log("<color=yellow>[FlashlightController]</color> Flashlight hidden for cutscene.");
            }
            else
            {
                if (_hasFlashlight && _wasActiveBeforeCutscene)
                {
                    if (_flashlightObject != null)
                    {
                        _flashlightObject.SetActive(true);
                    }

                    if (_flashlightLight != null)
                    {
                        _flashlightLight.enabled = _isLightOn;
                    }

                    Debug.Log("<color=yellow>[FlashlightController]</color> Flashlight restored after cutscene.");
                }
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
            if (!_hasFlashlight || _input == null || _isInCutscene) return;

            if (_input.flashLight)
            {
                _input.flashLight = false; // Reset input trigger
                SetLight(!_isLightOn);
            }
        }

        public void SetLight(bool state)
        {
            _isLightOn = state;

            if (_flashlightLight != null && !_isInCutscene)
            {
                _flashlightLight.enabled = _isLightOn;
            }

            Debug.Log(_isLightOn ? "Flashlight turned ON" : "Flashlight turned OFF");
        }
    }
}
