using UnityEngine;

namespace Deducted.Audio
{
    /// <summary>
    /// Non-spatial sounds (menu clicks, UI, notifications): always on both ears at full volume,
    /// not affected by missing ears. Call UISound.Play(clip) from anywhere; the player object
    /// creates itself on first use. Play(clip, volume, pan) with pan -1 / 1 = only the left / right ear.
    /// </summary>
    public class UISound : MonoBehaviour
    {
        private static UISound instance;
        private AudioSource source;
        private AudioSource leftSource;
        private AudioSource rightSource;

        public static void Play(AudioClip clip, float volume = 1f) => Play(clip, volume, 0f);

        /// <summary>pan &lt; 0 = left ear only, &gt; 0 = right ear only, 0 = both.</summary>
        public static void Play(AudioClip clip, float volume, float pan)
        {
            if (clip == null) return;
            if (instance == null)
            {
                var go = new GameObject("UISound");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<UISound>();
            }
            AudioSource target = pan < 0f ? instance.leftSource : pan > 0f ? instance.rightSource : instance.source;
            target.PlayOneShot(clip, volume);
        }

        private void Awake()
        {
            source = CreateSource(0f);
            leftSource = CreateSource(-1f);
            rightSource = CreateSource(1f);
        }

        // One source per side: panStereo applies to everything a source plays.
        private AudioSource CreateSource(float pan)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.panStereo = pan;
            s.ignoreListenerPause = true; // menu sounds work in the pause menu
            return s;
        }
    }
}
