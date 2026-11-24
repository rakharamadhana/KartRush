using System.Collections.Generic;
using UnityEngine;

namespace KartRush
{
    /// <summary>A banana peel left on the track: the first kart to touch it spins out.</summary>
    public class Banana : MonoBehaviour
    {
        static readonly List<Banana> all = new List<Banana>();
        float life = 60f;

        public static void Spawn(Vector3 nearPosition)
        {
            Vector3 pos = nearPosition;
            if (Physics.Raycast(nearPosition + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore))
                pos = hit.point;

            var go = new GameObject("Banana");
            go.transform.position = pos + Vector3.up * 0.4f;
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 1.1f;

            var assets = ItemAssets.Instance;
            var peel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            peel.name = "Peel";
            peel.transform.SetParent(go.transform, false);
            peel.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);
            peel.transform.localRotation = Quaternion.Euler(0f, 0f, 78f);
            Object.Destroy(peel.GetComponent<Collider>());
            if (assets != null) peel.GetComponent<MeshRenderer>().sharedMaterial = assets.bananaMat;

            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "Tip";
            tip.transform.SetParent(go.transform, false);
            tip.transform.localPosition = new Vector3(0.55f, 0.1f, 0f);
            tip.transform.localScale = Vector3.one * 0.18f;
            Object.Destroy(tip.GetComponent<Collider>());
            if (assets != null) tip.GetComponent<MeshRenderer>().sharedMaterial = assets.bananaDarkMat;

            // Needs a kinematic body so the kart sphere's trigger events reach us reliably.
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            all.Add(go.AddComponent<Banana>());
        }

        public static void ClearAll()
        {
            foreach (var b in all) if (b != null) Destroy(b.gameObject);
            all.Clear();
        }

        void OnDestroy() => all.Remove(this);

        void Update()
        {
            transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
            life -= Time.deltaTime;
            if (life <= 0f) Destroy(gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null || !KartItems.BySphere.TryGetValue(rb, out var items)) return;
            if (items.kart.SpinOut()) Destroy(gameObject);
        }
    }
}
