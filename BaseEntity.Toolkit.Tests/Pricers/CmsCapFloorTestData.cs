//
// Copyright (c)    2015. All rights reserved.
//
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

// ReSharper disable once CheckNamespace
namespace BaseEntity.Toolkit.Tests.Pricers.CmsCapFloorTestData
{
  #region All Test Data

  internal partial class Data
  {
    public static readonly Data[] All =
    {
      new Data(Name.SimpleSurfaceFlat)
      {
        CmsIndexId = "USDCMS_1Y_QUARTERLY",
        Effective = new Toolkit.Base.Dt(20160405),
        Maturity = new Dt(20260406),
        CapFloorType = CapFloorType.Cap,
        Strike = 0.02,
        Rate = 0.02,
        CurrentReset = 0.05,
        Volatility = new Volatility(Volatility.Flat, 0.5)
      },
      new Data(Name.RateCubeFlat)
      {
        CmsIndexId = "USDCMS_1Y_QUARTERLY",
        Effective = new Dt(20160405),
        Maturity = new Dt(20260406),
        CapFloorType = CapFloorType.Cap,
        Strike = 0.02,
        Rate = 0.02,
        CurrentReset = 0.05,
        Volatility = new Volatility(Volatility.RateCube, 0.5)
      },
      new Data(Name.SwaptionMarketCube)
      {
        CmsIndexId = "USDCMS_1Y_QUARTERLY",
        Effective = new Dt(20160405),
        Maturity = new Dt(20260406),
        CapFloorType = CapFloorType.Cap,
        Strike = 0.02,
        Rate = 0.02,
        CurrentReset = 0.05,
        Volatility = new Volatility(Volatility.MarketCube,
          new[,] {{0.8, 0.7}, {0.6, 0.4}},
          expiries: new[] {"3M", "1Y"},
          tenors: new[] {"1Y", "2Y"})
      },
      new Data(Name.SwaptionMarketCubeWithSkew)
      {
        CmsIndexId = "USDCMS_1Y_QUARTERLY",
        Effective = new Dt(20160405),
        Maturity = new Dt(20260406),
        CapFloorType = CapFloorType.Cap,
        Strike = 0.02,
        Rate = 0.02,
        CurrentReset = 0.05,
        Volatility = new Volatility(Volatility.MarketCube,
          new[,] {{0.8, 0.7}, {0.6, 0.4}},
          expiries: new[] {"3M", "1Y"},
          tenors: new[] {"1Y", "2Y"},
          skew: new VolatilitySkew(new [] {"1Y"}, new [] {"1Y"},
            new[] {-0.005, 0.0, 0.005},
            new[,] { {0.02, 0.0, 0.05} }))
      },
    };
  }

  #endregion

  #region Types: Volatility and Data

  public enum Name
  {
    SimpleSurfaceFlat,
    RateCubeFlat,
    SwaptionMarketCube,
    SwaptionMarketCubeWithSkew,
  }

  internal class VolatilitySkew
  {
    public readonly string[] Expiries;
    public readonly string[] Tenors;
    public readonly double[] StrikeShifts;
    public readonly double[,] Values;

    public VolatilitySkew(string[] expiries, string[] tenors,
      double[] strikes, double[,] values)
    {
      Expiries = expiries;
      Tenors = tenors;
      StrikeShifts = strikes;
      Values = values;
    }
  }

  internal class Volatility
  {
    public readonly string Name;
    public readonly VolatilityType Distribution;
    public readonly double[,] Values;
    public readonly string[] Expiries;
    public readonly string[] Tenors;
    public readonly VolatilitySkew Skew;

    public Volatility(string name, double volatility,
      VolatilityType type = VolatilityType.LogNormal)
    {
      Name = name;
      Values = new[,] { { volatility } };
      Distribution = type;
    }

    public Volatility(string name, double[,] values,
      string[] expiries = null,
      string[] tenors = null,
      VolatilityType type = VolatilityType.LogNormal,
      VolatilitySkew skew = null)
    {
      Name = name;
      Distribution = type;
      Values = values;
      Expiries = expiries;
      Tenors = tenors;
      Skew = skew;
    }

    public const string Flat = "Flat";
    public const string RateCube = "RateCube";
    public const string MarketCube = "MarketCube";
  }

