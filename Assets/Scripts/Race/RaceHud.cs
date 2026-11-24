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
        public Image driftBar;
        public MenuManager menu;
        KartItems items;

        static readonly Color[] DriftColors =
        {
            new Color(1f, 1f, 1f, 0.0f),
            new Color(0.4f, 0.8f, 1f, 1f),   // level 1
            new Color(1f, 0.6f, 0.15f, 1f),  // level 2
            new Color(1f, 0.25f, 0.9f, 1f),  // level 3
        };

        void Update()
        {
            if (race == null || kart == null) return;

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
                var held = items != null ? items.held : ItemType.None;
                itemText.text = held == ItemType.None
                    ? ""
                    : $"<color=#{ColorUtility.ToHtmlStringRGB(KartItems.DisplayColor(held))}>{KartItems.DisplayName(held)}</color>  <size=28>[E]</size>";
            }

            // Drift charge bar
            if (kart.IsDrifting)
            {
                driftBar.color = DriftColors[Mathf.Clamp(kart.DriftLevel, 0, 3)];
                driftBar.enabled = true;
            }
            else if (kart.IsBoosting)
            {
                driftBar.color = new Color(1f, 0.9f, 0.2f, 1f);
                driftBar.enabled = true;
            }
            else driftBar.enabled = false;

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
                    sb.Append($"Total  {RaceManager.FormatTime(race.RaceTime)}\n\nPress Enter to race again</size>");
                    centerText.text = sb.ToString();
                    centerText.fontSize = 90;
                    break;
            }
            if (menu != null && menu.IsOpen) centerText.text = "";
        }
    }
}
