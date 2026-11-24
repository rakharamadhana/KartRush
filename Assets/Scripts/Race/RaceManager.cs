using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KartRush
{
    /// <summary>
    /// Countdown, the player's lap timing (ordered checkpoints), and the whole field's standings.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public KartController kart;            // the player's kart
        public KartCamera cam;
        public int totalLaps = 3;
        public float countdownSeconds = 3f;
        public List<Checkpoint> checkpoints = new List<Checkpoint>();
        public List<Racer> racers = new List<Racer>();
        public Vector3[] racingLine;           // track centerline, in driving order (AI / standings)
        public Vector3 startPosition;
        public float startYaw;

        public enum State { Countdown, Racing, Finished }
        public State state { get; private set; }

        public float CountdownRemaining { get; private set; }
        public int CurrentLap { get; private set; }          // 1-based while racing
        public float RaceTime { get; private set; }
        public float LapTime { get; private set; }
        public float BestLap { get; private set; } = -1f;
        public List<float> LapTimes { get; } = new List<float>();
        public float GoFlashTimer { get; private set; }

        public List<Racer> Standings { get; } = new List<Racer>();
        public int PlayerPosition { get; private set; } = 1;
        public Racer PlayerRacer { get; private set; }

        readonly List<Racer> finishedOrder = new List<Racer>();
        int nextCheckpoint;
        bool crossedStartOnce;

        void Start() => Restart();

        public void Restart()
        {
            state = State.Countdown;
            CountdownRemaining = countdownSeconds;
            CurrentLap = 1;
            RaceTime = LapTime = 0f;
            BestLap = -1f;
            LapTimes.Clear();
            finishedOrder.Clear();
            nextCheckpoint = 0;
            crossedStartOnce = false;
            GoFlashTimer = 0f;
            lastBeep = -1;
            Sfx.Build();

            ItemBox.ResetAll();
            Banana.ClearAll();
            Shell.ClearAll();

            PlayerRacer = racers.FirstOrDefault(r => r.isPlayer);
            foreach (var r in racers) r.ResetRace();
            UpdateStandings();

            if (cam != null) cam.Snap();
        }

        public static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard" };
        static readonly float[] DifficultySpeed = { 0.90f, 0.96f, 1.03f };
        /// <summary>0 = Easy, 1 = Normal, 2 = Hard. Scales every rival's top speed.</summary>
        public int difficulty = 1;
        public float AiSpeedFactor => DifficultySpeed[Mathf.Clamp(difficulty, 0, 2)];

        /// <summary>True while the title screen is up: the countdown does not run.</summary>
        public bool Waiting { get; set; }
        int lastBeep = -1;

        void Update()
        {
            if (Waiting) return;
            switch (state)
            {
                case State.Countdown:
                    CountdownRemaining -= Time.deltaTime;
                    int whole = Mathf.CeilToInt(Mathf.Max(CountdownRemaining, 0f));
                    if (CountdownRemaining > 0f && whole != lastBeep)
                    {
                        lastBeep = whole;
                        Sfx.Play(Sfx.Beep, 0.8f);
                    }
                    if (CountdownRemaining <= 0f)
                    {
                        state = State.Racing;
                        foreach (var r in racers) r.kart.canDrive = true;
                        GoFlashTimer = 1.2f;
                        Sfx.Play(Sfx.Go, 0.9f);
                    }
                    break;
                case State.Racing:
                    RaceTime += Time.deltaTime;
                    LapTime += Time.deltaTime;
                    if (GoFlashTimer > 0f) GoFlashTimer -= Time.deltaTime;
                    break;
                case State.Finished:
                    RaceTime += 0f;
                    break;
            }

            if (state != State.Countdown) UpdateStandings();

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.enterKey.wasPressedThisFrame && state == State.Finished) Restart();
        }

        float finishClock;

        void UpdateStandings()
        {
            if (state == State.Finished) finishClock += Time.deltaTime; else finishClock = RaceTime;

            foreach (var r in racers)
                if (!r.Finished && r.Lap >= totalLaps) FinishRacer(r);

            Standings.Clear();
            Standings.AddRange(finishedOrder);
            Standings.AddRange(racers.Where(r => !r.Finished).OrderByDescending(r => r.Progress));
            if (PlayerRacer != null) PlayerPosition = Standings.IndexOf(PlayerRacer) + 1;
        }

        void FinishRacer(Racer r)
        {
            if (r.Finished) return;
            r.Finished = true;
            r.FinishTime = finishClock;
            finishedOrder.Add(r);
            if (!r.isPlayer) r.kart.canDrive = false;   // AI parks after the flag
        }

        public void OnCheckpoint(KartController who, Checkpoint cp)
        {
            if (state != State.Racing || who != kart) return;
            if (cp.index != nextCheckpoint) return; // wrong order / shortcut

            nextCheckpoint = (nextCheckpoint + 1) % checkpoints.Count;
            kart.SetCheckpointRespawn(cp.transform.position, cp.transform.eulerAngles.y);

            if (cp.index != 0) return;

            // Crossing the start/finish line
            if (!crossedStartOnce)
            {
                crossedStartOnce = true;
                LapTime = 0f;
                return;
            }

            LapTimes.Add(LapTime);
            if (BestLap < 0f || LapTime < BestLap) BestLap = LapTime;
            LapTime = 0f;

            if (LapTimes.Count >= totalLaps)
            {
                state = State.Finished;
                kart.canDrive = false;
                finishClock = RaceTime;
                if (PlayerRacer != null) FinishRacer(PlayerRacer);
                UpdateStandings();
                Sfx.Play(Sfx.Finish, 0.9f);
            }
            else
            {
                CurrentLap = LapTimes.Count + 1;
                Sfx.Play(Sfx.Lap, 0.8f);
            }
        }

        public static string FormatTime(float t)
        {
            if (t < 0f) return "--:--.---";
            int m = (int)(t / 60f);
            float s = t - m * 60f;
            return $"{m:00}:{s:00.000}";
        }

        public static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }
    }
}
