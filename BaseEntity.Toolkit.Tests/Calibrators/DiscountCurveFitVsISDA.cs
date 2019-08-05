// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Calibrators;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  /// <summary>
  /// Test the new swap calibrator.
  /// </summary>
  [TestFixture]
  public class DiscountCurveFitVsISDA
  {
    [Test, Smoke]
    public void AddWithCycle()
    {
      Dt date = new Dt(30, 09, 2008);
      Dt newDate = Dt.Add(date, Frequency.Quarterly, 1, CycleRule.None);
      Dt expectedDate = new Dt(30, 12, 2008);
      Assert.AreEqual(expectedDate.ToInt(), newDate.ToInt());
    }

    [Test, Smoke]
    public void Regular()
    {
      Dt start = new Dt(20100901);
      Dt stop = new Dt(20130101);
      for (Dt dt = start; dt < stop; dt = Dt.Add(dt, 1))
      {
        if (Dt.Roll(dt, BDConv, Calendar.None) == dt)
          TestCurvesForDate(dt, MaturityType.Regular);
      }
      return;
    }

    #region Data
    private const int M = 6;
    private const DayCount MMDayCount = DayCount.Actual360;
    private const DayCount FixedSwapDayCount = DayCount.Thirty360;
    private const Frequency FixedSwapFrequency = Frequency.SemiAnnual;
    private const BDConvention BDConv = BDConvention.Modified;

    private string[] tenors = {
      // money markets
      "1M", "2M", "3M", "6M", "9M", "1Y", 
      // swaps
      "2Y","3Y", "4Y","5Y","6Y","7Y","8Y","9Y",
      "10Y","12Y","15Y","20Y","25Y","30Y" 
    };
    private double[] rates = {
      // money markets
      0.327, 0.406, 0.493, 0.697, 0.871, 1.075,
      // swaps
      0.824, 1.192, 1.575, 1.940, 2.250, 2.498, 2.714,
      2.875, 3.011, 3.233, 3.462, 3.641, 3.726, 3.768
    };
    #endregion Data

    #region Helpers

    /// <summary>
    /// Build a zero curve and a curve,
    /// assert they are close (Number of points is different however).
    /// </summary>
    /// <param name="asOf">As of date.</param>
    void TestCurvesForDate(Dt asOf, MaturityType mt)
    {
      DiscountCurve zc = GetCurve("ISDA", asOf, mt);
      DiscountCurve qc = GetCurve("LIBOR", asOf, mt);
      int last = zc.Count - 1;
      for (int i = 0; i < last; ++i)
      {
        double zdf = zc.GetVal(i);
        double qdf = qc.Interpolate(zc.GetDt(i));
        if (Math.Abs(zdf - qdf) > 5E-6)
        {
          Assert.AreEqual(zdf, qdf, 1E-4, "@" + asOf.ToInt() + "Df[" + i + ']');
          return;
        }
      }
      return;
    }

    /// <summary>
    /// Create a discount curve with a given asOf date.
    /// </summary>
    /// <param name="asOf">As of date.</param>
    /// <returns>Discount curve</returns>
    private DiscountCurve GetCurve(string type, Dt asOf, MaturityType mt)
    {
      DiscountCalibrator calibrator;
      if (type == "ISDA")
      {
        var cal = new DiscountBootstrapCalibrator(asOf, asOf);
        cal.SwapCalibrationMethod = SwapCalibrationMethod.Solve;
        calibrator = cal;
      }
      else
      {
        CurveFitSettings setting = new CurveFitSettings
        {
          Method = CurveFitMethod.Bootstrap,
          MarketWeight = 1.0,
          SlopeWeight = 0.0,
          InterpScheme = new InterpScheme {Method = InterpMethod.Weighted}
        };
        calibrator = new DiscountCurveFitCalibrator(asOf, null, new CalibratorSettings(setting));
      }

      DiscountCurve curve = new DiscountCurve(calibrator);
      curve.Name = type + asOf.ToInt();

      // Add MM rates
      for (int i = 0; i < M; ++i)
      {
        var s = tenors[i];
        curve.AddMoneyMarket(s, GetBusinessDay(asOf, s),
          rates[i]/100, MMDayCount);
      }

      // Add swap rates
      for (int i = M; i < tenors.Length;++i)
      {
        var s = tenors[i];
        curve.AddSwap(s, SwapMaturity(asOf, s, mt),
                      rates[i] / 100, FixedSwapDayCount, FixedSwapFrequency,
                      BDConv, Calendar.None);
      }
      curve.Fit();

      return curve;
    }

    /// <summary>
    /// Gets the swap maturity with a given tenor name
    /// and maturity type.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="tenor">The tenor.</param>
    /// <param name="mt">Maturity type.</param>
    /// <returns>Maturity date</returns>
    private static Dt SwapMaturity(
      Dt asOf, string tenor, MaturityType mt)
    {
      Dt mat = Dt.Add(asOf, tenor);
      switch (mt)
      {
        case MaturityType.Short:
          mat = Dt.Add(mat, -7);
          break;
        case MaturityType.Long:
          mat = Dt.Add(mat, +7);
          break;
        default: break;
      }
      return mat;
    }

    /// <summary>
    /// Gets a business day with a given tenor name.
    /// </summary>
    /// <param name="asOf">As of date.</param>
    /// <param name="tenor">Tenor name.</param>
    /// <returns>Tenor date</returns>
    private static Dt GetBusinessDay(Dt asOf, string tenor)
    {
      Dt date = Dt.Add(asOf, tenor);
      int days = Dt.Diff(asOf, date);
      if (days <= 21)
        return Dt.Roll(date, BDConvention.Following, Calendar.None);
      else
        return Dt.Roll(date, BDConvention.Modified, Calendar.None);
    }

    enum MaturityType { Regular, Short, Long };

    #endregion Helpers
  }
}
