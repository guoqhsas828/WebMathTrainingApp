/*
 * DirectArbitrageFreeCalibrator.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Calibrators.BaseCorrelation
{
  /// <summary>
  ///   Calibrate base correlation term structure by arbitrage free method
  ///   <preliminary/>
  /// </summary>
  /// <exclude />
  [Serializable]
  public class DirectArbitrageFreeCalibrator : BaseCorrelationCalibrator
  {
    #region Constructors

    /// <summary>
    ///   Constructor   
    /// </summary>
    /// <param name="calibrtnMethod">Calibration method</param>
    /// <param name="cdoTerms">Tranche term</param>
    /// <param name="basket">Basket used to calibrate</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="tenorDates">Array of tenor dates</param>
    /// <param name="detachments">Array of detachments</param>
    /// <param name="runningPremiums">Array of running premiums</param>
    /// <param name="quotes">Tranche quotes</param>
    /// <param name="toleranceF">Tolerance of protections</param>
    /// <param name="toleranceX">Tolerence of correlation</param>
    /// <param name="bottomUp">True for bootom-up calibration, false for top-down</param>
    public DirectArbitrageFreeCalibrator(
      BaseCorrelationCalibrationMethod calibrtnMethod,
      SyntheticCDO cdoTerms,
      BasketPricer basket,
      DiscountCurve discountCurve,
      Dt[] tenorDates,
      double[] detachments,
      double[,] runningPremiums,
      double[,] quotes,
      double toleranceF,
      double toleranceX,
      bool bottomUp)
      : base(cdoTerms, basket, discountCurve, tenorDates,
          detachments, runningPremiums, quotes)
    {
      double totalPrincipal = 1.0E10; // 10 billions

      calibrtnMethod_ = calibrtnMethod;

      // Create an array of pricers
      pricers_ = new SyntheticCDOPricer[detachments.Length, tenorDates.Length];
      // Create pricers
      for (int d = 0; d < detachments.Length; ++d)
      {
        // Create correlation object
        CorrelationTermStruct correlation
          = CreateCorrelation(basket, tenorDates);

        for (int t = 0; t < tenorDates.Length; ++t)
          if (TrancheQuotes[t] != null)
            pricers_[d, t] = CreateCDOPricer(cdoTerms,
              0, detachments[d], tenorDates[t], totalPrincipal,
              basket, discountCurve, correlation);
      }

      toleranceF_= toleranceF;
      toleranceX_ = toleranceX;
      bottomUp_ = bottomUp;
      return;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Fit base correlations from a specific tenor and detachment
    /// </summary>
    /// <param name="baseCorrelation">Base correlation term structure to fit</param>
    /// <param name="dateIndex">Index of the start tenor</param>
    /// <param name="dpIndex">Index of the start detachment</param>
    /// <param name="min">Minimum value of correlation</param>
    /// <param name="max">Maximum value of correlation</param>
    public override void FitFrom(
      BaseCorrelationTermStruct baseCorrelation,
      int dateIndex, int dpIndex, double min, double max)
    {
      if (calibrtnMethod_ != baseCorrelation.CalibrationMethod)
        throw new System.ArgumentException(
          "Base correlation and calibrator not match");

      int nTenors = TenorDates.Length;
      int nDetachments = Detachments.Length;

      if (dateIndex >= nTenors || dpIndex >= nDetachments)
        return;

      // Never assume that the surface and calibrator have the same tenors,
      // because some tenors in the calibrator may not appear in the surface
      // due to the lack of tranche quotes (they may have index quotes).
      BaseEntity.Toolkit.Base.BaseCorrelation[] bcs = baseCorrelation.BaseCorrelations;
      Dt[] dates = baseCorrelation.Dates;
      int bcStart = 0;
      {
        Dt startDt = this.TenorDates[dateIndex];
        for (int i = 0; i < dates.Length; ++i)
          if (dates[i] >= startDt)
          {
            bcStart = i; break;
          }
      }

      UpdateSurvivalCurves();

      Timer timer = new Timer();

      if (bottomUp_)
      {
        for (int t = dateIndex, idx = bcStart; t < nTenors; ++t)
          if (TrancheQuotes[t] != null)
          {
            CalibrateBottomUp(bcs[idx], t, dpIndex, nDetachments, min, max, timer);
            dates[idx] = TenorDates[t];

            //- increase the idx
            if (++idx >= bcs.Length) break;
          }
        return;
      }

      // Now top-down calibration
      if (Detachments[nDetachments - 1] != 1.0)
        throw new InvalidOperationException("Top down calibration requires 100% detachment at top");

      for (int t = dateIndex, idx = bcStart; t < nTenors; ++t)
        if (TrancheQuotes[t] != null)
        {
          CalibrateTopDown(bcs[idx], t, dpIndex, nDetachments, min, max, timer);
          dates[idx] = TenorDates[t];

          //- increase the idx
          if (++idx >= bcs.Length) break;
        }

      return;
    }

    #region Bottom Up Calibration

    private void CalibrateBottomUp(BaseEntity.Toolkit.Base.BaseCorrelation bc, int t, 
      int dpIndex, int nDetachments, double min, double max,
      Timer timer)
    {
      BaseCorrelationStrikeMethod strikeMethod = bc.StrikeMethod;
      double[] tcorrs = bc.TrancheCorrelations;
      double[] corrs = bc.Correlations;
      double[] strikes = bc.Strikes;
      timer.start();
      int d = dpIndex;
      double c = d > 0 ? corrs[d - 1] : 0.0;
      try
      {
        for (; d < nDetachments; ++d)
        {
          c = FitBottomUp(t, d, c, min, max);
          if (c < 0)
          {
            // This hapens when the tranche exhausted.
            c = 0;
            corrs[d] = strikes[d] = Double.NaN;
          }
          else
          {
            corrs[d] = c;
            strikes[d] = BaseEntity.Toolkit.Base.BaseCorrelation.Strike(pricers_[d, t],
              strikeMethod, bc.StrikeEvaluator, c);
          }
        }
      }
      catch (SolverException e)
      {
        for (int i = d; i < nDetachments; ++i)
          corrs[i] = strikes[i] = Double.NaN;
        c = Double.NaN;
        bc.CalibrationFailed = true;
        bc.ErrorMessage = e.Message;
      }
      pricers_[nDetachments - 1, t].Basket.SetFactor(Math.Sqrt(c));
      if (tcorrs != null)
        tcorrs[0] = corrs[0];
      bc.ScalingFactor = BaseEntity.Toolkit.Base.BaseCorrelation.DetachmentScalingFactor(
        strikeMethod, pricers_[0, t].Basket, pricers_[0, 0].DiscountCurve);
      timer.stop();
      bc.CalibrationTime = timer.getElapsed();
      return;
    }

    /// <summary>
    ///    Fit correlation for a specific tenor and detachment
    /// </summary>
    /// <param name="t">Tenor index</param>
    /// <param name="d">Detachment index</param>
    /// <param name="ac">Correlation for the previous tranche</param>
    /// <param name="min">Minimum value of correlation</param>
    /// <param name="max">Maximum value of correlation</param>
    /// <returns>Correlation fit</returns>
    private double FitBottomUp(int t, int d, double ac, double min, double max)
    {
      double premium = 0, fee = 0;
      if (!GetTranchePremiumAndFee(t, d, ref premium, ref fee))
        return Double.NaN;
      double target = 0;
      if (d > 0)
      {
        target = evaluate(pricers_[d - 1, t],
          Math.Sqrt(ac), premium, fee);
      }
      SyntheticCDOPricer pricer = pricers_[d, t];
      if (pricer.CurrentDetachment <= 1E-8)
        return -1;  // Tranche exhausted.
      pricer.CDO.Premium = premium;
      pricer.CDO.Fee = fee;
      return CorrelationSolver.Solve(
        delegate(IPricer p) { return ((SyntheticCDOPricer)p).FlatPrice(); },
        target, pricer, toleranceF_, toleranceX_, min, max);
    }
    #endregion Bottom Up Calibration

    #region Top Down Calibration

    private void CalibrateTopDown(BaseEntity.Toolkit.Base.BaseCorrelation bc, int t,
      int dpIndex, int nDetachments, double min, double max,
      Timer timer)
    {
      BaseCorrelationStrikeMethod strikeMethod = bc.StrikeMethod;
      double[] tcorrs = bc.TrancheCorrelations;
      double[] corrs = bc.Correlations;
      double[] strikes = bc.Strikes;
      timer.start();
      double c = 0; int d = 0;
      try
      {
        int lastIdx = nDetachments - 1;
        tcorrs[lastIdx] = corrs[lastIdx] = strikes[lastIdx] = Double.NaN;
        for (d = lastIdx - 1; d >= dpIndex; --d)
        {
          c = corrs[d] = FitTopDown(t, d, c, min, max);
          strikes[d] = BaseEntity.Toolkit.Base.BaseCorrelation.Strike(pricers_[d, t],
            strikeMethod, bc.StrikeEvaluator, c);
        }
      }
      catch (SolverException e)
      {
        for (int i = d; i >= dpIndex; --i)
          corrs[i] = strikes[i] = Double.NaN;
        c = Double.NaN;
        bc.CalibrationFailed = true;
        bc.ErrorMessage = e.Message;
      }
      pricers_[dpIndex, t].Basket.SetFactor(Math.Sqrt(c));
      if (tcorrs != null)
        tcorrs[0] = corrs[0];
      bc.ScalingFactor = BaseEntity.Toolkit.Base.BaseCorrelation.DetachmentScalingFactor(
        strikeMethod, pricers_[0, t].Basket, pricers_[0, 0].DiscountCurve);
      timer.stop();
      bc.CalibrationTime = timer.getElapsed();
      return;
    }

    /// <summary>
    ///    Fit correlation for a specific tenor and detachment
    /// </summary>
    /// <param name="t">Tenor index</param>
    /// <param name="d">Detachment index</param>
    /// <param name="ac">Correlation for the previous tranche</param>
    /// <param name="min">Minimum value of correlation</param>
    /// <param name="max">Maximum value of correlation</param>
    /// <returns>Correlation fit</returns>
    internal double FitTopDown(int t, int d, double ac, double min, double max)
    {
      double premium = 0, fee = 0;
      if (!GetTranchePremiumAndFee(t, d + 1, ref premium, ref fee))
        return Double.NaN;
      double target = evaluate(pricers_[d + 1, t],
        Math.Sqrt(ac), premium, fee);
      SyntheticCDOPricer pricer = pricers_[d, t];
      pricer.CDO.Premium = premium;
      pricer.CDO.Fee = fee;
      return CorrelationSolver.Solve(
        delegate(IPricer p) { return ((SyntheticCDOPricer)p).FlatPrice(); },
        target, pricer, toleranceF_, toleranceX_, min, max);
    }
    #endregion Top Down Calibration

    /// <summary>
    ///   Calculate the pv per unit notional of a tranche at given premium and fee
    /// </summary>
    /// <param name="t">Tenor index</param>
    /// <param name="d">Detachment index</param>
    /// <param name="premium">Premium</param>
    /// <param name="fee">Fee</param>
    /// <returns>Flat pv</returns>
    internal protected override double TranchePriceAt(int t, int d,
      double premium, double fee)
    {
      SyntheticCDOPricer pricer = pricers_[d, t];
      pricer.CDO.Premium = premium;
      pricer.CDO.Fee = fee;
      double pv = pricer.FlatPrice();
      double notional = pricer.Notional;
      if (d > 0)
      {
        SyntheticCDOPricer pricer0 = pricers_[d - 1, t];
        pricer0.CDO.Premium = premium;
        pricer0.CDO.Fee = fee;
        pv -= pricer0.FlatPrice();
        notional -= pricer0.Notional;
      }
      return pv / notional;
    }

    /// <summary>
    ///   Evaluate flat price at given correlation factor, premium and fee
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <param name="factor">Correlation factor</param>
    /// <param name="premium">Premium</param>
    /// <param name="fee">Fee</param>
    /// <returns>Flat price</returns>
    private static double evaluate(
      SyntheticCDOPricer pricer, double factor,
      double premium, double fee)
    {
      pricer.CDO.Premium = premium;
      pricer.CDO.Fee = fee;
      pricer.Basket.SetFactor(factor);
      return pricer.FlatPrice();
    }

    /// <summary>
    ///   Create an empty correlation term structure to fill in
    /// </summary>
    /// <param name="basket">Basket</param>
    /// <param name="dates">Tenor dates</param>
    /// <returns>Correlation term struct</returns>
    private CorrelationTermStruct CreateCorrelation(
      BasketPricer basket, Dt[] dates)
    {
      string[] names = basket.EntityNames;
      if (calibrtnMethod_ == BaseCorrelationCalibrationMethod.TermStructure)
        return new CorrelationTermStruct(names, new double[dates.Length], dates);
      return new CorrelationTermStruct(names, new double[1], new Dt[1] { dates[0] });
    }

    /// <summary>
    ///    Create CDO pricer
    /// </summary>
    /// <param name="cdo">CDO</param>
    /// <param name="attachment">Attachment</param>
    /// <param name="detachment">Detachment</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="totalPrincipal">Total principal</param>
    /// <param name="basket">Basket</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="correlation">Correlation</param>
    /// <returns>CDO Pricer</returns>
    private static SyntheticCDOPricer CreateCDOPricer(
      SyntheticCDO cdo,
      double attachment,
      double detachment,
      Dt maturity,
      double totalPrincipal,
      BasketPricer basket,
      DiscountCurve discountCurve,
      CorrelationTermStruct correlation
      )
    {
      // Setup the CDO
      //   This may be redundant, but it's safe do it here
      cdo = (SyntheticCDO)cdo.Clone();
      cdo.Attachment = attachment;
      cdo.Detachment = detachment;
      cdo.FeeSettle = Dt.Add((cdo.Effective > basket.Settle) ? cdo.Effective : basket.Settle, 1);
      cdo.Maturity = maturity;

      // Create a basket for boostrapping
      BasketBootstrapCorrelationPricer bskt = new
        BasketBootstrapCorrelationPricer(basket, correlation);
      bskt.RawLossLevels = new 
        UniqueSequence<double>(attachment, detachment);
      bskt.Maturity = maturity;
      if (detachment + bskt.MaximumAmortizationLevel() <= 1.0)
        bskt.NoAmortization = true;
      bskt.Reset(bskt.OriginalBasket); // force reset default settlements
      
      // Create pricer
      SyntheticCDOPricer pricer = new SyntheticCDOPricer(cdo, bskt,
        discountCurve, (detachment - attachment) * totalPrincipal, false, null);

      return pricer;
    }

    /// <summary>
    ///  Updates the survival curves.
    /// </summary>
    /// <remarks>
    ///  The purpose of this function is to make sure that the updated
    ///  scaled curves are used after bumping the index quotes.
    /// </remarks>
    private void UpdateSurvivalCurves()
    {
      if (this.IndexTerm == null || this.Basket == null)
        return; // Nothing to do.

      // Find the scaled curves and the original principals.
      SurvivalCurve[] curves = this.IndexTerm.GetScaleSurvivalCurves();
      double[] prins = Basket.OriginalBasket.Participations;
      if (curves.Length != prins.Length)
        throw new ArgumentException("Basket size and index not match");

      // Remove the the defaulted curves and the curves with zero weights.
      curves = ArrayUtil.GenerateIf<SurvivalCurve>(
        curves.Length,
        delegate(int i)
        {
          return prins[i] != 0.0 &&
            curves[i].Defaulted != Defaulted.HasDefaulted;
        },
        delegate(int i) { return curves[i]; });

      // Reset the survival curves.
      for (int i = 0; i < pricers_.GetLength(0); ++i)
      {
        for (int j = 0; j < pricers_.GetLength(1); ++j)
          if (pricers_[i, j] != null)
          {
            if (curves.Length != pricers_[i, j].Basket.SurvivalCurves.Length)
              throw new ArgumentException("Basket size and index not match");
            pricers_[i, j].Basket.SurvivalCurves = curves;
            pricers_[i, j].Basket.Reset();
          }
      }
      return;
    }
    #endregion Methods

    #region Data

    private BaseCorrelationCalibrationMethod calibrtnMethod_;
    private SyntheticCDOPricer[,] pricers_;
    private double toleranceF_;
    private double toleranceX_;
    private bool bottomUp_;

    #endregion Data
  }

}
