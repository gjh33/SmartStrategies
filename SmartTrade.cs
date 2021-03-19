#region Using declarations
using System;
using NinjaTrader.Cbi;
#endregion

namespace NinjaTrader.NinjaScript.SmartStrategies
{
    public class SmartTrade
    {
        /// <summary>
        /// Long = Buy order going long
        /// Short = Short sell order going short
        /// </summary>
        public enum PositionType { Long, Short }
        /// <summary>
        /// None = No orders submitted
        /// Submitted = entry order submitted
        /// InMarket = order at least partially filled
        /// Completed = Stop loss or profit target fully filled and entry order filled or cancelled
        /// </summary>
        public enum MarketStatus { None, Submitted, InMarket, Completed }
        /// <summary>
        /// None = Trade hasn't completed
        /// StopLossHit = Stop loss was hit and the trade is a loss
        /// ProfitTargetHit = Profit target was hit and trade is a win
        /// Mixed = Profit target partially filled, and the rest was stop lossed
        /// </summary>
        public enum CompletionType { None, TotalLoss, TotalProfit, Mixed, Cancelled }
        /// <summary>
        /// None = nothing has been filled
        /// Full = order has been fully filled
        /// Partial = order has been partially filled
        /// </summary>
        public enum FillType { None, Full, Partial }

        public PositionType Position { get; private set; }
        public MarketStatus Status { get; private set; }
        public CompletionType Completion { get; private set; }
        public FillType Fill { get; private set; }
        public Configuration Config { get; private set; }
        /// <summary>
        /// How many actually got filled
        /// </summary>
        public int FillCount { get; private set; }
        /// <summary>
        /// How many successfully made a profit
        /// </summary>
        public int ProfitCount { get; private set; }
        /// <summary>
        /// How many got stopped out
        /// </summary>
        public int StopLossCount { get; private set; }
        /// <summary>
        /// The entry order for entering the trade
        /// </summary>
        public Order EntryOrder { get; private set; }
        /// <summary>
        /// The stop loss order used for stoplosses
        /// </summary>
        public Order StopLoss { get; private set; }
        /// <summary>
        /// The profit target order used for profit target
        /// </summary>
        public Order ProfitTarget { get; private set; }
        /// <summary>
        /// The bar index the trade was first submitted one
        /// </summary>
        public int SubmittedBarIndex { get; private set; }
        public double ProfitTargetPrice
        {
            get
            {
                if (EntryOrder == null) return Position == PositionType.Long ? Config.Strategy.GetCurrentAsk() + Config.InitialProfitTarget : Config.Strategy.GetCurrentBid() - Config.InitialProfitTarget;
                return Position == PositionType.Long ? EntryOrder.AverageFillPrice + Config.InitialProfitTarget : EntryOrder.AverageFillPrice - Config.InitialProfitTarget;
            }
        }

        public double StopLossPrice
        {
            get
            {
                if (EntryOrder == null) return Position == PositionType.Long ? Config.Strategy.GetCurrentAsk() - Config.InitialStopLoss : Config.Strategy.GetCurrentBid() + Config.InitialStopLoss;
                return Position == PositionType.Long ? EntryOrder.AverageFillPrice - Config.InitialStopLoss : EntryOrder.AverageFillPrice + Config.InitialStopLoss;
            }
        }

        // Events you can register to. You're welcome :)
        // OnSubmitted = when the first entry order is submitted. Called immediately after successful Submit
        // OnMarketEntered = when any quantity from the entry order is first filled
        // OnCompleted = when all trades have resolved and the trade is done
        // OnStoppedOut = called each time some quantity is filled on a stop loss
        // OnProffited = called each time some quantity is filled on a profit target
        // OnFilled = called each time some quantity is filled on an entry order
        public event Action<SmartTrade> OnSubmitted, OnMarketEntered, OnCompleted, OnStoppedOut, OnProfitted, OnFilled;

        private Guid OCOID;

        /// <summary>
        /// Creates a new smart trade. Does not submit on creation.
        /// You must call <see cref="Submit"/> first.
        /// </summary>
        /// <param name="config">The trade configuration</param>
        /// <param name="position">The position you want to take with this trade</param>
        public SmartTrade(Configuration config, PositionType position)
        {
            Config = config;
            Position = position;
            Init();
        }

        ~SmartTrade()
        {
            Config.Strategy.OrderUpdated -= OrderUpdatedCallback;
        }

