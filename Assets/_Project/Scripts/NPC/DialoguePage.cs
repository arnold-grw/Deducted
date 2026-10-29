using System.Collections.Generic;
using Deducted.Shop;
using UnityEngine;

namespace Deducted.NPC
{
    public enum PageType
    {
        /// <summary>A spoken line.</summary>
        Line,
        /// <summary>A shop: W/S select, hold Space to buy. Text (optional) is shown in the box below.</summary>
        Shop,
    }

    /// <summary>One step of a talk: a line or a shop. D = next page, A = previous page.</summary>
    [System.Serializable]
    public class DialoguePage
    {
        public PageType type = PageType.Line;
        [TextArea(2, 5)] public string text = "...";
        [Tooltip("Shop pages only.")]
        public List<ShopOffer> offers = new List<ShopOffer>();

        public DialoguePage() { }

        public DialoguePage(string text)
        {
            this.text = text;
        }
    }
}
