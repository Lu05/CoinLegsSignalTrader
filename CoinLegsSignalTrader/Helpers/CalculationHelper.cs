using System.Globalization;

namespace CoinLegsSignalTrader.Helpers
{
    public class CalculationHelper
    {
        public static string GetPnL(decimal entryPrice, decimal lastPrice, bool isShort, decimal leverage)
        {
            var value = (lastPrice / entryPrice - 1) * leverage * 100;
            if (isShort) value *= -1;

            return value.ToString("F2", CultureInfo.InvariantCulture) + "%";
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