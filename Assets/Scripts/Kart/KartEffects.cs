using UnityEngine;

namespace KartRush
{
    /// <summary>Drift smoke + colour-coded sparks at the rear wheels, and an exhaust flame while boosting.</summary>
    [RequireComponent(typeof(KartController))]
    public class KartEffects : MonoBehaviour
    {
        static readonly Color[] SparkColors =
        {
            new Color(1f, 0.95f, 0.7f),          // charging
            new Color(0.4f, 0.8f, 1f),           // level 1
            new Color(1f, 0.6f, 0.15f),          // level 2
            new Color(1f, 0.3f, 0.95f),          // level 3
        };

        KartController kart;
        ParticleSystem[] sparks = new ParticleSystem[2];
        ParticleSystem[] smoke = new ParticleSystem[2];
        ParticleSystem flame;

        void Start()
        {
            kart = GetComponent<KartController>();
            var assets = ItemAssets.Instance;
            if (assets == null) { enabled = false; return; }

            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.85f : 0.85f;
                sparks[i] = Make("Sparks" + i, new Vector3(x, 0.2f, -1.0f), Quaternion.Euler(-60f, 180f, 0f), assets.sparkMat, 0.35f, 0.4f, 5f, 0.22f, 1.6f, 40f);
                smoke[i] = Make("Smoke" + i, new Vector3(x, 0.15f, -1.0f), Quaternion.Euler(-30f, 180f, 0f), assets.smokeMat, 0.8f, 1.2f, 0.9f, 0.9f, -0.05f, 25f);
                var col = smoke[i].colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(new Color(0.85f, 0.85f, 0.85f), 0f), new GradientColorKey(new Color(0.6f, 0.6f, 0.6f), 1f) },
                          new[] { new GradientAlphaKey(0.45f, 0f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var sz = smoke[i].sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 1.6f)));
            }
            flame = Make("Flame", new Vector3(0f, 0.55f, -1.3f), Quaternion.Euler(0f, 180f, 0f), assets.sparkMat, 0.25f, 0.3f, 7f, 0.45f, 0f, 20f);
            flame.GetComponent<ParticleSystemRenderer>().sortingFudge = 0f;
            var fc = flame.main;
            fc.startColor = new Color(1f, 0.55f, 0.15f);

            kart.DriftStarted += OnDriftStarted;
        }

        void OnDestroy() { if (kart != null) kart.DriftStarted -= OnDriftStarted; }

        void OnDriftStarted()
        {
            if (smoke[0] == null) return;
            foreach (var s in smoke) s.Emit(6);
        }

        ParticleSystem Make(string name, Vector3 pos, Quaternion rot, Material mat, float life, float lifeMax, float speed, float size, float gravity, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.gravityModifier = gravity;
            main.maxParticles = 200;

            var em = ps.emission;
            em.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle * 0.5f;
            shape.radius = 0.05f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        static void SetRate(ParticleSystem ps, float rate)
        {
            var em = ps.emission;
            em.rateOverTime = rate;
        }

        void Update()
        {
            bool slide = kart.IsDrifting && kart.IsGrounded;
            int lvl = Mathf.Clamp(kart.DriftLevel, 0, 3);
            for (int i = 0; i < 2; i++)
            {
                var m = sparks[i].main;
                m.startColor = SparkColors[lvl];
                SetRate(sparks[i], slide ? (lvl == 0 ? 14f : 80f) : 0f);
                SetRate(smoke[i], slide ? 22f : 0f);
            }
            SetRate(flame, kart.IsBoosting ? 90f : 0f);
        }
    }
}
