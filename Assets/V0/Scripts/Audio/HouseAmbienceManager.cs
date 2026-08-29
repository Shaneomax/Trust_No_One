using UnityEngine;
using DG.Tweening;

namespace V0.Audio
{
    /// <summary>
    /// Manages global atmospheric audio with smooth crossfading between Outside Wind and Inside House Ambience.
    /// Supports volume blending, bleed-through, and trigger zones.
    /// </summary>
    public class HouseAmbienceManager : MonoBehaviour
    {
        public static HouseAmbienceManager Instance { get; private set; }

        [Header("Audio Clips")]
        [Tooltip("Looping ambience played when player is outdoors")]
        [SerializeField] private AudioClip _outsideAmbienceClip;

        [Tooltip("Looping ambience played when player is indoors")]
        [SerializeField] private AudioClip _insideAmbienceClip;

        [Header("Volume Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float _outsideMaxVolume = 0.60f;

        [Tooltip("Maximum volume when inside the house (0.45 = subtle atmospheric, not overpowering)")]
        [Range(0f, 1f)]
        [SerializeField] private float _insideMaxVolume = 0.45f;

        [Tooltip("Subtle outside wind bleed heard while inside the house (0 = completely muted, 0.08 = distant muffled wind)")]
        [Range(0f, 1f)]
        [SerializeField] private float _outsideBleedWhenInside = 0.08f;

        [Header("Transition Settings")]
        [Tooltip("Duration of the crossfade transition between indoor and outdoor audio (seconds)")]
        [SerializeField] private float _fadeDuration = 2.0f;

        [Tooltip("Easing curve for the volume transition")]
        [SerializeField] private Ease _fadeEase = Ease.InOutSine;

        [Header("Initial State")]
        [Tooltip("Start with Outside ambience active on game start")]
        [SerializeField] private bool _startOutside = true;

        private AudioSource _outsideSource;
        private AudioSource _insideSource;
        private bool _isCurrentlyInside = false;

        public bool IsCurrentlyInside => _isCurrentlyInside;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SetupAudioSources();
        }

        private void SetupAudioSources()
        {
            // Outside Audio Source (Looping)
            if (_outsideSource == null)
            {
                _outsideSource = gameObject.AddComponent<AudioSource>();
                _outsideSource.loop = true;
                _outsideSource.playOnAwake = false;
                _outsideSource.spatialBlend = 0f; // 2D Global Stereo Ambience
                _outsideSource.volume = 0f;
            }

            // Inside Audio Source (Looping, subtle atmospheric)
            if (_insideSource == null)
            {
                _insideSource = gameObject.AddComponent<AudioSource>();
                _insideSource.loop = true;
                _insideSource.playOnAwake = false;
                _insideSource.spatialBlend = 0f; // 2D Global Stereo Ambience
                _insideSource.volume = 0f;
            }

            if (_outsideAmbienceClip != null)
            {
                _outsideSource.clip = _outsideAmbienceClip;
            }
            if (_insideAmbienceClip != null)
            {
                _insideSource.clip = _insideAmbienceClip;
            }
        }

        private void Start()
        {
            // Auto-load OutsideSound if unassigned
            if (_outsideAmbienceClip == null)
            {
                AudioClip foundOutside = Resources.Load<AudioClip>("OutsideSound");
                if (foundOutside != null)
                {
                    _outsideAmbienceClip = foundOutside;
                    _outsideSource.clip = foundOutside;
                }
            }

            // Auto-load Inside_House if unassigned
            if (_insideAmbienceClip == null)
            {
                AudioClip foundInside = Resources.Load<AudioClip>("Inside_House");
                if (foundInside != null)
                {
                    _insideAmbienceClip = foundInside;
                    _insideSource.clip = foundInside;
                }
            }

            if (_outsideSource.clip != null && !_outsideSource.isPlaying)
            {
                _outsideSource.Play();
            }
            if (_insideSource.clip != null && !_insideSource.isPlaying)
            {
                _insideSource.Play();
            }

            _isCurrentlyInside = !_startOutside;
            ApplyVolumesImmediately(_isCurrentlyInside);
        }

        private void ApplyVolumesImmediately(bool inside)
        {
            _outsideSource.DOKill();
            _insideSource.DOKill();

            if (inside)
            {
                _outsideSource.volume = _outsideBleedWhenInside;
                _insideSource.volume = _insideMaxVolume;
            }
            else
            {
                _outsideSource.volume = _outsideMaxVolume;
                _insideSource.volume = 0f;
            }
        }

        /// <summary>
        /// Crossfades between outside and inside ambience.
        /// </summary>
        public static void SetInside(bool inside)
        {
            if (Instance != null)
            {
                Instance.TransitionToState(inside);
            }
        }

        public void TransitionToState(bool inside)
        {
            if (_isCurrentlyInside == inside) return;
            _isCurrentlyInside = inside;

            float targetOutsideVol = inside ? _outsideBleedWhenInside : _outsideMaxVolume;
            float targetInsideVol = inside ? _insideMaxVolume : 0f;

            _outsideSource.DOKill();
            _insideSource.DOKill();

            _outsideSource.DOFade(targetOutsideVol, _fadeDuration).SetEase(_fadeEase);
            _insideSource.DOFade(targetInsideVol, _fadeDuration).SetEase(_fadeEase);

            Debug.Log($"<color=cyan><b>[Ambience]</b></color> Crossfading to: <b>{(inside ? "INSIDE HOUSE" : "OUTSIDE")}</b>");
        }

        /// <summary>
        /// Dynamically assign or update the audio clips at runtime.
        /// </summary>
        public void SetAudioClips(AudioClip outsideClip, AudioClip insideClip)
        {
            if (outsideClip != null)
            {
                _outsideAmbienceClip = outsideClip;
                if (_outsideSource != null)
                {
                    _outsideSource.clip = outsideClip;
                    if (!_outsideSource.isPlaying) _outsideSource.Play();
                }
            }

            if (insideClip != null)
            {
                _insideAmbienceClip = insideClip;
                if (_insideSource != null)
                {
                    _insideSource.clip = insideClip;
                    if (!_insideSource.isPlaying) _insideSource.Play();
                }
            }
        }
    }
}
