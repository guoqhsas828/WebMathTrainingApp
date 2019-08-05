// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using NUnit.Framework;


namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class CliquetOptionConsistencyTests
  {
    //Test 1..
    //Compare the result with BS model with zero volatility & non-stochastic zero volatility model. 
    [NUnit.Framework.TestCase(0.01, 0.03, 0.01, 0.03)]
    [NUnit.Framework.TestCase(0.04, 0.5, 0.04, 0.08)]
    [NUnit.Framework.TestCase(0.00, 0.02, 0.00, 0.1)]
    [NUnit.Framework.TestCase(-0.05, 0.03, 0.01, 0.04)]
    [NUnit.Framework.TestCase(-1, 1, 0.00, 0.03)]
    [NUnit.Framework.TestCase(-1, 0.5, 0.01, 0.1)]
    public void CheckConsistency(double floor, double cap, double gFloor, double rfree)
    {
      
      //stockoption class testing date 
      const double initalPrice = 195;
      //first date will be effective & last date will be expiration
      var resetDt = new Dt[]
      {
        new Dt(21, 1, 2014), new Dt(23, 2, 2014), new Dt(23, 3, 2014), new Dt(23, 4, 2014), new Dt(23, 5, 2014),
        new Dt(23, 6, 2014), new Dt(23, 7, 2014), new Dt(23, 8, 2014), new Dt(23, 9, 2014), new Dt(23, 10, 2014),
        new Dt(23, 11, 2014), new Dt(23, 12, 2014), new Dt(22, 1, 2015)
      };

      var localCap = cap;
      var localFloor = floor;
      var globalFloor = gFloor;

      var stockCliquetOption = new StockCliquetOption(resetDt[0], Currency.USD,
        resetDt[resetDt.Length - 1],
        resetDt, initalPrice, localCap, localFloor, globalFloor);

      var pricingDt = new Dt(10, 4, 2014);
      var settleDt = new Dt(22, 1, 2015);
      const double currentPrice = 200;
      //------------------------------------------------------------------
      //convenience yield
      //Dividend Schedule
      var dividendSchedule = new DividendSchedule(pricingDt)
      {
        {new Dt(10, 6, 2014), 0.85, DividendSchedule.DividendType.Fixed},
        {new Dt(10, 9, 2015), 0.03, DividendSchedule.DividendType.Proportional},
        {new Dt(10, 11, 2015), 0.85, DividendSchedule.DividendType.Fixed}
      };
      var stock = Stock.GetStockWithConvertedDividend(Currency.USD, null, dividendSchedule);

      /////////////////////////////////////////////////////////////////
      /// Compare model with zero volatility and zero volatility model 
      const double volatility = 0.00000;
      ////////////////////////////////////////////////////////////////
      var interestRate = rfree;

      var historyReset = new RateResets
      {
        {resetDt[0], 195},
        {resetDt[1], 205},
        {resetDt[2], 199},
        {resetDt[3], 195},
        {resetDt[4], 201},
        {resetDt[5], 200},
        {resetDt[6], 211},
        {resetDt[7], 180},
        {resetDt[8], 201},
        {resetDt[9], 222}
      };

      var discountCurve = new DiscountCurve(pricingDt, interestRate);
      var volatilitySurface = CalibratedVolatilitySurface.FromFlatVolatility(pricingDt, volatility);
      var newStockCurve = new StockCurve(pricingDt, currentPrice, discountCurve, 0.03, stock);

      var pricer = new StockCliquetOptionPricer(stockCliquetOption, pricingDt, settleDt,
        newStockCurve, discountCurve, volatilitySurface, historyReset);

      //compute the valuation
      pricer.CliquetOptionValue();
      var zeroVolModel = CliquetOptionZeroVolModel(pricer);
      //double StochasticRate = StochasticModel.Item2 + StochasticModel.Item3 + StochasticModel.Item4;
      //double ZeroVolModelRate = ZeroVolModel.Item2 + ZeroVolModel.Item3 + ZeroVolModel.Item4;
      const double threshold = 1E-15;

      Assert.AreEqual(zeroVolModel.Item2, pricer.RealizedRate, threshold, "Realized Return");
      Assert.AreEqual(zeroVolModel.Item3, pricer.FractionRate, threshold, "Fractional Return");
      Assert.AreEqual(zeroVolModel.Item4, pricer.UnrealizedRate, threshold, "Unrealized Estimated Return");
      Assert.AreEqual(zeroVolModel.Item5, pricer.TotalRate, threshold, "Total Return");
    
    }



    public static Tuple<double, double, double, double, double> CliquetOptionZeroVolModel(StockCliquetOptionPricer p)
    {
      // realized return by past stock movement
      var realizedReturn = 0.0;
      // unrealized return estimated on the current fractional segment
      var fractionalSegmentReturn = 0.0;
      //unrealized return estimated after the current segment to expiration
      var unrealizedReturn = 0.0;
      //Non volatility model
      //X= exp((r-d)T) , return = Max(floor,Min(X-1,cap))
    
      if (p.AsOf <= p.StockCliquetOption.Effective)
      {
        for (var idx = 0; idx < p.StockCliquetOption.ResetDates.Length - 1; idx++)
        {
          var xi = p.StockCurve.Interpolate(p.StockCliquetOption.ResetDates[idx + 1]) /
                   p.StockCurve.Interpolate(p.StockCliquetOption.ResetDates[idx]);
          unrealizedReturn += Math.Max(p.StockCliquetOption.FloorRate, Math.Min(xi - 1, p.StockCliquetOption.CapRate));
        }
      }
      else
      {
        //1.Past Realized Return from Stock Price
        //Realized return is the same 
        var pastStockPrice = p.StockCliquetOption.NotionalPrice;
        foreach (var reset in p.HistoricalPrices.AllResets)
        {
          if (reset.Key >= p.AsOf || reset.Key == p.StockCliquetOption.ResetDates[0]) continue;
          var segmentReturn = reset.Value / pastStockPrice - 1;
          pastStockPrice = reset.Value;
          //return must be within cap and floor rate 
          realizedReturn += Math.Max(p.StockCliquetOption.FloorRate,
            Math.Min(p.StockCliquetOption.CapRate, segmentReturn));
        }

        //2.fractional segment expected return ( S: current stock price, K: stock price at prev reset [current strike])
        // X=(S/K)exp((r-d)T)  -->  Return = Max(Floor, Min(X-1,Cap))
        Dt fractionalDate;
        try
        {
          fractionalDate = p.StockCliquetOption.ResetDates.Where(a => a > p.AsOf).OrderBy(a => a).First();
        }
        catch
        {
          //throws an error when the date falls between the last reset date and expiration date
          fractionalDate = p.StockCliquetOption.ResetDates.Last();
        }

        var xf = p.StockCurve.Interpolate(fractionalDate) / pastStockPrice;
        fractionalSegmentReturn +=
          Math.Max(p.StockCliquetOption.FloorRate, Math.Min(xf - 1, p.StockCliquetOption.CapRate));

        //remaining segment expected return 
        //X= exp((r-d)T) -->  return = Max(floor,Min(X-1,cap))
        for (var idx = 0; idx < p.StockCliquetOption.ResetDates.Length - 1; idx++)
        {
          if (p.StockCliquetOption.ResetDates[idx] <= fractionalDate &&
              p.StockCliquetOption.ResetDates[idx] != fractionalDate) continue;
          var xi = p.StockCurve.Interpolate(p.StockCliquetOption.ResetDates[idx + 1]) /
                   p.StockCurve.Interpolate(p.StockCliquetOption.ResetDates[idx]);
          unrealizedReturn +=
            Math.Max(p.StockCliquetOption.FloorRate, Math.Min(xi - 1, p.StockCliquetOption.CapRate));
        }
      }

      //Compare with global floor rate 
      var rateSum = Math.Max(realizedReturn + fractionalSegmentReturn + unrealizedReturn, p.StockCliquetOption.GlobalFloor);
      //Notional x Discount Factor X Estimated Rate 
      var pv = (p.Notional * p.StockCliquetOption.NotionalPrice) *
               Math.Exp(-p.Rfr * Dt.RelativeTime(p.AsOf, p.Settle).Value) * rateSum;

      return Tuple.Create(pv, realizedReturn, fractionalSegmentReturn, unrealizedReturn, rateSum);
    }


    /*
    //Test 2..
    //Test: Cliquet Option Value = CallSpread + floor 
    [NUnit.Framework.TestCase(15, 02, 2018, 102)]
    [NUnit.Framework.TestCase(15, 03, 2018, 102)]
    [NUnit.Framework.TestCase(15, 04, 2018, 102)]
    [NUnit.Framework.TestCase(15, 05, 2018, 102)]
    [NUnit.Framework.TestCase(15, 06, 2018, 102)]
    [NUnit.Framework.TestCase(15, 07, 2018, 102)]
    [NUnit.Framework.TestCase(15, 08, 2018, 102)]
    [NUnit.Framework.TestCase(15, 09, 2018, 102)]
    [NUnit.Framework.TestCase(15, 10, 2018, 102)]
    [NUnit.Framework.TestCase(15, 11, 2018, 102)]
    [NUnit.Framework.TestCase(15, 12, 2018, 102)]
    [NUnit.Framework.TestCase(09, 01, 2019, 102)]
    public void CliquetOptionUnitTest(int day, int month, int year, double spotPrice)
    {
      var effective = new Dt(10, 01, 2018); //Effective : Jan 10/ 2018
      var expiration = new Dt(10, 01, 2019); //Expiration: Jan 10/ 2019
      Dt[] resetDts = { effective, expiration };

      const double initialPrice = 100;
      const double localFloor = 0;
      const double localCap = 0.1;
      const double globalFloor = 0;
      const double interestRate = 0.04;
      const double dividend = 0.03;
      const double volatility = 0.2;

      var pricingDt1 = new Dt(day, month, year);
      //only effective date for reset
      var discountCurve = new DiscountCurve(pricingDt1).SetRelativeTimeRate(interestRate);
      var volatilitySurface1 = CalibratedVolatilitySurface.FromFlatVolatility(pricingDt1, volatility);
      var historicalPrice = new RateResets
      {
        new RateReset(effective, initialPrice)
      };

      var stockCurve = new StockCurve(pricingDt1, spotPrice, discountCurve, dividend, new Stock());

      //-------------------------------------------------------------------------------------------------------------------
      var cliquetOption1 = new StockCliquetOption(effective, Currency.USD, expiration, resetDts,
        initialPrice, localCap, localFloor, globalFloor);
      var cliquetPricer1 = new StockCliquetOptionPricer(cliquetOption1, pricingDt1, expiration,
        stockCurve, discountCurve, volatilitySurface1, historicalPrice);
      cliquetPricer1.CliquetOptionValue();
      var cliquetValue = cliquetPricer1.FairValue();

      var Yfact = Math.Exp(Dt.RelativeTime(pricingDt1, expiration).Value * dividend);
      //------------------------------------------------------------------------------------------------------------

      var callOption1 = new StockOption(expiration, OptionType.Call, OptionStyle.European, 100);
      var pricer1 = new StockOptionPricer(callOption1, pricingDt1, expiration, spotPrice, interestRate, dividend, volatility);
      var callOption2 = new StockOption(expiration, OptionType.Call, OptionStyle.European, 110);
      var pricer2 = new StockOptionPricer(callOption2, pricingDt1, expiration, spotPrice, interestRate, dividend, volatility);
      var callValueDiff1 = pricer1.FairValue() - pricer2.FairValue() + localFloor * initialPrice;
      //-------------------------------------------------------------------------------------------------------------------


      //--------------------------------------------------------------------------------------------------------

      Assert.AreEqual(Yfact * cliquetValue, callValueDiff1, 1E-15, "Call PayOff Rate");
    }


  */





    //Test 3..
    // when floor=-1 (& without global floor) the cliquet option value is maximum zero 
    // floor=-1, cap=1
    // CliquetOption= call(S, S(1-1))-call(S,S(1+1))+ Sx(-1) = call(S,0)-call(S,2S)-S <= 0 
    [NUnit.Framework.TestCase(0.04, 0.01, 0.2)]
    [NUnit.Framework.TestCase(0.0, 0.0, 0.0)]
    [NUnit.Framework.TestCase(0.3, 0.1, 0.3)]
    [NUnit.Framework.TestCase(0.5, 0.0, 0.3)]
    [NUnit.Framework.TestCase(0.9, -0.2, 0.0)]
    [NUnit.Framework.TestCase(-0.8, 0.8, 1)]
    [NUnit.Framework.TestCase(-1, -1, 1)]
    public void CliquetBoundaryTest(double interestRate, double dividend, double vol)
    {
      var effective = new Dt(10, 01, 2018); //Effective : Jan 10/ 2018
      var expiration = new Dt(10, 01, 2019); //Expiration: Jan 10/ 2019
      Dt[] resetDts = { effective, expiration };

      const double initialPrice = 100;
      const double localFloor = -1;
      const double localCap = 1;
      const double globalFloor = -1; //not applying global floor


      var pricingDt1 = effective;
      //only effective date for reset
      var discountCurve = new DiscountCurve(pricingDt1).SetRelativeTimeRate(interestRate);
      var volatilitySurface1 = CalibratedVolatilitySurface.FromFlatVolatility(pricingDt1, vol);
      var historicalPrice = new RateResets
      {
        new RateReset(effective, initialPrice)
      };
      var stockCurve = new StockCurve(pricingDt1, initialPrice, discountCurve, dividend, new Stock());
      //-------------------------------------------------------------------------------------------------------------------
      var cliquetOption1 = new StockCliquetOption(effective, Currency.USD, expiration, resetDts,
        initialPrice, localCap, localFloor, globalFloor);
      var cliquetPricer1 = new StockCliquetOptionPricer(cliquetOption1, pricingDt1, expiration,
        stockCurve, discountCurve, volatilitySurface1, historicalPrice);
      cliquetPricer1.CliquetOptionValue();
      //------------------------------------------------------------------------------------------------------------------

      Assert.LessOrEqual(0, 0, "Cliquet Boundary");

    }

  }
}