  internal partial class Data
  {
    public Name Name;
    public string CmsIndexId;
    public Dt Effective, Maturity;
    public CapFloorType CapFloorType;
    public double Strike;
    public double Rate;
    public double CurrentReset = double.NaN;
    public Volatility Volatility;

    public Data(Name name)
    {
      Name = name;
    }

    public CmsCapFloorPricer GetPricer(int dateShift)
    {
      return GetCmsCapFloorPricer(CapFloorType, dateShift);
    }

    public CmsCapFloorPricer GetCmsCapFloorPricer(
      CapFloorType capFloorType, int asOfShift)
    {
      var index = GetSwapRateIndex();
      Dt asOf = Effective + asOfShift,
        settle = Dt.AddDays(asOf, 2, index.Calendar);

      // Create discount and projection curves
      var discountCurve = GetDiscountCurve(asOf);
      var referenceCurve = discountCurve;
      var volatility = GetVolatility(Volatility, referenceCurve);
      // Create product and pricer
      var calendar = index.ForwardRateIndex.Calendar;
      var dayCount = index.ForwardRateIndex.DayCount;
      var bdc = index.ForwardRateIndex.Roll;
      var freq = index.IndexFrequency;
      var cap = new CmsCap(index, Effective, Maturity,
        capFloorType, Strike, dayCount, freq, bdc, calendar);
      cap.CashflowFlag |= CashflowFlag.IncludeMaturityAccrual;
      var pricer = CapFloorPricerBase.CreatePricer(cap,
        asOf, settle, referenceCurve, discountCurve,
        volatility, GetComvexityParameters(volatility, index));
      if (!double.IsNaN(CurrentReset))
      {
        var expiry = pricer.LastExpiry;
        if (!expiry.IsEmpty() && expiry < asOf)
          pricer.Resets.Add(new RateReset(expiry, CurrentReset));
      }
      return (CmsCapFloorPricer)pricer;
    }

    public CapFloorPricer GetCapFloorPricer(
      CapFloorType capFloorType, int asOfShift)
    {
      var index = GetSwapRateIndex().ForwardRateIndex;
      Dt asOf = Effective + asOfShift,
        settle = Dt.AddDays(asOf, 2, index.Calendar);

      // Create discount and projection curves
      var discountCurve = GetDiscountCurve(asOf);
      var referenceCurve = discountCurve;
      var volatility = GetVolatility(Volatility, referenceCurve);
      // Create product and pricer
      var calendar = index.Calendar;
      var dayCount = index.DayCount;
      var bdc = index.Roll;
      var freq = index.IndexTenor.ToFrequency();
      var cap = new Cap(Effective, Maturity, index.Currency,
        capFloorType, Strike, dayCount, freq, bdc, calendar);
      cap.CashflowFlag |= CashflowFlag.IncludeMaturityAccrual;
      var pricer = (CapFloorPricer) CapFloorPricerBase.CreatePricer(cap,
        asOf, settle, referenceCurve, discountCurve, volatility);
      pricer.CheckResetInForwardRate = true;
      if (!double.IsNaN(CurrentReset))
      {
        var expiry = pricer.LastExpiry;
        if (!expiry.IsEmpty() && expiry < asOf)
          pricer.Resets.Add(new RateReset(expiry, CurrentReset));
      }
      return pricer;
    }

    public SwapPricer GetCmsSwapPricer(int asOfShift)
    {
      var index = GetSwapRateIndex();
      Dt asOf = Effective + asOfShift,
        settle = Dt.AddDays(asOf, 2, index.Calendar);

      // Create discount and projection curves
      var discountCurve = GetDiscountCurve(asOf);
      var referenceCurve = discountCurve;
      var rateResets = new RateResets(CurrentReset, double.NaN);
      // Create product and pricer
      var rateIndex = index.ForwardRateIndex;
      var calendar = rateIndex.Calendar;
      var dayCount = rateIndex.DayCount;
      var bdc = rateIndex.Roll;
      var freq = index.IndexFrequency;
      var fixedLeg = new SwapLeg(Effective, Maturity, index.Currency,
        Strike, dayCount, freq, bdc, calendar, false);
      var fixedPricer = new SwapLegPricer(fixedLeg, asOf, settle, -1.0,
        discountCurve, null, null, null, null, null);
      var floatLeg = new SwapLeg(Effective, Maturity, freq, 0.0, index);
      var floatPricer = new SwapLegPricer(floatLeg,
        asOf, settle, 1.0, discountCurve, index, referenceCurve,
        asOfShift <= 0 ? null : rateResets, null, null)
      {
        FwdRateModelParameters = GetComvexityParameters(
          GetVolatility(Volatility, referenceCurve), index)
      };
      return new SwapPricer(floatPricer, fixedPricer);
    }

