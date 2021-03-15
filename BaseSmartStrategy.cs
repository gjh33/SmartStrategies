#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript.Strategies;
#endregion

namespace NinjaTrader.NinjaScript.SmartStrategies
{
    /// <summary>
    /// The class to derive all Smart Strategies from!
    /// </summary>
	public abstract class BaseSmartStrategy : Strategy
	{
        /// <summary>
        /// Callback for when an order is updated.
        /// You can use this for your own order management.
        /// </summary>
        public event Action<Order> OrderUpdated;

        /// <summary>
        /// The correct bars in progress index to submit trades to.
        /// when backtesting this will return the index of the 1 tick chart
        /// when using realtime data this will submit to the main chart
        /// </summary>
        public int BarsInProgressIndex
        {
            get
            {
                return State == State.Historical ? 1 : 0;
            }
        }

        /// <summary>
        /// Called during the Configure state of the strategy
        /// </summary>
        protected virtual void Configure() { }
        /// <summary>
        /// Called on bar update.
        /// This method won't be on any data series except the primary.
        /// This method won't be called until min bars are on the chart.
        /// This method won't be called until CurrentBars[0] >= 1
        /// </summary>
        protected virtual void OnUpdate() { }
        /// <summary>
        /// Called on script termination
        /// </summary>
		protected virtual void Cleanup() { }
        /// <summary>
        /// Called when you should set the default values of your variables
        /// </summary>
		protected virtual void SetDefaults() { }
        /// <summary>
        /// Called once all data has been loaded
        /// </summary>
		protected virtual void OnDataLoaded() { }
        /// <summary>
        /// Called when the script has been activated
        /// </summary>
        protected virtual void OnActivated() { }
        /// <summary>
        /// Called when the strategy begins processing historical data
        /// </summary>
        protected virtual void OnBeginHistoricalData() { }
        /// <summary>
        /// Called when the strategy begins processing realtime data
        /// </summary>
        protected virtual void OnBeginRealtimeData() { }
        /// <summary>
        /// Called when historical data is finished processing
        /// </summary>
        protected virtual void OnHistoricalDataTransition() { }

		sealed protected override void OnStateChange()
		{
			switch (State)
            {
				case State.SetDefaults:
                    // All the NT Defaults are set here
                    Description = "A strategy built using the Smart Strategies framework!";
                    Name = "MyCustomSmartStrategy";
                    Calculate = Calculate.OnBarClose;
                    EntriesPerDirection = 1;
                    EntryHandling = EntryHandling.AllEntries;
                    IsExitOnSessionCloseStrategy = true;
                    ExitOnSessionCloseSeconds = 30;
                    IsFillLimitOnTouch = false;
                    MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                    OrderFillResolution = OrderFillResolution.Standard;
                    Slippage = 0;
                    StartBehavior = StartBehavior.WaitUntilFlat;
                    TimeInForce = TimeInForce.Gtc;
                    TraceOrders = false;
                    RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                    StopTargetHandling = StopTargetHandling.PerEntryExecution;
                    BarsRequiredToTrade = 20;
                    IsUnmanaged = true;
                    // Smart Strategies don't currently support this, but will in the future
                    IsInstantiatedOnEachOptimizationIteration = true;

                    SetDefaults();
                    break;
                case State.Configure:
                    AddDataSeries(BarsPeriodType.Tick, 1);
                    Configure();
                    break;
                case State.DataLoaded:
                    OnDataLoaded();
                    break;
                case State.Active:
                    OnActivated();
                    break;
                case State.Terminated:
                    Cleanup();
                    break;
                case State.Historical:
                    OnBeginHistoricalData();
                    break;
                case State.Realtime:
                    OnBeginRealtimeData();
                    break;
                case State.Transition:
                    OnHistoricalDataTransition();
                    break;
            }
		}

		sealed protected override void OnBarUpdate()
		{
            if (CurrentBar <= BarsRequiredToTrade)
				return;
			if (BarsInProgress != 0) 
				return;
			if (CurrentBars[0] < 1)
				return;
            OnUpdate();
		}

        sealed protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string comment)
        {
            if (OrderUpdated != null) OrderUpdated(order);
        }
    }
}
