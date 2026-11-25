using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KartRush
{
    /// <summary>Title screen and pause menu. Keyboard (arrows/WASD, Enter, Esc) or gamepad (d-pad, A, Start).</summary>
    public class MenuManager : MonoBehaviour
    {
        public enum Mode { None, Title, Pause, Finish }

        public RaceManager race;
        public GameObject panel;
        public Text titleText, menuText, hintText;

        public Mode mode { get; private set; }
        public bool IsOpen => mode != Mode.None;

        int sel;
        int laps = 3;

        static readonly string[] TitleItems = { "Start Race", "Laps", "Difficulty", "Quit" };
        static readonly string[] PauseItems = { "Resume", "Restart Race", "Music", "Back to Title" };
        static readonly string[] FinishItems = { "Restart Race", "Main Menu" };

        public float finishMenuDelay = 3f;   // lets the victory lap and fanfare play first
        float finishTimer;

        void Start()
        {
            laps = race != null ? race.totalLaps : 3;
            Open(Mode.Title);
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        void Open(Mode m)
        {
            mode = m;
            sel = 0;
            panel.SetActive(true);
            race.Waiting = (m == Mode.Title);
            Time.timeScale = m == Mode.Pause ? 0f : 1f;
            AudioListener.pause = m == Mode.Pause;
            titleText.text = m == Mode.Title ? "KART RUSH" : m == Mode.Pause ? "PAUSED" : "";
            titleText.fontSize = m == Mode.Title ? 150 : 110;
            // Finish menu sits low so the result text stays visible; the background stays light so the victory lap shows.
            menuText.rectTransform.anchoredPosition = new Vector2(0f, m == Mode.Finish ? -370f : 100f);
            panel.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.08f, m == Mode.Finish ? 0.18f : 0.62f);
            Refresh();
        }

        void Close()
        {
            mode = Mode.None;
            panel.SetActive(false);
            race.Waiting = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        string[] Items => mode == Mode.Title ? TitleItems : mode == Mode.Finish ? FinishItems : PauseItems;

        void Refresh()
        {
            var sb = new System.Text.StringBuilder();
            var items = Items;
            for (int i = 0; i < items.Length; i++)
            {
                string label = items[i];
                if (mode == Mode.Title && i == 1) label = $"Laps:  < {laps} >";
                if (mode == Mode.Title && i == 2) label = $"Difficulty:  < {RaceManager.DifficultyNames[race.difficulty]} >";
                if (mode == Mode.Pause && i == 2) label = "Music:  " + (MusicPlayer.Instance == null || MusicPlayer.Instance.MusicOn ? "On" : "Off");
                sb.Append(i == sel ? $"<color=#FFD633>>  {label}  <</color>\n" : $"{label}\n");
            }
            menuText.text = sb.ToString();
            hintText.text = mode == Mode.Title
                ? "UP / DOWN  select      LEFT / RIGHT  change      ENTER  confirm"
                : mode == Mode.Finish ? "UP / DOWN  select      ENTER  confirm"
                : "UP / DOWN  select      ENTER  confirm      ESC  resume";
        }

        void Update()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;

            bool up = false, down = false, left = false, right = false, ok = false, esc = false;
            if (kb != null)
            {
                up = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
                down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
                left = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
                right = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
                ok = kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                esc = kb.escapeKey.wasPressedThisFrame;
            }
            if (pad != null)
            {
                up |= pad.dpad.up.wasPressedThisFrame;
                down |= pad.dpad.down.wasPressedThisFrame;
                left |= pad.dpad.left.wasPressedThisFrame;
                right |= pad.dpad.right.wasPressedThisFrame;
                ok |= pad.buttonSouth.wasPressedThisFrame;
                esc |= pad.startButton.wasPressedThisFrame;
            }

            if (mode == Mode.None)
            {
                finishTimer = race.state == RaceManager.State.Finished ? finishTimer + Time.deltaTime : 0f;
                if (finishTimer >= finishMenuDelay) { finishTimer = 0f; Open(Mode.Finish); return; }
                if (esc) { Sfx.Play(Sfx.MenuSelect, 0.6f); Open(Mode.Pause); }
                return;
            }
            if (mode == Mode.Pause && esc) { Sfx.Play(Sfx.MenuSelect, 0.6f); Close(); return; }

            int n = Items.Length;
            if (up) { sel = (sel + n - 1) % n; Sfx.Play(Sfx.MenuMove, 0.6f); Refresh(); }
            if (down) { sel = (sel + 1) % n; Sfx.Play(Sfx.MenuMove, 0.6f); Refresh(); }

            if (mode == Mode.Title && sel == 1 && (left || right))
            {
                laps = Mathf.Clamp(laps + (right ? 1 : -1), 1, 9);
                Sfx.Play(Sfx.MenuMove, 0.6f);
                Refresh();
            }

            if (mode == Mode.Title && sel == 2 && (left || right))
            {
                race.difficulty = Mathf.Clamp(race.difficulty + (right ? 1 : -1), 0, 2);
                Sfx.Play(Sfx.MenuMove, 0.6f);
                Refresh();
            }

            if (ok) Activate();
        }

        void Activate()
        {
            Sfx.Play(Sfx.MenuSelect, 0.7f);
            if (mode == Mode.Title)
            {
                switch (sel)
                {
                    case 0:
                        race.totalLaps = laps;
                        Close();
                        race.Restart();
                        break;
                    case 1:
                        laps = laps % 9 + 1;
                        Refresh();
                        break;
                    case 2:
                        race.difficulty = (race.difficulty + 1) % 3;
                        Refresh();
                        break;
                    case 3:
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif
                        break;
                }
            }
            else if (mode == Mode.Finish)
            {
                if (sel == 0) { Close(); race.Restart(); }
                else { race.Restart(); Open(Mode.Title); }
            }
            else
            {
                switch (sel)
                {
                    case 0: Close(); break;
                    case 1: Close(); race.Restart(); break;
                    case 2:
                        if (MusicPlayer.Instance != null) MusicPlayer.Instance.MusicOn = !MusicPlayer.Instance.MusicOn;
                        Refresh();
                        break;
                    case 3: race.Restart(); Open(Mode.Title); break;
                }
            }
        }
    }
}
