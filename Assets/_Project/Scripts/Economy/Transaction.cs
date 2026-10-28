namespace Deducted.Economy
{
    /// <summary>One line on the receipt. Negative amount = deduction.</summary>
    [System.Serializable]
    public struct Transaction
    {
        public float time;
        public string description;
        public int amount;
        public int balanceAfter;

        public Transaction(float time, string description, int amount, int balanceAfter)
        {
            this.time = time;
            this.description = description;
            this.amount = amount;
            this.balanceAfter = balanceAfter;
        }
    }
}
