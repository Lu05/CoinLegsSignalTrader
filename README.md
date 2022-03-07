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
After that go to the folder you put the files and execute 
<br>
`chmod 777 ./CoinLegsSignalTrader`
<br>
to get the needed permissions. The app needs read/write permissions if you want logging. If you don't want to log, only read should be enough.

### General properties
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
| Property| Type|      Usage| 
|:----------|:-------------|:-------------|
MarginMode | sting | Isolated or Cross
OrderTimeout | int | order timeout in seconds if the order is not filled
MaxOpenPositions | int | max open position over all exchanges
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
#### ByBit
https://www.bybit.com/en-US/invite?ref=MOPVGP%230
<br>
Or ref code MOPVGP#0
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

Also I'm using the the libraries of [jkorf](https://github.com/JKorf). U can also support him.
</br>
The icon is from [here](https://www.flaticon.com/free-icons/robot)
