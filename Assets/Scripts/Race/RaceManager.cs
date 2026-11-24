using System.Collections.Generic;
using UnityEngine;

namespace KartRush
{
    /// <summary>Countdown, lap counting (ordered checkpoints), timing and finish state for the player's kart.</summary>
    public class RaceManager : MonoBehaviour
    {
        public KartController kart;
        public KartCamera cam;
        public int totalLaps = 3;
        public float countdownSeconds = 3f;
        public List<Checkpoint> checkpoints = new List<Checkpoint>();
        public Vector3[] racingLine;   // track centerline, in driving order (AI / tests)
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
            nextCheckpoint = 0;
            crossedStartOnce = false;
            GoFlashTimer = 0f;

            kart.canDrive = false;
            kart.PlaceAt(startPosition, startYaw);
            kart.SetCheckpointRespawn(startPosition, startYaw);
            if (cam != null) cam.Snap();
        }

        void Update()
        {
            switch (state)
            {
                case State.Countdown:
                    CountdownRemaining -= Time.deltaTime;
                    if (CountdownRemaining <= 0f)
                    {
                        state = State.Racing;
                        kart.canDrive = true;
                        GoFlashTimer = 1.2f;
                    }
                    break;
                case State.Racing:
                    RaceTime += Time.deltaTime;
                    LapTime += Time.deltaTime;
                    if (GoFlashTimer > 0f) GoFlashTimer -= Time.deltaTime;
                    break;
                case State.Finished:
                    break;
            }

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.enterKey.wasPressedThisFrame && state == State.Finished) Restart();
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
            }
            else
            {
                CurrentLap = LapTimes.Count + 1;
            }
        }

        public static string FormatTime(float t)
        {
            if (t < 0f) return "--:--.---";
            int m = (int)(t / 60f);
            float s = t - m * 60f;
            return $"{m:00}:{s:00.000}";
        }
    }
}
