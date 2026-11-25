using System;
using UnityEngine;

namespace KartRush
{
    /// <summary>All sounds are synthesized in code at startup, so the project needs no audio files.</summary>
    public static class Sfx
    {
        const int SR = 22050;

        public static AudioClip Beep, Go, Pickup, UseItem, Boost, Spin, Lap, Finish, MenuMove, MenuSelect, ChargeUp, Engine, Screech, Music;

        public static void Build()
        {
            if (Engine != null) return;

            Beep = Make("Beep", 0.2f, (t, p) => Square(880f * t) * 0.35f * (1f - p));
            Go = Make("Go", 0.55f, (t, p) => Square(1320f * t) * 0.35f * (1f - p * p));
            Pickup = Make("Pickup", 0.28f, (t, p) =>
            {
                float[] f = { 660f, 880f, 1100f, 1320f };
                int i = Mathf.Min((int)(t / 0.07f), 3);
                return Sine(f[i] * t) * 0.4f * (1f - p * 0.6f);
            });
            UseItem = Make("UseItem", 0.22f, (t, p) => Square((300f + 600f * p) * t) * 0.3f * (1f - p));
            Boost = Make("Boost", 0.7f, (t, p) =>
                (Noise() * 0.35f + Saw((180f + 400f * p) * t) * 0.3f) * Mathf.Sin(p * Mathf.PI) * 0.9f);
            Spin = Make("Spin", 0.8f, (t, p) =>
                Square((650f - 500f * p + 40f * Mathf.Sin(t * 40f)) * t) * 0.3f * (1f - p));
            Lap = Make("Lap", 0.3f, (t, p) => Sine((t < 0.12f ? 880f : 1175f) * t) * 0.4f * (1f - p * 0.5f));
            Finish = Make("Finish", 1.2f, (t, p) =>
            {
                float[] f = { 523f, 659f, 784f, 1047f };
                int i = Mathf.Min((int)(t / 0.15f), 3);
                float local = i < 3 ? 1f - (t - i * 0.15f) / 0.3f : 1f - (t - 0.45f) / 0.75f;
                return (Square(f[i] * t) * 0.18f + Sine(f[i] * t) * 0.25f) * Mathf.Clamp01(local);
            });
            ChargeUp = Make("ChargeUp", 0.14f, (t, p) => Square((t < 0.06f ? 1000f : 1500f) * t) * 0.3f * (1f - p * 0.4f));
            MenuMove = Make("MenuMove", 0.06f, (t, p) => Square(600f * t) * 0.25f * (1f - p));
            MenuSelect = Make("MenuSelect", 0.14f, (t, p) => Square((900f + 400f * p) * t) * 0.3f * (1f - p * 0.5f));

            // Loopable engine: 60 Hz fundamental over exactly 0.5 s => whole cycles, no click.
            Engine = Make("Engine", 0.5f, (t, p) =>
                (Saw(60f * t) * 0.45f + Saw(120f * t + 0.3f) * 0.25f + Square(30f * t) * 0.15f), true);

            float lp = 0f;
            Screech = Make("Screech", 1f, (t, p) =>
            {
                float n = Noise();
                lp += 0.25f * (n - lp);
                return (n - lp) * 0.45f;
            }, true);

            Music = BuildMusic();
        }

        // ------------------------------------------------------------ one-shot playback

        public static void Play(AudioClip clip, float volume = 1f, Vector3? at = null, float pitch = 1f)
        {
            if (clip == null) return;
            var go = new GameObject("Sfx_" + clip.name);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            if (at.HasValue)
            {
                go.transform.position = at.Value;
                src.spatialBlend = 1f;
                src.minDistance = 8f;
                src.maxDistance = 90f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.dopplerLevel = 0f;
            }
            src.Play();
            UnityEngine.Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        // ------------------------------------------------------------ synthesis helpers

        static AudioClip Make(string name, float seconds, Func<float, float, float> f, bool loop = false)
        {
            int n = Mathf.RoundToInt(seconds * SR);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                data[i] = Mathf.Clamp(f(t, i / (float)n), -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Phase arguments are "cycles" (frequency * time).
        static float Sine(float cycles) => Mathf.Sin(cycles * Mathf.PI * 2f);
        static float Square(float cycles) => (cycles - Mathf.Floor(cycles)) < 0.5f ? 1f : -1f;
        static float Saw(float cycles) => (cycles - Mathf.Floor(cycles)) * 2f - 1f;
        static readonly System.Random rng = new System.Random(1234);
        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

        // A short looping chiptune: bass + lead + kick/hat, A minor, 140 bpm.
        static AudioClip BuildMusic()
        {
            float step = 60f / 140f / 2f;   // eighth note
            const int steps = 32;
            float[] roots = { 110f, 87.3f, 98f, 82.4f };
            int[] rhythm = { 1, 1, 0, 1, 1, 0, 1, 1 };
            float[] lead =
            {
                440, 0, 523, 0, 659, 0, 523, 587,
                587, 0, 523, 0, 440, 0, 392, 0,
                440, 0, 523, 0, 659, 0, 784, 659,
                587, 523, 587, 659, 523, 0, 440, 0,
            };

            int total = Mathf.RoundToInt(step * steps * SR);
            var data = new float[total];
            float hatLp = 0f;
            for (int i = 0; i < total; i++)
            {
                float t = i / (float)SR;
                int s = Mathf.Min((int)(t / step), steps - 1);
                float local = t - s * step;           // seconds into this step
                float env = Mathf.Clamp01(1f - local / step);

                float v = 0f;
                if (rhythm[s % 8] == 1) v += Square(roots[s / 8] * 0.5f * t) * 0.16f * (0.4f + 0.6f * env);
                if (lead[s] > 0f)
                {
                    float c = lead[s] * t;
                    float duty = (c - Mathf.Floor(c)) < 0.25f ? 1f : -1f;
                    v += duty * 0.09f * env;
                }
                if (s % 4 == 0 && local < 0.09f)
                    v += Sine((120f - 700f * local) * local) * 0.5f * (1f - local / 0.09f);   // kick
                if (s % 2 == 1 && local < 0.04f)
                {
                    float n = Noise();
                    hatLp += 0.5f * (n - hatLp);
                    v += (n - hatLp) * 0.12f * (1f - local / 0.04f);                          // hat
                }
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var clip = AudioClip.Create("Music", total, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
