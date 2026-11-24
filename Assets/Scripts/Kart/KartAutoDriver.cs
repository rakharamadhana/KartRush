using UnityEngine;

namespace KartRush
{
    /// <summary>
    /// AI driver: follows the racing line (with a lane offset), slows for sharp corners,
    /// gets a gentle rubber-band so the pack stays close to the player, and un-sticks itself.
    /// Uses the kart's scripted-input hook, so it drives exactly like a player.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class KartAutoDriver : MonoBehaviour
    {
        public float lookAheadMeters = 16f;
        public float steerAngleForFullLock = 25f;
        public float cornerSlowAngle = 40f;
        [Range(0.5f, 1.1f)] public float skill = 1f;   // top-speed fraction; < 1 = slower opponent
        public float laneOffset = 0f;                  // metres from the centerline (+ = right)
        public bool rubberBand = true;

        KartController kart;
        Racer racer;
        RaceManager race;
        float baseMaxSpeed;
        int index;
        float stuckTimer;

        void Start()
        {
            kart = GetComponent<KartController>();
            racer = GetComponent<Racer>();
            race = FindFirstObjectByType<RaceManager>();
            kart.useScriptedInput = true;
            baseMaxSpeed = kart.maxSpeed;
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

            if (!kart.canDrive)
            {
                kart.scriptedThrottle = kart.scriptedBrake = kart.scriptedSteer = 0f;
                kart.scriptedDrift = false;
                stuckTimer = 0f;
                index = ClosestIndex();
                return;
            }

            // Track progress along the line (search a small window around the last index).
            int bestIdx = index; float bestD = float.MaxValue;
            for (int o = -4; o <= 16; o++)
            {
                int j = ((index + o) % n + n) % n;
                float d = (pts[j] - kart.transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; bestIdx = j; }
            }
            index = bestD > 900f ? ClosestIndex() : bestIdx;

            // Aim at a point a little way down the line, shifted into our lane.
            float acc = 0f; int t = index;
            while (acc < lookAheadMeters)
            {
                int nx = (t + 1) % n;
                acc += Vector3.Distance(pts[t], pts[nx]);
                t = nx;
            }
            Vector3 tan = (pts[(t + 1) % n] - pts[(t - 1 + n) % n]).normalized;
            Vector3 rightDir = Vector3.Cross(Vector3.up, tan);
            Vector3 target = pts[t] + rightDir * laneOffset;

            Vector3 to = target - kart.transform.position; to.y = 0f;
            float angle = Vector3.SignedAngle(kart.Heading, to, Vector3.up);

            // Rubber band: opponents behind the player speed up a little, ones far ahead ease off.
            float band = 1f;
            if (rubberBand && racer != null && race.PlayerRacer != null && race.PlayerRacer != racer)
            {
                float metersAhead = (racer.Progress - race.PlayerRacer.Progress) * (TrackMetersPerSample());
                band = 1f + Mathf.Clamp(-metersAhead / 300f, -0.06f, 0.08f);
            }
            float diff = (racer != null && racer.isPlayer) ? 1f : race.AiSpeedFactor;
            kart.maxSpeed = baseMaxSpeed * skill * band * diff;

            kart.scriptedSteer = Mathf.Clamp(angle / steerAngleForFullLock, -1f, 1f);
            kart.scriptedThrottle = Mathf.Abs(angle) > cornerSlowAngle ? 0.5f : 1f;
            kart.scriptedBrake = 0f;
            kart.scriptedDrift = false;

            // Stuck against a wall / another kart? Put the kart back on the line.
            if (kart.SpeedKmh < 4f) stuckTimer += Time.deltaTime; else stuckTimer = 0f;
            if (stuckTimer > 2.5f)
            {
                stuckTimer = 0f;
                Vector3 p = pts[index] + rightDir * laneOffset * 0.5f + Vector3.up * 1.2f;
                Vector3 f = pts[(index + 1) % n] - pts[index];
                kart.PlaceAt(p, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg);
            }
        }

        float metersPerSample;
        float TrackMetersPerSample()
        {
            if (metersPerSample > 0f) return metersPerSample;
            var pts = race.racingLine;
            float len = 0f;
            for (int i = 0; i < pts.Length; i++) len += Vector3.Distance(pts[i], pts[(i + 1) % pts.Length]);
            metersPerSample = len / pts.Length;
            return metersPerSample;
        }
    }
}
