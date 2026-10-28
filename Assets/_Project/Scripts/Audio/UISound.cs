using UnityEngine;

namespace Deducted.Audio
{
    /// <summary>
    /// Non-spatial sounds (menu clicks, UI, notifications): always on both ears at full volume,
    /// not affected by missing ears. Call UISound.Play(clip) from anywhere; the player object
    /// creates itself on first use.
    /// </summary>
    public class UISound : MonoBehaviour
    {
        private static UISound instance;
        private AudioSource source;

        public static void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            if (instance == null)
            {
                var go = new GameObject("UISound");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<UISound>();
            }
            instance.source.PlayOneShot(clip, volume);
        }

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.panStereo = 0f;
        }
    }
}
