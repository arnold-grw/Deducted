using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Deducted.UI
{
    /// <summary>
    /// Full-screen black that fades out again, e.g. when the Collector takes a part (Flash),
    /// or fade to black, switch something, fade back (Transition, e.g. entering a talk).
    /// Call it from anywhere; the canvas creates itself on first use
    /// (dithered like the rest of the UI, on top of everything).
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        private static ScreenFade instance;
        private Image image;
        private Coroutine routine;

        /// <summary>Black for hold seconds, then fades out over fade seconds. Restarts if already running.</summary>
        public static void Flash(float hold, float fade)
        {
            if (instance == null) Create();
            if (instance.routine != null) instance.StopCoroutine(instance.routine);
            instance.routine = instance.StartCoroutine(instance.FlashRoutine(hold, fade));
        }

        private static void Create()
        {
            var go = new GameObject("ScreenFade", typeof(RectTransform), typeof(Canvas));
            DontDestroyOnLoad(go); // stays black through a scene load and fades the new scene in
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // above HUD, dialogue and overview
            go.AddComponent<DitheredCanvas>().MinWorldHeight = 0f; // right at the near plane, nothing in front

            var imageGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
            imageGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)imageGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            instance = go.AddComponent<ScreenFade>();
            instance.image = imageGo.GetComponent<Image>();
            instance.image.raycastTarget = false;
            instance.SetAlpha(0f);
        }

        /// <summary>
        /// Fades to black over fadeIn seconds, calls onBlack (e.g. switch the camera), then fades out over fadeOut.
        /// Replaces a running fade.
        /// </summary>
        public static void Transition(float fadeIn, float fadeOut, System.Action onBlack)
        {
            if (instance == null) Create();
            if (instance.routine != null) instance.StopCoroutine(instance.routine);
            instance.routine = instance.StartCoroutine(instance.TransitionRoutine(fadeIn, fadeOut, onBlack));
        }

        /// <summary>Stops any fade at once (screen visible again); a pending onBlack is not called.</summary>
        public static void Cancel()
        {
            if (instance == null) return;
            if (instance.routine != null) instance.StopCoroutine(instance.routine);
            instance.routine = null;
            instance.SetAlpha(0f);
        }

        private IEnumerator TransitionRoutine(float fadeIn, float fadeOut, System.Action onBlack)
        {
            // From the current darkness, so a transition during a flash doesn't jump.
            for (float t = image.color.a * fadeIn; t < fadeIn; t += Time.unscaledDeltaTime)
            {
                SetAlpha(t / fadeIn);
                yield return null;
            }
            SetAlpha(1f);
            onBlack?.Invoke();
            yield return null; // let the new camera render once before revealing it
            yield return FadeOut(fadeOut);
            routine = null;
        }

        private IEnumerator FadeOut(float fade)
        {
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                SetAlpha(1f - t / fade);
                yield return null;
            }
            SetAlpha(0f);
        }

        private IEnumerator FlashRoutine(float hold, float fade)
        {
            SetAlpha(1f);
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                SetAlpha(1f - t / fade);
                yield return null;
            }
            SetAlpha(0f);
            routine = null;
        }

        private void SetAlpha(float alpha)
        {
            image.color = new Color(0f, 0f, 0f, alpha);
            image.enabled = alpha > 0f;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
