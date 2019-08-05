using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Tests.Sensitivity
{
  public static class SensitivityTestUtil
  {

    /// <summary>
    /// Create a calibrated IR curve
    /// </summary>
    public static DiscountCurve CreateIRCurve(Dt asOf, double bump = 0.0)
    {
      var mmTenors = new[] { "6 Month", "1 Year" };
      var mmRates = new[] { 0.0369, 0.0386 };
      var mmMaturities = new Dt[mmTenors.Length];
      for (int i = 0; i < mmTenors.Length; i++)
        mmMaturities[i] = Dt.Add(asOf, mmTenors[i]);
      var mmDayCount = DayCount.Actual360;
      var swapTenors = new[] { "2 Year", "3 Year", "5 Year", "7 Year", "10 Year" };
      var swapRates = new[] { 0.0399, 0.0407, 0.0417, 0.0426, 0.044 };
      var swapMaturities = new Dt[swapTenors.Length];
      for (int i = 0; i < swapTenors.Length; i++)
        swapMaturities[i] = Dt.Add(asOf, swapTenors[i]);
      var swapDayCount = DayCount.Thirty360;

      //IR curve is calibrated to market so need bootstrap calibrator
      var calibrator = new DiscountBootstrapCalibrator(asOf, asOf)
      {
        SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const)
      };
      var curve = new DiscountCurve(calibrator)
      {
        Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const),
        Ccy = Currency.USD,
        Category = "None",
        Name = "USD_LIBOR"
      };
      // Add MM rates
      var rand = new Random(-1234);
      for (var i = 0; i < mmTenors.Length; i++)
      {
        var r = mmRates[i] + rand.NextDouble() * 0.01;
        curve.AddMoneyMarket(mmTenors[i], mmMaturities[i], r + bump / 10000.0, mmDayCount);
      }
      // Add swap rates
      for (var i = 0; i < swapTenors.Length; i++)
      {
        var r = swapRates[i] + rand.NextDouble() * 0.01;
        curve.AddSwap(swapTenors[i], swapMaturities[i], r + bump / 10000.0, swapDayCount,
          Frequency.SemiAnnual, BDConvention.None, Calendar.None);
      }
      curve.Fit();
      return curve;
    }

    /// <summary>
    /// Create a survival curve for a name using info including discount curve, recovery rate, tenors and spreads quotes
    /// </summary>
    public static SurvivalCurve CreateSurvivalCurve(
      Dt asOf, Dt settle, DiscountCurve discountCurve, double recoveryRate,
      double sBump = 0.0, double sBumpm = 0.0, double rBump = 0.0, double irBump = 0.0)
    {
      var tenors = new[] { "1Y", "2Y", "3Y", "5Y", "7Y", "10Y" };
      var spreads = new[] { 6.0, 7.0, 10.0, 12.0, 15.0, 20.0 };
      if (discountCurve == null)
        discountCurve = CreateIRCurve(asOf, irBump);
      var recoveryCurve = new RecoveryCurve(asOf, recoveryRate) { Spread = rBump };
      var calibrator = new SurvivalFitCalibrator(asOf, settle, recoveryCurve, discountCurve) { ForceFit = true, NegSPTreatment = NegSPTreatment.Allow };
      var curve = new SurvivalCurve(calibrator) { Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Const), Ccy = Currency.USD, Category = "None" };
      var rand = new Random(-1234);
      for (var i = 0; i < tenors.Length; i++)
      {
        var s = spreads[i] + rand.NextDouble() * 0.4;
        curve.AddCDS(tenors[i], Dt.CDSMaturity(asOf, tenors[i]), 0.0, (s + sBump) * (1 + sBumpm) / 10000.0,
          DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      }
      curve.Fit();
      return curve;
    }

    /// <summary>
    /// Create array of survival curves
    /// </summary>
    public static SurvivalCurve[] CreateSurvialCurvesArray(Dt asOf, Dt settle,
      double[] sBumps = null, double[] rBumps = null, double irBump = 0.0)
    {
      var dc = CreateIRCurve(asOf, irBump);
      const int len = 125;
      var sc = new SurvivalCurve[len];
      var recoveryRates = CreateRecoveryRates(len);
      var rand = new Random(-1234);
      for (var i = 0; i < len; i++)
      {
        recoveryRates[i] = (rand.Next(2, 6)) / 10.0;
        var sBump = (sBumps != null && i < sBumps.Length) ? sBumps[i] : 0.0;
        var rBump = (rBumps != null && i < rBumps.Length) ? rBumps[i] : 0.0;
        sc[i] = CreateSurvivalCurve(asOf, settle, dc, recoveryRates[i], sBump, 0.0, rBump, irBump);
      }
      return sc;
    }


    // Create recovery rates
    private static double[] CreateRecoveryRates(int num)
    {
      var rand = new Random(-1234);
      var recoveries = new double[num];
      for (var i = 0; i < num; i++)
        recoveries[i] = (rand.Next(2, 6)) / 10.0;
      return recoveries;
    }

  }
}
