﻿using Jojatekok.PoloniexAPI;
using Jojatekok.PoloniexAPI.MarketTools;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PoloniexAutoTrader.Strategies
{
    class TrendFollow : Strategy
    {
        double topBuyPrice;
        double topSellPrice;
        static IList<IMarketChartData> candleInfo;
        string lineSeperator = "-------------------";

        public TrendFollow(string strategyName, MarketPeriod marketSeries, CurrencyPair symbol, bool? buy, bool? sell, double volume) : base(strategyName, marketSeries, symbol, buy, sell, volume)
        {
        }
        public override void OnStart()
        {
            // Output window
            outputData = new Strategy1Data();
            outputData.Show();
            outputData.Title = StrategyName + " " + Symbol;
        }

        public override async Task OnBar()
        {
            await TrendBot();
        }

        public override void OnTick(TickerChangedEventArgs ticker)
        {
            // Last ticker price

            if (ticker.CurrencyPair == Symbol)
            {

                Debug.WriteLine("TOP BUY " + ticker.MarketData.OrderTopBuy);
                Debug.WriteLine("TOP SELL " + ticker.MarketData.OrderTopSell);

                topBuyPrice = ticker.MarketData.OrderTopBuy;
                topSellPrice = ticker.MarketData.OrderTopSell;
            }
        }

        public override void OnStop()
        {
            Debug.WriteLine(StrategyName + " Stopped");
        }

        public async Task TrendBot()
        {

            DateTime startdate = new DateTime(2017, 1, 1);
            DateTime enddate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, DateTime.UtcNow.Minute, 0);

            // Get historical data
            candleInfo = await Client.PoloniexClient.Markets.GetChartDataAsync(Symbol, MarketSeries, startdate, enddate);
            // Get data index -1
            var index = candleInfo.Count() - 1;
            // Set MA Period
            int period = 50;
            // Set MA Period 2
            int slowPeriod = 20;
            // Lookback period
            int lookBack = 5;

            // ABR Calculation
            double ABR = Indicators.Indicator.ABR(candleInfo, index, period);
            // Last SMA value
            double SMA = Indicators.Indicator.GetBollingerBandsWithSimpleMovingAverage(candleInfo, index, period)[0];
            // Previous SMA value
            double previousSMA = Indicators.Indicator.GetBollingerBandsWithSimpleMovingAverage(candleInfo, index - 1, period)[0];
            // Last SMA 2 value
            double SMA2 = Indicators.Indicator.GetBollingerBandsWithSimpleMovingAverage(candleInfo, index, slowPeriod)[0];
            // Previous SMA 2 value
            double previousSMA2 = Indicators.Indicator.GetBollingerBandsWithSimpleMovingAverage(candleInfo, index - 1, slowPeriod)[0];

            // CLOSE UNDER / OVER SMA
            // SMA last > previous = Bullish
            bool smaIsRising = IsRising(index, lookBack, period);
            // SMA last < previous = Bearish
            bool smaIsFalling = IsFalling(index, lookBack, period);

            // MA CROSSOVER
            // SMA previous under SMA 2 previous, SMA over SMA2 last value
            bool bullishCross = BullishCrossver(SMA, SMA2, previousSMA, previousSMA2);
            // SMA previous over SMA 2 previous, SMA under SMA2 last value
            bool bearishCross = Bearishrossver(SMA, SMA2, previousSMA, previousSMA2);

            // Candle close under SMA previous & Then close over SMA last.
            var bullishSignal = candleInfo[index - 1].Low < previousSMA && candleInfo[index].Close > SMA;
            // Candle close over SMA previous & Then close under SMA last.
            var bearishSignal = candleInfo[index - 1].High > previousSMA && candleInfo[index].Close < SMA;

            // Output IBS to datawindow
            outputData.Strategy1Output.Text += "SMA at Index" + "\n" + SMA + "\n" + "Candle Close at Index" + "\n" + candleInfo[index].Close
                + "\n" + candleInfo[index].Time + "\n" + lineSeperator + "\n";

            outputData.Strategy1Output.Text += "SMA at Index -1" + "\n" + previousSMA + "\n" + "Candle Close at Index - 1" + "\n" + candleInfo[index - 1].Close
                + "\n" + candleInfo[index - 1].Time + "\n" + lineSeperator + "\n";

            // ABR
            outputData.Strategy1Output.Text += "ABR" + "\n" + ABR.ToStringNormalized() + "\n" + lineSeperator + "\n";


            // Output IBS to datawindow

            // 0.15% fee
            if ((bool)Buy)
            {
                if (bullishSignal && smaIsRising)
                {
                    var volume = quantity.TotalToQuantity(Total, topBuyPrice);
                    await marketOrder.ExecuteMarketOrder(Symbol, OrderType.Buy, volume);
                    // Output IBS to datawindow

                    string tradeOutputBuy = DateTime.Now + "\n" + "Volume = " + volume + "\n" + OrderType.Buy + "\n" + lineSeperator + "\n";
                    Debug.WriteLine(tradeOutputBuy);
                    outputData.Strategy1Output.Text += tradeOutputBuy;
                }
            }

            // 0.25% fee
            if ((bool)Sell)
            {
                if (bearishSignal && smaIsFalling)
                //if (bearishCross)
                {
                    var quoteBalance = balance.GetBalance(Symbol.QuoteCurrency);
                    var balanceQuote = quoteBalance.Result;

                    if (balanceQuote > minOrderSize)
                    {
                        var volume = quantity.TotalToQuantity(Total, topSellPrice);
                        await marketOrder.ExecuteMarketOrder(Symbol, OrderType.Sell, volume);

                        // Output IBS to datawindow
                        string tradeOutputSell = DateTime.Now + "\n" + "Volume = " + volume + "\n" + OrderType.Sell + "\n" + lineSeperator + "\n";
                        Debug.WriteLine(tradeOutputSell);
                        outputData.Strategy1Output.Text += tradeOutputSell;
                    }
                }
            }
        }

        // Check to see if SMA value is higher than SMA Value (x) bars ago (lookback period)
        static bool IsRising(int index, int lookBack, int period)
        {
            bool smaIsRising = false;
            double SMA = 0;
            double previousSMA = 0;

            //for (var i = index; i > Math.Max(index - lookBack, -1); i--)
             for (var i = index; i > (index - lookBack); i--)
                {
                // Last SMA value
                SMA = Indicators.Indicator.GetBollingerBandsWithSimpleMovingAverage(candleInfo, index, period)[0];
                // Previous SMA value
                previousSMA = Indicators.Indicator.GetBollingerBandsWithSimpleMovingAverage(candleInfo, index - i, period)[0];

                if (SMA > previousSMA)
                {
                    smaIsRising = true;                    
                }                
            }
            // Output to debug to check values are correct
            Debug.WriteLine("Is Rising = {0} | SMA {1} | Previous SMA {2} | {3} Bars ago", smaIsRising, SMA, previousSMA, lookBack);

            return smaIsRising;
        }

        // Check to see if SMA value is lower than SMA Value (x) bars ago (lookback period)
        static bool IsFalling(int index, int lookBack, int period)
        {
            bool smaIsFalling = false;
            double SMA = 0;
            double previousSMA = 0;

            //for (var i = index; i > Math.Max(index - lookBack, -1); i--)
              for (var i = index; i > (index - lookBack); i--)
                {
                // Last SMA value
                SMA = Indicators.Indicator.GetBollingerBandsWithSimpleMovingAverage(candleInfo, index, period)[0];
                // Previous SMA value
                previousSMA = Indicators.Indicator.GetBollingerBandsWithSimpleMovingAverage(candleInfo, index - i, period)[0];

                if (SMA < previousSMA)
                {
                    smaIsFalling = true;                   
                }                
            }

            // Output to debug to check values are correct
            Debug.WriteLine("Is Falling = {0} | SMA {1} | Previous SMA {2} | {3} Bars ago", smaIsFalling, SMA, previousSMA, lookBack);

            return smaIsFalling;
        }

        // Check if Fast SMA has crossed above slow SMA
        static bool BullishCrossver(double SMA, double SMA2, double previousSMA, double previousSMA2)
        {
            // CROSS OVER
            bool bullishCross = false;
            if (previousSMA < previousSMA2 && SMA >= SMA2)
            {
                bullishCross = true;
            }
            return bullishCross;
        }

        // Check if Fast SMA has cross below slow SMA
        static bool Bearishrossver(double SMA, double SMA2, double previousSMA, double previousSMA2)
        {
            // CROSS OVER
            bool bearishCross = false;
            if (previousSMA > previousSMA2 && SMA <= SMA2)
            {
                bearishCross = true;
            }
            return bearishCross;
        }

    }
}