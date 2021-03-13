# SmartStrategies
An automated trading framework for NinjaTrader 8 that adds lots of extra features and convenience.

* SmartTrade class that automatically tracks tons of useful data and provides callbacks for advanced trading logic
* Individual strategy callbacks for cleaner more robust code
* Automatic order management that adapts to your actions. You get direct access to underlying orders and can modify them as you please!
* Default strategy config to remove boilerplate code
* Automatic Tick Replay support. Submits orders to a 1 tick data series when using historical data, but submits to primary data series when using realtime data.

# Setup
To start using, simply clone this git repository into your `Custom` folder for NinjaTrader 8. By default this is in `MyDocuments/NinjaTrader 8/bin/Custom`
