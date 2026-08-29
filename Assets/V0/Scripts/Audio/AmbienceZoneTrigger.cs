using UnityEngine;

namespace V0.Audio
{
    /// <summary>
    /// Trigger zone placed over the house interior or doorways.
    /// When player enters, triggers HouseAmbienceManager to crossfade to inside ambience.
    /// When player leaves, triggers crossfade back to outside ambience.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AmbienceZoneTrigger : MonoBehaviour
    {
        [Tooltip("True = this zone represents the Inside House interior. False = Outdoor zone.")]
        [SerializeField] private bool _isInsideZone = true;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponentInParent<StarterAssets.FirstPersonController>() != null)
            {
                HouseAmbienceManager.SetInside(_isInsideZone);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponentInParent<StarterAssets.FirstPersonController>() != null)
            {
                // When exiting an inside zone, switch back to outside
                if (_isInsideZone)
                {
                    HouseAmbienceManager.SetInside(false);
                }
            }
        }
    }
}
