/*
 * BaseCorrelationMixed.cs
 *
 * A class for mixing base correlation data
 *
 *
 * $Id $
 *
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections;
using System.Data;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  ///
  /// <summary>
  ///   A class for mixing base correlation data
  /// </summary>
  ///
  /// <remarks>
  ///   This class provides basic data structures and defines basic interface
  ///   for base correlation term structures.  Conceptually, the term structure
  ///   can be considered as a sequence of base correlations estimated at the same
  ///   as-of date but with different horizons, such as 3 years, 5 years, 7 years,
  ///   10 years, etc..  Each of these base correlations has an associated maturity
  ///   date. 
  /// </remarks>
  ///
  [Serializable]
  public abstract partial class BaseCorrelationMixed : BaseCorrelationObject, ICorrelationBump, ICorrelationBumpTermStruct
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BaseCorrelationMixed));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="baseCorrelations">Array of base correlation objects matching dates</param>
    ///
    /// <returns>Created base correlation term structure</returns>
    ///
    public BaseCorrelationMixed(BaseCorrelationObject[] baseCorrelations)
      : this(baseCorrelations, Double.NaN, Double.NaN)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseCorrelationMixed"/> class.
    /// </summary>
    /// <param name="baseCorrelations">The base correlations.</param>
    /// <param name="min">The minimum correlation allowed.</param>
    /// <param name="max">The maximum correlation allowed.</param>
    internal BaseCorrelationMixed(
      BaseCorrelationObject[] baseCorrelations,
      double min, double max)
    {
      if (baseCorrelations == null)
        throw new ArgumentException(String.Format("Null base correlation array"));

      // Initialize data members
      baseCorrelations_ = baseCorrelations;
      if (Double.IsNaN(min) || Double.IsNaN(max))
        SetMinAndMaxCorrelations();
      if (!Double.IsNaN(min))
        MinCorrelation = min;
      if (!Double.IsNaN(max))
        MaxCorrelation = max;
      Extended = (MaxCorrelation > 1);

      return;
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
    ///   Array of the BumpSize objects applied to the correlations on the selected detachment
    ///   points.  If the array is null or empty, no bump is performed.  Else if it
    ///   contains only a single element, the element is applied to all detachment points.
    ///   Otherwise, the array is required to have the same length as the array of
    ///   detachments.
    /// </param>
    /// <param name="indexBump">
    ///   Array of bump sizes applied to index quotes by tenors
    /// </param>
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
      BaseCorrelationObject[] bcs = this.BaseCorrelations;
      if (bcs == null || bcs.Length == 0)
        return 0;
      double avg = 0;
      int j = 0;
      for (int i = 0; i < bcs.Length; ++i)
      {
        double res = bcs[i].BumpCorrelations(selectComponents, selectTenorDates, selectDetachments,
          trancheBumps, indexBump, relative, onquotes, hedgeInfo);
        // If not bumped, do not count avg.
        if (Math.Abs(res) > 1e-8)
        {          
          avg += (res - avg) / (1 + j);
          j++;
        }
      }
      return avg;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    /// <returns>Cloned object</returns>
    public override object Clone()
    {
      BaseCorrelationMixed obj = (BaseCorrelationMixed)base.Clone();
      obj.baseCorrelations_ = CloneUtil.Clone(baseCorrelations_);
      return obj;
    }
    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Interpolate detachment correlation as a factor correlation object
    /// </summary>
    ///
    /// <param name="dp">detachment point</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="names">Credit names</param>
    /// <param name="associates">Name by name correlation association</param>
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
    public FactorCorrelation
    GetCorrelations(
      double dp,
      Dt asOf,
      Dt settle,
      Dt maturity,
      string[] names,
      int[] associates,
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
      double[] data = new double[survivalCurves.Length];
      for (int idx = 0; idx < baseCorrelations_.Length; ++idx)
      {
        double corr = baseCorrelations_[idx].GetCorrelation(
          dp, asOf, settle, maturity,
          survivalCurves, recoveryCurves,
          discountCurve, principals,
          stepSize, stepUnit, copula, gridSize,
          integrationPointsFirst, integrationPointsSecond,
          toleranceF, toleranceX);
        SetFactor(Math.Sqrt(corr), idx, data, associates);
      }
      return new FactorCorrelation(names, 1, data);
    }

    /// <summary>
    ///   Interpolate detachment correlation as a factor correlation object
    /// </summary>
    /// <param name="cdo">Tranche</param>
    /// <param name="names">Credit names</param>
    /// <param name="associates">Name by name correlation association</param>
    /// <param name="basketPricer">Basket</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    /// <returns>Detachment Correlation</returns>
    public FactorCorrelation
    GetCorrelations(
      SyntheticCDO cdo,
      string[] names,
      int[] associates,
      BasketPricer basketPricer,
      DiscountCurve discountCurve,
      double toleranceF,
      double toleranceX)
    {
      double[] data = new double[basketPricer.Count];
      for (int idx = 0; idx < baseCorrelations_.Length; ++idx)
        if (baseCorrelations_[idx] != null)
        {
          double corr = baseCorrelations_[idx].GetCorrelation(
            cdo, basketPricer, discountCurve, toleranceF, toleranceX);
          SetFactor(Math.Sqrt(corr), idx, data, associates);
        }
      return new FactorCorrelation(names, 1, data);
    }

    // Set name by name correlation
    private static void SetFactor(
      double factor, int corrIdx, double[] data, int[] associates)
    {
      for (int i = 0; i < data.Length; ++i)
        if (associates[i] == corrIdx)
          data[i] = factor;
    }


    /// <summary>
    ///   Interpolate detachment correlation as a mixed factor correlation object
    /// </summary>
    /// <param name="cdo">Tranche</param>
    /// <param name="names">Credit names</param>
    /// <param name="weights">Weights for correlations</param>
    /// <param name="basketPricer">Basket</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    /// <returns>Detachment Correlation</returns>
    public CorrelationMixed
    GetCorrelations(
      SyntheticCDO cdo,
      string[] names,
      double[] weights,
      BasketPricer basketPricer,
      DiscountCurve discountCurve,
      double toleranceF,
      double toleranceX)
    {
      Correlation[] correlations = new Correlation[basketPricer.Count];
      for (int idx = 0; idx < baseCorrelations_.Length; ++idx)
        if (baseCorrelations_[idx] != null)
        {
          double corr = baseCorrelations_[idx].GetCorrelation(
            cdo, basketPricer, discountCurve, toleranceF, toleranceX);
          correlations[idx] = new SingleFactorCorrelation(names, Math.Sqrt(corr));
        }
      return new CorrelationMixed(correlations,weights);
    }

    /// <summary>
    ///   Interpolate detachment correlation as weighted average
    /// </summary>
    ///
    /// <param name="dp">detachment point</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="weights">Weights</param>
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
    public double
    GetCorrelation(
      double dp,
      Dt asOf,
      Dt settle,
      Dt maturity,
      double[] weights,
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
      double sumWeight = 0;
      double result = 0;
      for (int idx = 0; idx < baseCorrelations_.Length; ++idx)
      {
        double corr = baseCorrelations_[idx].GetCorrelation(
          dp, asOf, settle, maturity,
          survivalCurves, recoveryCurves,
          discountCurve, principals,
          stepSize, stepUnit, copula, gridSize,
          integrationPointsFirst, integrationPointsSecond,
          toleranceF, toleranceX);
        sumWeight += weights[idx];
        result += weights[idx] * corr;
      }
      if (Math.Abs(sumWeight) > 1E-15)
        result /= sumWeight;
      return result;
    }

    /// <summary>
    ///   Interpolate detachment correlation as weighted average
    /// </summary>
    /// 
    /// <param name="cdo">Tranche</param>
    /// <param name="weights">Weights</param>
    /// <param name="basketPricer">Basket</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    /// <returns>Detachment Correlation</returns>
    public double
    GetCorrelation(
      SyntheticCDO cdo,
      double[] weights,
      BasketPricer basketPricer,
      DiscountCurve discountCurve,
      double toleranceF,
      double toleranceX)
    {
      double sumWeight = 0;
      double result = 0;
      for (int idx = 0; idx < baseCorrelations_.Length; ++idx)
      {
        double corr = baseCorrelations_[idx].GetCorrelation(
          cdo, basketPricer, discountCurve, toleranceF, toleranceX);
        sumWeight += weights[idx];
        result += weights[idx] * corr;
      }
      if (Math.Abs(sumWeight) > 1E-15)
        result /= sumWeight;
      return result;
    }

    ///
    /// <summary>
    ///   Convert correlation to a data table
    /// </summary>
    ///
    /// <returns>Content orgainzed in a data table</returns>
    ///
    public override DataTable Content()
    {
      throw new System.NotImplementedException();
    }

    /// <summary>
    ///   Get embeded term structure dates
    /// </summary>
    /// <returns>Dates or null if not applicable</returns>
    internal override Dt[] GetTermStructDates()
    {
      ArrayList list = null;
      foreach (BaseCorrelationObject bco in baseCorrelations_)
      {
        Dt[] dates = bco.GetTermStructDates();
        if (dates == null || dates.Length ==0)
          continue;
        if (list == null)
          list = new ArrayList();
        foreach (Dt dt in dates)
        {
          int pos = list.BinarySearch(dt);
          if (pos < 0)
            list.Insert(~pos, dt);
        }
      }
      return list != null ? (Dt[])list.ToArray(typeof(Dt)) : null;
    }

    /// <summary>
    ///   Set the correlation data from another
    ///   correlation object of the same type.
    /// </summary>
    /// <param name="source">Source correlation object</param>
    internal override void SetCorrelations(CorrelationObject source)
    {
      if (source == null)
        throw new ArgumentException("The source object can not be null.");

      BaseCorrelationMixed other = source as BaseCorrelationMixed;
      if (other == null)
        throw new ArgumentException("The source object is not a base correlation mixed object.");

      if (this.baseCorrelations_ == null)
        throw new NullReferenceException("The base correlation array is null.");

      if (other.baseCorrelations_ == null
        || other.baseCorrelations_.Length != this.baseCorrelations_.Length)
      {
        throw new ArgumentException("The source correlation array does not match this data.");
      }

      for (int i = 0; i < baseCorrelations_.Length; ++i) {
        if (this.baseCorrelations_[i] != null && other.baseCorrelations_[i] != null)
        {
          this.baseCorrelations_[i].SetCorrelations(other.baseCorrelations_[i]);
        }
      }

      return;
    }
    #endregion // Methods

    #region Walker
    /// <summary>
    ///   Object tree walker
    /// </summary>
    /// <param name="visit">Visitor</param>
    internal override void Walk(VisitFn visit)
    {
      if (visit(this) && baseCorrelations_ != null)
      {
        foreach (BaseCorrelationObject bco in baseCorrelations_)
          bco.Walk(visit);
      }
      return;
    }
    #endregion Walker

    #region Properties

    /// <summary>
    ///   Base correlation objects
    /// </summary>
    public BaseCorrelationObject[] BaseCorrelations
    {
      get { return baseCorrelations_; }
    }

    #endregion Properties

    #region Data
    private BaseCorrelationObject[] baseCorrelations_;
    #endregion Data

    #region ICorrelationBump Members

    ///
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
      double avg = 0.0;
      for (int j = 0; j < baseCorrelations_.Length; j++) {
        if (baseCorrelations_[j] != null) {
          avg += baseCorrelations_[j].BumpCorrelations(i, bump, relative, factor);
        }
      }
      return avg / baseCorrelations_.Length;
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
      double avg = 0.0;
      for (int j = 0; j < baseCorrelations_.Length; j++) {
        if (baseCorrelations_[j] != null) {
          avg += baseCorrelations_[j].BumpCorrelations(bump, relative, factor);
        }
      }

      return avg / baseCorrelations_.Length;
    }

    /// <summary>
    ///   Get name
    /// </summary>
    ///
    /// <param name="i">index</param>
    ///
    /// <returns>name</returns>
    ///
    public string GetName(int i)
    {
      if (i >= 0)
      {
        for (int iName = 0; iName < baseCorrelations_.Length; ++iName)
        {
          ICorrelationBump corr = (ICorrelationBump)baseCorrelations_[iName];
          try
          {
            return corr.GetName(i);
          }
          catch { }
        }
      }
      throw new ArgumentException(String.Format("index {0} is out of range", i));
    }

    /// <summary>
    ///   Number of strikes
    /// </summary>
    [Browsable(false)]
    public int NameCount
    {
      get
      {
        int count = 0;
        for (int i = 0; i < baseCorrelations_.Length; ++i)
          if (baseCorrelations_[i] != null && count < ((ICorrelationBump)baseCorrelations_[i]).NameCount)
            count = ((ICorrelationBump)baseCorrelations_[i]).NameCount;
        return count;
      }
    }
		
    #endregion

    #region ICorrelationBumpTermStruct Members

    ///
    /// <summary>
    ///   Bump correlations by index of strike and tenor
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
    /// <param name="tenor">Index of tenor</param>
    /// <param name="i">Index of strike i</param>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlation</returns>
    public double BumpTenor(int tenor, int i, double bump, bool relative, bool factor)
    {
      double sumOfBumps = 0;
      int numBumped = 0;
      foreach (BaseCorrelationObject bc in baseCorrelations_)
      {
        if (bc is ICorrelationBumpTermStruct)
        {
          int bcTenor = FindTenorIndex(((ICorrelationBumpTermStruct)bc).Dates, Dates[tenor]);
          if (bcTenor >= 0)
          {
            double b = ((ICorrelationBumpTermStruct)bc).BumpTenor(bcTenor, i, bump, relative, factor);
            sumOfBumps += b;
            numBumped++;
          }
          
        }
      }
      //for now do average.
      return sumOfBumps / numBumped;
    }


    /// <summary>
    ///  Bump all the correlations simultaneously for a given tenor on all of the base correlations in the mixed surface.
    /// </summary>
    /// 
    /// <param name="tenor">Index of tenor</param>
    /// <param name="bump">Size to bump (.02 = 2 percent)</param>
    /// <param name="relative">Bump is relative</param>
    /// <param name="factor">Bump factor correlation rather than correlation if applicable</param>
    ///
    /// <returns>The average change in correlations</returns>
    public double BumpTenor(int tenor, double bump, bool relative, bool factor)
    {
      double sumOfBumps = 0;
      int numBumped = 0;
      foreach (BaseCorrelationObject bc in baseCorrelations_)
      {
        if (bc is ICorrelationBumpTermStruct)
        {
          int bcTenor = FindTenorIndex(((ICorrelationBumpTermStruct) bc).Dates, Dates[tenor]);
          if (bcTenor >=0)
          {
            double b = ((ICorrelationBumpTermStruct)bc).BumpTenor(bcTenor, bump, relative, factor);
            sumOfBumps += b;
            numBumped++;
          }
        }
      }
      //for now do average.
      return sumOfBumps/numBumped;
    }

    private static int FindTenorIndex(Dt[] dt, Dt tenor)
    {
      for (int i = 0; i < dt.Length; i++)
      {
        if (dt[i].Equals(tenor))
          return i;
      }
      return -1;
    }

    /// <summary>
    ///   Maturity dates
    /// </summary>
    public Dt[] Dates
    {
      get
      {
        SortedDictionary<Dt, Dt> result = new SortedDictionary<Dt, Dt>();
        foreach (BaseCorrelationObject bc in baseCorrelations_)
        {
          if(bc is ICorrelationBumpTermStruct)
          {
            foreach (Dt date in ((ICorrelationBumpTermStruct)bc).Dates)
            {
              result[date] = date;
            }  
          }    
        }
        Dt[] resultArray = new Dt[result.Keys.Count];
        result.Keys.CopyTo(resultArray, 0);
        return resultArray;
      }
    }

    #endregion
   

  } // class BaseCorrelationMixed
}
