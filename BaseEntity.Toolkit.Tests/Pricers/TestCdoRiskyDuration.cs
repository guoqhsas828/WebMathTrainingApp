//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  using NUnit.Framework;
  public class TestCdoRiskyDuration
  {
    [SetUp]
    public void Initialize()
    {
      _discountCurve = new DiscountCurve(_asOf, 0.07);
      _survivalCurves = GetSurvivalCurves();
    }
    
    [TestCase(CdoType.Unfunded)]
    [TestCase(CdoType.Po)]
    [TestCase(CdoType.IoFundedFloating)]
    [TestCase(CdoType.IoFundedFixed)]
    [TestCase(CdoType.FundedFloating)]
    [TestCase(CdoType.FundedFixed)]
    public void TestCdoRiskyDruationWithCdoType(CdoType cdoType)
    {
      var pricer = GetCdoPricer(_cdoPremium, cdoType);
      double expect;
      switch (cdoType)
      {
        case CdoType.Po:
          expect = PoDuration(pricer);
          break;
        case CdoType.Unfunded:
        case CdoType.IoFundedFixed:
        case CdoType.IoFundedFloating:
          expect = IoDuration(pricer);
          break;
        case CdoType.FundedFixed:
        case CdoType.FundedFloating:
          expect = 0.5*(PoDuration(pricer) + IoDuration(pricer));
          break;
        default:
          throw new Exception("Missing cdo type");
      }
      Assert.AreEqual(expect, pricer.RiskyDuration(), 1e-10);
    }

    private static double PoDuration(SyntheticCDOPricer pricer)
    {
      Dt asOf = pricer.AsOf, settle = pricer.Settle,
        maturity = pricer.CDO.Maturity;
      var fraction = Dt.TimeInYears(settle, pricer.CDO.Maturity);
      return fraction
             *pricer.DiscountCurve.DiscountFactor(asOf, maturity)
             *pricer.ExpectedSurvival();
    }

    private static double IoDuration(SyntheticCDOPricer pricer)
    {
      var df = pricer.DiscountCurve.DiscountFactor(
        pricer.AsOf, pricer.Settle);

      pricer.Reset();
      pricer = (SyntheticCDOPricer) pricer.ShallowCopy();
      pricer.CDO = (SyntheticCDO) pricer.CDO.ShallowCopy();
      pricer.CDO.CdoType = CdoType.Unfunded;
      var pv0 = pricer.FeePv() - pricer.Accrued()*df;
      pricer.CDO.Premium += 1;
      pricer.Reset();
      var pv1 = pricer.FeePv() - pricer.Accrued()*df;
      return (pv1 - pv0)/pricer.Notional;
    }

    private SyntheticCDOPricer GetCdoPricer(double premium, CdoType cdoType)
    {
      if (cdoType == CdoType.Po)
        premium = 0.0;
      var cdo = new SyntheticCDO(_effective, _maturity, Currency.USD,
        premium / 10000.0, _cdoDaycount, _cdoFreq, _cdoRoll, _cdoCal);
      cdo.Attachment = _attach;
      cdo.Detachment = _detach;
      cdo.CdoType = cdoType;
      cdo.FixedRecovery = false;
      cdo.Description = "CDO";
      cdo.Validate();
      return GetCdoPricer(cdo);
    }

    private SyntheticCDOPricer GetCdoPricer(SyntheticCDO cdo)
    {
      for (int i = 0; i < _principals.Length; i++)
        _principals[i] = 1.0;

      Copula copula = new Copula(CopulaType.Gauss, 2, 2);
      Dt portfolioStart = new Dt();
      string[] names = new string[_survivalCurves.Length];
      for (int i = 0; i < _survivalCurves.Length; i++)
        if (null != _survivalCurves[i])
          names[i] = _survivalCurves[i].Name;
      CorrelationObject correlation = new SingleFactorCorrelation(names, Math.Sqrt(_corr));

      return BasketPricerFactory.CDOPricerHeterogeneous(
        new[] { cdo }, portfolioStart, _asOf, _settle, _discountCurve, 
        null, _survivalCurves, _principals, copula, correlation, 3, 
        TimeUnit.Months, 30, 0, new [] { _cdoNotional }, false, null)[0];
    }

    
    private SurvivalCurve[] GetSurvivalCurves()
    {
      const int N = 125;
      const double hazardRate = 0.7;
      SurvivalCurve[] survivalCurves =
        ArrayUtil.Generate<SurvivalCurve>(N, i =>
        {
          SurvivalFitCalibrator cal = new SurvivalFitCalibrator(
            _asOf, _settle, _recovery, _discountCurve);
          SurvivalCurve sc = new SurvivalCurve(cal);
          sc.Set(new SurvivalCurve(_asOf, hazardRate * (1 - ((double)i) / N)));
          sc.Name = "curve_" + (i + 1);
          return sc;
        });

      return survivalCurves;
    }

    #region Data
    private readonly Dt _asOf = new Dt(23, 3, 2009);
    private readonly Dt _settle = new Dt(24, 3, 2009);
    private readonly Dt _effective = new Dt(9, 8, 2007);
    private readonly Dt _maturity = new Dt(8, 6, 2012);

    private readonly double _recovery = 0.4;
    private readonly double _cdoPremium = 250;
    private readonly DayCount _cdoDaycount = DayCount.Actual360;
    private readonly Frequency _cdoFreq = Frequency.Quarterly;
    private readonly BDConvention _cdoRoll = BDConvention.Following;
    private readonly Calendar _cdoCal = Calendar.NYB;
    private readonly double _attach = 0;
    private readonly double _detach = 0.03;
    private readonly double _cdoNotional = 10000000;
    private readonly double[] _principals = new double[125];
    private readonly double _corr = 0.5;

    private DiscountCurve _discountCurve;
    private SurvivalCurve[] _survivalCurves;
    #endregion
  }
}