        private void Init()
        {
            GenerateUniqueOCOID();
            Config.Strategy.OrderUpdated += OrderUpdatedCallback;
        }


        /// <summary>
        /// Attempts to submit the entry order for the trade
        /// </summary>
        /// <returns>Whether the entry order was submitted successfully</returns>
        public bool Submit()
        {
            if (Status != MarketStatus.None) return false;
            OrderAction action = Position == PositionType.Long ? OrderAction.Buy : OrderAction.SellShort;
            EntryOrder = Config.Strategy.SubmitOrderUnmanaged(Config.Strategy.BarsInProgressIndex, action, Config.OrderType, Config.Quantity, Config.InitialLimitPrice, Config.InitialStopPrice, "", Config.SignalName);
            if (EntryOrder != null)
            {
                SubmittedBarIndex = Config.Strategy.CurrentBar;
                Status = MarketStatus.Submitted;
                Config.Strategy.VPrint("T: Status Updated: Submitted");
                if (OnSubmitted != null) OnSubmitted(this);
            }
            return EntryOrder != null;
        }

        /// <summary>
        /// Cancels the trade. A trade can only be cancelled
        /// if it's not in market or completed yet
        /// </summary>
        /// <returns>whether cancellation was successful</returns>
        public bool TryCancel()
        {
            if (Status != MarketStatus.Submitted) return false;
            Config.Strategy.CancelOrder(EntryOrder);
            return true;
        }

        private void GenerateUniqueOCOID()
        {
            OCOID = Guid.NewGuid();
        }

        private void OrderUpdatedCallback(Order order)
        {
            if (order == null) return;
            if (order != EntryOrder && order != StopLoss && order != ProfitTarget) return;
            if (order.OrderState == OrderState.Rejected)
            {
                throw new Exception("An order was rejected! Strategy failure!");
            }
            // If our stop loss or profit target are filled before we fully fill the entry order
            // then we should just cancel the original order
            if (order == StopLoss || order == ProfitTarget)
            {
                if (order.OrderState == OrderState.Filled && EntryOrder.OrderState != OrderState.Filled)
                {
                    Config.Strategy.VPrint("T: Cancelling Remaining Entry Orders");
                    Config.Strategy.CancelOrder(EntryOrder); // OCO will handle the other orders
                }
            }
            if (order == EntryOrder && order.Filled > 0)
            {
                // If our order has been filled and we don't have a StopLoss yet then create one and a profit target
                // for the filled amount
                if (StopLoss == null && Config.AutoProfitAndStop)
                {
                    Config.Strategy.VPrint("T: Creating Stop Loss and Profit Target");
                    OrderAction action = Position == PositionType.Long ? OrderAction.Sell : OrderAction.BuyToCover;
                    StopLoss = Config.Strategy.SubmitOrderUnmanaged(Config.Strategy.BarsInProgressIndex, action, OrderType.StopMarket, order.Filled, 0, StopLossPrice, OCOID.ToString(), "Stop Loss");
                    ProfitTarget = Config.Strategy.SubmitOrderUnmanaged(Config.Strategy.BarsInProgressIndex, action, OrderType.Limit, order.Filled, ProfitTargetPrice, 0, OCOID.ToString(), "Profit Target");
                    if (StopLoss == null || ProfitTarget == null)
                    {
                        // Just in case, throw exception
                        throw new Exception("Failed to create Stop Loss or Profit Target order. This shouldn't happen.");
                    }
                }

                // if order filled is greater than our stop loss and our stop loss hasn't been completely filled
                // update the stop loss and profit target quantities
                if (StopLoss != null && order.Filled > StopLoss.Quantity && StopLoss.OrderState != OrderState.Filled && ProfitTarget.OrderState != OrderState.Filled)
                {
                    Config.Strategy.VPrint("T: Adjusting Stop Loss and Profit Target Quantities To New Fill Count");
                    Config.Strategy.ChangeOrder(StopLoss, order.Filled, StopLoss.LimitPrice, StopLoss.StopPrice);
                    Config.Strategy.ChangeOrder(ProfitTarget, order.Filled, ProfitTarget.LimitPrice, ProfitTarget.StopPrice);
                }

                // NOTE: We changed and modified any orders before updating states or callbacks
                if (order.Filled != FillCount)
                {
                    Filled();
                }

                // Ensure fill count and fill type are updated before market status callback
                if (Status == MarketStatus.None)
                {
                    MarketEntered();
                }
            }
            if (order == EntryOrder && order.OrderState == OrderState.Cancelled && Status == MarketStatus.Submitted)
            {
                Status = MarketStatus.Completed;
                Completion = CompletionType.Cancelled;
                Config.Strategy.VPrint("T: Trade Cancelled");
                Config.Strategy.VPrint("T: Status Updated: Completed");
                if (OnCompleted != null) OnCompleted(this);
            }
            if (order == StopLoss)
            {
                if (StopLossCount != StopLoss.Filled)
                {
                    StoppedOut();
                }
            }
            if (order == ProfitTarget)
            {
                if (ProfitCount != ProfitTarget.Filled)
                {
                    Profitted();
                }
            }
            // Completion events are always last so all data is filled first!
            if ((order == StopLoss || order == ProfitTarget) && ProfitTarget != null && StopLoss != null)
            {
                if (StopLoss.Filled + ProfitTarget.Filled == EntryOrder.Filled)
                {
                    CompleteTrade();
                }
            }
        }

