using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KartRush
{
    /// <summary>Title screen and pause menu. Keyboard (arrows/WASD, Enter, Esc) or gamepad (d-pad, A, Start).</summary>
    public class MenuManager : MonoBehaviour
    {
        public enum Mode { None, Title, Pause }

        public RaceManager race;
        public GameObject panel;
        public Text titleText, menuText, hintText;

        public Mode mode { get; private set; }
        public bool IsOpen => mode != Mode.None;

        int sel;
        int laps = 3;

        static readonly string[] TitleItems = { "Start Race", "Laps", "Quit" };
        static readonly string[] PauseItems = { "Resume", "Restart Race", "Music", "Back to Title" };

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
            titleText.text = m == Mode.Title ? "KART RUSH" : "PAUSED";
            titleText.fontSize = m == Mode.Title ? 150 : 110;
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

        string[] Items => mode == Mode.Title ? TitleItems : PauseItems;

        void Refresh()
        {
            var sb = new System.Text.StringBuilder();
            var items = Items;
            for (int i = 0; i < items.Length; i++)
            {
                string label = items[i];
                if (mode == Mode.Title && i == 1) label = $"Laps:  < {laps} >";
                if (mode == Mode.Pause && i == 2) label = "Music:  " + (MusicPlayer.Instance == null || MusicPlayer.Instance.MusicOn ? "On" : "Off");
                sb.Append(i == sel ? $"<color=#FFD633>>  {label}  <</color>\n" : $"{label}\n");
            }
            menuText.text = sb.ToString();
            hintText.text = mode == Mode.Title
                ? "UP / DOWN  select      LEFT / RIGHT  change      ENTER  confirm"
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
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif
                        break;
                }
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
