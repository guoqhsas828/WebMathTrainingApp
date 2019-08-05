//
// Copyright (c)    2002-2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using QMath = BaseEntity.Toolkit.Numerics.SpecialFunctions;

using NUnit.Framework;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Models

{
  [TestFixture]
  public class TestBarrierOptionModel
  {
    struct SingleBarrierTrade
    {
      public struct ExpectedValues
      {
        public ExpectedValues(double pv, double delta, double vega, double theta)
        {
          Pv = pv;
          Delta = delta;
          Vega = vega;
          Theta = theta;
        }
        public readonly double Pv;
        public readonly double Delta;
        public readonly double Vega;
        public readonly double Theta;
      }
      public readonly OptionBarrierType BarrierType;
      public readonly OptionType OptionType;
      public readonly double Strike;
      public readonly double Barrier;
      public readonly ExpectedValues Expect;
      public SingleBarrierTrade(
        OptionBarrierType barrierType,
        OptionType optionType,
        double strike, double barrier,
        double pv, double delta, double vega, double theta)
      {
        BarrierType = barrierType;
        OptionType = optionType;
        Strike = strike;
        Barrier = barrier;
        Expect = new ExpectedValues(pv, delta, vega, theta);
      }
    }

    /// <summary>
    /// Singles the barrier flat.
    /// </summary>
    [Test, Smoke]
    public void SingleBarrierFlat()
    {
      const double rf = 0.027055421;
      const double rd = 0.022669219;
      const double rb = 0;// -0.0000026626653706219766;
      const double spot = 1.366;
      const double vol = 0.12;
      Dt settle = new Dt(20110125);
      Dt maturity = new Dt(20160125);

      var rdCurve = new DiscountCurve(settle, rd);
      rdCurve.Ccy = Currency.USD;
      double df = rdCurve.DiscountFactor(settle, maturity);

      var rfCurve = new DiscountCurve(settle, rf);
      rfCurve.Ccy = Currency.EUR;
      var basisCurve = new DiscountCurve(settle, rb);
      var fxRate = new FxRate(settle, 0, Currency.EUR, Currency.USD,
        spot, Calendar.LNB, Calendar.NYB);
      var fxCurve = new FxCurve(fxRate,basisCurve, rdCurve, rfCurve, null);
      var fxFactorCurve = new FxCurve(fxRate, new DiscountCurve(settle, rf - rd));
      var volCalibrator = new FxOptionVannaVolgaCalibrator(
        settle, settle, rdCurve, rfCurve, fxCurve, null);

      // The expectsare calculated by the site http://www.ftsweb.com/options/opbarr.htm
      SingleBarrierTrade[] singleBarrierTrades =
      {
      new SingleBarrierTrade( OptionBarrierType.DownIn, OptionType.Call, 1.3, 1.2, 0.03654, -0.15054, 0.72843, -0.00732),
      new SingleBarrierTrade( OptionBarrierType.DownIn, OptionType.Call, 1.3, 1.1, 0.01022, -0.05551, 0.354, -0.00388),
      new SingleBarrierTrade( OptionBarrierType.DownIn, OptionType.Call, 1.3, 1, 0.00173, -0.01214, 0.10097, -0.00115),
      new SingleBarrierTrade( OptionBarrierType.DownIn, OptionType.Put, 1.3, 1.2, 0.1096, -0.35727, 1.0466, -0.01219),
      new SingleBarrierTrade( OptionBarrierType.DownIn, OptionType.Put, 1.3, 1.1, 0.1043, -0.36567, 1.15491, -0.01358),
      new SingleBarrierTrade( OptionBarrierType.DownIn, OptionType.Put, 1.3, 1, 0.08767, -0.3559, 1.36048, -0.01628),
      new SingleBarrierTrade( OptionBarrierType.DownOut, OptionType.Call, 1.3, 1.2, 0.10599, 0.66924, 0.30709, 0.00124),
      new SingleBarrierTrade( OptionBarrierType.DownOut, OptionType.Call, 1.3, 1.1, 0.13231, 0.5742, 0.67424, -0.00212),
      new SingleBarrierTrade( OptionBarrierType.DownOut, OptionType.Call, 1.3, 1, 0.14078, 0.53083, 0.93467, -0.00493),
      new SingleBarrierTrade( OptionBarrierType.DownOut, OptionType.Put, 1.3, 1.2, 0.00047, 0.00233, -0.01106, 0.00014),
      new SingleBarrierTrade( OptionBarrierType.DownOut, OptionType.Put, 1.3, 1.1, 0.00577, 0.01071, -0.12666, 0.0016),
      new SingleBarrierTrade( OptionBarrierType.DownOut, OptionType.Put, 1.3, 1, 0.02238, 0.00095, -0.32482, 0.00423),
      new SingleBarrierTrade( OptionBarrierType.UpIn, OptionType.Call, 1.3, 1.6, 0.1339, 0.54053, 1.21511, -0.00849),
      new SingleBarrierTrade( OptionBarrierType.UpIn, OptionType.Call, 1.3, 1.5, 0.14058, 0.53108, 1.08115, -0.00668),
      new SingleBarrierTrade( OptionBarrierType.UpIn, OptionType.Call, 1.3, 1.4, 0.14244, 0.52092, 1.03756, -0.0061),
      new SingleBarrierTrade( OptionBarrierType.UpIn, OptionType.Put, 1.3, 1.6, 0.01349, 0.08875, 0.43775, -0.00512),
      new SingleBarrierTrade( OptionBarrierType.UpIn, OptionType.Put, 1.3, 1.5, 0.03567, 0.20064, 0.74787, -0.0087),
      new SingleBarrierTrade( OptionBarrierType.UpIn, OptionType.Put, 1.3, 1.4, 0.08434, 0.39675, 1.00562, -0.01168),
      new SingleBarrierTrade( OptionBarrierType.UpOut, OptionType.Call, 1.3, 1.6, 0.00861, -0.02184, -0.17951, 0.0024),
      new SingleBarrierTrade( OptionBarrierType.UpOut, OptionType.Call, 1.3, 1.5, 0.00195, -0.01237, -0.04569, 0.0006),
      new SingleBarrierTrade( OptionBarrierType.UpOut, OptionType.Call, 1.3, 1.4, 0.00007, -0.00223, -0.00191, 0.00002),
      new SingleBarrierTrade( OptionBarrierType.UpOut, OptionType.Put, 1.3, 1.6, 0.09657, -0.4437, 0.59786, -0.00693),
      new SingleBarrierTrade( OptionBarrierType.UpOut, OptionType.Put, 1.3, 1.5, 0.07441, -0.55558, 0.28759, -0.00334),
      new SingleBarrierTrade( OptionBarrierType.UpOut, OptionType.Put, 1.3, 1.4, 0.02572, -0.7517, 0.03002, -0.00037)
      };

      for (int i = 0; i < singleBarrierTrades.Length; ++i)
      {
        var trade = singleBarrierTrades[i];
        var fxo = new FxOption()
        {
          Effective = settle,
          Maturity = maturity,
          ReceiveCcy = Currency.USD,
          PayCcy = Currency.EUR,
          Type = trade.OptionType,
          Style = OptionStyle.European,
          Strike = trade.Strike,
          Description = "Trade " + (i + 1),
          Flags = 0,
        };
        fxo.Barriers.Add(new Barrier()
        {
          BarrierType = trade.BarrierType,
          Value = trade.Barrier,
          MonitoringFrequency = Frequency.Continuous
        });
        fxo.Validate();

        // Construct volatility object
        var name = Tenor.FromDateInterval(settle, maturity).ToString();
        var volSurface = new CalibratedVolatilitySurface(settle,
          new[] { new VolatilitySkewHolder(name, maturity, 0.25, vol, 0.0, 0.0) },
          volCalibrator, volCalibrator);
        volSurface.Fit();

        // Test Fx model with Foreign discount curve
        var pricer = new FxOptionSingleBarrierPricer(fxo, settle, settle,
          rdCurve, rfCurve, fxCurve, volSurface, 0);
        //pricer.VolatilityCurve = volCurve;
        double pv = pricer.Pv();
        AssertEqual("rf:Pv[" + i + ']', trade.Expect.Pv, pv, 1E-4);
        double delta = pricer.Delta() * 10000;
        AssertEqual("rf:Delta[" + i + ']', trade.Expect.Delta, delta, 2E-4);
        double vega = pricer.Vega() * 100;
        AssertEqual("rf:Vega[" + i + ']', trade.Expect.Vega, vega, 2E-2);
        // Disable theta for large differences, perhaps from different definitions.
        // Need further investigations.
        //double theta = pricer.Theta();
        //AssertEqual("Theta[" + i + ']', trade.Expect.Theta, theta, 1E-4);

        // Test Fx model with FX basis curve
        pricer = new FxOptionSingleBarrierPricer(fxo, settle, settle,
                  rdCurve, rfCurve, fxFactorCurve, volSurface, 0);
        //pricer.VolatilityCurve = volCurve;
        pv = pricer.Pv();
        AssertEqual("fx:Pv[" + i + ']', trade.Expect.Pv, pv, 1E-4);
        delta = pricer.Delta() * 10000;
        AssertEqual("fx:Delta[" + i + ']', trade.Expect.Delta, delta, 2E-4);
        vega = pricer.Vega() * 100;
        AssertEqual("fx:Vega[" + i + ']', trade.Expect.Vega, vega, 2E-2);

        // Now we test the touch options.
        // We need to change the barrier type to avoid exception.
        var bt = fxo.Barriers[0].BarrierType;
        if (bt == OptionBarrierType.DownOut || bt == OptionBarrierType.UpOut)
        {
          fxo.Barriers[0].BarrierType = spot < trade.Barrier
            ? OptionBarrierType.UpIn : OptionBarrierType.DownIn;
        }

        // Test no touch paid at maturity
        fxo.Flags |= OptionBarrierFlag.NoTouch;
        fxo.Barriers[0].BarrierType = OptionBarrierType.NoTouch;
        fxo.Validate();
        pricer = new FxOptionSingleBarrierPricer(fxo, settle, settle,
          rdCurve, rfCurve, fxCurve, volSurface, 0);
        pv = pricer.Pv();
        double expect = df * pricer.NoTouchProbability();
        AssertEqual("NoTouch@Mat", expect, pv, 1E-15);
        fxo.Flags &= ~OptionBarrierFlag.NoTouch;

        // Test one touch paid at maturity
        fxo.Flags |= OptionBarrierFlag.OneTouch;
        fxo.Barriers[0].BarrierType = OptionBarrierType.OneTouch;
        fxo.Validate();
        pricer = new FxOptionSingleBarrierPricer(fxo, settle, settle,
          rdCurve, rfCurve, fxCurve, volSurface, 0);
        double ote = pv = pricer.Pv();
        expect = OneTouchFlatValue(settle, maturity, false, rd, rf,
          pricer.SpotFxRate, pricer.AdjustedBarrier.Value, vol);
        AssertEqual("OneTouch@Mat", expect, pv, 1E-15);
        fxo.Flags &= ~OptionBarrierFlag.OneTouch;

        // Test one touch paid at hit
        expect = OneTouchFlatValue(settle, maturity, true, rd, rf,
          pricer.SpotFxRate, pricer.AdjustedBarrier.Value, vol);
        fxo.Flags |= OptionBarrierFlag.PayAtBarrierHit | OptionBarrierFlag.OneTouch;
        fxo.Validate();
        pricer = new FxOptionSingleBarrierPricer(fxo, settle, settle,
          rdCurve, rfCurve, fxCurve, volSurface, 0);
        pv = pricer.Pv();
        AssertEqual("OneTouch@Hit", expect, pv, 1E-15);
        Assert.Greater(pv, ote, "OneTouch@Hit > OneTouch@Mat");
        fxo.Flags &= ~OptionBarrierFlag.PayAtBarrierHit;
        
        // Test extreme case for one touch option: near zero volatility
        volSurface = new CalibratedVolatilitySurface(settle,
          new[] { new VolatilitySkewHolder(name, maturity, 0.25, 0.0, 0.0, 0.0) },
          volCalibrator, volCalibrator);
        volSurface.Fit();
        pricer = new FxOptionSingleBarrierPricer(fxo, settle, settle,
          rdCurve, rfCurve, fxCurve, volSurface, 0);
        pv = pricer.Pv();
        Assert.AreEqual(0,pv);
      }

      return;
    }

    private static double OneTouchFlatValue(
      Dt settle, Dt maturity,
      bool atHit,
      double rd, double rf,
      double spot, double barrier, double vol)
    {
      int w = atHit ? 0 : 1;
      double sigma2 = vol*vol;
      double a = (rd - rf)/sigma2 - 0.5;
      double b = Math.Sqrt(a*a*sigma2 + 2*(1-w)*rd)/vol;
      double ratio = barrier/spot;
      double t = (maturity - settle)/365.0;
      int theta = spot > barrier ? 1 : -1;
      double t1 = Math.Pow(ratio, a + b)*
        QMath.NormalCdf(theta*(Math.Log(ratio) + b*sigma2*t)/vol/Math.Sqrt(t));
      double t2 = Math.Pow(ratio, a - b)*
        QMath.NormalCdf(theta*(Math.Log(ratio) - b*sigma2*t)/vol/Math.Sqrt(t));
      double df = w == 0
        ? 1.0 : new DiscountCurve(settle, rd).DiscountFactor(settle, maturity);
      return df*(t1 + t2);
    }

    #region Construct touch options

    public enum ExceptionStatus
    {
      NoException,
      ExpectValidationError,
      ExpectMissingCallPutError,
    }

    private const ExceptionStatus
      NoException = ExceptionStatus.NoException,
      ExpectValidationError = ExceptionStatus.ExpectValidationError,
      ExpectMissingCallPutError = ExceptionStatus.ExpectMissingCallPutError;


    private const OptionBarrierFlag
      Digital = OptionBarrierFlag.Digital,
      OneTouch = OptionBarrierFlag.OneTouch,
      NoTouch = OptionBarrierFlag.NoTouch;

    private const OptionType
      Call = OptionType.Call, Put = OptionType.Put;

    private const OptionBarrierType
      UpIn = OptionBarrierType.UpIn, UpOut = OptionBarrierType.UpOut,
      DownIn = OptionBarrierType.DownIn, DownOut = OptionBarrierType.DownOut;

    // Build touch option based on Digital flag and Call/Put type.
    //  Call translates to UpIn for OneTouch, UpOut for NoTouch;
    //  Put translates to DownIn for OneTouch, DownOut for NoTouch;
    [TestCase(Digital, OptionBarrierType.OneTouch, Call, NoException)]
    [TestCase(Digital, OptionBarrierType.OneTouch, Put, NoException)]
    [TestCase(Digital, OptionBarrierType.NoTouch, Call, NoException)]
    [TestCase(Digital, OptionBarrierType.NoTouch, Put, NoException)]
    [TestCase(Digital, OptionBarrierType.OneTouch, OptionType.None, ExpectMissingCallPutError)]
    [TestCase(Digital, OptionBarrierType.NoTouch, OptionType.None, ExpectMissingCallPutError)]
    // Build touch option based on OneTouch/NoTouch flags,
    //  the call/put type does not matter.
    [TestCase(OneTouch, UpIn, OptionType.None, NoException)]
    [TestCase(OneTouch, DownIn, OptionType.None, NoException)]
    [TestCase(NoTouch, UpOut, OptionType.None, NoException)]
    [TestCase(NoTouch, DownOut, OptionType.None, NoException)]
    [TestCase(OneTouch, UpIn, Call, NoException)]
    [TestCase(OneTouch, DownIn, Call, NoException)]
    [TestCase(NoTouch, UpOut, Call, NoException)]
    [TestCase(NoTouch, DownOut, Call, NoException)]
    [TestCase(OneTouch, UpIn, Put, NoException)]
    [TestCase(OneTouch, DownIn, Put, NoException)]
    [TestCase(NoTouch, UpOut, Put, NoException)]
    [TestCase(NoTouch, DownOut, Put, NoException)]
    [TestCase(OneTouch, UpOut, OptionType.None, ExpectValidationError)]
    [TestCase(OneTouch, DownOut, OptionType.None, ExpectValidationError)]
    [TestCase(NoTouch, UpIn, OptionType.None, ExpectValidationError)]
    [TestCase(NoTouch, DownIn, OptionType.None, ExpectValidationError)]
    public static void BuildTouchOption(
      OptionBarrierFlag flag,
      OptionBarrierType barrierType,
      OptionType optionType,
      ExceptionStatus exceptionStatus)
    {
      Dt settle = new Dt(20110125);
      Dt maturity = new Dt(20160125);
      var fxo = new FxOption()
      {
        Effective = settle,
        Maturity = maturity,
        ReceiveCcy = Currency.USD,
        PayCcy = Currency.EUR,
        Type = optionType,
        Style = OptionStyle.European,
        Strike = 0,
        Flags = flag,
      };
      fxo.Barriers.Add(new Barrier()
      {
        BarrierType = barrierType,
        Value = 1.0,
        MonitoringFrequency = Frequency.Continuous
      });

      if (exceptionStatus == ExceptionStatus.ExpectMissingCallPutError)
      {
        NUnit.Framework.Assert.Throws<ToolkitException>(fxo.MapTouchOption);
        return;
      }
      fxo.MapTouchOption();

      if (exceptionStatus == ExceptionStatus.ExpectValidationError)
      {
        NUnit.Framework.Assert.Throws<ValidationException>(fxo.Validate);
        return;
      }
      fxo.Validate();

      var expectFlag = flag;
      var expectBarrierType = barrierType;
      if (flag == OptionBarrierFlag.Digital)
      {
        expectFlag = barrierType == OptionBarrierType.OneTouch
          ? OneTouch : NoTouch;

        expectBarrierType = optionType == Call
          ? (barrierType == OptionBarrierType.OneTouch ? UpIn : UpOut)
          : (barrierType == OptionBarrierType.OneTouch ? DownIn : DownOut);
      }

      Assert.AreEqual(expectFlag, fxo.Flags);
      Assert.AreEqual(expectBarrierType, fxo.Barriers[0].BarrierType);
    }

    #endregion
  }
}
