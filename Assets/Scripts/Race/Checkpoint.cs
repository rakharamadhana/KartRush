using UnityEngine;

namespace KartRush
{
    /// <summary>Trigger volume across the track. Index 0 is the start/finish line.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Checkpoint : MonoBehaviour
    {
        public int index;
        public RaceManager race;

        void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (race == null) return;
            var kart = other.GetComponentInParent<KartController>();
            if (kart == null && other.attachedRigidbody != null)
                kart = race.kart != null && other.attachedRigidbody == race.kart.sphere ? race.kart : null;
            if (kart != null) race.OnCheckpoint(kart, this);
        }
    }
}
