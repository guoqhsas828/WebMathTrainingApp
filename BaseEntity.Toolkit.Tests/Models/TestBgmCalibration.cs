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
using BaseEntity.Toolkit.Util;
using Correlation = BaseEntity.Toolkit.Models.BGM.BgmCorrelation;
using BgmCalibrationMethod = BaseEntity.Toolkit.Base.VolatilityBootstrapMethod;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestBgmCalibration : ToolkitTestBase
  {
    #region Data
    private double[,] historicalCorrelation_ =
      {
        {
          1, 0.82, 0.69, 0.65, 0.58, 0.47, 0.29, 0.23, 0.43, 0.47, 0.33, 0.43,
          0.29, 0.23, 0.26, 0.21, 0.23, 0.29, 0.25
        },
        {
          0.82, 1, 0.8, 0.73, 0.68, 0.55, 0.45, 0.4, 0.53, 0.57, 0.42, 0.45, 0.48
          , 0.34, 0.35, 0.32, 0.32, 0.31, 0.32
        },
        {
          0.69, 0.8, 1, 0.76, 0.72, 0.63, 0.47, 0.56, 0.67, 0.61, 0.48, 0.52,
          0.48, 0.54, 0.46, 0.42, 0.45, 0.42, 0.35
        },
        {
          0.65, 0.73, 0.76, 1, 0.78, 0.67, 0.58, 0.56, 0.68, 0.7, 0.56, 0.59,
          0.58, 0.5, 0.5, 0.48, 0.49, 0.44, 0.35
        },
        {
          0.58, 0.68, 0.72, 0.78, 1, 0.84, 0.66, 0.67, 0.71, 0.73, 0.7, 0.67,
          0.64, 0.59, 0.58, 0.65, 0.65, 0.53, 0.42
        },
        {
          0.47, 0.55, 0.63, 0.67, 0.84, 1, 0.77, 0.68, 0.73, 0.69, 0.77, 0.69,
          0.66, 0.63, 0.61, 0.68, 0.7, 0.57, 0.45
        },
        {
          0.29, 0.45, 0.47, 0.58, 0.66, 0.77, 1, 0.72, 0.71, 0.65, 0.65, 0.62,
          0.71, 0.62, 0.63, 0.66, 0.64, 0.52, 0.38
        },
        {
          0.23, 0.4, 0.56, 0.56, 0.67, 0.68, 0.72, 1, 0.73, 0.66, 0.64, 0.56,
          0.61, 0.72, 0.59, 0.64, 0.64, 0.49, 0.46
        },
        {
          0.43, 0.53, 0.67, 0.68, 0.71, 0.73, 0.71, 0.73, 1, 0.75, 0.59, 0.66,
          0.69, 0.69, 0.69, 0.63, 0.64, 0.52, 0.4
        },
        {
          0.47, 0.57, 0.61, 0.7, 0.73, 0.69, 0.65, 0.66, 0.75, 1, 0.63, 0.68, 0.7
          , 0.63, 0.64, 0.65, 0.62, 0.52, 0.4
        },
        {
          0.33, 0.42, 0.48, 0.56, 0.7, 0.77, 0.65, 0.64, 0.59, 0.63, 1, 0.83,
          0.72, 0.64, 0.58, 0.68, 0.73, 0.57, 0.45
        },
        {
          0.43, 0.45, 0.52, 0.59, 0.67, 0.69, 0.62, 0.56, 0.66, 0.68, 0.83, 1,
          0.82, 0.69, 0.67, 0.7, 0.69, 0.65, 0.43
        },
        {
          0.29, 0.48, 0.48, 0.58, 0.64, 0.66, 0.71, 0.61, 0.69, 0.7, 0.72, 0.82,
          1, 0.79, 0.78, 0.79, 0.72, 0.59, 0.42
        },
        {
          0.23, 0.34, 0.54, 0.5, 0.59, 0.63, 0.62, 0.72, 0.69, 0.63, 0.64, 0.69,
          0.79, 1, 0.82, 0.83, 0.79, 0.6, 0.45
        },
        {
          0.26, 0.35, 0.46, 0.5, 0.58, 0.61, 0.63, 0.59, 0.69, 0.64, 0.58, 0.67,
          0.78, 0.82, 1, 0.9, 0.8, 0.5, 0.22
        },
        {
          0.21, 0.32, 0.42, 0.48, 0.65, 0.68, 0.66, 0.64, 0.63, 0.65, 0.68, 0.7,
          0.79, 0.83, 0.9, 1, 0.94, 0.71, 0.46
        },
        {
          0.23, 0.32, 0.45, 0.49, 0.65, 0.7, 0.64, 0.64, 0.64, 0.62, 0.73, 0.69,
          0.72, 0.79, 0.8, 0.94, 1, 0.82, 0.66
        },
        {
          0.29, 0.31, 0.42, 0.44, 0.53, 0.57, 0.52, 0.49, 0.52, 0.52, 0.57, 0.65,
          0.59, 0.6, 0.5, 0.71, 0.82, 1, 0.84
        },
        {
          0.25, 0.32, 0.35, 0.35, 0.42, 0.45, 0.38, 0.46, 0.4, 0.4, 0.45, 0.43,
          0.42, 0.45, 0.22, 0.46, 0.66, 0.84, 1
        }
      };

    private double[,] swapnVolatilities_ =
      {
        {40.00, 37.30, 33.20, 30.60, 28.90, 27.30, 26.30, 25.60, 25.20, 24.80},
        {31.20, 27.90, 25.90, 24.70, 23.80, 23.10, 22.50, 22.10, 21.90, 21.80},
        {25.70, 23.50, 22.10, 21.40, 21.00, 20.50, 20.10, 19.90, 19.70, 19.70},
        {22.00, 20.60, 19.70, 19.40, 19.20, 18.90, 18.50, 18.30, 18.10, 18.10},
        {20.50, 19.50, 19.00, 18.40, 17.70, 17.40, 17.20, 17.00, 17.00, 16.90},
        {19.40, 18.40, 18.85, 16.75, 16.65, 16.40, 16.25, 16.15, 16.15, 16.10},
        {18.00, 17.00, 16.60, 16.00, 15.60, 15.40, 15.30, 15.30, 15.30, 15.30},
        {17.10, 16.50, 15.60, 15.57, 15.20, 14.88, 14.59, 14.33, 14.09, 13.88},
        {16.20, 15.70, 15.20, 14.90, 14.55, 14.25, 13.97, 13.72, 13.49, 13.29},
        {15.75, 15.00, 14.50, 14.35, 14.02, 13.72, 13.46, 13.22, 13.00, 12.81}
      };
    #endregion Data

    static double[,] ResizeMatrix(double[,] matrix, int dim)
    {
      var data = new double[dim, dim];
      for (int i = 0; i < dim; ++i)
        for (int j = 0; j < dim; ++j)
          data[i, j] = matrix[i, j];
      return data;
    }

    static double[,] CreateFlatVolatility(int dim, double vol)
    {
      var data = new double[dim, dim];
      for (int i = 0; i < dim; ++i)
        for (int j = 0; j < dim; ++j)
          data[i, j] = vol;
      return data;
    }

    class SymmetricMatrix<T>
    {
      private T[] data_;
      private int index(int i, int j)
      {
        if (i < j)
        {
          var tmp = i;
          i = j;
          j = tmp;
        }
        int idx = i * (i + 1) / 2 + j;
        if (idx >= data_.Length)
        {
          throw new ToolkitException(String.Format(
            "Index [{0},{1}] out of boundary.", i, j));
        }
        return idx;
      }
      public SymmetricMatrix(int dim)
      {
        if (dim <= 0) data_ = new T[0];
        else data_ = new T[dim * (dim + 1) / 2];
      }
      public T this[int i, int j]
      {
        get { return data_[index(i, j)]; }
        set { data_[index(i, j)] = value; }
      }
    }

    private const BDConvention bdc_ = BDConvention.Modified;
    private const CycleRule cycleRule_ = CycleRule.None;
    private static readonly Calendar calendar_ = Calendar.NYB;

    class RandomForwardVolatilityCurves
    {
      public Dt AsOf { get; private set; }
      public int Count => Curves.Length;
      public VolatilityCurve[] Curves { get; private set; }
      public double[,] AverageVolatilities { get; private set; }
      public double[] Dates { get; private set; }

      public RandomForwardVolatilityCurves(
        Dt asOf,
        int dim, // number of expiries
        RandomNumberGenerator rng)
      {
        if (rng == null) rng = new RandomNumberGenerator();
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
        double date0 = 0;
        for (int t = 0; t < dim; ++t)
        {
          // For period (t, t+1), the first active rate resets at (t+1).
          Dt date = Dt.Roll(Dt.Add(asOf, Frequency.Quarterly,
            t + 1, cycleRule_), bdc_, calendar_);
          dates[t] = date - asOf;
          double frac = (dates[t] - date0);
          date0 = dates[t];
          for (int i = t; i < count; ++i)
          {
            // the volatility of rate i in period (t, t+1)
            double sigma = rng.Uniform(0, 1);
            curves[i].AddVolatility(date, sigma);
            avgs[i, t] += sigma * sigma * frac;
          }
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
        }
        // Find those dates after the last expiry.
        // Note: This depends date convention.
        for (int t = dim; t < dates.Length; ++t)
        {
          dates[t] = Dt.Roll(Dt.Add(asOf, Frequency.Quarterly,
            t + 1, cycleRule_), bdc_, calendar_) - asOf;
        }
      }
    } // class RandomForwardVolatilityCurves

    /// <summary>
    /// Swaptions the rount trip.
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="dim">The dim.</param>
    /// <param name="rho">The rho.</param>
    public void SwaptionRountTrip(Dt asOf, int dim, double rho)
    {
      var rng = new RandomNumberGenerator();

      // Build a set of random volatility curves.
      var v = new RandomForwardVolatilityCurves(asOf, dim, rng);
      int count = v.Count; // the number active rates
      var dates = v.Dates; // the array of maturity dates.

      // Build a random discount curve.
      var rates = new double[dates.Length];  // to store an array of rates
      var dfracs = new double[dates.Length]; // to store an array of discount fractions
      var dc = new DiscountCurve(asOf); // The discount curve.
      {
        double df = 1.0;
        for (int i = 0; i < dates.Length; ++i)
        {
          double frac = (dates[i] - (i == 0 ? 0.0 : dates[i - 1])) / 365.0;
          double rate = rates[i] = rng.Uniform(0, 1);
          df /= 1 + rate * frac;
          dfracs[i] = df * frac;
          dc.Add(Dt.Add(asOf, (int)(dates[i] + 0.1)), df);
        }
      }

      // Build a simple correlation matrix function.
      Func<int, Correlation> makeCorrelation = (n) =>
      {
        var corrs = new double[n, n];
        for (int i = 0; i < n; ++i)
        {
          corrs[i, i] = 1.0;
          for (int j = 0; j < i; ++j)
            corrs[i, j] = corrs[j, i] = rho;
        }
        return Correlation.CreateBgmCorrelation(
          BgmCorrelationType.CorrelationMatrix, n, corrs);
      };

      // Build a matrix of swaption volatilities
      var swpnVolatilities = new double[dim, dim];
      {
        for (int i = 0; i < dim; ++i)
        {
          Dt expiry = Dt.Add(asOf, (int)(dates[i] + 0.01));
          for (int j = 0; j < dim; ++j)
          {
            //Dt maturity = Dt.Add(asOf, (int) (dates[i + j + 1] + 0.1));
            var sv = BgmCalibrations.SwaptionVolatility(
              false, v.AsOf, expiry,
              rates.Skip(i + 1).Take(j + 1).ToArray(),
              dfracs.Skip(i + 1).Take(j + 1).ToArray(),
              v.Curves.Skip(i).Take(j + 1).ToArray(),
              makeCorrelation(j + 1));
            swpnVolatilities[i, j] = sv;
          }
        }
      }

      // Build expiries/tenors
      var tenors = new string[dim];
      for (int i = 0; i < dim; ++i)
      {
        tenors[i] = (i + 1).ToString() + 'Q';
      }

      // Now call the cailbrator to build a surface
      var surface = BgmForwardVolatilitySurface.Create(asOf,
        new BgmCalibrationParameters { CalibrationMethod = VolatilityBootstrapMethod.Cascading },
        dc, tenors, tenors, cycleRule_, bdc_, calendar_,
        makeCorrelation(count),
        CloneUtil.Clone(swpnVolatilities),
        DistributionType.LogNormal);

      // Do we get back the original forward volatilties?
      {
        // The cascading has bigger round off error with digh dimension.
        double tol = 2E-8 * dim;
        var ff = ((BgmCalibratedVolatilities)surface.CalibratedVolatilities)
          .GetForwardVolatilities();
        for (int t = 0; t < dim; ++t)
        {
          for (int i = t; i < dim; ++i)
          {
            var expect = v.Curves[i].GetVal(t);
            var actual = ff[i, t];
            if (Math.Abs(actual - expect) > tol)
              AssertEqual("FwdVol[" + i + ',' + t + ']', expect, actual, tol);
          }
        }
      }

      // Do we get back the original swaption volatilties?
      {
        const double tol = 1E-14;
        for (int i = 0; i < dim; ++i)
        {
          Dt expiry = Dt.Add(asOf, (int)(dates[i] + 0.1));
          for (int j = 0; j < dim; ++j)
          {
            var expect = swpnVolatilities[i, j];
            Dt maturity = Dt.Add(asOf, (int)(dates[i + j + 1] + 0.1));
            var interp = surface.GetSwaptionVolatilityInterpolator(
              expiry, maturity);
            double actual = interp.Interpolate(v.AsOf, 0);
            if (Math.Abs(actual - expect) > tol)
              AssertEqual("Swpn[" + i + ',' + j + ']', expect, actual, tol);
          }
        }
      }
      return;
    }


    [Test]
    public void SwaptionVolatilityRoundTrip()
    {
      foreach (int dim in new[] { 1, 2, 3, 5, 10, 20, 50 })
        SwaptionRountTrip(Dt.Today(), dim, 0.0);
      return;
    }

    

    [Test]
    public void CascadingFitFlatVolatility()
    {
      string[] tenors = new[] { "1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y" };
      Dt asOf = Dt.Today();
      var par = new BgmCalibrationParameters
      {
        CalibrationMethod = VolatilityBootstrapMethod.Cascading,
        Tolerance = 0.0001,
        PsiUpperBound = 1.0,
        PsiLowerBound = 0.0,
        PhiUpperBound = 1.002,
        PhiLowerBound = 0.9
      };
      var model = DistributionType.LogNormal;
      var sigma = 0.248;
      var dim = tenors.Length;

      var corrMatrix = Correlation.CreateBgmCorrelation(
        BgmCorrelationType.CorrelationMatrix,
        historicalCorrelation_.GetLength(0), historicalCorrelation_);
      // We test two factor correlation
      var correlation = corrMatrix.ReduceRank(2);

      var discountCurve = new DiscountCurve(asOf, 0.03);
      var surface = BgmForwardVolatilitySurface.Create(asOf,
        par, discountCurve, tenors, tenors,
        CycleRule.None, BDConvention.None, Calendar.None,
        correlation, CreateFlatVolatility(dim, sigma), model);
      int last = dim;
      for (int i = 0; i < last; ++i)
      {
        Dt expiry = Dt.Add(asOf, tenors[i]);
        for (int j = 0; j < dim; ++j)
        {
          Dt maturity = Dt.Add(expiry, tenors[j]);
          var interp = surface.GetSwaptionVolatilityInterpolator(expiry,
            maturity);
          double vol = interp.Interpolate(asOf, 0);
          const double tol = 1E-14;
          if (Math.Abs(vol - sigma) > tol)
            AssertEqual("Swpn[" + (i + 1) + ',' + (j + 1) + ']',
              sigma, vol, tol);
        }
      }
      return;
    }

    [Test]
    public void CascadingFitFlatVolatilityRectangular()
    {
      string[] expiries = { "1M", "2M", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "15Y", "20Y", "25Y", "30Y" };
      string[] tenors = { "1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y", "15Y", "20Y", "25Y", "30Y" };
      double[,] data = 
      {{ 0.430708872, 0.455326866, 0.425945933, 0.384810189, 0.353923112, 0.326822749, 0.305934322,
         0.289860774, 0.276978215, 0.267430958, 0.232805641, 0.220272702, 0.215261865, 0.212760352 },
       { 0.439169866, 0.448798476, 0.413919093, 0.374039522, 0.344152407, 0.318057608, 0.297918355,
         0.281849479, 0.269965394, 0.260420518, 0.228052251, 0.216021487, 0.210762142, 0.208011553 },
       { 0.451864305, 0.447776664, 0.405398405, 0.3662746, 0.336387182, 0.310990168, 0.290406335,
         0.274840854, 0.263455162, 0.253912903, 0.222801017, 0.211771487, 0.205764049, 0.20276429 },
       { 0.523521085, 0.454468615, 0.410319227, 0.363935156, 0.327331448, 0.300939131, 0.280364399,
         0.265472369, 0.253422429, 0.244385291, 0.216288265, 0.205264261, 0.200258403, 0.197260184 },
       { 0.583342086, 0.461290897, 0.402919182, 0.349808239, 0.308423626, 0.282846658, 0.263952492,
         0.248900663, 0.236364965, 0.227337328, 0.204266333, 0.195249733, 0.19024876, 0.187752668 },
       { 0.475179571, 0.385274819, 0.335897171, 0.296499721, 0.265423109, 0.246865722, 0.232824841,
         0.221298382, 0.212778154, 0.205757998, 0.189726942, 0.183221897, 0.179726536, 0.177234988 },
       { 0.373087036, 0.308982317, 0.274898656, 0.24783934, 0.226298109, 0.212768659, 0.203250026,
         0.195237057, 0.188722571, 0.183719551, 0.173207491, 0.168211795, 0.166219789, 0.164230795 },
       { 0.295106304, 0.251820555, 0.228775268, 0.21074376, 0.196226296, 0.187715029, 0.180208217,
         0.175196622, 0.17019842, 0.166696239, 0.159698312, 0.156707764, 0.154220856, 0.153232456 },
       { 0.241723622, 0.212227595, 0.196704349, 0.184692394, 0.175190458, 0.169186933, 0.164179282,
         0.160183407, 0.157183287, 0.155179903, 0.150193647, 0.147209675, 0.145723979, 0.144238108 },
       { 0.174933532, 0.163160533, 0.15616798, 0.150672489, 0.147166427, 0.144674101, 0.142676109,
         0.141174342, 0.140183084, 0.139189444, 0.135708953, 0.133726974, 0.13174423, 0.129759396 },
       { 0.134531876, 0.132169004, 0.131185289, 0.129688969, 0.129683882, 0.129197192, 0.130202125,
         0.130206318, 0.130208467, 0.130209263, 0.127734364, 0.12525587, 0.123273902, 0.119790369 },
       { 0.128286136, 0.129261767, 0.128756955, 0.128751285, 0.128745934, 0.129252248, 0.12875797,
         0.128761141, 0.128763263, 0.128764698, 0.126784933, 0.123305425, 0.119820759, 0.116330322 },
       { 0.128864165, 0.129292501, 0.128791535, 0.12779142, 0.128288892, 0.128794026, 0.129297484, 
         0.129799935, 0.129802477, 0.130303779, 0.12732441, 0.122840376, 0.118349159, 0.114356476 },
       { 0.130955098, 0.132323138, 0.132822597, 0.132323383, 0.132822831, 0.132828092, 0.132832172,
         0.132835475, 0.132838282, 0.132840731, 0.128355071, 0.123361154, 0.117868118, 0.11237305 },
       { 0.131560058, 0.13535636, 0.134858093, 0.134359806, 0.134360978, 0.133862647, 0.133364291,
         0.133365407, 0.133366505, 0.132368582, 0.126370513, 0.120375538, 0.114378797, 0.109382282 }
      };
      var par = new BgmCalibrationParameters
      {
        CalibrationMethod = VolatilityBootstrapMethod.Cascading,
        Tolerance = 0.0001,
        PsiUpperBound = 1.0,
        PsiLowerBound = 0.0,
        PhiUpperBound = 1.002,
        PhiLowerBound = 0.9
      };
      Dt asOf = Dt.Today();
      DiscountCurve dc = new DiscountCurve(asOf, 0.05);
      BgmCorrelation correlations = BgmCorrelation.CreateBgmCorrelation(BgmCorrelationType.PerfectCorrelation, expiries.Length, new double[0, 0]);
      var retVal = BgmForwardVolatilitySurface.Create(
        asOf, par, dc, expiries, tenors, CycleRule.None,
        BDConvention.None, Calendar.None, correlations,
        data, DistributionType.LogNormal);
      var fwdVols = ((BgmCalibratedVolatilities)retVal.CalibratedVolatilities)
        .GetForwardVolatilities();
      var resetDates = retVal.CalibratedVolatilities.ResetDates;
      AssertEqual("RateCount", fwdVols.GetLength(0), resetDates.Length);
      var curves = ((BgmCalibratedVolatilities)retVal.CalibratedVolatilities)
        .BuildForwardVolatilityCurves();
      AssertEqual("CurveCount", curves.Length, resetDates.Length);
    }
  }
}
