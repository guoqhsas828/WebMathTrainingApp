/*
 * MaturityMatchCalibrator.cs
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

namespace BaseEntity.Toolkit.Calibrators.BaseCorrelation
{
  /// <summary>
  ///   Calibrate base correlation term structure by maturity match method
  ///   <preliminary/>
  /// </summary>
  /// <remarks>
  ///   This calibrator differs from <see cref="DirectArbitrageFreeCalibrator"/>
  ///   in that it has made futher effort to reuse pvs in previous tenors to do maturity
  ///   matching while <see cref="DirectArbitrageFreeCalibrator"/> only applies
  ///   it to <see cref="BaseCorrelationCalibrationMethod">TermStruct</see>
  ///   method
  /// </remarks>
  /// <exclude />
  [Serializable]
  public class MaturityMatchCalibrator : BaseCorrelationCalibrator
  {
    #region Constructors

    /// <summary>
    ///   Constructor   
    /// </summary>
    /// <param name="cdoTerms">Tranche term</param>
    /// <param name="basket">Basket used to calibrate</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="tenorDates">Array of tenor dates</param>
    /// <param name="detachments">Array of detachments</param>
    /// <param name="runningPremiums">Array of runningPremiums</param>
    /// <param name="quotes">Tranche quotes</param>
    /// <param name="toleranceF">Tolerance of protections</param>
    /// <param name="toleranceX">Tolerence of correlation</param>
    public MaturityMatchCalibrator(
      SyntheticCDO cdoTerms,
      BasketPricer basket,
      DiscountCurve discountCurve,
      Dt[] tenorDates,
      double[] detachments,
      double[,] runningPremiums,
      double[,] quotes,
      double toleranceF,
      double toleranceX)
      : base(cdoTerms, basket, discountCurve, tenorDates,
          detachments, runningPremiums, quotes)
    {
      double totalPrincipal = 1.0E10; // 10 billions

      baseEvaluators_ = new BaseEvaluator[detachments.Length];
      for (int i = 0; i < detachments.Length; ++i)
        baseEvaluators_[i] = new BaseEvaluator(
          cdoTerms, basket, discountCurve,
          detachments[i], totalPrincipal, tenorDates);
      toleranceF_ = toleranceF;
      toleranceX_ = toleranceX;
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
      BaseEntity.Toolkit.Base.BaseCorrelation[] bcs = baseCorrelation.BaseCorrelations;
      if (bcs.Length <= dateIndex
        || bcs[dateIndex].Detachments.Length <= dpIndex)
      {
        return;
      }

      Timer timer = new Timer();
      int nTenors = bcs.Length;
      int nDetachments = baseEvaluators_.Length;
      for (int t = dateIndex; t < nTenors; ++t)
      {
        BaseCorrelationStrikeMethod strikeMethod = bcs[t].StrikeMethod;
        double[] tcorrs = bcs[t].TrancheCorrelations;
        double[] corrs = bcs[t].Correlations;
        double[] strikes = bcs[t].Strikes;
        timer.start();
        int d = 0;
        try
        {
          for (d = dpIndex; d < nDetachments; ++d)
          {
            double c = corrs[d] = 
              Fit(t, d, d > 0 ? corrs[d - 1] : 0.0, min, max);
            strikes[d] = baseEvaluators_[d].CalcStrike(
              c, bcs[t].StrikeEvaluator, strikeMethod);
          }
        }
        catch (SolverException e)
        {
          for (int i = d; i < nDetachments; ++i)
            corrs[i] = strikes[i] = Double.NaN;
          bcs[t].CalibrationFailed = true;
          bcs[t].ErrorMessage = e.Message;
        }
        if (tcorrs != null)
          tcorrs[0] = corrs[0];
        bcs[t].ScalingFactor =
          baseEvaluators_[0].DetachmentScalingFactor(strikeMethod);
        timer.stop();
        bcs[t].CalibrationTime = timer.getElapsed();
      }
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
    internal double Fit(int t, int d, double ac, double min, double max)
    {
      double premium = 0, fee = 0;
      if (!GetTranchePremiumAndFee(t, d, ref premium, ref fee))
        return Double.NaN;
      double target = 0;
      if (d > 0)
        target = baseEvaluators_[d - 1].evaluate(
          Math.Sqrt(ac), t, premium, fee);
      return baseEvaluators_[d].Solve(target,
        t, premium, fee, toleranceF_, toleranceX_, min, max);
    }

    /// <summary>
    ///   Calculate the pv of a detachment point at given premium and fee
    /// </summary>
    /// <param name="t">Tenor index</param>
    /// <param name="d">Detachment index</param>
    /// <param name="premium">Premium</param>
    /// <param name="fee">Fee</param>
    /// <returns>Flat pv</returns>
    internal protected override double TranchePriceAt(int t, int d,
      double premium, double fee)
    {
      throw new System.NotImplementedException(String.Format(
        "TranchePricerAt function is not yet implemented for {0}", this.GetType().Name));
    }

    #endregion Methods

    #region Data

    private BaseEvaluator[] baseEvaluators_;
    private double toleranceF_;
    private double toleranceX_;

    #endregion Data
  }
}
