// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.IO;
using System.Linq;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Calibrators.Volatilities.Bump;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Calibrators.Volatilities
{
  [TestFixture]
  public class TestSwaptionVolatilityCube
  {
    #region Tests

    [TestCase(VolatilityType.LogNormal, SkewSpec.WithSkew)]
    [TestCase(VolatilityType.Normal, SkewSpec.WithSkew)]
    [TestCase(VolatilityType.LogNormal, SkewSpec.NoSkew)]
    [TestCase(VolatilityType.Normal, SkewSpec.NoSkew)]
    public void InterpolateForwardRateVolatilities(
      VolatilityType volatilityType, SkewSpec spec)
    {
      var cube = GetSwaptionVolatilityCube(
        new Dt(20160112), volatilityType, spec);
      var curve = cube.RateVolatilityCalibrator.DiscountCurve;
      var expiries = ((RateVolatilitySurface)cube.AtmVolatilityObject)
        .RateVolatilityCalibrator.Dates;
      var model = (IModelParameter) cube;
      var atmvols = GetAtmVolatilities(volatilityType);
      var skews = GetSkews(volatilityType, spec);
      var strikeShifts = _skewStrikeShifts;
      var tenors = _fwdTenors;
      for (int i = 0; i < tenors.Length; ++i)
      {
        var index = new InterestRateIndex("LIBOR_" + tenors[i],
          Tenor.Parse(tenors[i]).ToFrequency(), Currency.USD,
          DayCount.Actual360, Calendar.NYB, 0);
        for (int j = 0; j < expiries.Length; ++j)
        {
          Dt expiry = expiries[j];
          var fwdRate = CalculateForwardRate(curve, expiry, index);

          // ATM volatility should round-trip exactly.
          var atm = atmvols[j, i];
          var vol = model.Interpolate(expiry, fwdRate, index);
          Assert.AreEqual(atm, vol);

          if (skews.IsNullOrEmpty()) continue;

          // Skews should all round-trip exactly.
          for (int s = 0; s < skews.Length; ++s)
          {
            vol = model.Interpolate(expiry, fwdRate + strikeShifts[s], index);
            Assert.AreEqual(atm + skews[s], vol);
          }
        }
      }
    }

    [TestCase(VolatilityType.LogNormal, SkewSpec.WithSkew, BumpType.Parallel)]
    [TestCase(VolatilityType.Normal, SkewSpec.WithSkew, BumpType.Parallel)]
    [TestCase(VolatilityType.LogNormal, SkewSpec.NoSkew, BumpType.Parallel)]
    [TestCase(VolatilityType.Normal, SkewSpec.NoSkew, BumpType.Parallel)]
    [TestCase(VolatilityType.LogNormal, SkewSpec.WithSkew, BumpType.ByTenor)]
    [TestCase(VolatilityType.Normal, SkewSpec.WithSkew, BumpType.ByTenor)]
    [TestCase(VolatilityType.LogNormal, SkewSpec.NoSkew, BumpType.ByTenor)]
    [TestCase(VolatilityType.Normal, SkewSpec.NoSkew, BumpType.ByTenor)]
    [TestCase(VolatilityType.LogNormal, SkewSpec.WithSkew, BumpType.Uniform)]
    [TestCase(VolatilityType.Normal, SkewSpec.WithSkew, BumpType.Uniform)]
    [TestCase(VolatilityType.LogNormal, SkewSpec.NoSkew, BumpType.Uniform)]
    [TestCase(VolatilityType.Normal, SkewSpec.NoSkew, BumpType.Uniform)]
    public void TestBumpInterpolated(VolatilityType volatilityType, 
      SkewSpec spec, BumpType bumpType)
    {
      var asOf = new Dt(20160112);
      var swaption = GetSwaption(asOf, 0.4);
      var cube = GetSwaptionVolatilityCube(asOf, volatilityType, spec);
      var curve = cube.RateVolatilityCalibrator.DiscountCurve;
      var pricer = GetSwaptionPricer(asOf, swaption, curve, cube);

      TestSwaptionVolBumpInterpolated(pricer, bumpType);
    }

    [TestCase(@"toolkit/test/data/Pricers/Swaption.xml")]
    [TestCase(@"toolkit/test/data/Pricers/SwaptionWithPayment.xml")]
    public void TestBumpInterpolated(string path)
    {
      var bumpType = new[] {BumpType.Uniform, BumpType.ByTenor, BumpType.Parallel};

      var swaptionFile = Path.Combine(SystemContext.InstallDir, path);
      var pricer = XmlSerialization.ReadXmlFile(swaptionFile) as SwaptionBlackPricer;
      if (pricer != null)
      {
        foreach (var type in bumpType)
        {
          TestSwaptionVolBumpInterpolated(pricer, type);
        }
      }
    }

    [Test]
    public void TestBumpInterpolatedException()
    {
      var asOf = new Dt(20160112);
      var cube = GetSwaptionVolatilityCube(asOf, VolatilityType.LogNormal,
        SkewSpec.WithSkew);
      var expectedMessage = String.Format("Bump interpolated value " +
                                          "of {0} not supported yet",
        cube.VolatilitySurface.GetType().Name);
      Assert.That(()=>
      {
        cube.VolatilitySurface.Interpolator = null;
        ((SwaptionVolatilitySpline) cube.VolatilitySurface)
          .RateVolatilityInterpolator = new VolatilityPlainInterpolator();
        cube.VolatilitySurface.BumpInterpolated(1.0, BumpFlags.BumpInterpolated);
      },
      Throws.InstanceOf<NotSupportedException>().And.Message.EqualTo(expectedMessage));
    }


    private void TestSwaptionVolBumpInterpolated(SwaptionBlackPricer pricer, BumpType bumpType)
    {
      BumpFlags flags = BumpFlags.BumpInterpolated;
      var cube = pricer.VolatilityObject as SwaptionVolatilityCube;
      if (cube == null) return;

      var volatility = pricer.Volatility;
      var pv = pricer.ProductPv();
      pricer.Reset();

      double eps = 1e-13;

      BumpResult bump;
      double bumpedVolatility, bumpedPv;
      var selection = new[]
      {
        cube.VolatilitySurface
      }.SelectParallel(null).First();
      try
      {
        bump = selection.Bump(1.0, flags);
        bumpedVolatility = pricer.Volatility;
        bumpedPv = pricer.ProductPv();
      }
      finally
      {
        selection.Restore(flags);
      }

      Assert.AreEqual(bump.Amount, bumpedVolatility - volatility, eps, "Bump");
      Assert.AreEqual(1.0, bump.Amount * 10000, eps, "Amount");

      // Second check the delta from the sensitivity function
      pricer.Reset();
      var table = Sensitivities2.Calculate(new IPricer[] { pricer },
        "Pv", null, BumpTarget.Volatilities, 1.0, 0.0, bumpType,
        BumpFlags.BumpInterpolated, null, true, false, null, false, true, null);
      var tableCalcHedge = Sensitivities2.Calculate(new IPricer[] { pricer },
        "Pv", null, BumpTarget.Volatilities, 1.0, 0.0, bumpType,
        BumpFlags.BumpInterpolated, null, true, false, null, true, true, null);
      var delta = (double)table.Rows[0]["Delta"];
      var delta1 = (double)tableCalcHedge.Rows[0]["Delta"];

      Assert.AreEqual(delta1, delta, eps, "Delta");
      Assert.AreEqual((bumpedPv - pv) / bump.Amount, delta, eps, "Delta");


      var originalSurface = cube.VolatilitySurface;
      try
      {
        originalSurface.BumpInterpolated(1.0, flags);
        pricer.Reset();
        var bumpedPv1 = pricer.ProductPv();
        Assert.AreEqual(delta, (bumpedPv1 - pv) / bump.Amount,
          eps, "Delta bumping original");
      }
      finally
      {
        originalSurface.RestoreInterpolated();
      }

      //Test the direct bump
      TestDirectBump(cube);
    }


    private void TestDirectBump(SwaptionVolatilityCube cube)
    {
      var surface = cube.VolatilitySurface as RateVolatilitySurface;
      if (surface == null) return;
      var skew = cube.Skew;
      BumpFlags flags = BumpFlags.BumpInterpolated;
      var curve = cube.RateVolatilityCalibrator.DiscountCurve;
      var tenor = Tenor.Parse("1Y");
      var expiry = Dt.Add(surface.AsOf, "3M");
      const double bumpSize = 1.0;
      try
      {
        var index = new InterestRateIndex("LIBOR_" + tenor,
          tenor.ToFrequency(), Currency.USD,
          DayCount.Actual360, Calendar.NYB, 0);
        var fwdRate = CalculateForwardRate(curve, expiry, index);

        Dt maturity = Dt.Roll(Dt.Add(expiry, index.IndexTenor), index.Roll, index.Calendar);
        if (maturity <= expiry) maturity = expiry + 1;
        var duration = SwaptionVolatilityCube.ConvertForwardTenor(expiry, maturity);

        double volSkew = 0;
        if (skew != null)
        {
          volSkew = skew.Evaluate(expiry, duration, fwdRate, 0.04);
        }

        var rateInterp = surface.RateVolatilityInterpolator;
        var swapInterp = rateInterp as ISwapVolatilitySurfaceInterpolator;
        if (swapInterp != null)
        {
          surface.RateVolatilityInterpolator =
            new SwapVolatilityBumpInterpolator(swapInterp,
              0.0, flags, new BumpAccumulator());
        }

        var vol = ((SwapVolatilityBumpInterpolator) surface
            .RateVolatilityInterpolator)
          .Interpolate(surface, expiry, volSkew, duration);

        //bump the surface
        surface.BumpInterpolated(1.0, flags);

        var bumpedVol = ((SwapVolatilityBumpInterpolator) surface
          .RateVolatilityInterpolator).Interpolate(surface, expiry, volSkew, duration);

        Assert.AreEqual(bumpedVol-vol, 1E-4*bumpSize, 1E-15);
      }
      finally
      {
        surface.RestoreInterpolated();
      }
    }

    private static SwaptionBlackPricer GetSwaptionPricer(Dt asOf, Swaption swpn, 
      DiscountCurve dc, RateVolatilitySurface vol)
    {
      var pricer = new SwaptionBlackPricer(swpn, asOf, asOf, dc, dc, vol);
      pricer.Validate();
      return pricer;
    }

    private static Swaption GetSwaption(Dt effective, double strike)
    {
      var rateIndex = (InterestRateIndex)StandardReferenceIndices.Create("USDLIBOR_3M");
      var currency = rateIndex.Currency;
      var roll = rateIndex.Roll;
      var calendar = rateIndex.Calendar;
      const int noticeDays = 2;

      var oExpiry = RateVolatilityUtil.SwaptionStandardExpiry(effective,
        rateIndex, Tenor.Parse("1Y"));

      var swapEffective = RateVolatilityUtil
        .SwaptionStandardForwardSwapEffective(oExpiry, noticeDays, Calendar.NYB);

      var swapMaturity = Dt.Roll(Dt.Add(swapEffective, Tenor.Parse("1Y")),
        roll, calendar);

      var fixedLeg = new SwapLeg(swapEffective, swapMaturity, currency, strike,
        DayCount.Thirty360, Frequency.SemiAnnual, roll, calendar, false);
      var floatingLeg = new SwapLeg(swapEffective, swapMaturity, Frequency.Quarterly, 
        0.0, rateIndex, currency, DayCount.Actual360, roll, calendar);

      var swpn = new Swaption(effective, swapEffective, rateIndex.Currency, fixedLeg,
        floatingLeg, noticeDays, PayerReceiver.Payer, OptionStyle.European, strike);
      swpn.Validate();
      return swpn;
    }

    private static double CalculateForwardRate(
      DiscountCurve curve, Dt expiry, InterestRateIndex index)
    {
      Dt maturity = Dt.Roll(Dt.Add(expiry, index.IndexTenor),
        index.Roll, index.Calendar);
      if (maturity <= expiry) maturity = expiry + 1;
      var fraction = Dt.Fraction(expiry, maturity, expiry, maturity,
        index.DayCount, index.IndexTenor.ToFrequency());
      return (1/curve.Interpolate(expiry, maturity) - 1)/fraction;
    }

    #endregion

    #region Set up swaption volatility cube

    public enum SkewSpec
    {
      NoSkew,
      WithSkew
    };

    private SwaptionVolatilityCube GetSwaptionVolatilityCube(
      Dt asOf, VolatilityType volatilityType, SkewSpec spec)
    {
      var index = (InterestRateIndex)StandardReferenceIndices.
        Create("USDLIBOR_3M");
      var curve = new DiscountCurve(asOf, 0.02) { Name = "RateCurve" };
      return SwaptionVolatilityCube.CreateSwaptionMarketCube(asOf,
        curve, _expiries, _fwdTenors, GetAtmVolatilities(volatilityType),
        index, volatilityType, GetOnSkew(spec,_expiries),
        GetOnSkew(spec, _skewStrikeShifts), GetOnSkew(spec,_fwdTenors),
        GetSkewMatrix(volatilityType, spec), null, null,
        index.DayCount, index.Roll, Frequency.None, index.Calendar, 0);
    }

    private static T[] GetOnSkew<T>(SkewSpec spec, T[] a)
    {
      return spec != SkewSpec.WithSkew ? EmptyArray<T>.Instance : a;
    }

    private double[,] GetAtmVolatilities(VolatilityType volatilityType)
    {
      return volatilityType == VolatilityType.LogNormal
        ? _atmLogNormalVols : _atmNormalVols;
    }

    private double[] GetSkews(VolatilityType volatilityType, SkewSpec spec)
    {
      return spec != SkewSpec.WithSkew ? EmptyArray<double>.Instance
        : (volatilityType == VolatilityType.LogNormal ? _skewAdjusts
          : _skewAdjusts.Select(v => v/100).ToArray());
    }

    private double[,] GetSkewMatrix(VolatilityType volatilityType, SkewSpec spec)
    {
      int rows = _expiries.Length*_fwdTenors.Length;
      var skews = GetSkews(volatilityType, spec);
      return skews.IsNullOrEmpty() ? new double[0,0]
        : Enumerable.Repeat(skews, rows).ToArray2D(rows, skews.Length);
    }

    private string[] _expiries = { "1Y", "2Y", "5Y" };
    private string[] _fwdTenors = {"1M", "3M", "6M", "1Y"};

    private double[,] _atmLogNormalVols =
    {
      {0.70, 0.35, 0.25, 0.20},
      {0.45, 0.30, 0.25, 0.20},
      {0.30, 0.25, 0.20, 0.18}
    };

    private double[,] _atmNormalVols =
    {
      {0.0090, 0.0110, 0.0105, 0.0100},
      {0.0120, 0.0115, 0.0110, 0.0100},
      {0.0115, 0.0110, 0.0105, 0.0095}
    };

    private double[] _skewStrikeShifts = {-0.01, -0.005, 0.0, 0.005, 0.01};
    private double[] _skewAdjusts = {0.01, 0.005, 0.0, 0.0075, 0.0125};

    #endregion
  }
}
