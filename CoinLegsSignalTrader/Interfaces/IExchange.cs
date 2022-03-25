using CoinLegsSignalTrader.Enums;
using CoinLegsSignalTrader.EventArgs;
using CoinLegsSignalTrader.Model;
using Skender.Stock.Indicators;

namespace CoinLegsSignalTrader.Interfaces
{
    public interface IExchange
    {
        Task<bool> PlaceOrderAsync(string symbolName, decimal signalPrice, bool isShort, bool isLimitOrder, decimal amount, decimal stopLoss, decimal takeProfit, decimal leverage);
        Task<bool> SymbolExists(string symbolName);
        Task<int> GetSymbolDigits(string symbolName);
        Task<bool> SetStopLoss(string symbolName, bool isShort, decimal stopLoss);
        Task<IList<IQuote>> GetKlines(string symbolName, KLinePeriod period, DateTime start, DateTime end);
        Task<ExchangePositionData> GetPositionInfos(string symbolName);
        event EventHandler<OrderFilledEventArgs> OnOrderFilled;
        event EventHandler<PositionClosedEventArgs> OnPositionClosed;
        event EventHandler<TickerUpdateEventArgs> OnTickerChanged;
    }
}