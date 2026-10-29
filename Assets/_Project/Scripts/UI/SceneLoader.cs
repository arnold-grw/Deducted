using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deducted.UI
{
    /// <summary>
    /// Loads a scene behind a fade to black (ScreenFade survives the load and fades the new scene in).
    /// Call SceneLoader.Load("Name") from code, or put this on an object and hook LoadScene(string)
    /// into a UnityEvent (e.g. PauseMenu > On Main Menu with "MainMenu").
    /// The scene must be in the build profile's scene list.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float fadeIn = 0.6f;
        [SerializeField, Min(0f)] private float fadeOut = 1f;

        public static bool IsLoading { get; private set; }
        /// <summary>A load began; the value is how long the screen takes to go black (music fades out with it).</summary>
        public static event System.Action<float> LoadStarted;

        public void LoadScene(string sceneName) => Load(sceneName, fadeIn, fadeOut);

        public static void Load(string sceneName, float fadeIn = 0.6f, float fadeOut = 1f)
        {
            if (IsLoading) return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"SceneLoader: scene \"{sceneName}\" isn't in the build profile's scene list.");
                return;
            }
            IsLoading = true;
            LoadStarted?.Invoke(fadeIn);
            ScreenFade.Transition(fadeIn, fadeOut, () =>
            {
                // Coming from the pause menu: unfreeze before the new scene starts.
                Time.timeScale = 1f;
                AudioListener.pause = false;
                SceneManager.LoadScene(sceneName);
                IsLoading = false;
            });
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
