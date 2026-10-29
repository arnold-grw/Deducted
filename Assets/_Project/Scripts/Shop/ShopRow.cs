using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deducted.Shop
{
    /// <summary>One line of a shop list: name left, price right. Copied per offer by ShopView.</summary>
    public class ShopRow : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private LayoutElement layout;
        [Tooltip("Row height; rows with a second price line (subscriptions) get Tall Height.")]
        [SerializeField] private float height = 56f;
        [SerializeField] private float tallHeight = 84f;

        public void Set(string label, string price, Color textColor, Color backgroundColor)
        {
            ShopView.SetText(nameText, label);
            ShopView.SetText(priceText, price);
            nameText.color = textColor;
            priceText.color = textColor;
            background.color = backgroundColor;
            if (layout != null) layout.preferredHeight = price.Contains("\n") ? tallHeight : height;
        }
    }
}
