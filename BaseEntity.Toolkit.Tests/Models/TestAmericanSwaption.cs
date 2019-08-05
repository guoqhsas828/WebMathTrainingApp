//
// Copyright (c)    2002-2018. All rights reserved.
//

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Calibrators;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class AmericanSwaptionTests
  {
    private Dt _effective = _D("11-Jun-14"), _maturity = _D("11-Jun-19"),
      _callBegin = _D("11-Jun-13"), _callEnd = _D("11-Jun-14"),
      _pricingDate = _D("11-Jun-2013");

    private double _volatility = 0.4;
 
    [Test]
    public void TestAmericanSwaption()
    {
      DayCount fixDc = DayCount.Thirty360, floatDc = DayCount.Actual360;
      Frequency fixFr = Frequency.SemiAnnual, floatFr = Frequency.Quarterly;
      double fixCpn = 0.014;
      const bool accrueOnCycle = false;
      var bdc = BDConvention.Modified;
      var calendar = Calendar.NYB;
      var ccy = Currency.USD;
      Dt effective = _effective, maturity = _maturity,
        expiry = _callBegin, asOf = _pricingDate;

      DiscountCurve discountCurve, projectCurve;
      new RateCurveBuilder().GetRateCurves(asOf, out discountCurve, out projectCurve);

      var fixLeg = new SwapLeg(effective, maturity, ccy,
        fixCpn, fixDc, fixFr, bdc, calendar, accrueOnCycle);
      var floatLeg = new SwapLeg(effective, maturity, 0.0, floatFr,
        projectCurve.ReferenceIndex, ProjectionType.SimpleProjection,
        CompoundingConvention.None, Frequency.None, false);

      var swaption = new Swaption(asOf, expiry, ccy, fixLeg, floatLeg,
        2, PayerReceiver.Payer, OptionStyle.American, fixCpn);
      Dt start = _callBegin, end = _callEnd;
      var exercisePeriods = new[]
        {
          swaption.OptionType == OptionType.Call
            ? (IOptionPeriod) new CallPeriod(start, end, 1.0,
              0, OptionStyle.American, 0)
            : new PutPeriod(start, end, 1.0, OptionStyle.European)
        };

      var vol = new FlatVolatility()
        {
          DistributionType = DistributionType.LogNormal,
          Volatility = _volatility
        };
      var pricer = new SwapBermudanBgmTreePricer(swaption, asOf, asOf,
        discountCurve, projectCurve, null, exercisePeriods, vol);
      pricer.IsAmericanOption = false;
      var pv0 = pricer.ProductPv();

      pricer.IsAmericanOption = true;
      pricer.Reset();
      var pv1 = pricer.ProductPv();

      /*
      var pvs = new[] { "3M", "1M", "2W", "1W", "3D", "1D" }
        .Select(s =>
        {
          pricer.StepSize = Tenor.Parse(s);
          pricer.Reset();
          return pricer.ProductPv();
        }).ToArray();

    pv0	0.021346139707888241	double
    pv1	0.038597176260109306	double
-		pvs	{double[6]}	double[]
    [0]	0.038597176260109306	double
    [1]	0.039400364721610182	double
    [2]	0.039647434786997782	double
    [3]	0.039922266318313068	double
    [4]	0.0401271613201606	double
    [5]	0.040239544888721432	double
       */
      return;
    }

    private static Dt _D(string input)
    {
      return Dt.FromStr(input);
    }
  }
}
