<img src="https://user-images.githubusercontent.com/3795343/154130985-939b02b1-a722-4bc2-b9ac-7a16a2b77540.svg" width="200" height="200" />

# CoinLegsSignalTrader
**IMPORTANT**: This software is free of use and YOU and nobody else will be responsible for anything that is executed!
Also this has nothing to do with the official coinlegs team. It is my private work.

## What this tool will do
You will get notified if you have a valid coinlegs.com subscription where API is included.
Once you get a signal it will execute a trade on your configured exchange and will than monitor it.
There is nothing saveed if you restart the application.
So, if a position was entered it will not be monitored anymore.
But stop loss and take profit are at the exchange so this will execute for sure. 

## Configuration
### VPS
It is best to have a linux VPSto run the app 24/7. You don't need one wich much performance. 1GB ram and some GB disk space for logging should be enough.
If you have set up you VPS you can download the files from the [release page](https://github.com/Lu05/CoinLegsSignalTrader/releases).
Put the files wherever  you want to on the server.
If this is the first time you run this you maybe need to install the .Net 6 runtime.
<br>
https://docs.microsoft.com/en-us/dotnet/core/install/linux
<br>
After that go to the folder you put the files and execute 
<br>
`sudo chmod 777 ./CoinLegsSignalTrader`
<br>
to get the needed permissions. The app needs read/write permissions if you want logging. If you don't want to log, only read should be enough.
<br>
To start it run `./CoinLegsSignalTrader` at the folder where you copied the files to.

### General parameters
| Property| Type|      Usage| 
|:----------|:-------------|:-------------|
Port | int | port on which the app will listen
MaxPositions | int | max open positions at the same time
### Exchange
After that go to the appsettings.json file.
It should look like this:
```json:
`{
  "Port": 53145,
  "Exchanges": [
    //    {
    //      "Name": "BybitFutures",
    //      "ApiKey": "Your_Api_Key",
    //      "SecretKey": "Your_Secret_Key",
    //      "RestApiBaseAddress": "https://api-testnet.bybit.com",
    //      "SocketPublicBaseAddress": "wss://stream-testnet.bybit.com/realtime_public",
    //      "SocketPrivateBaseAddress": "wss://stream-testnet.bybit.com/realtime_private"
    //    }
  ],
  "Signals": [
    //{
    //  "Type": 503,
    //  "SignalTypeId": 4,
    //  "Exchange": "BybitFutures",
    //  "Strategy": "BlackFishMoveTakeProfitM2Strategy",
    //  "Leverage": 10.0,
    //  "RiskPerTrade": 100.0,
    //  "SignalName": "",
    //  "TakeProfitIndex": 0
    //},
    //{
    //  "Type": 503,
    //  "SignalTypeId": 1,
    //  "Exchange": "BybitFutures",
    //  "Strategy": "MarketPlaceFixedTakeProfitStrategy",
    //  "Leverage": 10.0,
    //  "RiskPerTrade": 100.0,
    //  "SignalName": "",
    //  "TakeProfitIndex": 2
    //}
  ]
}`
```
At the first line you can configure the port to which the app should listen to. You will need that later for the coinlegs.com configuration.

Next you should configure the exchanges section.
At the moment only ByBit is supported. Enter your API and secret key here. The urls are for testnet. If you want to go live you can find the urls needed [here](https://bybit-exchange.github.io/docs/inverse/#t-authentication).
You will need both, rest and sockets for updates.

#### Parameters

| Property| Type|      Usage| 
|:----------|:-------------|:-------------|
MarginMode | sting | Isolated or Cross
OrderTimeout | int | order timeout in seconds if the order is not filled
MaxOpenPositions | int | max open position over all exchanges
PositionTimeout | int | timeout in seconds when the position will be market closed
### Signals
The next section is the singnals section. Here you should use whatever you like to execute at the exchange.
#### Parameters
| Property|      Type| Usage| 
|:----------|:-------------|:-------------|
| Type | int |  the signal type from coinlegs. 503 is for market place signals 
| SignalTypeId |int|    the id for the signal type from coinlegs. 4 is for BlackFish short for example, 1 is for long
| Exchange |string| name from the exchange on which the order should ne executed 
| Strategy| string| strategy which should be used for the signal
| Leverage|decimal| leverage for this signal
| RiskPerTrade|decimal| risk per trade - dollar value which will be lost if stop loss will be hit with the configured leverage. You will not loose more money for one trade.
| SignalName| string | not in use at the moment
| TakeProfitIndex| int| take profit index for some strategies. 1 is for Target1, 2 for Target2...
TrailingOffset | decimal| offset of between current price and stop loss
TrailingStartOffset | decimal | value where the trailing will start
UseStopLossFromSignal | bool | Use the stop loss from the signal or the stop loss value from the config
TakeProfit | decimal | take profit
StopLoss | decimal | stop loss

> **NOTE** all decimal parameters except Leverage and RiskPerTrade are percentage values. So 1 means 100% and 0.01 means 1%

> **NOTE** not all properties are used in every strategy. If the parameter is not given the default value of the type will be used.
### Config on coinlegs
Can be found [here](https://medium.com/@coinlegs/coinlegs-api-216cbd1978a4).
Remeber to use the port configured at appsettings.config. The url will than look like this:
[http://YourIpAddress:YOUR_PORT/api/notification/listen](http://youripaddress:5000/api/notification/listen)
Your IP address will be shown at the console on start up.
### Logging
There is a nlog.example.config at the release folder.
If you rename it to nlog.config logging will be done as configured at this file.
At the moment I recommend file logging. Otherwise it is hard to find bugs.
#### Telegram support
You can add a chat id and a bot token to the appsettings.json. Look at the example file.
If this is configured you will receive notifications via telegram and there are also some commands to check open positions etc.
Here is how to create a [bot](https://core.telegram.org/bots).

The old method via nlog is still supported.
If you configure both you will receive most notifications twice.
### run the application
If eyerything is configured you could run the application.
It's best to click the 'Test yout link' button on coinlegs to see if you receive notifications.
## Exchanges
### Is binance supported?
No. At the moment only bybit is supported. I can not implement binance because they have some restrictions for some countries and I'm only allowed to trade spot here. If anyone can implement and test it I will merge it here. It shouldn't be too hard. Only the interface IExchange has to be imlemented. This should be done using [this](https://github.com/JKorf/Binance.Net) package. Otherwise there could be dependency problems.
### What if the pair doesn't exist on bybit
No problem. No position will be opended and the signal will be ignored.
## Strategies
The strategies tell the app how a signal should be executed.
At the moment there are only 2 strategies. Maybe more at the future.
If you have any good idea how to handle a signal please don't hasitate to open an issue so we can discuss it.
All the strategies that exists at the moment are not testet enough. Please test it on your own before you go live.
### MarketPlaceFixedTakeProfitStrategy
A simple strategy which executes on the signal with stop loss from the notification and take profit from the TakeProfitIndex configuration.
### BlackFishMoveTakeProfitM2Strategy
More complex strategy. Executes on signal and move the stop loss on price updates.
If the price will hit TP(take profit) 2 SL(stop loss) will be set to break even, TP3 will update SL to TP1, TP4 will update SL to TP3 and if TP5 is hit the position will be closed. TakeProfitIndex is not taken into account here.

![image](https://user-images.githubusercontent.com/3795343/153482176-ab4cfc30-c6b9-427f-9430-d0df3d1c49a6.png)

### MarketPlaceTrailingStopLossStrategy
Simple trailing the stop loss. Starts at TrailingStartOffset and will keep the stop loss TrailingOffset away from the current price.

### MarketPlaceCustomTakeProfitStrategy
Simple strategy which takes the set take profit and stop loss into account.
Thie difference to the MarketPlaceFixedTakeProfitStrategy is that at MarketPlaceFixedTakeProfitStrategy is always used from the signal and TakeProfitIndex have to be set.

## Remote Commands

It is possible to control the bot based on the market situation but it needs to be done by you.
Eg you can write a trading view script and enable or disable the strategies for long or short.
</br>
The endpoint for that is http://YOUR_IP:YOUR_PORT/api/remotecommand/execute
</br>
Please note that if you use trading view for that you need to run your app on port 80. That's a limitation from trading view. If you execute a rest post from anywhere else you can choose your port.
<br>

Params of the json to send are:
| Property|      Type| Required| Description| 
|:----------|:-------------|:-------------|:-------------|
Type | string | Yes | Command type - Values 'ChangeStrategyState' or 'ChangeStrategyRisk'
Target | string | Yes | Command target - Values 'Long', 'Short', 'All'
RiskFactor | decimal | Only for ChangeStrategyRisk | Factor for risk - 0.5 means only 50% risk per trade
IsSignalActive | boolean | Only for ChangeStrategyState | Enables or disables a signal

</br>
ChangeStrategyState will change the state of a signal. If you disable a signal it will not be used for executing trades. Signals are defined at the appsettings.config
</br>
ChangeStrategyRisk is used to change the risk of a signal. If the market does not look good for longs you can change the risk for all long signals.
</br>
</br>
Here is an example of an trading view script

```:
//@version=5
indicator('TestSignal')
alert_array = array.new_string()
array.push(alert_array, '"Type": "ChangeStrategyRisk"')
array.push(alert_array, '"Target": "Short"')
array.push(alert_array, '"RiskFactor": "0.3"')
alertstring = '{' + array.join(alert_array, ', ') + '}'
alert(alertstring)
```

> **NOTE** the direction must be set for each signal. This is new and if you want to use remote command it should be configured at the appsettings.json

## Filters
Filters are a way to filter out some signals based on conditions.
</br>
Examples for a signal with a filter:
```json:
{
      "Type": 503,
      "SignalTypeId": 4,
      "Exchange": "BybitFutures",
      "Strategy": "MarketPlaceTrailingStopLossStrategy",
      "Leverage": 10.0,
      "RiskPerTrade": 100.0,
      "SignalName": "",
      "TrailingOffset": 0.00651140,
      "TrailingStartOffset": 0.0078105,
      "UseStopLossFromSignal": true,
      "TakeProfit": 0.026962144,
      "Direction": "Short",
      "Filters": [
        {
          "Name": "CciFilter",
          "Period": 20,
          "Symbol": "BTCUSDT",
          "Offset": 1
        }
      ]
    }
```
Available filters are
### CciFilter
Will calculate the cci value for the defined symbol. If the signal direction is long and the cci value is less than 0 the signal will not be executed.</br>
If the signal direction is short and the cci value is greater than 0 the signal will not be executed.
</br>
Params
| Property|      Type|  Description| 
|:----------|:-------------|:-------------|
Name | string | CciFilter
Period | int | Period for which the CCI value will be calculated
Symbol | string | Symbol for which the CCI value will be calculated
Offset | int | Offset for the CCI value in days. 2 Means look at the value 2 days ago

Example
```json:
"Filters": [
 {
   "Name": "CciFilter",
   "Period": 20,
   "Symbol": "BTCUSDT",
   "Offset": 1
 }
 ]
```

## FAQ

### How much balance should I have to run the bot?
In short, 12 x RiskPerTrade.</br>
The long version:</br>
It should be enough to execute 4 trades in parallel at the beginning.</br>
Most of the time there will not be so many signals open at the same time.</br>
To do that with isolated margin you need to calculate the margin needed for each trade.</br>
I set the leverage to 18 but there are some pairs where only 12 is available.</br>
If only 12 is available the bot will automatically use the max available leverage.</br>
If you set the leverage above 18 you usually will get liquidated before stop loss is hit (around 3%).</br>
So the margin needed with leverage 18 is around 2 x RiskPerTrade. If the leverage is 12 it is around 3 x RiskPerTrade. So in worst case you have 4 trades with 12x leverage. You would need 4 x 3 x RiskPerTrade => 12x.</br>
This does not apply to cross margin. </br>


### How to run the bot 24/7
I personally use [screen](https://linuxize.com/post/how-to-use-linux-screen/) but there are many different options at linux.</br>
`screen -S YourSessionName` will start a new session. If you start the bot within the session it will run also after you close the console.</br>
`screen -r YourSessionName` will resume the session.</br>



## Support

If you need technical support, want to talk about this project or discuss new ideas you can find it here:
</br>
<img src="https://user-images.githubusercontent.com/3795343/154133549-215dd069-4ca3-4bc6-b3b5-d715b40689c9.png" width="100" />
</br>
https://t.me/CoinLegsSignalTrader


As described above the app is free of use.
If you are making some good money with it you have multiple possibilities to support my work.
### Ref links
You can create an account with my ref link.
<br>
<br>
<img src="https://user-images.githubusercontent.com/3795343/159178504-8d3cf118-9a71-46c8-b85d-593c209511d5.png" width="200" />

#### ByBit
https://partner.bybit.com/b/Lu05
<br>
Or ref code 33417
#### Binance
https://accounts.binance.com/en/register?ref=38895065
<br>
Or ref code 38895065

### Send some crypto
#### BTC
bc1qtf3fg05xp5zau7k20sewpd0qtswfevept9v09v
#### ETH
0x419dB75736Ce12C6100fB3059485E4eBae366f05 
#### BSC
0x419dB75736Ce12C6100fB3059485E4eBae366f05
#### USDT ERC20/BEP20
0x419dB75736Ce12C6100fB3059485E4eBae366f05
#### USDT TRC20
TZA8WRHyd4Bcmu4Nuyto6t8ATsf17fh2Bt

### Used libraries

https://github.com/JKorf/Bybit.Net<br>
https://daveskender.github.io/Stock.Indicators/<br>
https://github.com/TelegramBots/Telegram.Bot<br>
https://github.com/NLog/NLog

</br>
The icon is from [here](https://www.flaticon.com/free-icons/robot)
