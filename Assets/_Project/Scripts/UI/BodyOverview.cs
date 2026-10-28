using System.Text;
using Deducted.Body;
using Deducted.Economy;
using Deducted.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deducted.UI
{
    /// <summary>
    /// Body overview (Tab): full-screen background with the body, a state label per part,
    /// the growing receipt on the right and the current balance. Not a pause menu:
    /// the game keeps running (subscriptions bill on), only player movement is frozen.
    /// Built by Deducted > Build Body Overview.
    /// </summary>
    public class BodyOverview : MonoBehaviour
    {
        [System.Serializable]
        public class PartView
        {
            public BodyPartId part;
            [Tooltip("Text next to the part on the body image.")]
            public TMP_Text label;
            [Tooltip("Optional image for this part on the body picture; sprite swaps with the state.")]
            public Image image;
            public Sprite organicSprite;
            public Sprite syntheticSprite;
            public Sprite missingSprite;
        }

        [Tooltip("Everything that is shown/hidden with Tab.")]
        [SerializeField] private GameObject root;
        [SerializeField] private PlayerBody body;
        [SerializeField] private PlayerIdentity identity;
        [Tooltip("Top centre: the player's name, or the subject ID once the name is sold.")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PartView[] parts;
        [SerializeField] private TMP_Text receiptText;
        [SerializeField] private ScrollRect receiptScroll;
        [SerializeField] private TMP_Text balanceText;
        [Tooltip("Optional always-visible balance on the HUD.")]
        [SerializeField] private TMP_Text hudBalanceText;
        [Tooltip("Currency symbol. If it shows as a box, the TMP font lacks the glyph: use \"CR\" or add it to the font.")]
        [SerializeField] private string currency = "₵";
        [Tooltip("Freeze player movement while the overview is open.")]
        [SerializeField] private bool blockMovement = true;

        private readonly StringBuilder receipt = new StringBuilder();
        private bool isOpen;
        private CanvasGroup fadeGroup;

        [Tooltip("Seconds to fade in and out. The 1-bit dither turns the fade into a dissolve.")]
        [SerializeField, Min(0f)] private float fadeDuration = 0.5f;

        [Header("Sounds")]
        [SerializeField] private AudioClip openSound;
        [Tooltip("Empty = uses the open sound.")]
        [SerializeField] private AudioClip closeSound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

        public bool IsOpen => isOpen;

        private void Start()
        {
            if (body == null) body = FindAnyObjectByType<PlayerBody>();
            if (identity == null) identity = FindAnyObjectByType<PlayerIdentity>();
            if (identity != null) identity.IdentityChanged += RefreshName;
            if (playerMovement == null) playerMovement = FindAnyObjectByType<PlayerMovement>();

            if (body != null) body.PartChanged += (_, __) => RefreshParts();
            if (BalanceManager.Instance != null)
            {
                foreach (var t in BalanceManager.Instance.Transactions) AppendReceipt(t);
                BalanceManager.Instance.TransactionAdded += OnTransaction;
                BalanceManager.Instance.BalanceChanged += OnBalanceChanged;
            }

            if (root != null)
            {
                fadeGroup = root.GetComponent<CanvasGroup>();
                if (fadeGroup == null) fadeGroup = root.AddComponent<CanvasGroup>();
                fadeGroup.alpha = 0f;
                root.SetActive(false);
            }
            RefreshParts();
            RefreshBalance();
            RefreshName();
        }

        private void OnDestroy()
        {
            if (BalanceManager.Instance != null)
            {
                BalanceManager.Instance.TransactionAdded -= OnTransaction;
                BalanceManager.Instance.BalanceChanged -= OnBalanceChanged;
            }
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame)
                SetOpen(!IsOpen);

            UpdateFade();
        }

        // Fades the whole overview in/out (unscaled time) and deactivates it once invisible.
        private void UpdateFade()
        {
            if (fadeGroup == null || !root.activeSelf) return;

            float target = isOpen ? 1f : 0f;
            float step = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
            fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, target, step);
            fadeGroup.blocksRaycasts = isOpen;

            if (!isOpen && fadeGroup.alpha <= 0f)
                root.SetActive(false);
        }

        public void SetOpen(bool open)
        {
            if (root == null || open == isOpen) return;
            isOpen = open;
            if (open) root.SetActive(true);
            Deducted.Audio.UISound.Play(open || closeSound == null ? openSound : closeSound, soundVolume);

            if (blockMovement && playerMovement != null)
            {
                // A lock instead of switching input off: an action (smoke, interact) ending while the
                // overview is open can't leave the player frozen.
                if (open) playerMovement.Lock(this);
                else playerMovement.Unlock(this);
            }

            if (open)
            {
                RefreshParts();
                RefreshBalance();
                RefreshName();
                ScrollReceiptToBottom();
            }
        }

        private void OnBalanceChanged(int balance) => RefreshBalance();

        private void OnTransaction(Transaction t)
        {
            AppendReceipt(t);
            if (IsOpen) ScrollReceiptToBottom();
        }

        private void AppendReceipt(Transaction t)
        {
            int seconds = Mathf.FloorToInt(t.time);
            string sign = t.amount >= 0 ? "+" : "-";
            receipt.AppendLine($"[{seconds / 60:00}:{seconds % 60:00}] {t.description}");
            receipt.AppendLine($"        {sign}{Mathf.Abs(t.amount)} {currency}   = {t.balanceAfter} {currency}");
            if (receiptText != null) receiptText.text = receipt.ToString();
        }

        private void RefreshName()
        {
            if (nameText != null && identity != null)
                nameText.text = identity.DisplayName;
        }

        private void RefreshBalance()
        {
            int balance = BalanceManager.Instance != null ? BalanceManager.Instance.Balance : 0;
            float perSecond = SubscriptionManager.Instance != null ? SubscriptionManager.Instance.TotalCostPerSecond : 0f;
            string text = perSecond > 0f ? $"{balance} {currency}  (-{perSecond:0.#} {currency}/s)" : $"{balance} {currency}";
            if (balanceText != null) balanceText.text = text;
            if (hudBalanceText != null) hudBalanceText.text = text;
        }

        private void RefreshParts()
        {
            if (body == null || parts == null) return;
            foreach (var view in parts)
            {
                BodyPartState state = body.GetState(view.part);
                if (view.label != null)
                    view.label.text = StateLabel(view.part, state);
                if (view.image != null)
                {
                    Sprite sprite = state switch
                    {
                        BodyPartState.Synthetic => view.syntheticSprite,
                        BodyPartState.Missing => view.missingSprite,
                        _ => view.organicSprite,
                    };
                    view.image.sprite = sprite;
                    view.image.enabled = sprite != null;
                }
            }
        }

        private string StateLabel(BodyPartId id, BodyPartState state)
        {
            BodyPartConfig config = body.GetConfig(id);
            string name = config != null ? config.displayName.ToUpperInvariant() : id.ToString();
            return state switch
            {
                BodyPartState.Synthetic => $"{name}\nSYNTHETIC - {config?.subscriptionName} ({config?.subscriptionCostPerSecond:0.#} {currency}/s)",
                BodyPartState.Missing => $"{name}\nMISSING - SOLD",
                _ => $"{name}\nORGANIC",
            };
        }

        private void ScrollReceiptToBottom()
        {
            if (receiptScroll == null) return;
            Canvas.ForceUpdateCanvases();
            receiptScroll.verticalNormalizedPosition = 0f;
        }
    }
}
