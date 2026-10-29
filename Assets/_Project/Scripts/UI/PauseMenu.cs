using System.Collections.Generic;
using Deducted.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deducted.UI
{
    /// <summary>
    /// Pause menu (Esc or P). Freezes the game (Time.timeScale 0: Collector, subscriptions, animations,
    /// movement) and pauses world sounds (AudioListener.pause); ambience and UI sounds keep playing,
    /// ear ads are cut off. W/S select, Space confirm. Entries: Resume, Controls (key table), Main Menu.
    /// Other scripts check PauseMenu.BlocksInput. Built by Deducted > Build Pause Menu.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            public Image background;
            public TMP_Text label;
        }

        [System.Serializable]
        public class ControlRow
        {
            [Tooltip("Empty = a section header (Action is shown as the title).")]
            public string keys;
            public string action;

            public ControlRow(string keys, string action)
            {
                this.keys = keys;
                this.action = action;
            }
        }

        public static PauseMenu Instance { get; private set; }
        public static bool IsPaused { get; private set; }
        /// <summary>True while paused and on the frame the menu closed (the confirm key mustn't also act in the game).</summary>
        public static bool BlocksInput => IsPaused || closedFrame == Time.frameCount;
        public static event System.Action<bool> PauseChanged;

        private static int closedFrame = -1;

        [SerializeField] private GameObject root;
        [SerializeField] private GameObject listPanel;
        [SerializeField] private GameObject controlsPanel;
        [Tooltip("In order: Resume, Controls, Main Menu.")]
        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField] private TMP_Text controlKeysText;
        [SerializeField] private TMP_Text controlActionsText;

        [Header("Controls table")]
        [SerializeField] private List<ControlRow> controlRows = new List<ControlRow>
        {
            new ControlRow("", "MOVEMENT"),
            new ControlRow("W A S D", "Walk"),
            new ControlRow("SPACE", "Interact / talk"),
            new ControlRow("R", "Smoke a cigarette"),
            new ControlRow("TAB / E", "Body overview + receipt"),
            new ControlRow("ESC / P", "Pause"),
            new ControlRow("", "TALKS + SHOPS"),
            new ControlRow("D / SPACE", "Next"),
            new ControlRow("A", "Back"),
            new ControlRow("TAB / E", "Leave"),
            new ControlRow("W / S", "Select"),
            new ControlRow("HOLD SPACE", "Buy / sell"),
        };

        [Header("Keys")]
        [SerializeField] private Key pauseKey = Key.Escape;
        [Tooltip("Second pause key: in the browser Esc often only leaves fullscreen.")]
        [SerializeField] private Key pauseKeyAlt = Key.P;
        [SerializeField] private Key upKey = Key.W;
        [SerializeField] private Key downKey = Key.S;
        [SerializeField] private Key acceptKey = Key.Space;

        [Header("Look")]
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color selectedTextColor = Color.black;
        [SerializeField] private Color selectedBackground = Color.white;

        [Header("Sounds (optional)")]
        [SerializeField] private AudioClip openSound;
        [SerializeField] private AudioClip closeSound;
        [SerializeField] private AudioClip selectSound;
        [SerializeField] private AudioClip confirmSound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

        [Header("Main Menu")]
        [Tooltip("Scene Main Menu loads (nothing of the run is kept).")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField, Min(0f)] private float mainMenuFadeIn = 1f;
        [SerializeField, Min(0f)] private float mainMenuFadeOut = 1.2f;
        [Tooltip("Extra things to do when Main Menu is chosen (optional).")]
        [SerializeField] private UnityEvent onMainMenu = new UnityEvent();

        private int selected;
        private bool showingControls;
        private float timeScaleBefore = 1f;

        private void Awake()
        {
            Instance = this;
            IsPaused = false;
            if (root != null) root.SetActive(false);
            FillControls();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            // Scene unloads while paused (Main Menu): unfreeze quietly, without the close sound.
            if (IsPaused)
            {
                IsPaused = false;
                Time.timeScale = 1f;
                AudioListener.pause = false;
            }
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || SceneLoader.IsLoading) return;

            if (Pressed(kb, pauseKey) || Pressed(kb, pauseKeyAlt))
            {
                if (!IsPaused) SetPaused(true);
                else if (showingControls) ShowControls(false);
                else SetPaused(false);
                return;
            }
            if (!IsPaused) return;

            bool confirm = Pressed(kb, acceptKey);
            if (showingControls)
            {
                if (confirm) ShowControls(false); // back to the list
                return;
            }

            int before = selected;
            if (Pressed(kb, upKey)) selected--;
            if (Pressed(kb, downKey)) selected++;
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, entries.Count - 1));
            if (selected != before)
            {
                UISound.Play(selectSound, soundVolume);
                RefreshEntries();
            }

            if (confirm) Choose(selected);
        }

        private static bool Pressed(Keyboard kb, Key key) => key != Key.None && kb[key].wasPressedThisFrame;

        private void Choose(int index)
        {
            UISound.Play(confirmSound, soundVolume);
            switch (index)
            {
                case 0: SetPaused(false); break;
                case 1: ShowControls(true); break;
                case 2:
                    // Stays paused under the fade; the reload starts a fresh run.
                    onMainMenu.Invoke();
                    SceneLoader.Load(mainMenuScene, mainMenuFadeIn, mainMenuFadeOut);
                    break;
            }
        }

        /// <summary>Opens or closes the pause menu and freezes / unfreezes the game.</summary>
        public void SetPaused(bool paused)
        {
            if (paused == IsPaused) return;
            IsPaused = paused;

            if (paused)
            {
                timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                AudioListener.pause = true; // world sounds stop and resume where they were
                selected = 0;
                ShowControls(false);
                UISound.Play(openSound, soundVolume);
            }
            else
            {
                Time.timeScale = timeScaleBefore;
                AudioListener.pause = false;
                closedFrame = Time.frameCount;
                UISound.Play(closeSound, soundVolume);
            }

            if (root != null) root.SetActive(paused);
            RefreshEntries();
            PauseChanged?.Invoke(paused);
        }

        private void ShowControls(bool show)
        {
            showingControls = show;
            if (listPanel != null) listPanel.SetActive(!show);
            if (controlsPanel != null) controlsPanel.SetActive(show);
        }

        private void RefreshEntries()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                bool isSelected = i == selected;
                if (entries[i].background != null)
                    entries[i].background.color = isSelected ? selectedBackground : Color.clear;
                if (entries[i].label != null)
                    entries[i].label.color = isSelected ? selectedTextColor : textColor;
            }
        }

        // Two columns: keys left, what they do right. Rows without keys are section titles.
        private void FillControls()
        {
            if (controlKeysText == null || controlActionsText == null) return;
            var keys = new System.Text.StringBuilder();
            var actions = new System.Text.StringBuilder();
            for (int i = 0; i < controlRows.Count; i++)
            {
                ControlRow row = controlRows[i];
                bool header = string.IsNullOrEmpty(row.keys);
                if (header && i > 0)
                {
                    keys.AppendLine();
                    actions.AppendLine();
                }
                keys.AppendLine(header ? $"<b>{row.action}</b>" : row.keys);
                actions.AppendLine(header ? "" : row.action);
            }
            controlKeysText.text = keys.ToString();
            controlActionsText.text = actions.ToString();
        }

        private void OnValidate()
        {
            if (Application.isPlaying) FillControls();
        }
    }
}
