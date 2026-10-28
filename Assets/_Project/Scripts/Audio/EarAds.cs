using System.Collections;
using System.Collections.Generic;
using Deducted.Body;
using UnityEngine;

namespace Deducted.Audio
{
    /// <summary>
    /// CONTINUUM ads played inside rented (synthetic) ears. Every ad starts with the jingle, the ad follows
    /// right after it. Subscribing an ear plays the Welcome Ad; after that a random ad (never the same twice
    /// in a row) every few seconds, more often when both ears are rented. Every ad plays in one ear only:
    /// the welcome ad in the ear just rented, the others in a rented ear (random one when both are rented).
    /// Only one ad at a time. Keeps running in the Tab menu, during animations and pauses.
    /// </summary>
    public class EarAds : MonoBehaviour
    {
        [Tooltip("Empty = found in the scene.")]
        [SerializeField] private PlayerBody body;

        [Header("Clips")]
        [Tooltip("Played before every ad.")]
        [SerializeField] private AudioClip jingle;
        [Tooltip("Played when an ear subscription starts (ad_1).")]
        [SerializeField] private AudioClip welcomeAd;
        [Tooltip("Random ads (without the welcome ad).")]
        [SerializeField] private List<AudioClip> ads = new List<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        [Header("Seconds between ads (min, max)")]
        [SerializeField] private Vector2 intervalOneEar = new Vector2(45f, 90f);
        [SerializeField] private Vector2 intervalBothEars = new Vector2(25f, 50f);

        private AudioSource source;
        private Coroutine playing;
        private float nextAdTime;
        private int lastAd = -1;
        private BodyPartId adEar;

        private bool LeftRented => body != null && body.GetState(BodyPartId.LeftEar) == BodyPartState.Synthetic;
        private bool RightRented => body != null && body.GetState(BodyPartId.RightEar) == BodyPartState.Synthetic;

        private void Awake()
        {
            if (body == null) body = FindFirstObjectByType<PlayerBody>();
            if (body == null) Debug.LogWarning("EarAds: no PlayerBody found.", this);

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.volume = volume;
        }

        private void OnEnable()
        {
            if (body != null) body.PartChanged += OnPartChanged;
        }

        private void OnDisable()
        {
            if (body != null) body.PartChanged -= OnPartChanged;
        }

        private void Start()
        {
            ScheduleNext();
        }

        private void OnPartChanged(BodyPartId part, BodyPartState state)
        {
            if (part != BodyPartId.LeftEar && part != BodyPartId.RightEar) return;

            if (state == BodyPartState.Synthetic) StartAd(welcomeAd, part);
            else if (playing != null && part == adEar) StopAd(); // the ear playing the ad is gone
        }

        private void Update()
        {
            bool left = LeftRented, right = RightRented;
            if (!left && !right) return;

            source.volume = volume;

            if (playing == null && Time.unscaledTime >= nextAdTime)
            {
                // Both rented: a random ear each time.
                bool useLeft = left && (!right || Random.value < 0.5f);
                StartAd(PickAd(), useLeft ? BodyPartId.LeftEar : BodyPartId.RightEar);
            }
        }

        private AudioClip PickAd()
        {
            if (ads.Count == 0) return null;
            int index = Random.Range(0, ads.Count);
            if (ads.Count > 1 && index == lastAd)
                index = (index + 1 + Random.Range(0, ads.Count - 1)) % ads.Count;
            lastAd = index;
            return ads[index];
        }

        private void StartAd(AudioClip ad, BodyPartId ear)
        {
            StopAd();
            adEar = ear;
            // Only inside that ear.
            source.panStereo = ear == BodyPartId.LeftEar ? -1f : 1f;
            playing = StartCoroutine(PlayAd(ad));
        }

        private void StopAd()
        {
            if (playing != null) StopCoroutine(playing);
            playing = null;
            source.Stop();
            ScheduleNext();
        }

        private IEnumerator PlayAd(AudioClip ad)
        {
            if (jingle != null)
            {
                source.clip = jingle;
                source.Play();
                yield return new WaitWhile(() => source.isPlaying);
            }
            if (ad != null)
            {
                source.clip = ad;
                source.Play();
                yield return new WaitWhile(() => source.isPlaying);
            }
            playing = null;
            ScheduleNext();
        }

        // Timer counts from the end of the last ad, in real time (Tab menu or slow motion don't stop it).
        private void ScheduleNext()
        {
            Vector2 range = LeftRented && RightRented ? intervalBothEars : intervalOneEar;
            nextAdTime = Time.unscaledTime + Random.Range(range.x, range.y);
        }
    }
}
