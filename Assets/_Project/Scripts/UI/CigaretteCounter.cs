using Deducted.Player;
using TMPro;
using UnityEngine;

namespace Deducted.UI
{
    /// <summary>
    /// Shows how many cigarettes the player has, e.g. "10 CIGS". Sits on a TMP text in the HUD
    /// (built by Deducted > Build Cigarette Counter, bottom left, mirrored from the balance).
    /// </summary>
    public class CigaretteCounter : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [Tooltip("{0} = count.")]
        [SerializeField] private string format = "{0} CIGS";

        private Cigarettes cigarettes;

        private void Reset()
        {
            text = GetComponent<TMP_Text>();
        }

        private void Start()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            cigarettes = FindFirstObjectByType<Cigarettes>();
            if (cigarettes == null)
            {
                Debug.LogWarning("CigaretteCounter: no Cigarettes on the player.", this);
                return;
            }
            cigarettes.CountChanged += Show;
            Show(cigarettes.Count);
        }

        private void OnDestroy()
        {
            if (cigarettes != null) cigarettes.CountChanged -= Show;
        }

        private void Show(int count)
        {
            if (text != null) text.text = string.Format(format, count);
        }
    }
}
