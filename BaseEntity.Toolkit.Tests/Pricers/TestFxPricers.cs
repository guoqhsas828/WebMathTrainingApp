//
// Copyright (c)    2018. All rights reserved.
//

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestFxPricers : ToolkitTestBase
  {
    [SetUp]
    public void SetUp()
    {
      AsOf = Dt.FromStr("13-JUL-2011");
      var dd = new DiscountData()
               {
                 AsOf = AsOf.ToStr("%D"),
                 Bootst = new DiscountData.Bootstrap()
                            {
                              MmDayCount = DayCount.Actual360,
                              MmTenors = new string[] {"1M", "2M", "3M", "6M", "9M"},
                              MmRates = new double[] {0.011, 0.012, 0.013, 0.016, 0.019},
                              SwapDayCount = DayCount.Actual360,
                              SwapFrequency = Frequency.SemiAnnual,
                              SwapInterp = InterpMethod.Cubic,
                              SwapExtrap = ExtrapMethod.Const,
                       SwapTenors =
                         new string[] {"2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y", "15Y", "30Y"},
                       SwapRates =
                         new double[] { 0.022, 0.023, 0.024, 0.025, 0.026, 0.027, 0.028, 0.029, 0.030, 0.035, 0.04}
                     },
          Category = "Empty", 
          Name = "USD_DiscCurve"
        };
      USDDiscountCurve = dd.GetDiscountCurve();

      var fd1 = new FxData
                 {
                   AsOf = AsOf,
                   Name = "GBPUSD",
                   FromCcy = Currency.GBP,
                   ToCcy = Currency.USD,
                   FromCcyCalendar = Calendar.LNB,
                   ToCcyCalendar = Calendar.NYB,
                   SpotFx = 1.60,
                   TenorNames = new string[] {"1Y", "10Y"},
                   FwdFx = new double[] {1.70, 1.80}
                 };
      GBPUSDFxCurve = fd1.GetFxCurve();
      
      var fd2 = new FxData
      {
        AsOf = AsOf,
        Name = "EURUSD",
        FromCcy = Currency.EUR,
        ToCcy = Currency.USD,
        FromCcyCalendar = Calendar.TGT,
        ToCcyCalendar = Calendar.NYB,
        SpotFx = 1.40,
        TenorNames = new string[] { "1Y", "10Y" },
        FwdFx = new double[] { 1.50, 1.60 }
      };
      EURUSDFxCurve = fd2.GetFxCurve();

    }

    [Test]
    public void TestFxForwardPricing()
    {
      GBPUSDForward();
      GBPEURForward();
    }

    private void GBPUSDForward()
    {
      var valueDate = Dt.FromStr("13-AUG-11");
      double fx = GBPUSDFxCurve.FxRate(valueDate);
      var fwd = new FxForward(valueDate,
                              Currency.GBP,
                              Currency.USD,
                              fx);
      var pricer = new FxForwardPricer(fwd, AsOf, AsOf, 1e6, Currency.USD, USDDiscountCurve, GBPUSDFxCurve, null);
      using (new CheckStates(true, new[] { pricer }))
      {
        var pv = pricer.ProductPv();
        Assert.AreEqual(0.0, pv, 1e-9);
      }
    }

    private void GBPEURForward()
    {
      var valueDate = Dt.FromStr("13-AUG-11");
      double fx = GBPUSDFxCurve.FxRate(valueDate) / EURUSDFxCurve.FxRate(valueDate);
      var fwd = new FxForward(valueDate,
                              Currency.GBP,
                              Currency.EUR,
                              fx);
      var pricer = new FxForwardPricer(fwd, AsOf, AsOf, 1e6, Currency.USD, USDDiscountCurve, GBPUSDFxCurve, EURUSDFxCurve);
      using (new CheckStates(true, new[] { pricer }))
      {
        var pv = pricer.ProductPv();
        Assert.AreEqual(0.0, pv, 1e-9);
      }
    }

    [Test]
    public void TestFxSwapPricing()
    {
      GBPUSDSwap();
      EURGBPSwap();
    }

    private void GBPUSDSwap()
    {
      var near = Dt.FromStr("13-AUG-11");
      double fx1 = GBPUSDFxCurve.FxRate(near)/EURUSDFxCurve.FxRate(near);
      var far = Dt.FromStr("13-NOV-11");
      double fx2 = GBPUSDFxCurve.FxRate(far)/EURUSDFxCurve.FxRate(far);

      var swap = new FxSwap(near,
                            Currency.GBP,
                            Currency.EUR,
                            fx1,
                            far,
                            fx2);
      var pricer = new FxSwapPricer(swap, AsOf, AsOf, 1e6, Currency.USD, USDDiscountCurve, GBPUSDFxCurve, EURUSDFxCurve);
      using (new CheckStates(true, new[] { pricer }))
      {
        var pv = pricer.ProductPv();
        Assert.AreEqual(0.0, pv, 1e-9);
      }
    }

    private void EURGBPSwap()
    {
      var near = Dt.FromStr("13-AUG-11");
      double fx1 = EURUSDFxCurve.FxRate(near);
      var far = Dt.FromStr("13-NOV-11");
      double fx2 = EURUSDFxCurve.FxRate(far);

      var swap = new FxSwap(near,
                            Currency.EUR,
                            Currency.GBP,
                            fx1,
                            far,
                            fx2);
      var pricer = new FxSwapPricer(swap, AsOf, AsOf, 1e6, Currency.USD, USDDiscountCurve, EURUSDFxCurve, null);
      using (new CheckStates(true, new[] { pricer }))
      {
        var pv = pricer.ProductPv();
        Assert.AreEqual(0.0, pv, 1e-9);
      }
    }
    Dt AsOf { get; set;}
    DiscountCurve USDDiscountCurve { get; set; }
    FxCurve GBPUSDFxCurve { get; set; }
    FxCurve EURUSDFxCurve { get; set; }
  }
}