using System.Collections.Generic;
using UnityEngine;

namespace KartRush
{
    /// <summary>Floating item box: drive through it to receive a random item, then it respawns.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ItemBox : MonoBehaviour
    {
        public static readonly List<ItemBox> All = new List<ItemBox>();

        public Transform visual;
        public float respawnSeconds = 4f;

        BoxCollider col;
        float respawnTimer;
        float baseY;
        bool collected;

        void Awake()
        {
            col = GetComponent<BoxCollider>();
            col.isTrigger = true;
            All.Add(this);
        }

        void OnDestroy() => All.Remove(this);

        void Start() { if (visual != null) baseY = visual.localPosition.y; }

        public static void ResetAll()
        {
            foreach (var b in All) if (b != null) b.SetCollected(false);
        }

        void SetCollected(bool value)
        {
            collected = value;
            respawnTimer = respawnSeconds;
            col.enabled = !value;
            if (visual != null) visual.gameObject.SetActive(!value);
        }

        void Update()
        {
            if (collected)
            {
                respawnTimer -= Time.deltaTime;
                if (respawnTimer <= 0f) SetCollected(false);
                return;
            }
            if (visual != null)
            {
                visual.Rotate(0f, 110f * Time.deltaTime, 0f, Space.World);
                var p = visual.localPosition;
                p.y = baseY + Mathf.Sin(Time.time * 2.5f + transform.position.x) * 0.15f;
                visual.localPosition = p;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (collected) return;
            var rb = other.attachedRigidbody;
            if (rb == null || !KartItems.BySphere.TryGetValue(rb, out var items)) return;
            if (items.GiveRandom()) SetCollected(true);
        }
    }
}
