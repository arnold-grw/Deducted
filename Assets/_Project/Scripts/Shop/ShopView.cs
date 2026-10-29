using System.Collections.Generic;
using Deducted.Economy;
using TMPro;
using UnityEngine;

namespace Deducted.Shop
{
    /// <summary>
    /// Draws a shop page: one row per offer (selected = inverted, white box with black text),
    /// the balance before / after the selected purchase and the hold-F bar.
    /// Input is handled by DialogueSystem. Built by Deducted > Build Shop UI.
    /// </summary>
    public class ShopView : MonoBehaviour
    {
        [SerializeField] private RectTransform rowContainer;
        [Tooltip("Inactive row that gets copied per offer.")]
        [SerializeField] private ShopRow rowTemplate;
        [SerializeField] private TMP_Text balanceText;
        [Tooltip("Fill of the hold bar: its width follows the hold progress.")]
        [SerializeField] private RectTransform holdFill;

        [Header("Look")]
        [SerializeField] private string currency = "₵";
        [Tooltip("Shown on the right of a selected entry that can't be bought.")]
        [SerializeField] private string unavailableText = "NOT AVAILABLE";
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color selectedTextColor = Color.black;
        [SerializeField] private Color selectedBackground = Color.white;
        [SerializeField] private Color unavailableTextColor = new Color(0.45f, 0.45f, 0.45f);

        private readonly List<ShopRow> rows = new List<ShopRow>();

        private void Awake()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        public void Refresh(IReadOnlyList<ShopOffer> offers, int selected, float holdProgress, ShopContext ctx)
        {
            while (rows.Count < offers.Count)
            {
                ShopRow row = Instantiate(rowTemplate, rowContainer, false);
                row.name = "Row " + rows.Count;
                rows.Add(row);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < offers.Count;
                if (rows[i].gameObject.activeSelf != used) rows[i].gameObject.SetActive(used);
                if (!used) continue;

                ShopOffer offer = offers[i];
                bool isSelected = i == selected;
                bool available = offer.IsAvailable(ctx);

                string label = offer.DisplayName(ctx);
                if (offer.HasStock) label += $"  ({offer.StockLeft} LEFT)";

                string price = isSelected && !available
                    ? unavailableText
                    : PriceText(offer.BalanceChange(ctx), offer.CostPerSecond(ctx));

                Color color = isSelected ? selectedTextColor : available ? textColor : unavailableTextColor;
                rows[i].Set(label, price, color, isSelected ? selectedBackground : Color.clear);
            }

            if (balanceText != null)
            {
                int balance = BalanceManager.Instance != null ? BalanceManager.Instance.Balance : 0;
                string text = $"BALANCE  {balance} {currency}";
                if (selected >= 0 && selected < offers.Count && offers[selected].IsAvailable(ctx))
                    text += $"     AFTER  {balance + offers[selected].BalanceChange(ctx)} {currency}";
                SetText(balanceText, text);
            }

            if (holdFill != null)
            {
                Vector2 max = holdFill.anchorMax;
                max.x = Mathf.Clamp01(holdProgress);
                holdFill.anchorMax = max;
            }
        }

        // +100 = the player gets money, -100 = pays; subscriptions get a second line with the cost per second.
        private string PriceText(int change, float perSecond)
        {
            string text = change > 0 ? $"+{change} {currency}" : change < 0 ? $"-{-change} {currency}" : $"0 {currency}";
            if (perSecond > 0f) text += $"\n<size=75%>-{perSecond:0.##} {currency} / s</size>";
            return text;
        }

        internal static void SetText(TMP_Text text, string value)
        {
            if (text != null && text.text != value) text.text = value;
        }
    }
}
