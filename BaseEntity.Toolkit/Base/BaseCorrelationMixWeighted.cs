/*
 * BaseCorrelationMixWeighted.cs
 *
 * A class for mixing base correlation data
 *
 *  . All rights reserved.
 *
 */
using System;
using System.Collections;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Mixed base correlation for Pv averaging pricers
  /// </summary>
  [Serializable]
	public partial class BaseCorrelationMixWeighted : BaseCorrelationMixed
  {
    #region Constructors
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="baseCorrelations">Array of base correlations</param>
    /// <param name="weights">Array of weights</param>
    public BaseCorrelationMixWeighted(
      BaseCorrelationObject[] baseCorrelations,
      double[] weights
      )
      : base(baseCorrelations)
    {
      weights_ = weights;
    }

    /// <summary>
    ///    Clone
    /// </summary>
    /// <returns>Cloned object</returns>
    public override object Clone()
    {
      BaseCorrelationMixWeighted obj = (BaseCorrelationMixWeighted)base.Clone();
      obj.weights_ = CloneUtil.Clone(weights_);
      return obj;
    }
    #endregion // Constructors

    #region Methods
    /// <summary>
    ///   Interpolate base correlation at detachment point
    /// </summary>
    ///
    /// <param name="dp">detachment point</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="copula">The copula object</param>
    /// <param name="gridSize">The grid used to update probabilities (default to reasonable guess if zero)</param>
    /// <param name="integrationPointsFirst">Integration points used in numerical integration
    ///   for the primary factor</param>
    /// <param name="integrationPointsSecond">Integration points used in numerical integration
    ///   for the secondary factor (if applicable)</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    public override double
    GetCorrelation(
      double dp,
      Dt asOf,
      Dt settle,
      Dt maturity,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond,
      double toleranceF,
      double toleranceX
      )
    {
      return base.GetCorrelation(dp, asOf, settle, maturity, weights_,
        survivalCurves, recoveryCurves, discountCurve, principals, stepSize, stepUnit, copula,
        gridSize, integrationPointsFirst, integrationPointsSecond, toleranceF, toleranceX);
    }

    /// <summary>
    ///   Interpolate base correlation at detachment point
    /// </summary>
    /// <param name="cdo">Tranche</param>
    /// <param name="basketPricer">Basket</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    /// <returns>Detachment Correlation</returns>
    public override double
    GetCorrelation(
      SyntheticCDO cdo,
      BasketPricer basketPricer,
      DiscountCurve discountCurve,
      double toleranceF,
      double toleranceX
      )
    {
      return base.GetCorrelation(cdo, weights_, basketPricer, discountCurve, toleranceF, toleranceX);
    }

    /// <summary>
    ///   Interpolate base correlation at detachment point for an array of dates
    /// </summary>
    /// <remarks>
    ///   <para>When the parameter <paramref name="names"/> is null, the name list will
    ///    be taken from the names of the survival curves inside the <paramref name="basket"/>.</para>
    ///   <para>When the parameter <paramref name="dates"/> is null, the natural dates
    ///    are used.  If the base correlation is a term structure, the natural dates
    ///    are the dates embeded in the term structure.  Otherwise, the natural date is
    ///    simply the maturity date of the <paramref name="cdo"/> product.</para>
    ///   <para>This function modifies directly the states of <paramref name="cdo"/> and
    ///    <paramref name="basket"/>, including maturity, correlation object and loss levels.
    ///    If it is desired to preserve the states of cdo and basket, the caller can pass
    ///    cloned copies of them and leave the original ones intact.</para>
    /// </remarks>
    /// <param name="cdo">Base tranche, modified on output.</param>
    /// <param name="names">Array of underlying names, or null, which means to use the
    ///   credit names in the <paramref name="basket"/>.</param>
    /// <param name="dates">Array of dates to interpolate, or null, which means to use
    ///   the natural dates.</param>
    /// <param name="basket">Basket to interpolate correlation, modified on output.</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations.
    ///   A value of 0 means to use the default accuracy level.</param>
    /// <param name="toleranceX">Accuracy level of implied correlations.
    ///   A value of 0 means to use the default accuracy level.</param>
    /// <returns>A <see cref="CorrelationMixed"/> object containing
    ///   all the correlations with non-zero weights.</returns>
    public override CorrelationObject GetCorrelations(
      SyntheticCDO cdo,
      string[] names,
      Dt[] dates,
      BasketPricer basket,
      DiscountCurve discountCurve,
      double toleranceF,
      double toleranceX
      )
    {
      if (weights_ == null || weights_.Length == 0)
        return null;
      if (names == null)
        names = basket.EntityNames;

      CorrelationObject[] correlations = new CorrelationObject[weights_.Length];
      for (int i = 0; i < weights_.Length; ++i)
        if (Math.Abs(weights_[i]) > 1E-15)
        {
          correlations[i] = this.BaseCorrelations[i].GetCorrelations(
            cdo, names, dates, basket, discountCurve, toleranceF, toleranceX);
        }
      return new CorrelationMixed(correlations, weights_, MinCorrelation, MaxCorrelation);
    }

    /// <summary>
    ///   Imply tranche correlation from base correlation
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calculates the implied tranche correlation for a synthetic CDO tranche from a
    ///   base correlation set.</para>
    ///
    ///   <para>Interpolation of the attachment and detachment base correlations are scaled
    ///   by the relative weighted loss of the tranche.</para>
    /// </remarks>
    ///
    /// <param name="cdo">CDO tranche</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years.</param>
    /// <param name="apBump">Bump for base correlation at attachment point</param>
    /// <param name="dpBump">Bump for base correlation at detachment point</param>
    /// <param name="copula">The copula object</param>
    /// <param name="gridSize">The grid used to update probabilities (default to reasonable guess if zero)</param>
    /// <param name="integrationPointsFirst">Integration points used in numerical integration
    ///   for the primary factor</param>
    /// <param name="integrationPointsSecond">Integration points used in numerical integration
    ///   for the secondary factor (if applicable)</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    ///
    public override double TrancheCorrelation(
      SyntheticCDO cdo,
      Dt asOf,
      Dt settle,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[] principals,
      int stepSize,
      TimeUnit stepUnit,
      double apBump,
      double dpBump,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond,
      double toleranceF,
      double toleranceX
      )
    {
      throw new ToolkitException("The method or operation is not implemented.");
    }
    #endregion // Methods

    #region Properites
    /// <summary>
    ///   Weights for base correlations
    /// </summary>
    public double[] Weights
    {
      get { return weights_; }
    }
    #endregion // Properties

    
    #region Data
    private double[] weights_;
    #endregion // Data
  } // Class BaseCorrelationMixWeighted
}
