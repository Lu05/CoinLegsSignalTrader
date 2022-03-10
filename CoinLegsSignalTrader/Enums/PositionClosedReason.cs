namespace CoinLegsSignalTrader.Enums
{
    public enum PositionClosedReason
    {
        /// <summary>
        /// The position is closed by selling it
        /// </summary>
        PositionClosedSell,
        /// <summary>
        /// The order is cancled and the position was never open
        /// </summary>
        PositionCancled
    }
}
