using Deducted.Body;
using Deducted.Economy;
using Deducted.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Deducted.Shop
{
    public enum OfferKind
    {
        /// <summary>Buy Amount cigarettes for Price.</summary>
        Cigarettes,
        /// <summary>VITALIS: sell an organic part (pays its sell price, set on PlayerBody).</summary>
        SellPart,
        /// <summary>CONTINUUM: synthetic part per second (an organic part is traded in and paid out).</summary>
        SubscribePart,
        /// <summary>CONTINUUM: end a running subscription (the part is missing again).</summary>
        CancelSubscription,
        /// <summary>Pays Price (negative = pays out) and runs On Bought.</summary>
        Custom,
    }

    /// <summary>What a shop needs to know about the buyer.</summary>
    public class ShopContext
    {
        public PlayerBody body;
        public Cigarettes cigarettes;
        /// <summary>Shown on the receipt, e.g. the NPC's name.</summary>
        public string vendor;
    }

    /// <summary>One entry in a shop page. Balance change, availability and effect depend on Kind.</summary>
    [System.Serializable]
    public class ShopOffer
    {
        public OfferKind kind = OfferKind.Cigarettes;
        [Tooltip("Shown in the list. Empty = automatic (part name, subscription name, \"Cigarettes x5\").")]
        public string name;
        [Tooltip("Sell Part / Subscribe Part / Cancel Subscription: which part.")]
        public BodyPartId part;
        [Tooltip("Cigarettes / Custom: what the player pays (₵). Negative = the player gets money.")]
        public int price = 20;
        [Tooltip("Cigarettes: how many per purchase.")]
        [Min(1)] public int amount = 1;
        [Tooltip("How often it can be bought. -1 = unlimited.")]
        public int stock = -1;
        [Tooltip("Custom: what happens on purchase (also runs for the other kinds).")]
        public UnityEvent onBought = new UnityEvent();

        [System.NonSerialized] private int bought;

        public bool HasStock => stock >= 0;
        public int StockLeft => HasStock ? Mathf.Max(0, stock - bought) : int.MaxValue;

        public string DisplayName(ShopContext ctx)
        {
            if (!string.IsNullOrEmpty(name)) return name;
            BodyPartConfig config = ctx.body != null ? ctx.body.GetConfig(part) : null;
            switch (kind)
            {
                case OfferKind.Cigarettes: return amount > 1 ? $"Cigarettes x{amount}" : "Cigarette";
                case OfferKind.SellPart: return config != null ? config.displayName : part.ToString();
                case OfferKind.SubscribePart: return config != null ? config.subscriptionName : part.ToString();
                case OfferKind.CancelSubscription: return "Cancel " + (config != null ? config.subscriptionName : part.ToString());
                default: return "Item";
            }
        }

        /// <summary>One-off balance change of a purchase: + = the player gets money, - = pays.</summary>
        public int BalanceChange(ShopContext ctx)
        {
            BodyPartConfig config = ctx.body != null ? ctx.body.GetConfig(part) : null;
            switch (kind)
            {
                case OfferKind.SellPart:
                    return config != null ? config.sellPrice : 0;
                case OfferKind.SubscribePart:
                    // An organic part is traded in (VITALIS pays for it).
                    return config != null && ctx.body.GetOwnedState(part) == BodyPartState.Organic ? config.sellPrice : 0;
                case OfferKind.CancelSubscription:
                    return 0;
                default:
                    return -price;
            }
        }

        /// <summary>Running cost per second this purchase starts (subscriptions), else 0.</summary>
        public float CostPerSecond(ShopContext ctx)
        {
            if (kind != OfferKind.SubscribePart || ctx.body == null) return 0f;
            BodyPartConfig config = ctx.body.GetConfig(part);
            return config != null ? config.subscriptionCostPerSecond : 0f;
        }

        public bool IsAvailable(ShopContext ctx)
        {
            if (StockLeft <= 0) return false;
            switch (kind)
            {
                case OfferKind.Cigarettes: return ctx.cigarettes != null;
                case OfferKind.SellPart: return ctx.body != null && ctx.body.GetOwnedState(part) == BodyPartState.Organic;
                case OfferKind.SubscribePart: return ctx.body != null && ctx.body.GetOwnedState(part) != BodyPartState.Synthetic;
                case OfferKind.CancelSubscription: return ctx.body != null && ctx.body.GetOwnedState(part) == BodyPartState.Synthetic;
                default: return true;
            }
        }

        /// <summary>Buy it. Going into the minus is allowed. False if not available.</summary>
        public bool Buy(ShopContext ctx)
        {
            if (!IsAvailable(ctx)) return false;
            bool done;
            switch (kind)
            {
                case OfferKind.Cigarettes:
                    Charge(ctx);
                    ctx.cigarettes.Add(amount);
                    done = true;
                    break;
                case OfferKind.SellPart: done = ctx.body.Sell(part); break;
                case OfferKind.SubscribePart: done = ctx.body.Subscribe(part); break;
                case OfferKind.CancelSubscription: done = ctx.body.CancelSubscription(part); break;
                default:
                    Charge(ctx);
                    done = true;
                    break;
            }
            if (!done) return false;
            bought++;
            onBought?.Invoke();
            return true;
        }

        private void Charge(ShopContext ctx)
        {
            if (price == 0 || BalanceManager.Instance == null) return;
            string who = string.IsNullOrEmpty(ctx.vendor) ? "" : ctx.vendor + ": ";
            BalanceManager.Instance.Apply(who + DisplayName(ctx), -price);
        }
    }
}
