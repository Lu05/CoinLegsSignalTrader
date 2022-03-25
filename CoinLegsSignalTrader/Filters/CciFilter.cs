using CoinLegsSignalTrader.Enums;
using CoinLegsSignalTrader.Interfaces;
using Skender.Stock.Indicators;

namespace CoinLegsSignalTrader.Filters
{
    /// <summary>
    /// Filter for CCI Values
    /// Long if CCI greater than 0 and Short if CCI less than 0
    /// </summary>
    public class CciFilter: ISignalFilter
    {
        public string Symbol { get; set; }
        public int Period { get; set; }
        public int Offset { get; set; }

        private readonly List<CciResult> _data = new();
        public string Name => "CciFilter";

        public async Task<bool> Pass(ISignal signal, INotification notification, IExchange exchange)
        {
            await TryUpdateValues(exchange);
            var cci = _data.Last(c => c.Date < DateTime.UtcNow.Subtract(TimeSpan.FromDays(Offset)));
            if (signal.Direction == SignalDirection.Long && cci.Cci > 0)
                return true;
            if (signal.Direction == SignalDirection.Short && cci.Cci < 0)
                return true;
            return false;
        }

        private async Task TryUpdateValues(IExchange exchange)
        {
            if (_data.Count == 0)
            {
                var klines = await exchange.GetKlines(Symbol, KLinePeriod.Day, DateTime.UtcNow.Subtract(TimeSpan.FromDays(Period * 2 + Offset)), DateTime.UtcNow);
                var ccis = klines.GetCci(Period).OrderBy(c => c.Date);
                foreach (var cciResult in ccis)
                {
                    _data.Add(cciResult);
                }
                return;
            }

            var now = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day);
            if (now >= _data.Last().Date)
            {
                _data.Clear();
                var klines = await exchange.GetKlines(Symbol, KLinePeriod.Day, DateTime.UtcNow.Subtract(TimeSpan.FromDays(Period * 2 + Offset)), DateTime.UtcNow);
                var ccis = klines.GetCci(Period).OrderBy(c => c.Date);
                foreach (var cciResult in ccis)
                {
                    _data.Add(cciResult);
                }
            }
        }
    }
}
