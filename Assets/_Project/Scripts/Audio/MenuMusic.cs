using Deducted.UI;
using UnityEngine;

namespace Deducted.Audio
{
    /// <summary>
    /// Music for the main menu on both ears. Fades in at the start, loops seamlessly (Loop Blend seconds
    /// before the end a second copy starts from the beginning and the two crossfade) and fades out
    /// together with the screen going black when a scene load starts (NEW GAME).
    /// Put it on any object in the menu scene; makes its own AudioSources.
    /// </summary>
    public class MenuMusic : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
        [Tooltip("Seconds the end of the song overlaps its own restart (at most half the song).")]
        [SerializeField, Min(0f)] private float loopBlend = 3f;
        [Tooltip("Seconds to fade in when the menu opens.")]
        [SerializeField, Min(0f)] private float fadeInTime = 1.5f;
        [Tooltip("Seconds to fade out when the game starts. 0 = as long as the screen takes to go black.")]
        [SerializeField, Min(0f)] private float fadeOutTime = 0f;

        private AudioSource current;
        private AudioSource incoming; // set while the loop crossfade runs
        private AudioSource spare;
        private float crossStart;
        private float fade;           // 0..1 fade in / out
        private float fadeTarget = 1f;
        private float fadeSpeed;

        private void Awake()
        {
            current = CreateSource();
            spare = CreateSource();
            fadeSpeed = fadeInTime > 0f ? 1f / fadeInTime : float.MaxValue;
        }

        private void OnEnable() => SceneLoader.LoadStarted += FadeForLoad;
        private void OnDisable() => SceneLoader.LoadStarted -= FadeForLoad;

        private void Start()
        {
            if (clip == null) return;
            BeginClip(current);
        }

        private void FadeForLoad(float blackIn)
        {
            float time = fadeOutTime > 0f ? fadeOutTime : blackIn;
            fadeTarget = 0f;
            fadeSpeed = time > 0f ? 1f / time : float.MaxValue;
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
            if (clip == null) return;
            fade = Mathf.MoveTowards(fade, fadeTarget, fadeSpeed * Time.unscaledDeltaTime);
            float level = volume * fade;

            float blend = Mathf.Min(loopBlend, clip.length * 0.5f);
            // Near the end: start the next round on the spare source.
            if (incoming == null && current.time >= clip.length - blend)
            {
                incoming = spare;
                spare = null;
                BeginClip(incoming);
                crossStart = Time.unscaledTime;
            }

            if (incoming != null)
            {
                float k = blend > 0f ? Mathf.Clamp01((Time.unscaledTime - crossStart) / blend) : 1f;
                current.volume = level * (1f - k);
                incoming.volume = level * k;
                if (k >= 1f)
                {
                    current.Stop();
                    spare = current;
                    current = incoming;
                    incoming = null;
                }
            }
            else
            {
                current.volume = level;
                if (!current.isPlaying) BeginClip(current);
            }
        }

        private void BeginClip(AudioSource source)
        {
            source.clip = clip;
            source.time = 0f;
            source.volume = 0f;
            source.Play();
        }
    }
}
