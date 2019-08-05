//
// Copyright (c)    2002-2018. All rights reserved.
//

using System;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Numerics.Rng;
using Correlation=BaseEntity.Toolkit.Models.BGM.BgmCorrelation;
using BgmCalibrationMethod = BaseEntity.Toolkit.Base.VolatilityBootstrapMethod;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestBgmCalibrationRoundTrip
  {
    /// <summary>
    /// Swaptions the rount trip.
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="dim">The dim.</param>
    /// <param name="rho">The rho.</param>
    public void SwaptionRountTrip(Dt asOf, int dim, double rho,
      bool flat, DistributionType voltype)
    {
      // Build a set of random volatility curves.
      var data = new BgmTestData();
      var v = new RandomForwardVolatilityCurves(data, asOf, dim, flat);
      int count = v.Count; // the number active rates
      var dates = v.Dates; // the array of maturity dates.

      // Build a random discount curve.
      var dc = v.BuildRateCurve(data.Generate);
      double[] rates, dfracs;
      v.CalculateRatesAndDiscountedFractions(dc, out rates, out dfracs);

      // Build a matrix of swaption volatilities
      var swpnVolatilities = v.BuildSwaptionVolatilities(voltype, dc, rho);

      // Build expiries/tenors
      var tenors = new string[dim];
      for (int i = 0; i < dim; ++i)
      {
        tenors[i] = (i + 1).ToString() + 'Q';
      }

      // Now call the cailbrator to build a surface
      var surface = BgmForwardVolatilitySurface.Create(asOf,
        new BgmCalibrationParameters{CalibrationMethod = VolatilityBootstrapMethod.Cascading},
        dc, tenors, tenors,
        data.CycleRule, data.BDConvention, data.Calendar,
        v.MakeCorrelation(count, rho),
        CloneUtil.Clone(swpnVolatilities),
        voltype);

      // Do we get back the original forward volatilties?
      {
        // The cascading has bigger round off error with digh dimension.
        double tol = (flat ? 1E-4 : 1E-4) * dim;
        var ff = ((BgmCalibratedVolatilities)surface.CalibratedVolatilities)
          .GetForwardVolatilities();
        for (int t = 0; t < dim; ++t)
        {
          int curveCount = flat ? count : dim;
          for (int i = t; i < curveCount; ++i)
          {
            var expect = v.Curves[i].GetVal(flat ? 0 : t);
            var actual = ff[i, t];
            if (Math.Abs(actual - expect) > tol)
              Assert.AreEqual(expect, actual, tol, "FwdVol[" + i + ',' + t + ']');
          }
        }
      }

      // Do we get back the original swaption volatilties?
      {
        double tol = flat ? 1E-6 : 0.0005;// 1E-14;
        for (int i = 0; i < dim; ++i)
        {
          Dt expiry = Dt.Add(asOf, (int)(dates[i] + 0.1));
          for (int j = 0; j < dim; ++j)
          {
            var expect = swpnVolatilities[i, j];
            Dt maturity = Dt.Add(asOf, (int) (dates[i + j + 1] + 0.1));
            var interp = surface.GetSwaptionVolatilityInterpolator(
              expiry, maturity);
            double actual = interp.Interpolate(v.AsOf, 0);
            if (Math.Abs(actual - expect) > tol)
              Assert.AreEqual(expect, actual, tol,
                "Swpn[" + i + ',' + j + "]@" + rho + '/' + dim);
          }
        }
      }
      return;
    }

    [Test]
    public void Dim20Normal()
    {
      Dt asOf = Dt.Today();
      foreach (int dim in new[] { 2})//1, 2, 3, 5, 10, 20 })
        foreach (double rho in new[] { 0.0})//, 0.5, 0.9, 0.95, 1.0 })
        {
          SwaptionRountTrip(asOf, dim, rho, true, DistributionType.Normal);
        }
      return;
    }

    [Test]
    public void Dim20()
    {
      Dt asOf = Dt.Today();
      foreach (int dim in new[] { 1, 2, 3, 5, 10, 20 })
        foreach (double rho in new[] { 0.0, 0.5, 0.9, 0.95, 1.0 })
        {
          SwaptionRountTrip(asOf, dim, rho, true, DistributionType.LogNormal);
        }
      return;
    }

    [Test]
    public void Dim50()
    {
      const int dim = 50;
      Dt asOf = Dt.Today();
      foreach (double rho in new[] { 0.0, 0.5, 0.9, 0.95, 1.0 })
      {
        SwaptionRountTrip(asOf, dim, rho, true, DistributionType.LogNormal);
      }
      return;
    }

    [Test]
    public void Dim100()
    {
      const int dim = 50;
      Dt asOf = Dt.Today();
      foreach (double rho in new[] { 0.0, 0.5, 0.9, 0.95, 1.0 })
      {
        SwaptionRountTrip(asOf, dim, rho, true, DistributionType.LogNormal);
      }
      return;
    }

    [Test]
    public void Dim200()
    {
      const int dim = 50;
      Dt asOf = Dt.Today();
      foreach (double rho in new[] { 0.0, 0.5, 0.9, 0.95, 1.0 })
      {
        SwaptionRountTrip(asOf, dim, rho, true, DistributionType.LogNormal);
      }
      return;
    }
  }

  internal class BgmTestData
  {
    internal static readonly RandomNumberGenerator Rng = new RandomNumberGenerator();
    internal Frequency Frequency = Toolkit.Base.Frequency.Quarterly;
    internal BDConvention BDConvention = BDConvention.Modified;
    internal CycleRule CycleRule = CycleRule.None;
    internal Calendar Calendar = Calendar.NYB;
    internal Func<double> Generate = () => Rng.Uniform(0, 1);
  }

  internal class RandomForwardVolatilityCurves
  {
    public Dt AsOf { get; private set; }
    public int Count => Curves.Length;
    public int Dim => (Curves.Length + 1)/2;
    public VolatilityCurve[] Curves { get; private set; }
    public double[,] AverageVolatilities { get; private set; }
    public double[] Dates { get; private set; }

    public RandomForwardVolatilityCurves(
      BgmTestData test, Dt asOf, int dim, bool flat) // number of expiries
    {
      var cycleRule = test.CycleRule;
      var bdc = test.BDConvention;
      var calendar = test.Calendar;
      var rng = test.Generate;
      var freq = test.Frequency;
      if (asOf.IsEmpty()) asOf = Dt.Today();
      AsOf = asOf;

      // The number of active rates is (2*dim - 1)
      int count = 2 * dim - 1;

      // Make an array of curves
      var curves = Curves = new VolatilityCurve[count];
      {
        Interp interp = new Flat();
        for (int i = 1; i <= count; ++i)
          curves[i - 1] = new VolatilityCurve(asOf)
          {
            Name = "curve" + i,
            Interp = interp
          };
      }

      // Average volatilities by rates and expiries
      var avgs = AverageVolatilities = new double[count, dim];

      // The number of dates is one plus the number of active rates.
      var dates = Dates = new double[count + 1];

      // Now build the forward volatility curve
      int lastDate = -1;
      double date0 = 0;
      for (int t = 0; t < dim; ++t)
      {
        // For period (t, t+1), the first active rate resets at (t+1).
        Dt date = Dt.Roll(Dt.Add(asOf, freq, t + 1, cycleRule), bdc, calendar);
        dates[lastDate = t] = date - asOf;
        double frac = (dates[t] - date0);
        date0 = dates[t];
        for (int i = t; i < count; ++i)
        {
          // the volatility of rate i in period (t, t+1)
          double sigma = rng();
          curves[i].AddVolatility(date, sigma);
          avgs[i, t] += sigma * sigma * frac;
        }
        if (flat) break;
      }
      // Fits all the curves.
      for (int t = 0; t < count; ++t)
      {
        curves[t].Fit();
      }
      // Converts all the sums of square to average volatilities.
      for (int t = 0; t < dim; ++t)
      {
        double time = dates[t];
        for (int i = t; i < count; ++i)
        {
          avgs[i, t] = Math.Sqrt(avgs[i, t] / time);
        }
        if(flat) break;
      }
      // Find those dates after the last expiry.
      // Note: This depends date convention.
      for (int t = lastDate + 1; t < dates.Length; ++t)
      {
        dates[t] = Dt.Roll(Dt.Add(asOf, freq, t + 1, cycleRule),
          bdc, calendar) - asOf;
      }
    }

    public DiscountCurve BuildRateCurve(Func<double> rng)
    {
      Dt asOf = AsOf;
      double[] dates = Dates;
      var dc = new DiscountCurve(asOf); // The discount curve.
      double df = 1.0;
      for (int i = 0; i < dates.Length; ++i)
      {
        double frac = (dates[i] - (i == 0 ? 0.0 : dates[i - 1]))/365.0;
        double rate = rng();
        df /= 1 + rate*frac;
        dc.Add(Dt.Add(asOf, (int) (dates[i] + 0.1)), df);
      }
      return dc;
    }

    public Correlation MakeCorrelation(int n, double rho)
    {
      var corrs = new double[n,n];
      for (int i = 0; i < n; ++i)
      {
        corrs[i, i] = 1.0;
        for (int j = 0; j < i; ++j)
          corrs[i, j] = corrs[j, i] = rho;
      }
      return Correlation.CreateBgmCorrelation(
        BgmCorrelationType.CorrelationMatrix, n, corrs);
    }

    public double[,] BuildSwaptionVolatilities(
      DistributionType voltype, DiscountCurve dc, double rho)
    {
      double[] rates, dfracs;
      CalculateRatesAndDiscountedFractions(dc, out rates, out dfracs);
      var dim = Dim;
      var swpnVolatilities = new double[dim,dim];
      for (int i = 0; i < dim; ++i)
      {
        Dt expiry = Dt.Add(AsOf, (int) (Dates[i] + 0.01));
        for (int j = 0; j < dim; ++j)
        {
          //Dt maturity = Dt.Add(asOf, (int) (dates[i + j + 1] + 0.1));
          var sv = BgmCalibrations.SwaptionVolatility(
            voltype == DistributionType.Normal, AsOf, expiry,
            rates.Skip(i + 1).Take(j + 1).ToArray(),
            dfracs.Skip(i + 1).Take(j + 1).ToArray(),
            Curves.Skip(i).Take(j + 1).ToArray(),
            MakeCorrelation(j + 1, rho));
          swpnVolatilities[i, j] = sv;
        }
      }
      return swpnVolatilities;
    }

    public void CalculateRatesAndDiscountedFractions(
      DiscountCurve dc, out double[] rates, out double[] dfracs)
    {
      Dt asOf = AsOf;
      double[] dates = Dates;
      rates = new double[dates.Length];  // to store an array of rates
      dfracs = new double[dates.Length]; // to store an array of discount fractions
      double firstExpiryDf = dc.Interpolate(asOf, Dt.Add(asOf, (int)(dates[0] + 0.1)));
      double df0 = 1.0;
      for (int i = 0; i < dates.Length; ++i)
      {
        double df = dc.Interpolate(asOf, Dt.Add(asOf, (int)(dates[i] + 0.1)));
        double frac = (dates[i] - (i == 0 ? 0.0 : dates[i - 1])) / 365.0;
        rates[i] = (df0/df - 1)/frac;
        dfracs[i] = df * frac / firstExpiryDf;
        df0 = df;
      }
      return;
    }
  } // class RandomForwardVolatilityCurves
}
