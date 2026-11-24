using System.Collections.Generic;
using UnityEngine;

namespace KartRush
{
    /// <summary>
    /// A green-shell style projectile: races ahead along the track and homes in on the kart in front
    /// of its owner once it gets close. Spins out the first kart it touches.
    /// </summary>
    public class Shell : MonoBehaviour
    {
        static readonly List<Shell> all = new List<Shell>();

        public float speed = 58f;
        public float homingRange = 35f;
        public float hitRadius = 2.4f;

        KartItems owner;
        Racer target;
        RaceManager race;
        int index;
        float age;

        public static void Spawn(KartItems owner, Racer target)
        {
            var race = owner.racer.race;
            Vector3 pos = owner.transform.position + owner.kart.Heading * 3f + Vector3.up * 0.6f;

            var go = new GameObject("Shell");
            go.transform.position = pos;
            var assets = ItemAssets.Instance;

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = Vector3.one * 0.9f;
            Object.Destroy(body.GetComponent<Collider>());
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.transform.SetParent(go.transform, false);
            ring.transform.localScale = new Vector3(1.0f, 0.04f, 1.0f);
            Object.Destroy(ring.GetComponent<Collider>());
            if (assets != null)
            {
                body.GetComponent<MeshRenderer>().sharedMaterial = assets.shellMat;
                ring.GetComponent<MeshRenderer>().sharedMaterial = assets.shellRingMat;
            }

            var s = go.AddComponent<Shell>();
            s.owner = owner;
            s.target = target;
            s.race = race;
            s.index = s.ClosestIndex(pos);
            all.Add(s);
        }

        public static void ClearAll()
        {
            foreach (var s in all) if (s != null) Destroy(s.gameObject);
            all.Clear();
        }

        void OnDestroy() => all.Remove(this);

        int ClosestIndex(Vector3 p)
        {
            var pts = race.racingLine;
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
            {
                float d = (pts[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age > 9f) { Destroy(gameObject); return; }

            var pts = race.racingLine;
            int n = pts.Length;
            Vector3 pos = transform.position;

            // Keep our index on the line.
            float bd = float.MaxValue; int best = index;
            for (int o = -2; o <= 8; o++)
            {
                int j = ((index + o) % n + n) % n;
                float d = (pts[j] - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = j; }
            }
            index = best;

            Vector3 dir;
            Vector3 toTarget = default;
            bool homing = target != null && (toTarget = target.transform.position - pos).sqrMagnitude < homingRange * homingRange;
            if (homing) { toTarget.y = 0f; dir = toTarget.normalized; }
            else
            {
                dir = pts[(index + 3) % n] - pos; dir.y = 0f; dir.Normalize();
            }

            pos += dir * speed * Time.deltaTime;
            if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y + 0.55f;
            transform.position = pos;
            transform.Rotate(0f, 540f * Time.deltaTime, 0f, Space.World);

            foreach (var k in KartItems.BySphere.Values)
            {
                if (k == null || (k == owner && age < 0.6f)) continue;
                Vector3 d = k.kart.sphere.position - pos; d.y = 0f;
                if (d.magnitude < hitRadius && k.kart.SpinOut())
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }
}
