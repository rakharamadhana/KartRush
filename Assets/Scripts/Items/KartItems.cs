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

        // Roulette: after grabbing a box the item name flickers for a moment, then the real item is revealed.
        public bool Rolling { get; private set; }
        public ItemType RollDisplay { get; private set; }
        public float rollSeconds = 1.6f;
        ItemType pending;
        float rollTimer, flipTimer;

        public void Clear() { held = ItemType.None; aiTimer = 0f; Rolling = false; }

        void UpdateRoll()
        {
            rollTimer -= Time.deltaTime;
            flipTimer -= Time.deltaTime;
            if (flipTimer <= 0f)
            {
                flipTimer = 0.08f;
                int next = (int)RollDisplay % 3 + 1;
                if (Random.value < 0.35f) next = next % 3 + 1;
                RollDisplay = (ItemType)next;
                if (IsPlayer) Sfx.Play(Sfx.MenuMove, 0.25f, null, 1.5f);
            }
            if (rollTimer <= 0f)
            {
                Rolling = false;
                held = pending;
                aiTimer = Random.Range(0.5f, 2.5f);
                Sfx.Play(Sfx.Pickup, IsPlayer ? 0.8f : 0.4f, IsPlayer ? (Vector3?)null : transform.position);
            }
        }

        /// <summary>Hand out a random item; being further back gets stronger / more useful items.</summary>
        public bool GiveRandom()
        {
            if (held != ItemType.None || Rolling || Finished) return false;
            float back = 0.5f;
            var race = racer != null ? racer.race : null;
            if (race != null && race.Standings.Count > 1)
                back = race.Standings.IndexOf(racer) / (float)(race.Standings.Count - 1);

            float wMushroom = Mathf.Lerp(30f, 55f, back);
            float wShell = Mathf.Lerp(15f, 35f, back);
            float wBanana = Mathf.Lerp(55f, 10f, back);
            float roll = Random.value * (wMushroom + wShell + wBanana);
            pending = roll < wMushroom ? ItemType.Mushroom : roll < wMushroom + wShell ? ItemType.Shell : ItemType.Banana;
            Rolling = true;
            rollTimer = rollSeconds;
            flipTimer = 0f;
            RollDisplay = (ItemType)Random.Range(1, 4);
            return true;
        }

        /// <summary>Once a kart has taken the flag it is on its victory lap: no pickups, no item use.</summary>
        bool Finished => racer != null && racer.Finished;

        void Update()
        {
            if (Finished) { held = ItemType.None; Rolling = false; return; }
            if (Rolling) UpdateRoll();
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
            Sfx.Play(Sfx.UseItem, IsPlayer ? 0.8f : 0.4f, IsPlayer ? (Vector3?)null : transform.position);
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
