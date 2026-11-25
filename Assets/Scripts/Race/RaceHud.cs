using UnityEngine;
using UnityEngine.UI;

namespace KartRush
{
    /// <summary>uGUI heads-up display: lap, timers, speed, drift charge, countdown and finish screen.</summary>
    public class RaceHud : MonoBehaviour
    {
        public RaceManager race;
        public KartController kart;
        public Text lapText, timeText, bestText, speedText, centerText, helpText, positionText, standingsText, itemText;
        public Image driftBar;          // the coloured fill
        public GameObject driftRoot;    // background + ticks + fill
        public MenuManager menu;
        KartItems items;
        string helpBase;

        static readonly Color[] DriftColors =
        {
            new Color(0.85f, 0.85f, 0.9f, 1f),   // charging, no level yet
            new Color(0.4f, 0.8f, 1f, 1f),   // level 1
            new Color(1f, 0.6f, 0.15f, 1f),  // level 2
            new Color(1f, 0.25f, 0.9f, 1f),  // level 3
        };

        void SetFill(float f)
        {
            var rt = driftBar.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(f), 1f);
        }

        void Update()
        {
            if (race == null || kart == null) return;

            if (helpBase == null) helpBase = helpText.text;
            helpText.text = helpBase + (race.Autopilot ? "    <color=#FFD633>AUTOPILOT ON (T)</color>" : "    T autopilot");

            lapText.text = $"LAP {Mathf.Min(race.CurrentLap, race.totalLaps)}/{race.totalLaps}";
            timeText.text = RaceManager.FormatTime(race.RaceTime);
            bestText.text = "BEST " + RaceManager.FormatTime(race.BestLap);
            speedText.text = $"{Mathf.RoundToInt(kart.SpeedKmh)} <size=24>km/h</size>";

            // Position + standings
            int total = race.racers.Count;
            if (positionText != null)
                positionText.text = $"{race.PlayerPosition}<size=40>/{total}</size>";
            if (standingsText != null)
            {
                var sb2 = new System.Text.StringBuilder();
                for (int i = 0; i < race.Standings.Count; i++)
                {
                    var r = race.Standings[i];
                    string hex = ColorUtility.ToHtmlStringRGB(r.color);
                    string name = r.isPlayer ? r.displayName.ToUpper() : r.displayName;
                    sb2.Append($"{i + 1}. <color=#{hex}>{name}</color>{(r.Finished ? "  FIN" : "")}\n");
                }
                standingsText.text = sb2.ToString();
            }

            // Held item
            if (itemText != null)
            {
                if (items == null) items = kart.GetComponent<KartItems>();
                bool rolling = items != null && items.Rolling;
                var shown = rolling ? items.RollDisplay : (items != null ? items.held : ItemType.None);
                if (shown == ItemType.None) itemText.text = "";
                else
                {
                    string col = ColorUtility.ToHtmlStringRGB(KartItems.DisplayColor(shown));
                    itemText.text = rolling
                        ? $"<color=#{col}>{KartItems.DisplayName(shown)}</color>"
                        : $"<color=#{col}>{KartItems.DisplayName(shown)}</color>  <size=28>[E]</size>";
                }
            }

            // Drift charge bar
            // (driftBar is the fill inside driftRoot; its width is set through the right anchor)
            if (kart.IsDrifting)
            {
                driftRoot.SetActive(true);
                driftBar.color = DriftColors[Mathf.Clamp(kart.DriftLevel, 0, 3)];
                SetFill(kart.DriftCharge01);
            }
            else if (kart.IsBoosting)
            {
                driftRoot.SetActive(true);
                driftBar.color = new Color(1f, 0.9f, 0.2f, 1f);
                SetFill(1f);
            }
            else driftRoot.SetActive(false);

            switch (race.state)
            {
                case RaceManager.State.Countdown:
                    centerText.text = Mathf.CeilToInt(Mathf.Max(race.CountdownRemaining, 0.01f)).ToString();
                    centerText.fontSize = 140;
                    break;
                case RaceManager.State.Racing:
                    centerText.text = race.GoFlashTimer > 0f ? "GO!" : "";
                    centerText.fontSize = 140;
                    break;
                case RaceManager.State.Finished:
                    var sb = new System.Text.StringBuilder($"{RaceManager.Ordinal(race.PlayerPosition)} PLACE!\n");
                    sb.Append("<size=40>");
                    for (int i = 0; i < race.LapTimes.Count; i++)
                        sb.Append($"Lap {i + 1}  {RaceManager.FormatTime(race.LapTimes[i])}\n");
                    sb.Append($"Total  {RaceManager.FormatTime(race.RaceTime)}</size>");
                    centerText.text = sb.ToString();
                    centerText.fontSize = 90;
                    break;
            }
            // Title / pause menus replace the centre text; the finish menu shows below the result.
            if (menu != null && (menu.mode == MenuManager.Mode.Title || menu.mode == MenuManager.Mode.Pause)) centerText.text = "";
        }
    }
}
