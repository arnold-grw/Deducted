namespace Deducted.Body
{
    public enum BodyPartId
    {
        LeftEye,
        RightEye,
        LeftEar,
        RightEar,
        LeftLeg,
        RightLeg,
    }

    public enum BodyPartState
    {
        /// <summary>Your own. Free.</summary>
        Organic,
        /// <summary>Sold, but replaced by a CONTINUUM subscription. Works, costs ₵/s.</summary>
        Synthetic,
        /// <summary>Sold, nothing replaces it.</summary>
        Missing,
    }

    /// <summary>Prices and names for one body part. Edited on PlayerBody.</summary>
    [System.Serializable]
    public class BodyPartConfig
    {
        public BodyPartId id;
        public string displayName;
        [UnityEngine.Tooltip("₵ VITALIS pays when you sell it.")]
        public int sellPrice;
        [UnityEngine.Tooltip("Name of the CONTINUUM replacement, shown on the receipt.")]
        public string subscriptionName;
        [UnityEngine.Tooltip("₵ per second while the synthetic replacement runs.")]
        public float subscriptionCostPerSecond;

        public BodyPartConfig(BodyPartId id, string displayName, int sellPrice, string subscriptionName, float costPerSecond)
        {
            this.id = id;
            this.displayName = displayName;
            this.sellPrice = sellPrice;
            this.subscriptionName = subscriptionName;
            subscriptionCostPerSecond = costPerSecond;
        }
    }
}
