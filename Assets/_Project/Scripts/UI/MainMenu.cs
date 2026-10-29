using System.Collections.Generic;
using Deducted.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deducted.UI
{
    /// <summary>
    /// Title screen: W/S select, Space confirm. NEW GAME loads the game scene behind a fade,
    /// QUIT closes the game (hidden in the web build, a browser tab can't be closed).
    /// Built by Deducted > Build Main Menu.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public enum Action { NewGame, Quit }

        [System.Serializable]
        public class Entry
        {
            public Action action;
            public Image background;
            public TMP_Text label;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [Tooltip("Scene NEW GAME loads (must be in the build profile's scene list).")]
        [SerializeField] private string gameScene = "Test";
        [SerializeField, Min(0f)] private float fadeIn = 1f;
        [SerializeField, Min(0f)] private float fadeOut = 1.2f;

        [Header("Keys")]
        [SerializeField] private Key upKey = Key.W;
        [SerializeField] private Key downKey = Key.S;
        [SerializeField] private Key acceptKey = Key.Space;

        [Header("Look")]
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color selectedTextColor = Color.black;
        [SerializeField] private Color selectedBackground = Color.white;

        [Header("Sounds (optional)")]
        [SerializeField] private AudioClip selectSound;
        [SerializeField] private AudioClip confirmSound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

        private readonly List<Entry> visible = new List<Entry>();
        private int selected;
        private bool chosen;

        private void Awake()
        {
            // Coming back from a paused game.
            Time.timeScale = 1f;
            AudioListener.pause = false;

            foreach (Entry entry in entries)
            {
                bool show = !(entry.action == Action.Quit && Application.platform == RuntimePlatform.WebGLPlayer);
                if (entry.background != null) entry.background.gameObject.SetActive(show);
                if (show) visible.Add(entry);
            }
            Refresh();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || chosen || visible.Count == 0) return;

            int before = selected;
            if (kb[upKey].wasPressedThisFrame) selected--;
            if (kb[downKey].wasPressedThisFrame) selected++;
            selected = Mathf.Clamp(selected, 0, visible.Count - 1);
            if (selected != before)
            {
                UISound.Play(selectSound, soundVolume);
                Refresh();
            }

            if (kb[acceptKey].wasPressedThisFrame) Choose(visible[selected].action);
        }

        private void Choose(Action action)
        {
            UISound.Play(confirmSound, soundVolume);
            switch (action)
            {
                case Action.NewGame:
                    chosen = true;
                    SceneLoader.Load(gameScene, fadeIn, fadeOut);
                    break;
                case Action.Quit:
                    SceneLoader.Quit();
                    break;
            }
        }

        private void Refresh()
        {
            for (int i = 0; i < visible.Count; i++)
            {
                bool isSelected = i == selected;
                if (visible[i].background != null)
                    visible[i].background.color = isSelected ? selectedBackground : Color.clear;
                if (visible[i].label != null)
                    visible[i].label.color = isSelected ? selectedTextColor : textColor;
            }
        }
    }
}