    public SwapPricer GetSwapPricer(int asOfShift)
    {
      var index = GetSwapRateIndex().ForwardRateIndex;
      Dt asOf = Effective + asOfShift,
        settle = Dt.AddDays(asOf, 2, index.Calendar);

      // Create discount and projection curves
      var discountCurve = GetDiscountCurve(asOf);
      var referenceCurve = discountCurve;
      var rateResets = new RateResets(CurrentReset, double.NaN);
      // Create product and pricer
      var calendar = index.Calendar;
      var dayCount = index.DayCount;
      var bdc = index.Roll;
      var freq = index.IndexTenor.ToFrequency();
      var fixedLeg = new SwapLeg(Effective, Maturity, index.Currency,
        Strike, dayCount, freq, bdc, calendar, false);
      var fixedPricer = new SwapLegPricer(fixedLeg, asOf, settle, -1.0,
        discountCurve, null, null, null, null, null);
      var floatLeg = new SwapLeg(Effective, Maturity, freq, 0.0, index);
      var floatPricer = new SwapLegPricer(floatLeg,
        asOf, settle, 1.0, discountCurve, index, referenceCurve,
        asOfShift <= 0 ? null : rateResets, null, null);
      return new SwapPricer(floatPricer, fixedPricer);
    }

    private SwapRateIndex GetSwapRateIndex()
    {
      return (SwapRateIndex)StandardReferenceIndices.Create(CmsIndexId);
    }

    private DiscountCurve GetDiscountCurve(Dt asOf)
    {
      var referenceRate = InterestReferenceRate.Get("USDLIBOR");
      var tenors = CurveTenorFactory.BuildTenors(new List<string>() { "1Y" }, new List<string>(){ "MM" },
        new[,] { { Rate } }, new[] { QuotingConvention.Yield },
        new IReferenceRate[] { referenceRate }, new string[]{ "3M" });
      return MultiRateCurveFitCalibrator.Fit(
        "DiscountCurve", "", asOf, referenceRate, Tenor.ThreeMonths, Tenor.Empty, 
        null, Tenor.Empty, Tenor.Empty, Tenor.Empty,
        tenors, null, null);
    }

    private RateModelParameters GetComvexityParameters(
      IVolatilityObject volatility, SwapRateIndex index)
    {
      return new RateModelParameters(
        RateModelParameters.Model.BGM,
        new[] { RateModelParameters.Param.Sigma },
        new[] { (IModelParameter)volatility },
        index);
    }

    private static readonly Const Const = new Const();

    private static IVolatilityObject GetVolatility(
      Volatility data, DiscountCurve discountCurve)
    {
      var asOf = discountCurve.AsOf;
      var index = (InterestRateIndex)discountCurve.ReferenceIndex;
      var skew = data.Skew;
      switch (data.Name)
      {
        case Volatility.Flat:
          return new FlatVolatility { Volatility = data.Values[0, 0] };
        case Volatility.RateCube:
          if (data.Values.Length == 1)
            return RateVolatilityCube.CreateFlatVolatilityCube(asOf,
              new[] { asOf }, new[] { data.Values[0, 0] }, data.Distribution, index);
          break;
        case Volatility.MarketCube:
          return SwaptionVolatilityCube.CreateSwaptionMarketCube(
            asOf, discountCurve, data.Expiries, data.Tenors, data.Values,
            index, data.Distribution,
            skew?.Expiries, skew?.StrikeShifts, skew?.Tenors, skew?.Values,
            new Cubic(Const, Const), new Linear(Const, Const),
            index.DayCount, index.Roll,
            Frequency.SemiAnnual, index.Calendar, 0);
      }
      return null;
    }

  }

  #endregion
}