        private void Filled()
        {
            if (EntryOrder.Filled == EntryOrder.Quantity)
            {
                Fill = FillType.Full;
            }
            else
            {
                Fill = FillType.Partial;
            }
            FillCount = EntryOrder.Filled;
            Config.Strategy.VPrint("T: Entry Filled! Total: " + FillCount);
            if (OnFilled != null) OnFilled(this);
        }

        private void MarketEntered()
        {
            Status = MarketStatus.InMarket;
            Config.Strategy.VPrint("T: Status Updated: Market Entered");
            if (OnMarketEntered != null) OnMarketEntered(this);
        }

        private void StoppedOut()
        {
            StopLossCount = StopLoss.Filled;
            Config.Strategy.VPrint("T: Stopped Out " + StopLossCount);
            if (OnStoppedOut != null) OnStoppedOut(this);
        }

        private void Profitted()
        {
            ProfitCount = ProfitTarget.Filled;
            Config.Strategy.VPrint("T: Profited " + ProfitCount);
            if (OnProfitted != null) OnProfitted(this);
        }

        private void CompleteTrade()
        {
            if (ProfitTarget.Filled == EntryOrder.Filled)
            {
                Completion = CompletionType.TotalProfit;
                Config.Strategy.VPrint("T: Total Profit");
            }
            else if (StopLoss.Filled == EntryOrder.Filled)
            {
                Completion = CompletionType.TotalLoss;
                Config.Strategy.VPrint("T: Total Loss");
            }
            else
            {
                Completion = CompletionType.Mixed;
                Config.Strategy.VPrint("T: Mixed Results");
            }
            Status = MarketStatus.Completed;
            Config.Strategy.VPrint("T: Status Updated: Completed");
            Config.Strategy.OrderUpdated -= OrderUpdatedCallback;
            if (OnCompleted != null) OnCompleted(this);
        }

        public struct Configuration
        {
            /// <summary>
            /// The smart strategy this trade executes on
            /// </summary>
            public BaseSmartStrategy Strategy;
            /// <summary>
            /// The entry order type for this trade
            /// </summary>
            public OrderType OrderType;
            /// <summary>
            /// The target quantity of the trade
            /// </summary>
            public int Quantity;
            /// <summary>
            /// If limit order, set this for limit price
            /// </summary>
            public double InitialLimitPrice;
            /// <summary>
            /// If stop order, set this for stop price
            /// </summary>
            public double InitialStopPrice;
            /// <summary>
            /// Optional signal name
            /// </summary>
            public string SignalName;
            /// <summary>
            /// Whether profit target and stop loss are managed automatically
            /// </summary>
            public bool AutoProfitAndStop;
            /// <summary>
            /// Currency amount below entry point to place auto stop loss
            /// </summary>
            public double InitialStopLoss;
            /// <summary>
            /// Currency amount above entry point to place auto profit target
            /// </summary>
            public double InitialProfitTarget;

            public Configuration(BaseSmartStrategy strategy, OrderType orderType, int quantity, double initialLimitPrice, double initialStopPrice, string signalName, bool autoProfitAndStop, double initialStopLoss, double initialProfitTarget)
            {
                Strategy = strategy;
                OrderType = orderType;
                Quantity = quantity;
                InitialLimitPrice = initialLimitPrice;
                InitialStopPrice = initialStopLoss;
                SignalName = signalName;
                AutoProfitAndStop = autoProfitAndStop;
                InitialStopLoss = initialStopLoss;
                InitialProfitTarget = initialProfitTarget;
            }
        }
    }
}
