//
// Copyright (c)    2002-2016. All rights reserved.
//

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Curves
{
  [TestFixture, Smoke]
  public class TestCurveUpfrontQuotes : ToolkitTestBase
  {
    #region Tests
    [Test, Smoke]
    public void CdsCurve()
    {
      SurvivalCurve[] curves = CreateCurves(false);
      CheckInputInvariance(curves[0], fees_, premiums_);
      CheckCurveEquivalence(curves[0], curves[1]);
      CheckCurveBumpEquivalence(curves);
    }

    [Test, Smoke]
    public void LcdsCurve()
    {
      SurvivalCurve[] curves = CreateCurves(true);
      CheckInputInvariance(curves[0], fees_, premiums_);
      CheckCurveEquivalence(curves[0], curves[1]);
      CheckCurveBumpEquivalence(curves);
    }
    #endregion Tests

    #region Helpers
    /// <summary>
    ///   Create two curves:
    ///    the first with upfront fees and actual running premiums;
    ///    the second with zero upfronts and quivalent all-running premiums.
    /// </summary>
    /// <param name="withRefinance">True if fit Lcds curves</param>
    /// <returns>Two curves</returns>
    public SurvivalCurve[] CreateCurves(bool withRefinance)
    {
      Dt asOf = PricingDate != 0 ? new Dt(this.PricingDate) : Dt.Today();
      Currency ccy = this.Currency;
      DayCount cdsDayCount = this.DayCount;
      Frequency cdsFrequency = this.Frequency;
      BDConvention cdsRoll = this.Roll;
      Calendar cdsCalendar = this.Calendar;
      DiscountCurve dc = new DiscountCurve(asOf, 0.03);

      if (withRefinance)
      {
        SurvivalCurve refi = new SurvivalCurve(asOf, 0.01);
        double corr = 0.3;
        SurvivalCurve curve0 = SurvivalCurve.FitLCDSQuotes(
          asOf, ccy, "Original", cdsDayCount, cdsFrequency, cdsRoll, cdsCalendar,
          interpMethod, extrapMethod, nspTreatment, dc,
          tenorNames_, tenorDates_, fees_, premiums_,
          recoveries_, recoveryDisp_, forceFit_, eventDates_, refi, corr);
        double[] allRunnings = GetAllRunningPremiums(curve0);
        SurvivalCurve curve1 = SurvivalCurve.FitLCDSQuotes(
          asOf, ccy, "Equivalent", cdsDayCount, cdsFrequency, cdsRoll, cdsCalendar,
          interpMethod, extrapMethod, nspTreatment, dc,
          tenorNames_, tenorDates_, null, allRunnings,
          recoveries_, recoveryDisp_, forceFit_, eventDates_, refi, corr);
        return new SurvivalCurve[] { curve0, curve1 };
      }
      else
      {
        SurvivalCurve curve0 = SurvivalCurve.FitCDSQuotes(
          asOf, ccy, "Original", cdsDayCount, cdsFrequency, cdsRoll, cdsCalendar,
          interpMethod, extrapMethod, nspTreatment, dc,
          tenorNames_, tenorDates_, fees_, premiums_,
          recoveries_, recoveryDisp_, forceFit_, eventDates_);
        double[] allRunnings = GetAllRunningPremiums(curve0);
        SurvivalCurve curve1 = SurvivalCurve.FitCDSQuotes(
          asOf, ccy, "Equivalent", cdsDayCount, cdsFrequency, cdsRoll, cdsCalendar,
          interpMethod, extrapMethod, nspTreatment, dc,
          tenorNames_, tenorDates_, null, allRunnings,
          recoveries_, recoveryDisp_, forceFit_, eventDates_);
        return new SurvivalCurve[] { curve0, curve1 };
      }
      // done!
    }

    /// <summary>
    ///   Get the equivalent all running premiums
    /// </summary>
    /// <param name="curve">Survival curve</param>
    /// <returns>array of all-running premiums</returns>
    private static double[] GetAllRunningPremiums(SurvivalCurve curve)
    {
      int count = curve.Tenors.Count;
      double[] premiums = new double[count];
      for (int i = 0; i < count; ++i)
        premiums[i] = ((CDS)curve.Tenors[i].Product).Premium * 10000;
      return premiums;
    }

    /// <summary>
    ///   Are curve.OriginalQuotes the same as input data?
    /// </summary>
    /// <param name="curve"></param>
    /// <param name="fees"></param>
    /// <param name="spreads"></param>
    private static void CheckInputInvariance(
      SurvivalCurve curve, double[] fees, double[] spreads)
    {
      int count = curve.Tenors.Count;
      AssertEqual("TenorCount", spreads.Length, count);
      if (count > spreads.Length)
        count = spreads.Length;
      for (int i = 0; i < count; ++i)
      {
        CurveTenor.UpfrontFeeQuote quote
          = (CurveTenor.UpfrontFeeQuote)curve.Tenors[i].OriginalQuote;
        double fee = (fees == null || fees.Length == 0 ? 0.0
          : (fees.Length == 1 ? fees[0] : fees[i]));
        AssertEqual("Fee[" + i + "]", fee, quote.Fee, tol);
        AssertEqual("Premium[" + i + "]", spreads[i]/10000, quote.Premium, tol);
      }
      return;
    }

    /// <summary>
    ///   Are two curves equivalent except for the recorded original quotes?
    /// </summary>
    /// <param name="curve1"></param>
    /// <param name="curve2"></param>
    private static void CheckCurveEquivalence(
      SurvivalCurve curve1, SurvivalCurve curve2)
    {
      // All-running premium should be the same
      int count1 = curve1.Tenors.Count;
      int count2 = curve2.Tenors.Count;
      AssertEqual("Tenor.Count", count1, count2);
      int count = count1 < count2 ? count1 : count2;
      for (int i = 0; i < count; ++i)
      {
        CDS cds1 = (CDS)curve1.Tenors[i].Product;
        CDS cds2 = (CDS)curve2.Tenors[i].Product;
        AssertEqual("cds[" + i + "].Fee", cds1.Fee, cds2.Fee, tol);
        AssertEqual("cds[" + i + "].Premium", cds1.Premium, cds2.Premium, tol);
      }

      // Native curve properites should be the same
      AssertEqual("Curve.Spread", curve1.Spread, curve2.Spread, tol);

      // Curve points should be the same
      count1 = curve1.Count;
      count2 = curve2.Count;
      AssertEqual("Curve.PointsCount", count1, count2);
      count = count1 < count2 ? count1 : count2;
      for (int i = 0; i < count; ++i)
      {
        AssertEqual("Dt[" + i + "]", curve1.GetDt(i).ToInt(), curve2.GetDt(i).ToInt());
        // Survival probabilities can only be accurate up to 10^-6,
        // which is the accuracy level of our calibrator.
        AssertEqual("Sp[" + i + "]", curve1.GetVal(i), curve2.GetVal(i), 1E-6);
      }
      return;
    }

    /// <summary>
    ///   Are two curves have equivalent bump behaviours?
    /// </summary>
    /// <param name="curves"></param>
    private static void CheckCurveBumpEquivalence(SurvivalCurve[] curves)
    {
      // All-running premium should be the same
      int count1 = curves[0].Tenors.Count;
      int count2 = curves[1].Tenors.Count;
      AssertEqual("Tenor.Count", count1, count2);
      int count = count1 < count2 ? count1 : count2;
      for (int i = 0; i < count; ++i)
      {
        SurvivalCurve[] bumpedCurves; double[] bumps, hedges;

        // Up bump
        bumpedCurves = CloneUtil.Clone(curves);
        // Do 10% relative bumps and check the returned absolute bump values
        bumps = CurveUtil.CurveBump(bumpedCurves, i, 0.1, true, true, true, null);
        AssertEqual("upBump[" + i + "]", bumps[0], bumps[1], tol);
        // Check hedge values equivalence
        hedges = CurveUtil.CurveHedge(curves, bumpedCurves, curves[0].Tenors[i].Maturity);
        // Cannot get the match much more accurace than calibrator tolerance
        AssertEqual("upHedge[" + i + "]", hedges[0], hedges[1], 1E-7);

        // Down bump
        bumpedCurves = CloneUtil.Clone(curves);
        // Do 10% relative bumps and check the returned absolute bump values
        bumps = CurveUtil.CurveBump(bumpedCurves, i, 0.1, false, true, true, null);
        AssertEqual("downBump[" + i + "]", bumps[0], bumps[1], tol);
        // Check hedge values equivalence
        hedges = CurveUtil.CurveHedge(curves, bumpedCurves, curves[0].Tenors[i].Maturity);
        // Cannot get the match much more accurace than calibrator tolerance
        AssertEqual("downHedge[" + i + "]", hedges[0], hedges[1], 1E-7);
      }
      return;
    }
    #endregion Helpers

    #region Properties
    public string[] TenorNames
    {
      set { tenorNames_ = value; }
    }
    public Dt[] TenorDates
    {
      set { tenorDates_ = value; }
    }
    public double[] Fees
    {
      set { fees_ = value; }
    }
    public double[] Premiums
    {
      set { premiums_ = value; }
    }
    public double[] Recoveries
    {
      set { recoveries_ = value; }
    }
    public double RecoveryDispersions
    {
      set { recoveryDisp_ = value; }
    }
    public bool ForceFit
    {
      set { forceFit_ = value; }
    }
    public Dt[] EventDates
    {
      set { eventDates_ = value; }
    }
    #endregion Properties

    #region Data
    string[] tenorNames_;
    Dt[] tenorDates_;
    double[] fees_;
    double[] premiums_;
    double[] recoveries_;
    double recoveryDisp_;
    bool forceFit_;
    Dt[] eventDates_;

    InterpMethod interpMethod = InterpMethod.Weighted;
    ExtrapMethod extrapMethod = ExtrapMethod.Const;
    NegSPTreatment nspTreatment = NegSPTreatment.Allow;

    const double tol = 1.0E-9;
    #endregion Data
  }
} 
