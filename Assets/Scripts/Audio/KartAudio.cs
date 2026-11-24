using UnityEngine;

namespace KartRush
{
    /// <summary>Engine pitch, drift screech, and boost / spin-out sounds for one kart.</summary>
    [RequireComponent(typeof(KartController))]
    public class KartAudio : MonoBehaviour
    {
        KartController kart;
        AudioSource engine, screech;
        bool isPlayer, wasBoosting, wasSpinning;

        void Start()
        {
            Sfx.Build();
            kart = GetComponent<KartController>();
            var racer = GetComponent<Racer>();
            isPlayer = racer != null && racer.isPlayer;

            engine = MakeSource(Sfx.Engine);
            screech = MakeSource(Sfx.Screech);
            engine.volume = 0f; screech.volume = 0f;
        }

        AudioSource MakeSource(AudioClip clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = isPlayer ? 0f : 1f;
            s.minDistance = 6f;
            s.maxDistance = 70f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.dopplerLevel = 0f;
            s.Play();
            return s;
        }

        void Update()
        {
            float speed01 = Mathf.Clamp01(Mathf.Abs(kart.ForwardSpeed) / kart.boostSpeed);
            float master = isPlayer ? 0.35f : 0.45f;
            engine.volume = (kart.canDrive ? 0.35f + 0.65f * speed01 : 0.25f) * master;
            engine.pitch = 0.7f + speed01 * 1.5f + (kart.IsBoosting ? 0.2f : 0f);

            bool slide = kart.IsDrifting && kart.IsGrounded;
            screech.volume = Mathf.MoveTowards(screech.volume, slide ? (isPlayer ? 0.3f : 0.2f) : 0f, 4f * Time.deltaTime);

            Vector3? at = isPlayer ? (Vector3?)null : transform.position;
            if (kart.IsBoosting && !wasBoosting) Sfx.Play(Sfx.Boost, 0.7f, at);
            if (kart.IsSpinning && !wasSpinning) Sfx.Play(Sfx.Spin, 0.8f, at);
            wasBoosting = kart.IsBoosting;
            wasSpinning = kart.IsSpinning;
        }
    }
}
