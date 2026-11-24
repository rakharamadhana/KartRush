using UnityEngine;

namespace KartRush
{
    /// <summary>
    /// Race bookkeeping for one kart (player or AI): where it is along the racing line,
    /// which lap it is on, and whether it has finished. Used to rank the field.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class Racer : MonoBehaviour
    {
        public string displayName = "Racer";
        public bool isPlayer;
        public Color color = Color.white;
        public RaceManager race;
        public Vector3 startPosition;
        public float startYaw;

        public KartController kart { get; private set; }

        /// <summary>-1 before the first crossing of the line, 0 during lap 1, ... totalLaps when done.</summary>
        public int Lap { get; private set; } = -1;
        /// <summary>Monotonic distance measure in line samples: Lap * N + position along the line.</summary>
        public float Progress { get; private set; }
        public bool Finished;
        public float FinishTime;

        int index;

        void Awake() => kart = GetComponent<KartController>();

        public void ResetRace()
        {
            Lap = -1;
            Finished = false;
            FinishTime = 0f;
            kart.canDrive = false;
            kart.PlaceAt(startPosition, startYaw);
            kart.SetCheckpointRespawn(startPosition, startYaw);
            index = ClosestIndex(startPosition);
            Progress = -race.racingLine.Length + index;
        }

        int ClosestIndex(Vector3 p)
        {
            var pts = race.racingLine;
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
            {
                float d = FlatSqr(pts[i] - p);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        static float FlatSqr(Vector3 v) => v.x * v.x + v.z * v.z;

        void Update()
        {
            if (race == null || race.racingLine == null || race.racingLine.Length < 3) return;
            var pts = race.racingLine;
            int n = pts.Length;
            Vector3 p = kart.transform.position;

            int best = index; float bd = float.MaxValue;
            for (int o = -6; o <= 14; o++)
            {
                int j = ((index + o) % n + n) % n;
                float d = FlatSqr(pts[j] - p);
                if (d < bd) { bd = d; best = j; }
            }
            if (bd > 900f) best = ClosestIndex(p);   // teleported (respawn): relocate

            int prev = index;
            index = best;
            if (prev > n * 3 / 4 && index < n / 4) Lap++;          // crossed the line forwards
            else if (prev < n / 4 && index > n * 3 / 4) Lap--;     // crossed it backwards

            Vector3 a = pts[index], b = pts[(index + 1) % n];
            Vector3 ab = b - a; ab.y = 0f;
            Vector3 ap = p - a; ap.y = 0f;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
            Progress = Lap * n + index + t;
        }
    }
}
