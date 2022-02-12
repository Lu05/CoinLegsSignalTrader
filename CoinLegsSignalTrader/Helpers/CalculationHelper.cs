using System.Globalization;

namespace CoinLegsSignalTrader.Helpers
{
    public class CalculationHelper
    {
        public static string GetPnL(decimal quantity, decimal entryPrice, decimal lastPrice, bool isShort)
        {
            var value = quantity * (lastPrice - entryPrice);
            if (isShort)
            {
                value = quantity * (entryPrice - lastPrice);
            }

            return value.ToString("F2", CultureInfo.InvariantCulture) + "$";
        }

        public static int GetDigits(decimal value)
        {
            return BitConverter.GetBytes(decimal.GetBits(value)[3])[2];
        }

        public static decimal CalculateAmount(decimal riskPerTrade, decimal stopLoss, decimal entryPrice)
        {
            return Math.Round(Math.Abs(riskPerTrade / (1 - stopLoss / entryPrice) / entryPrice), 8);
        }
    }
}