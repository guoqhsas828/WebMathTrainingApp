//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util.Configuration;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Models
{
  /// <summary>
  ///   Test basket with short names
  /// </summary>
  [TestFixture, Smoke]
  public class TestBasketShortNames : ToolkitTestBase
  {
    #region Data
    private new readonly ToolkitConfigSettings settings_ = ToolkitConfigurator.Settings;

    //private double tolerance_ = 1E-8;

    //private Dt asOf_;
    //private Dt settle_;
    //private Dt effective_;
    //private Dt maturity_;
    private string tenor_ = "5Y";
    private double discountRate_ = 0.04;
    private double hazardRate_ = 0.0025;
    private double recoveryRate_ = 0.40;

    private double correlation_ = 0.3;
    private int basketSize_ = 100;
    private int shortNames_ = 10;
    private int principal_ = 1000000;
    private int notionalSign_ = 1;

    private double attachment_ = 0.03;
    private double detachment_ = 0.10;
    private double premium_ = 100;

    #endregion // Data

    #region TestHelpers

    /// <summary>
    ///   Create two CDO pricer: the first includes all the defaulted names,
    ///   the second excludes the defaulted names and manually adjust the
    ///   CDO subordination in the remaining basket.  Both pricers should
    ///   produce the same prices if the implemetation is correct.
    /// </summary>
    /// <param name="basketType">
    ///   Type of the basket
    /// </param>
    /// <returns>
    ///   An array of two pricers:
    ///   position 0 is a pricer with defaulted names;
    ///   position 1 is a pricer with defaulted names removed from the basket.
    /// </returns>
    private SyntheticCDOPricer CreatePricer(Type basketType)
    {
      // Get the user input dates
      Dt asOf, settle;
      if (this.PricingDate == 0 )
      {
        asOf = Dt.Today();
        settle = Dt.Add(asOf, 1);
      }
      else
      {
        asOf = new Dt(this.PricingDate);
        settle = this.SettleDate == 0 ? Dt.Add(asOf, 1) : new Dt(this.SettleDate);
      }
      Dt effective = this.EffectiveDate == 0 ? settle : new Dt(this.EffectiveDate);
      Dt maturity = this.MaturityDate == 0 ? Dt.CDSMaturity(effective, tenor_) : new Dt(this.MaturityDate);

      // Get product terms
      Currency ccy = Get(Currency.None);
      DayCount dayCount = Get(DayCount.Actual360);
      BDConvention roll = Get(BDConvention.Following);
      Frequency freq = Get(Frequency.Quarterly);
      Calendar calendar = Get(Calendar.NYB);

      // DiscountCurve
      DiscountCurve discountCurve = new DiscountCurve(asOf, discountRate_);

      // Total names
      int totalNames = basketSize_ + shortNames_;

      // Survival curves
      SurvivalCurve survivalCurve = CreateSurvivalCurve(
        asOf, settle, ccy, dayCount, freq, roll, calendar);
      SurvivalCurve[] survivalCurves = new SurvivalCurve[totalNames];
      for (int i = 0; i < totalNames; ++i)
        survivalCurves[i] = (SurvivalCurve)survivalCurve.Clone();

      // Principals and make the short names appears first
      double[] principals = new double[totalNames];
      for (int i = 0; i < shortNames_; ++i)
        principals[i] = -principal_;
      for (int i = shortNames_; i < totalNames; ++i)
        principals[i] = principal_;

      // Create CDO
      SyntheticCDO cdo = new SyntheticCDO(effective, maturity, ccy,
        premium_ / 10000.0, dayCount, freq, roll, calendar);
      double notional;
      if (settings_.BasketPricer.SubstractShortedFromPrincipal)
      {
        double shortPortion = shortNames_ * 1.0 / basketSize_;
        cdo.Attachment = attachment_ / (1 - shortPortion);
        cdo.Detachment = detachment_ / (1 - shortPortion);
        notional = cdo.TrancheWidth * (basketSize_ - shortNames_) * principal_ * notionalSign_;
      }
      else
      {
        cdo.Attachment = attachment_;
        cdo.Detachment = detachment_;
        notional = cdo.TrancheWidth * basketSize_ * principal_ * notionalSign_;
      }

      // Create pricers
      return CreatePricer(basketType,
        cdo, asOf, settle, discountCurve, survivalCurves,
        principals, this.QuadraturePoints, notional);
    }

    /// <summary>
    ///   Create a survival curve
    /// </summary>
    private SurvivalCurve CreateSurvivalCurve(
      Dt asOf, Dt settle, Currency ccy,
      DayCount dayCount, Frequency freq,
      BDConvention roll, Calendar calendar)
    {
      Dt maturity = Dt.CDSMaturity(settle, tenor_);
      SurvivalCurve survivalCurve = SurvivalCurve.FromProbabilitiesWithCDS(
        asOf, ccy, "None",
        BaseEntity.Toolkit.Numerics.InterpMethod.Linear, BaseEntity.Toolkit.Numerics.ExtrapMethod.Const,
        new Dt[] { maturity },
        new double[] { Math.Exp(-hazardRate_ * Dt.FractDiff(settle, maturity) / 365) },
        new string[] { tenor_ },
        new DayCount[] { dayCount }, new Frequency[] { freq },
        new BDConvention[] { roll }, new Calendar[] { calendar },
        new double[] { recoveryRate_ }, 0.0);
      return survivalCurve;
    }

    /// <summary>
    ///   Create a CDO Pricer
    /// </summary>
    private SyntheticCDOPricer CreatePricer(
      Type basketType, SyntheticCDO cdo,
      Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      SurvivalCurve[] survivalCurves,
      double[] principals,
      int quadPoints,
      double notional)
    {
      int stepSize = 0; TimeUnit stepUnit = TimeUnit.None;
      GetTimeGrid(ref stepSize, ref stepUnit);
      if (stepSize <= 0)
      {
        stepSize = 3;
        stepUnit = TimeUnit.Months;
      }

      SyntheticCDOPricer pricer;
      if (basketType == typeof(LargePoolBasketPricer))
      {
        pricer = BasketPricerFactory.CDOPricerLargePool(
          new SyntheticCDO[] { cdo }, new Dt(), asOf, settle,
          discountCurve, survivalCurves, principals,
          GetCopula(),
          new SingleFactorCorrelation(new string[survivalCurves.Length], Math.Sqrt(correlation_)),
          stepSize, stepUnit, quadPoints, null, false)[0];
      }
      else if (basketType == typeof(UniformBasketPricer))
      {
        pricer = BasketPricerFactory.CDOPricerUniform(
          new SyntheticCDO[] { cdo }, new Dt(), asOf, settle,
          discountCurve, survivalCurves, principals,
          GetCopula(),
          new SingleFactorCorrelation(new string[survivalCurves.Length], Math.Sqrt(correlation_)),
          stepSize, stepUnit, quadPoints, null, false)[0];
      }
      else if (basketType == typeof(HomogeneousBasketPricer))
      {
        pricer = BasketPricerFactory.CDOPricerHomogeneous(
          new SyntheticCDO[] { cdo }, new Dt(), asOf, settle,
          discountCurve, survivalCurves, principals,
          GetCopula(),
          new SingleFactorCorrelation(new string[survivalCurves.Length], Math.Sqrt(correlation_)),
          stepSize, stepUnit, quadPoints, null, false)[0];
      }
      else if (basketType == typeof(HeterogeneousBasketPricer))
      {
        pricer = BasketPricerFactory.CDOPricerHeterogeneous(
          new SyntheticCDO[] { cdo }, new Dt(), asOf, settle,
          discountCurve, survivalCurves, principals,
          GetCopula(),
          new SingleFactorCorrelation(new string[survivalCurves.Length], Math.Sqrt(correlation_)),
          stepSize, stepUnit, quadPoints, 0, null, false)[0];
      }
      else if (basketType == typeof(MonteCarloBasketPricer))
      {
        pricer = BasketPricerFactory.CDOPricerMonteCarlo(
          new SyntheticCDO[] { cdo }, new Dt(), asOf, settle,
          discountCurve, survivalCurves, principals,
          GetCopula(),
          new SingleFactorCorrelation(new string[survivalCurves.Length], Math.Sqrt(correlation_)),
          stepSize, stepUnit, this.SampleSize, null, false, 0)[0];
      }
      else
      {
        SyntheticCDO cdo1 = (SyntheticCDO)cdo.Clone();
        cdo1.Detachment = 1.0;
        pricer = BasketPricerFactory.CDOPricerSemiAnalytic(
          new SyntheticCDO[] { cdo, cdo1 }, new Dt(), asOf, settle,
          discountCurve, survivalCurves, principals,
          GetCopula(),
          new SingleFactorCorrelation(new string[survivalCurves.Length], Math.Sqrt(correlation_)),
          stepSize, stepUnit, quadPoints, 0, null, false, false)[0];
      }
      pricer.Notional = notional;

      return pricer;
    }

    class LabelValueList
    {
      public LabelValueList()
      {
        Values = new List<double>();
        Labels = new List<string>();
      }
      public void Add(string label, double value)
      {
        Labels.Add(label); Values.Add(value);
      }
      public readonly List<double> Values;
      public readonly List<string> Labels;
    }
    LabelValueList CalculateValues(SyntheticCDOPricer pricer)
    {
      LabelValueList list = new LabelValueList();

      list.Add("CurrentNotional", pricer.EffectiveNotional);
      list.Add("Accrued", pricer.Accrued());

      list.Add("ExpectedLoss", pricer.ExpectedLoss());
      list.Add("ExpectedSurvival", pricer.ExpectedSurvival());

      list.Add("ProtectionPv", pricer.ProtectionPv());

      list.Add("FeePv", pricer.FeePv());
      list.Add("FullFeePv", pricer.FullFeePv());
      list.Add("FlatFeePv", pricer.FlatFeePv());

      list.Add("Pv", pricer.Pv());
      list.Add("FullPrice", pricer.FullPrice());
      list.Add("FlatPrice", pricer.FlatPrice());

      list.Add("RiskyDuration", pricer.RiskyDuration());
      list.Add("BreakEvenFee", pricer.BreakEvenFee());
      list.Add("BreakEvenPremium", pricer.BreakEvenPremium());
      list.Add("Premium01", pricer.Premium01());
      list.Add("Carry", pricer.Carry());
      list.Add("MTMCarry", pricer.MTMCarry());

      return list;
    }


    private void DoTest(Type pricerType)
    {
      // Requirement
      //    SyntheticCDOPricer.UseOriginalNotionalForFee == false;
      if (settings_.SyntheticCDOPricer.UseOriginalNotionalForFee)
        throw new InvalidOperationException("Require SyntheticCDOPricer.UseOriginalNotionalForFee cannot be true to run this test");
      Timer timer = new Timer();
      timer.Start();
      SyntheticCDOPricer pricer = CreatePricer(pricerType);
      LabelValueList actuals = CalculateValues(pricer);
      timer.Stop();
      MatchExpects(actuals.Values, actuals.Labels, timer.Elapsed);
    }

    #endregion // TestHelpers

    #region Tests

    [Test, Smoke]
    public void LargePoolPricer()
    {
      DoTest(typeof(LargePoolBasketPricer));
    }

    [Test, Smoke]
    public void UniformPricer()
    {
      Assert.Throws<ToolkitException>(() =>
      {
        DoTest(typeof(UniformBasketPricer));
      });
    }

    [Test, Smoke]
    public void HomogeneousPricer()
    {
      Assert.Throws<ToolkitException>(() =>
      {
        DoTest(typeof(HomogeneousBasketPricer));
      });
    }

    [Test, Smoke]
    public void HeterogeneousPricer()
    {
      DoTest(typeof(HeterogeneousBasketPricer));
    }

    [Test, Smoke]
    public void SemiAnalyticPricer()
    {
      DoTest(typeof(SemiAnalyticBasketPricer));
    }

    [Test, Smoke]
    public void MonteCarloPricer()
    {
      DoTest(typeof(MonteCarloBasketPricer));
    }

    #endregion // Tests

  } // class TestBasketDefaultedNames
}
