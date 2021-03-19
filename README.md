# SmartStrategies
An automated trading framework for NinjaTrader 8 that adds lots of extra features and convenience.

* SmartTrade class that automatically tracks tons of useful data and provides callbacks for advanced trading logic
* Individual strategy callbacks for cleaner more robust code
* Automatic order management that adapts to your actions. You get direct access to underlying orders and can modify them as you please!
* Default strategy config to remove boilerplate code
* Automatic Tick Replay support. Submits orders to a 1 tick data series when using historical data, but submits to primary data series when using realtime data.

# Disclaimer

Use at your own risk. It's advised not to use this with real money trades, as it's still very untested and in it's early stages.

# Setup
To start using, simply clone this git repository into your `Custom` folder for NinjaTrader 8. By default this is in `MyDocuments/NinjaTrader 8/bin/Custom`. Then open the ninja script project in visual studio. To do this, from the ninja trader script editor, click the button on the top to open visual studio project.

![image](https://user-images.githubusercontent.com/7050512/111719152-b1a3a900-8831-11eb-9367-622b0e33d91a.png)

Once in visual studio, click "show all files" in solution explorer. Then find the SmartStrategies folder and add to solution. Be sure to save your new solution. This may need to be re done if ninja trader re generates their solution.

![image](https://user-images.githubusercontent.com/7050512/111719255-e31c7480-8831-11eb-9e63-2e4d5b13f0bd.png)

# Usage
## High Fidelity Backtesting
Smart Strategies will automatically submit orders on historical data to a 1 tick version of your primary chart. This is exactly the same result as setting "Order Fill Resolution" to High in historical fill settings in Ninja Trader 8. However when done manually like this, allows the use of Tick Replay. This is important because if you want to add any additional data series to your chart, note the BarsInProgressIndex will be 2, not 1. Since index 1 is being used for this feature.

Please follow [this guide](https://ninjatrader.com/support/helpGuides/nt8/?tick_replay.htm) from Ninja Trader to enable the use of tick replay. Now when backtesting you can enable this option. Without this option enabled, orders and logic are not computed on every tick when simulating historical data. While this is much faster, it's not at all accurate for tick by tick strategies. It's recommended you enable it for backtesting any strategies. Smart Strategies will handle the rest!

## Smart Settings
All Smart Strategies have a seperate set of settings specific to them when configuring your strategy in NinjaTrader 8.

- Verbose: This setting enables verbose print statements to help trace exactly what is happening with your trade. 
- Ignore Historical Data: This setting blocks the OnUpdate() method in your strategy when running on historical data. Useful for live testing when you don't care about historical fills and don't want to wait for them to process.

# Creating A SmartStrategy
This section is for those familiar with coding. If you want to make your own, a good place to start is simply reading the source code. I know. I know. But most of the API is at the top of the two major classes, so you don't need to understand the logic. The source files are also well commented. All APIs support visual studio's hint feature. So it's easy to use. I will however provide a template of a simple strategy to get you started!

```csharp
using NinjaTrader.NinjaScript.SmartStrategies;

namespace NinjaTrader.NinjaScript.Strategies
{
  public class SimpleSmartStrategy : BaseSmartStrategy // Be sure it inherit from this class
  {
    private SmartTrade.Configuration tradeConfig;
    private SmartTrade activeTrade;
  
    protected override void SetDefaults()
    {
      // Here you set default values
      Name = "Simple Smart Strategy";
      Description = "An example smart strategy";
    }
  
    protected override void Configure()
    {
      // Here you configure anything, like adding chart indicators
      // For this example we'll make a trade config. You can do this whenever, but
      // if all your trades are the same you mind as well do it here :)
      tradeConfig = new SmartTrade.Configuration
      {
        Strategy = this,
        OrderType = OrderType.Market,
        Quantity = 4,
        AutoProfitAndStop = true,
        InitialStopLoss = 8 * TickSize,
        InitialProfitTarget = 4 * TickSize,
      }
    }
    
    protected override void OnUpdate()
    {
      // Just go long whenever we're not in the market. You can only win if you're in the market, right?
      if (Position.MarketPosition == MarketPosition.Flat && activeTrade == null)
      {
        tradeConfig.SignalName = "GO LONG!!"
        // enter long trade with tradeConfig. tradeConfig is a struct that's copied so
        // you're free to keep using it without fear of it being modified by the trade!
        SmartTrade trade = new SmartTrade(tradeConfig, SmartTrade.PositionType.Long);
        if (trade.Submit())
        {
          // Let's register to one of the many callbacks to we can clear our activeTrade variable
          // when the trade is done
          trade.OnCompleted += OnTradeCompleted;
          activeTrade = trade;
        }
      }
    }
    
    // Called when trade is completed or cancelled
    private void OnTradeCompleted(SmartTrade trade)
    {
      trade.OnCompleted -= OnTradeCompleted;
      activeTrade = null;
    }
  }
}  
```

# Bug Reports
Please use the Issues feature on github. Export any historical or market replay data that caused the error and describe clearly what happened so it can be reproduced on our end!

# Contribution
To contribute, simply fork the project and make pull requests
