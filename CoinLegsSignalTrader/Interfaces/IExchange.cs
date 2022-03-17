using CoinLegsSignalTrader.EventArgs;
using CoinLegsSignalTrader.Model;

namespace CoinLegsSignalTrader.Interfaces
{
    public interface IExchange
    {
        Task<bool> PlaceOrderAsync(string symbolName, decimal signalPrice, bool isShort, bool isLimitOrder, decimal amount, decimal stopLoss, decimal takeProfit, decimal leverage);
        Task<bool> SymbolExists(string symbolName);
        Task<int> GetSymbolDigits(string symbolName);
        Task<bool> SetStopLoss(string symbolName, bool isShort, decimal stopLoss);
        Task<ExchangePositionData> GetPositionInfos(string symbolName);
        event EventHandler<OrderFilledEventArgs> OnOrderFilled;
        event EventHandler<PositionClosedEventArgs> OnPositionClosed;
        event EventHandler<TickerUpdateEventArgs> OnTickerChanged;
    }
}