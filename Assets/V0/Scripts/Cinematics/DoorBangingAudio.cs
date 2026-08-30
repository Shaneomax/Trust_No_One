using UnityEngine;
using DG.Tweening;
using StarterAssets;

namespace V0.Cinematics
{
    /// <summary>
    /// Directional 3D Audio component attached to the Chained Room Door.
    /// - Dynamic 3D Spatial Audio: Panned in stereo 3D relative to player orientation.
    /// - Distance-Varying Volume:
    ///   * Right in front of door (<= 3m): 100% full loud volume (1.0).
    ///   * Inside house corridor (10-15m): ~0.65 volume.
    ///   * Outside porch / far distance (25-35m): ~0.25 - 0.35 volume.
    ///   * Far distance (>= 50m): 0 volume.
    /// - Starts on FirstTrigger (ChainedRoomCutscene) and stops on SecondTrigger (StrangerDialogueCutscene).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class DoorBangingAudio : MonoBehaviour
    {
        public static DoorBangingAudio Instance { get; private set; }

        [Header("Audio Settings")]
        [Tooltip("Audio clip for frantic door banging / knocking from inside the room")]
        [SerializeField] private AudioClip _bangingAudioClip;

        [Range(0f, 1f)]
        [SerializeField] private float _maxVolume = 1.0f;

        [Range(0f, 1f)]
        [SerializeField] private float _minVolumeFar = 0.20f;

        [Header("Distance Attenuation Settings")]
        [Tooltip("Distance in meters where volume is at maximum 100%")]
        [SerializeField] private float _closeDistance = 3.0f;

        [Tooltip("Distance in meters where volume fades to far level")]
        [SerializeField] private float _farDistance = 40.0f;

        [Tooltip("Cutoff distance where sound completely fades out")]
        [SerializeField] private float _maxCutoffDistance = 60.0f;

        [Range(0f, 1f)]
        [Tooltip("1 = 100% 3D spatial directional audio")]
        [SerializeField] private float _spatialBlend = 1.0f;

        private AudioSource _audioSource;
        private Tweener _fadeTween;
        private bool _isBanging = false;
        private Transform _playerTransform;

        public bool IsBanging => _isBanging;

        public static DoorBangingAudio GetOrCreate()
        {
            if (Instance != null) return Instance;

            DoorBangingAudio found = Object.FindFirstObjectByType<DoorBangingAudio>();
            if (found != null)
            {
                Instance = found;
                return Instance;
            }

            // Find Chained Door (DoorInteractable with RequiredKeyId == "ChainSaw" or SM_Door_interior_01_LOD0)
            V0.Interaction.DoorInteractable[] doors = Object.FindObjectsByType<V0.Interaction.DoorInteractable>(FindObjectsSortMode.None);
            GameObject chainedDoorObj = null;
            foreach (var d in doors)
            {
                if (d != null && d.RequiredKeyId != null && d.RequiredKeyId.Equals("ChainSaw", System.StringComparison.OrdinalIgnoreCase))
                {
                    chainedDoorObj = d.gameObject;
                    break;
                }
            }

            if (chainedDoorObj == null)
            {
                chainedDoorObj = GameObject.Find("SM_Door_interior_01_LOD0");
            }

            if (chainedDoorObj != null)
            {
                Instance = chainedDoorObj.GetComponent<DoorBangingAudio>() ?? chainedDoorObj.AddComponent<DoorBangingAudio>();
                return Instance;
            }

            return null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();

            ConfigureAudioSource();
            AutoResolveAudioClip();
        }

        private void ConfigureAudioSource()
        {
            if (_audioSource == null) return;
            _audioSource.playOnAwake = false;
            _audioSource.loop = true;
            _audioSource.spatialBlend = _spatialBlend;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = _closeDistance;
            _audioSource.maxDistance = _maxCutoffDistance;
            _audioSource.dopplerLevel = 0f;
        }

        private void Update()
        {
            if (!_isBanging || _audioSource == null || !_audioSource.isPlaying) return;

            // Dynamically calculate distance from player to door
            if (_playerTransform == null)
            {
                FirstPersonController fpc = Object.FindFirstObjectByType<FirstPersonController>();
                if (fpc != null) _playerTransform = fpc.transform;
                else if (Camera.main != null) _playerTransform = Camera.main.transform;
            }

            if (_playerTransform != null)
            {
                float distance = Vector3.Distance(transform.position, _playerTransform.position);
                float targetVol;

                if (distance <= _closeDistance)
                {
                    targetVol = _maxVolume;
                }
                else if (distance >= _maxCutoffDistance)
                {
                    targetVol = 0f;
                }
                else if (distance >= _farDistance)
                {
                    float t = (distance - _farDistance) / (_maxCutoffDistance - _farDistance);
                    targetVol = Mathf.Lerp(_minVolumeFar, 0f, t);
                }
                else
                {
                    float t = (distance - _closeDistance) / (_farDistance - _closeDistance);
                    // Smooth curve: louder in medium distance, tapering to far volume
                    targetVol = Mathf.Lerp(_maxVolume, _minVolumeFar, Mathf.SmoothStep(0f, 1f, t));
                }

                // Smoothly update volume
                _audioSource.volume = Mathf.MoveTowards(_audioSource.volume, targetVol, Time.deltaTime * 3.0f);
            }
        }

        private void OnEnable()
        {
            ChainedRoomCutscene chainedCutscene = Object.FindFirstObjectByType<ChainedRoomCutscene>();
            if (chainedCutscene != null)
            {
                chainedCutscene.OnCutsceneStarted += HandleFirstTriggerStarted;
                chainedCutscene.OnCutsceneCompleted += HandleFirstTriggerCompleted;
            }

            StrangerDialogueCutscene strangerCutscene = Object.FindFirstObjectByType<StrangerDialogueCutscene>();
            if (strangerCutscene != null)
            {
                strangerCutscene.OnCutsceneStarted += HandleSecondTriggerStarted;
            }
        }

        private void OnDisable()
        {
            ChainedRoomCutscene chainedCutscene = Object.FindFirstObjectByType<ChainedRoomCutscene>();
            if (chainedCutscene != null)
            {
                chainedCutscene.OnCutsceneStarted -= HandleFirstTriggerStarted;
                chainedCutscene.OnCutsceneCompleted -= HandleFirstTriggerCompleted;
            }

            StrangerDialogueCutscene strangerCutscene = Object.FindFirstObjectByType<StrangerDialogueCutscene>();
            if (strangerCutscene != null)
            {
                strangerCutscene.OnCutsceneStarted -= HandleSecondTriggerStarted;
            }
        }

        private void HandleFirstTriggerStarted()
        {
            StartBanging();
        }

        private void HandleFirstTriggerCompleted()
        {
            if (!_isBanging)
            {
                StartBanging();
            }
        }

        private void HandleSecondTriggerStarted()
        {
            StopBanging();
        }

        /// <summary>
        /// Starts looping directional 3D door banging sound from this door's exact position.
        /// </summary>
        public void StartBanging()
        {
            if (_isBanging && _audioSource != null && _audioSource.isPlaying) return;
            _isBanging = true;

            ConfigureAudioSource();
            AutoResolveAudioClip();

            if (_audioSource != null && _bangingAudioClip != null)
            {
                _audioSource.clip = _bangingAudioClip;
                
                // Initial volume based on distance
                if (_playerTransform != null)
                {
                    float d = Vector3.Distance(transform.position, _playerTransform.position);
                    float t = Mathf.Clamp01((d - _closeDistance) / (_farDistance - _closeDistance));
                    _audioSource.volume = Mathf.Lerp(_maxVolume, _minVolumeFar, t);
                }
                else
                {
                    _audioSource.volume = _minVolumeFar;
                }

                if (!_audioSource.isPlaying)
                {
                    _audioSource.Play();
                }

                Debug.Log("<color=cyan><b>[DoorBangingAudio]</b> Started directional 3D door banging audio on chained door!</color>");
            }
            else
            {
                Debug.LogWarning("[DoorBangingAudio] Could not start banging: AudioSource or AudioClip is missing!");
            }
        }

        /// <summary>
        /// Stops the door banging sound smoothly when the player reaches SecondTrigger.
        /// </summary>
        public void StopBanging()
        {
            if (!_isBanging) return;
            _isBanging = false;

            if (_audioSource != null && _audioSource.isPlaying)
            {
                _fadeTween?.Kill();
                _fadeTween = _audioSource.DOFade(0f, 0.5f).OnComplete(() =>
                {
                    _audioSource.Stop();
                });

                Debug.Log("<color=green><b>[DoorBangingAudio]</b> Stopped door banging audio (Player reached SecondTrigger).</color>");
            }
        }

        public void AutoResolveAudioClip()
        {
            if (_bangingAudioClip != null) return;

            #if UNITY_EDITOR
            string[] searchPaths = new string[]
            {
                "Assets/V0/Audio/Door_Banging.wav",
                "Assets/V0/Audio/DoorBanging.mp3",
                "Assets/V0/Audio/Door_Bang.mp3",
                "Assets/V0/Audio/Door_Knock.mp3",
                "Assets/V0/Audio/DoorKnock.mp3"
            };

            foreach (string path in searchPaths)
            {
                AudioClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    _bangingAudioClip = clip;
                    break;
                }
            }
            #endif

            if (_bangingAudioClip == null)
            {
                AudioClip[] allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
                foreach (var c in allClips)
                {
                    string n = c.name.ToLower();
                    if (n.Contains("banging") || n.Contains("door_bang") || n.Contains("knock"))
                    {
                        _bangingAudioClip = c;
                        break;
                    }
                }
            }
        }
    }
}
