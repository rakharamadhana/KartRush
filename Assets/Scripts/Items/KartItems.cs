using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KartRush
{
    public enum ItemType { None, Mushroom, Banana, Shell }

    /// <summary>
    /// Holds one item for a kart, hands out random items by race position, and lets the player
    /// (E / Left Ctrl / gamepad West) or the AI use it.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class KartItems : MonoBehaviour
    {
        public static readonly Dictionary<Rigidbody, KartItems> BySphere = new Dictionary<Rigidbody, KartItems>();

        public ItemType held;
        public float mushroomBoostSeconds = 2f;

        public KartController kart { get; private set; }
        public Racer racer { get; private set; }
        float aiTimer;

        public bool IsPlayer => racer != null && racer.isPlayer;

        void Awake()
        {
            kart = GetComponent<KartController>();
            racer = GetComponent<Racer>();
            if (kart.sphere != null) BySphere[kart.sphere] = this;
        }

        void OnDestroy()
        {
            if (kart != null && kart.sphere != null) BySphere.Remove(kart.sphere);
        }

        public void Clear() { held = ItemType.None; aiTimer = 0f; }

        /// <summary>Hand out a random item; being further back gets stronger / more useful items.</summary>
        public bool GiveRandom()
        {
            if (held != ItemType.None) return false;
            float back = 0.5f;
            var race = racer != null ? racer.race : null;
            if (race != null && race.Standings.Count > 1)
                back = race.Standings.IndexOf(racer) / (float)(race.Standings.Count - 1);

            float wMushroom = Mathf.Lerp(30f, 55f, back);
            float wShell = Mathf.Lerp(15f, 35f, back);
            float wBanana = Mathf.Lerp(55f, 10f, back);
            float roll = Random.value * (wMushroom + wShell + wBanana);
            held = roll < wMushroom ? ItemType.Mushroom : roll < wMushroom + wShell ? ItemType.Shell : ItemType.Banana;
            aiTimer = Random.Range(1.5f, 4f);
            return true;
        }

        void Update()
        {
            if (IsPlayer)
            {
                var kb = Keyboard.current;
                var pad = Gamepad.current;
                bool press = (kb != null && (kb.eKey.wasPressedThisFrame || kb.leftCtrlKey.wasPressedThisFrame))
                          || (pad != null && (pad.buttonWest.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame));
                if (press) UseItem();
            }
            else if (held != ItemType.None && kart.canDrive)
            {
                aiTimer -= Time.deltaTime;
                if (aiTimer <= 0f) AiTryUse();
            }
        }

        Racer Ahead()
        {
            var race = racer != null ? racer.race : null;
            if (race == null) return null;
            int i = race.Standings.IndexOf(racer);
            return i > 0 ? race.Standings[i - 1] : null;
        }

        void AiTryUse()
        {
            aiTimer = 1f;   // re-check every second until the item makes sense
            switch (held)
            {
                case ItemType.Mushroom:
                    if (Mathf.Abs(kart.scriptedSteer) < 0.4f) UseItem();
                    break;
                case ItemType.Shell:
                    if (Ahead() != null) UseItem();
                    break;
                case ItemType.Banana:
                    var race = racer.race;
                    foreach (var r in race.racers)
                    {
                        if (r == racer) continue;
                        Vector3 d = r.transform.position - transform.position; d.y = 0f;
                        if (d.magnitude < 40f && Vector3.Dot(d, kart.Heading) < 0f) { UseItem(); break; }
                    }
                    break;
            }
        }

        public void UseItem()
        {
            if (held == ItemType.None || !kart.canDrive || kart.IsSpinning) return;
            var item = held;
            held = ItemType.None;
            switch (item)
            {
                case ItemType.Mushroom:
                    kart.GiveBoost(mushroomBoostSeconds);
                    break;
                case ItemType.Banana:
                    Banana.Spawn(transform.position - kart.Heading * 3f);
                    break;
                case ItemType.Shell:
                    Shell.Spawn(this, Ahead());
                    break;
            }
        }

        public static string DisplayName(ItemType t) =>
            t == ItemType.Mushroom ? "Mushroom" : t == ItemType.Banana ? "Banana" : t == ItemType.Shell ? "Shell" : "";

        public static Color DisplayColor(ItemType t) =>
            t == ItemType.Mushroom ? new Color(1f, 0.55f, 0.15f) :
            t == ItemType.Banana ? new Color(1f, 0.9f, 0.2f) :
            t == ItemType.Shell ? new Color(0.3f, 0.9f, 0.35f) : Color.white;
    }
}
