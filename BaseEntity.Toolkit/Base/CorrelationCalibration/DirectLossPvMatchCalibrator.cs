/*
 * DirectLossPvMatchCalibrator.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.BaseCorrelation
{
  /// <summary>
  ///   Calibrate base correlation by protection matching method
  ///   <preliminary/>
  /// </summary>
  /// <exclude />
  [Serializable]
	public class DirectLossPvMatchCalibrator : BaseCorrelationCalibrator
  {
    #region Constructors
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="cdoTerms">Tranche term</param>
    /// <param name="basket">Basket used the calcibrate base correlation</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="tenorDates">Tenor dates for term structure</param>
    /// <param name="detachments">Detachment points</param>
    /// <param name="runningPremiums">Running Premiums</param>
    /// <param name="quotes">Tranche quotes</param>
    /// <param name="toleranceF">Tolerance of protections</param>
    /// <param name="toleranceX">Tolerence of correlation</param>
    public DirectLossPvMatchCalibrator(
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

      calibrtnMethod_ = BaseCorrelationCalibrationMethod.MaturityMatch;

      // Create an array of pricers
      pricers_ = new SyntheticCDOPricer[
        detachments.Length, tenorDates.Length];

      // Create pricers
      double attachment = 0;
      for (int d = 0; d < detachments.Length; ++d)
      {
        // Create correlation object
        CorrelationTermStruct correlation
          = CreateCorrelation(basket, tenorDates);

        double detachment = detachments[d];
        for (int t = 0; t < tenorDates.Length; ++t)
          pricers_[d, t] = CreateCDOPricer(cdoTerms,
            attachment, detachment, tenorDates[t], totalPrincipal,
            basket, discountCurve, correlation);

        attachment = detachment;
      }

      toleranceF_ = toleranceF;
      toleranceX_ = toleranceX;
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

      BaseEntity.Toolkit.Base.BaseCorrelation[] bcs = baseCorrelation.BaseCorrelations;
      if (bcs.Length <= dateIndex
        || bcs[dateIndex].Detachments.Length <= dpIndex)
      {
        return;
      }

      int nTenors = bcs.Length;
      int nDetachments = Detachments.Length;
      for (int t = dateIndex; t < nTenors; ++t)
      {
        BaseCorrelationStrikeMethod strikeMethod = bcs[t].StrikeMethod;
        double[] tcorrs = bcs[t].TrancheCorrelations;
        double[] corrs = bcs[t].Correlations;
        double[] strikes = bcs[t].Strikes;
        int d = dpIndex;
        double c = d > 0 ? corrs[d - 1] : 0.0;
        for (; d < nDetachments; ++d)
        {
          c = Fit(t, d, c, min, max);
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
              strikeMethod, bcs[t].StrikeEvaluator, c);
          }
        }
        pricers_[nDetachments - 1, t].Basket.SetFactor(Math.Sqrt(c));
        if (tcorrs != null)
          tcorrs[0] = corrs[0];
        bcs[t].ScalingFactor = BaseEntity.Toolkit.Base.BaseCorrelation.DetachmentScalingFactor(
          strikeMethod, pricers_[0, t].Basket, pricers_[0, 0].DiscountCurve);
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
        PriceMeasure.FlatPrice, target,
        pricer, toleranceF_, toleranceX_, min, max);
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
      SyntheticCDOPricer pricer = pricers_[d, t];
      pricer.CDO.Premium = premium;
      pricer.CDO.Fee = fee;
      double pv = pricer.FlatPrice();
      double notional = pricer.Notional;
      if (d > 0)
      {
        SyntheticCDOPricer pricer0 = pricers_[d-1, t];
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
    private SyntheticCDOPricer CreateCDOPricer(
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

    #endregion Methods

    #region Data

    private BaseCorrelationCalibrationMethod calibrtnMethod_;
    private SyntheticCDOPricer[,] pricers_;
    private double toleranceF_;
    private double toleranceX_;

    #endregion Data
  }
}
