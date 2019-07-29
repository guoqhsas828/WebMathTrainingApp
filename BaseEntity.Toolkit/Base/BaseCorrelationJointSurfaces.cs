/*
 * BaseCorrelationCombined.cs
 *
 * A class for mixing base correlation data
 *
 *
 */
using System;
using System.Collections;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Combined base correlation object
  /// </summary>
  /// <remarks>
  ///   <para>This is simply a wrapper of the base correlation combining method prior to release 8.7.</para>
  /// </remarks>
  [Serializable]
  public partial class BaseCorrelationJointSurfaces : BaseCorrelationMixed
  {
    #region Constructors
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="baseCorrelations">Base correlation surfaces to combine</param>
    /// <param name="weights">Weights to combine base correlations</param>
    /// <param name="timeInterp">Interpolation method between dates</param>
    /// <param name="timeExtrap">Extrapolation method for dates</param>
    /// <param name="min">Minimum return value if Smooth extrapolation
    ///   is chosen for strikes ().</param>
    /// <param name="max">Maximum return value if Smooth extrapolation is chosen for strikes</param>
    public BaseCorrelationJointSurfaces(
      BaseCorrelationObject[] baseCorrelations,
      double[] weights,
      InterpMethod timeInterp,
      ExtrapMethod timeExtrap,
      double min,
      double max)
      : base(baseCorrelations, min, max)
    {
      // Sanity check
      if (baseCorrelations.Length != weights.Length)
        throw new ArgumentException("Base correlations and strike should be of the same size");

      // public data
      weights_ = weights;

      // other data member
      timeInterp_ = timeInterp;
      timeExtrap_ = timeExtrap;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    /// <returns>Cloned object</returns>
    public override object Clone()
    {
      BaseCorrelationJointSurfaces obj = (BaseCorrelationJointSurfaces)base.Clone();
      obj.weights_ = CloneUtil.Clone(weights_);
      obj.timeInterp_ = timeInterp_;
      obj.timeExtrap_ = timeExtrap_;
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
    /// <returns>Detachment correlation</returns>
    public override double GetCorrelation(
      double dp, Dt asOf, Dt settle, Dt maturity,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[] principals,
      int stepSize, TimeUnit stepUnit,
      Copula copula, double gridSize,
      int integrationPointsFirst, int integrationPointsSecond,
      double toleranceF, double toleranceX)
    {
      double result = 0;
      double sumWeights = 0;
      for (int i = 0; i < weights_.Length; ++i)
        if (weights_[i] > 1E-14)
        {
          sumWeights += weights_[i];
          double c = BaseCorrelations[i].GetCorrelation(
            dp, asOf, settle, maturity, survivalCurves,
            recoveryCurves, discountCurve, principals, stepSize, stepUnit, copula,
            gridSize, integrationPointsFirst, integrationPointsSecond,
            toleranceF, toleranceX);
          result += weights_[i] * (c - result) / sumWeights;
        }
      return result;
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
    public override double GetCorrelation(
      SyntheticCDO cdo,
      BasketPricer basketPricer,
      DiscountCurve discountCurve,
      double toleranceF, double toleranceX)
    {
      double result = 0;
      double sumWeights = 0;
      for (int i = 0; i < weights_.Length; ++i)
        if (weights_[i] > 1E-14)
        {
          sumWeights += weights_[i];
          double c = BaseCorrelations[i].GetCorrelation(
            cdo, basketPricer, discountCurve, toleranceF, toleranceX);
          result += weights_[i] * (c - result) / sumWeights;
        }
      return result;
    }

    /// <summary>
    ///   Interpolate base correlation at detachment point for an array of dates
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>When the parameter <paramref name="names"/> is null, the name list will
    ///    be taken from the names of the survival curves inside the <paramref name="basket"/>.
    ///   </para>
    /// 
    ///   <para>When the parameter <paramref name="dates"/> is null, the natural dates
    ///    are used.  If the base correlation is a term structure, the natural dates
    ///    are the dates embeded in the term structure.  Otherwise, the natural date is
    ///    simply the maturity date of the <paramref name="cdo"/> product.</para>
    /// 
    ///   <para>This function modifies directly the states of <paramref name="cdo"/> and
    ///    <paramref name="basket"/>, including maturity, correlation object and loss levels.
    ///    If it is desired to preserve the states of cdo and basket, the caller can pass
    ///    cloned copies of them and leave the original ones intact.</para>
    /// </remarks>
    /// 
    /// <param name="cdo">
    ///   Base tranche, modified on output.
    /// </param>
    /// <param name="names">
    ///   Array of underlying names, or null, which means to use the
    ///   credit names in the <paramref name="basket"/>.
    /// </param>
    /// <param name="dates">
    ///   Array of dates to interpolate, or null, which means to use
    ///   the natural dates.
    /// </param>
    /// <param name="basket">
    ///   Basket to interpolate correlation, modified on output.
    /// </param>
    /// <param name="discountCurve">
    ///   Discount curve
    /// </param>
    /// <param name="toleranceF">
    ///   Relative error allowed in PV when calculating implied correlations.
    ///   A value of 0 means to use the default accuracy level.
    /// </param>
    /// <param name="toleranceX">
    ///   Accuracy level of implied correlations.
    ///   A value of 0 means to use the default accuracy level.
    /// </param>
    /// 
    /// <returns>Correlation object</returns>
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
      CorrelationTermStruct cot;
      CorrelationObject[] cos = new CorrelationObject[weights_.Length];
      if (dates != null)
      {
        for (int i = 0; i < weights_.Length; ++i)
          if (weights_[i] > 1E-14)
          {
            cos[i] = BaseCorrelations[i].GetCorrelations(
              cdo, names, dates, basket, discountCurve, toleranceF, toleranceX);
          }
      }
      else
      {
        UniqueSequence<Dt> dts = new UniqueSequence<Dt>();
        for (int i = 0; i < weights_.Length; ++i)
          if (weights_[i] > 1E-14)
          {
            CorrelationObject co = cos[i] = BaseCorrelations[i].GetCorrelations(
              cdo, names, dates, basket, discountCurve, toleranceF, toleranceX);
            CorrelationTermStruct ct = co as CorrelationTermStruct;
            if (ct == null) continue;
            dts.Add(ct.Dates);
          }
        dates = dts.ToArray();
      }

      if (dates.Length <= 1)
      {
        double sumWeights = 0;
        double result = 0;
        for (int i = 0; i < cos.Length; ++i)
          if (cos[i] != null)
          {
            double c = GetCorrelation(cos[i]);
            sumWeights += weights_[i];
            result += weights_[i] * (c - result) / sumWeights;
          }
        cot = new CorrelationTermStruct(names,
          new double[] { result }, new Dt[] { cdo.Maturity },
          MinCorrelation, MaxCorrelation);
      }
      else
      {
        double sumWeights = 0;
        double[] corrs = new double[dates.Length];
        double[] work = new double[dates.Length];
        for (int i = 0; i < cos.Length; ++i)
          if (cos[i] != null)
          {
            GetCorrelationArray(dates, cos[i], work);
            sumWeights += weights_[i];
            for (int t = 0; t < work.Length; ++t)
              corrs[t] += weights_[i] * (work[t] - corrs[t]) / sumWeights;
          }
        cot = new CorrelationTermStruct(names, corrs, dates, MinCorrelation, MaxCorrelation);
      }

      return cot;
    }

    private void GetCorrelationArray(Dt[] dates, CorrelationObject co, double[] data)
    {
      CorrelationTermStruct ct = co as CorrelationTermStruct;
      if (ct != null)
      {
        Curve curve = new Curve(dates[0]);
        curve.Interp = InterpFactory.FromMethod(ct.InterpMethod, ct.ExtrapMethod, MinCorrelation, MaxCorrelation);
        for (int i = 0; i < ct.Dates.Length; ++i)
          curve.Add(ct.Dates[i], ct.Correlations[i]);
        for (int i = 0; i < dates.Length; ++i)
          data[i] = curve.Interpolate(dates[i]);
        return;
      }
      FactorCorrelation fc = co as FactorCorrelation;
      if (fc != null)
      {
        double c = fc.Correlations[0];
        for (int i = 0; i < dates.Length; ++i)
          data[i] = c;
        return;
      }
      GeneralCorrelation gc = co as GeneralCorrelation;
      if (gc != null)
      {
        double c = Math.Sqrt(gc.Correlations[gc.Correlations.Length < 2 ? 0 : 1]);
        for (int i = 0; i < dates.Length; ++i)
          data[i] = c;
        return;
      }
      throw new ToolkitException("Unknown correlation: " + co.Name);
    }

    private static double GetCorrelation(CorrelationObject co)
    {
      CorrelationTermStruct ct = co as CorrelationTermStruct;
      if (ct != null)
        return ct.Correlations[0];
      FactorCorrelation fc = co as FactorCorrelation;
      if (fc != null)
        return fc.Correlations[0];
      GeneralCorrelation gc = co as GeneralCorrelation;
      if (gc != null)
        return Math.Sqrt(gc.Correlations[gc.Correlations.Length < 2 ? 0 : 1]);
      throw new ToolkitException("Unknown correlations: " + co.Name);
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
    /// <returns>Tranche correlation</returns>
    public override double TrancheCorrelation(SyntheticCDO cdo,
      Dt asOf, Dt settle,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[] principals,
      int stepSize, TimeUnit stepUnit,
      double apBump, double dpBump,
      Copula copula, double gridSize,
      int integrationPointsFirst, int integrationPointsSecond,
      double toleranceF, double toleranceX)
    {
      double result = 0;
      double sumWeights = 0;
      for (int i = 0; i < weights_.Length; ++i)
        if (weights_[i] > 1E-14)
        {
          double c = BaseCorrelations[i].TrancheCorrelation(cdo, asOf, settle, survivalCurves,
        recoveryCurves, discountCurve, principals, stepSize, stepUnit, copula,
        gridSize, integrationPointsFirst, integrationPointsSecond, toleranceF, toleranceX);
          sumWeights += weights_[i];
          result += (c - result) / sumWeights;
        }
      return result;
    }

    /// <summary>
    ///   Bump base correlations selected by detachments, tenor dates and components.
    /// </summary>
    /// 
    /// <param name="selectComponents">
    ///   Array of names of the selected components to bump.  This parameter applies
    ///   to mixed base correlation objects and it is ignored for non-mixed single
    ///   object.  A null value means bump all components.
    /// </param>
    /// <param name="selectTenorDates">
    ///   Array of the selected tenor dates to bump.  This parameter applies to base
    ///   correlation term structures and it is ignored for simple base correlation
    ///   without term structure.  A null value means bump all tenors.
    /// </param>
    /// <param name="selectDetachments">
    ///   Array of the selected base tranches to bump.  This should be an array
    ///   of detachments points associated with the strikes.  A null value means
    ///   to bump the correlations at all strikes.
    /// </param>
    /// <param name="trancheBumps">
    ///   Array of the BbumpSize objects applied to the correlations on the selected detachment
    ///   points.  If the array is null or empty, no bump is performed.  Else if it
    ///   contains only a single element, the element is applied to all detachment points.
    ///   Otherwise, the array is required to have the same length as the array of
    ///   detachments.
    /// </param>
    /// <param name="indexBump">Array of bump sizes on index</param>
    /// <param name="relative">
    ///   Boolean value indicating if a relative bump is required.
    /// </param>
    /// <param name="onquotes">
    ///   True if bump market quotes instead of correlation themselves.
    /// </param>
    /// <param name="hedgeInfo">
    ///   Hedge delta info.  Null if no head info is required.
    /// </param>
    /// 
    /// <returns>
    ///   The average of the absolute changes in correlations, which may be different
    ///   than the bump size requested due to the restrictions on lower bound and upper
    ///   bound.
    /// </returns>
    /// 
    public override double BumpCorrelations(
      string[] selectComponents,
      Dt[] selectTenorDates,
      double[] selectDetachments,
      BumpSize[] trancheBumps,
      BumpSize indexBump,
      bool relative, bool onquotes,
      ArrayList hedgeInfo)
    {
      double avgBump = base.BumpCorrelations(
        selectComponents, selectTenorDates, selectDetachments,
        trancheBumps, indexBump, relative, onquotes, hedgeInfo);
      return avgBump;
    }

    /// <summary>
    ///  Bump all the correlations simultaneously
    /// </summary>
    /// 
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlations</returns>
    public override double BumpCorrelations(double bump, bool relative, bool factor)
    {
      double avgBump = base.BumpCorrelations(bump, relative, factor);
      return avgBump;
    }

    /// <summary>
    ///   Bump correlations by index of strike
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Note that bumps are designed to be symetrical (ie
    ///   bumping up then down results with no change for both
    ///   relative and absolute bumps.</para>
    ///
    ///   <para>If bump is relative and +ve, correlation is
    ///   multiplied by (1+<paramref name="bump"/>)</para>
    ///
    ///   <para>else if bump is relative and -ve, correlation
    ///   is divided by (1+<paramref name="bump"/>)</para>
    ///
    ///   <para>else bumps correlation by <paramref name="bump"/></para>
    /// </remarks>
    ///
    /// <param name="i">Index of strike i</param>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlation</returns>
    public override double BumpCorrelations(int i, double bump, bool relative, bool factor)
    {
      double avgBump = base.BumpCorrelations(i, bump, relative, factor);
      return avgBump;
    }

    #endregion // Methods


    #region Properties
    /// <summary>
    ///   Weights
    /// </summary>
    public double[] Weights
    {
      get { return weights_; }
    }
    internal InterpMethod TenorInterp
    {
      get { return timeInterp_; }
    }
    internal ExtrapMethod TenorExtrap
    {
      get { return timeExtrap_; }
    }
    #endregion // Properties

    #region Data
    private double[] weights_;
    private InterpMethod timeInterp_;
    private ExtrapMethod timeExtrap_;
    #endregion // Data
  }
}
