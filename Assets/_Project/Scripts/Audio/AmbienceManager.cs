using System.Collections.Generic;
using Deducted.Economy;
using UnityEngine;

namespace Deducted.Audio
{
    /// <summary>
    /// Background ambience on both ears (not affected by missing ears), always running.
    /// Normal plays while the balance is 0 or more, Negative while it's below 0; switching crossfades over
    /// Switch Time. In the pause menu Pause plays (empty = Normal). Each ambience loops seamlessly: Loop Blend seconds before the clip ends, a second copy
    /// starts from the beginning and the two crossfade.
    /// One per scene; makes its own AudioSources. Fades out with the screen when a scene load starts.
    /// </summary>
    public class AmbienceManager : MonoBehaviour
    {
        [System.Serializable]
        public class Ambience
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.6f;
        }

        [SerializeField] private Ambience normal = new Ambience();
        [Tooltip("Plays instead of Normal while the balance is negative.")]
        [SerializeField] private Ambience negative = new Ambience();
        [Tooltip("Plays while the pause menu is open. Empty = Normal.")]
        [SerializeField] private Ambience pause = new Ambience();

        [Header("Blending")]
        [Tooltip("Seconds the end of a clip overlaps its own restart (at most half the clip).")]
        [SerializeField, Min(0f)] private float loopBlend = 4f;
        [Tooltip("Seconds to crossfade between Normal and Negative.")]
        [SerializeField, Min(0.01f)] private float switchTime = 3f;
        [Tooltip("Overall volume of all ambience.")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

        // One layer per ambience: two sources that take turns, so a loop can overlap its own restart.
        private class Layer
        {
            public Ambience ambience;
            public AudioSource current;
            public AudioSource incoming; // set while a loop crossfade runs
            public float crossStart;
            public float weight;          // 0..1 how much this ambience is heard (switch crossfade)
            public bool playing;
            public AudioSource spare;
        }

        private readonly List<Layer> layers = new List<Layer>();
        private Layer normalLayer;
        private Layer negativeLayer;
        private Layer pauseLayer;
        private float sceneFade = 1f;     // goes to 0 when a scene load starts
        private float sceneFadeSpeed;

        private void OnEnable() => Deducted.UI.SceneLoader.LoadStarted += FadeForLoad;
        private void OnDisable() => Deducted.UI.SceneLoader.LoadStarted -= FadeForLoad;

        // Fade out together with the screen going black.
        private void FadeForLoad(float duration) => sceneFadeSpeed = duration > 0f ? 1f / duration : float.MaxValue;

        private void Awake()
        {
            normalLayer = CreateLayer(normal);
            negativeLayer = CreateLayer(negative);
            pauseLayer = CreateLayer(pause);
        }

        private Layer CreateLayer(Ambience ambience)
        {
            var layer = new Layer { ambience = ambience, current = CreateSource(), spare = CreateSource() };
            layers.Add(layer);
            return layer;
        }

        private AudioSource CreateSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false; // looping is done by hand, with the crossfade
            s.spatialBlend = 0f;
            s.ignoreListenerPause = true;
            s.volume = 0f;
            return s;
        }

        private void Update()
        {
            bool isNegative = BalanceManager.Instance != null && BalanceManager.Instance.IsNegative;
            Layer active = isNegative && negative.clip != null ? negativeLayer : normalLayer;
            if (Deducted.UI.PauseMenu.IsPaused) active = pause.clip != null ? pauseLayer : normalLayer;

            if (sceneFadeSpeed > 0f) sceneFade = Mathf.MoveTowards(sceneFade, 0f, sceneFadeSpeed * Time.unscaledDeltaTime);

            float step = Time.unscaledDeltaTime / switchTime;
            foreach (Layer layer in layers)
            {
                float target = layer == active ? 1f : 0f;
                layer.weight = Mathf.MoveTowards(layer.weight, target, step);
                UpdateLayer(layer, target);
            }
        }

        private void UpdateLayer(Layer layer, float target)
        {
            AudioClip clip = layer.ambience.clip;
            if (clip == null) return;

            // Silent and not wanted: stop, so it starts fresh next time.
            if (layer.weight <= 0f && target <= 0f)
            {
                if (layer.playing) StopLayer(layer);
                return;
            }

            if (!layer.playing || (!layer.current.isPlaying && layer.incoming == null))
            {
                BeginClip(layer.current, clip);
                layer.playing = true;
            }

            float blend = Mathf.Min(loopBlend, clip.length * 0.5f);
            // Near the end: start the next round on the spare source.
            if (layer.incoming == null && layer.current.time >= clip.length - blend)
            {
                layer.incoming = layer.spare;
                layer.spare = null;
                BeginClip(layer.incoming, clip);
                layer.crossStart = Time.unscaledTime;
            }

            float volume = layer.ambience.volume * masterVolume * layer.weight * sceneFade;
            if (layer.incoming != null)
            {
                float k = blend > 0f ? Mathf.Clamp01((Time.unscaledTime - layer.crossStart) / blend) : 1f;
                layer.current.volume = volume * (1f - k);
                layer.incoming.volume = volume * k;
                if (k >= 1f)
                {
                    layer.current.Stop();
                    layer.spare = layer.current;
                    layer.current = layer.incoming;
                    layer.incoming = null;
                }
            }
            else
            {
                layer.current.volume = volume;
            }
        }

        private static void BeginClip(AudioSource source, AudioClip clip)
        {
            source.clip = clip;
            source.time = 0f;
            source.volume = 0f;
            source.Play();
        }

        private static void StopLayer(Layer layer)
        {
            layer.current.Stop();
            if (layer.incoming != null)
            {
                layer.incoming.Stop();
                layer.spare = layer.incoming;
                layer.incoming = null;
            }
            layer.playing = false;
        }
    }
}
