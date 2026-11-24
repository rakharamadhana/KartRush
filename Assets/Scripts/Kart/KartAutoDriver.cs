using UnityEngine;

namespace KartRush
{
    /// <summary>
    /// Drives a kart along the track's racing line using the kart's scripted-input hook.
    /// Base for AI opponents; also used for automated lap tests.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class KartAutoDriver : MonoBehaviour
    {
        public float lookAheadMeters = 16f;
        public float steerAngleForFullLock = 25f;
        public float cornerSlowAngle = 40f;
        [Range(0.5f, 1f)] public float skill = 1f;   // < 1 = slower / sloppier opponent

        KartController kart;
        RaceManager race;
        int index;

        void Start()
        {
            kart = GetComponent<KartController>();
            race = FindFirstObjectByType<RaceManager>();
            kart.useScriptedInput = true;
            index = ClosestIndex();
        }

        int ClosestIndex()
        {
            var pts = race.racingLine;
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
            {
                float d = (pts[i] - kart.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        void Update()
        {
            var pts = race.racingLine;
            if (pts == null || pts.Length < 3) return;
            int n = pts.Length;

            // Track progress along the line (search a small window around the last index).
            int bestIdx = index; float bestD = float.MaxValue;
            for (int o = -4; o <= 16; o++)
            {
                int j = ((index + o) % n + n) % n;
                float d = (pts[j] - kart.transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; bestIdx = j; }
            }
            index = bestIdx;

            // Aim at a point a little way down the line.
            float acc = 0f; int t = index;
            while (acc < lookAheadMeters)
            {
                int nx = (t + 1) % n;
                acc += Vector3.Distance(pts[t], pts[nx]);
                t = nx;
            }

            Vector3 to = pts[t] - kart.transform.position; to.y = 0f;
            float angle = Vector3.SignedAngle(kart.Heading, to, Vector3.up);

            kart.scriptedSteer = Mathf.Clamp(angle / steerAngleForFullLock, -1f, 1f);
            kart.scriptedThrottle = (Mathf.Abs(angle) > cornerSlowAngle ? 0.5f : 1f) * skill;
            kart.scriptedBrake = 0f;
            kart.scriptedDrift = false;
        }
    }
}
