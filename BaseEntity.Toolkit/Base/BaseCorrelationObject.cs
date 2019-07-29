/*
 * BaseCorrelationObject.cs
 *
 * Base class for all the base correlation objects
 *
 *  . All rights reserved.
 *
 */
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections;
using BaseEntity.Toolkit.Calibrators.BaseCorrelation;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Shared;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;
namespace BaseEntity.Toolkit.Base
{
  ///
	/// <summary>
	///   The base clasee for all the base correlation objects
	/// </summary>
	///
	/// <remarks>
	///   This abstract class defines the basic functionality all the base correlation
  ///   objects should implement.
	/// </remarks>
	///
  [Serializable]
	public abstract partial class BaseCorrelationObject : CorrelationObject
  {
		#region Constructors

		/// <summary>
		///   Default constructor
		/// </summary>
		///
		/// <returns>Created base correlation object</returns>
		///
		protected
		BaseCorrelationObject()
		{
      EntityNames = null;
		}

    /// <summary>
    ///   Clone
    /// </summary>
    /// <returns>Cloned object</returns>
    public override object Clone()
    {
      BaseCorrelationObject obj = (BaseCorrelationObject)base.Clone();
      obj.EntityNames = CloneUtil.Clone(EntityNames);
      obj.Calibrator = CloneUtil.Clone(Calibrator);
      return obj;
    }

    /// <summary>
    ///   Create a copy of the base correlation object with the selected entity names
    /// </summary>
    /// <param name="entityNames">Selected names</param>
    /// <returns>A copy of the orginal correlation</returns>
    /// <exclude />
    public virtual BaseCorrelationObject Create(string[] entityNames)
    {
      BaseCorrelationObject obj = (BaseCorrelationObject)ShallowCopy();
      obj.EntityNames = entityNames;
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
    /// <returns>Detachment Correlation</returns>
    public abstract double
		GetCorrelation(
      double dp,
      Dt asOf,
      Dt settle,
      Dt maturity,
      SurvivalCurve [] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double [] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond,
      double toleranceF,
      double toleranceX
      );

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
    public abstract double
		TrancheCorrelation(
      SyntheticCDO cdo,
      Dt asOf,
      Dt settle,
      SurvivalCurve [] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double [] principals,
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
      );

    /// <summary>
    ///   Interpolate base correlation at detachment point
    /// </summary>
    /// <param name="cdo">Tranche</param>
    /// <param name="basketPricer">Basket</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    /// <returns>Detachment Correlation</returns>
    public abstract double
    GetCorrelation(
      SyntheticCDO cdo,
      BasketPricer basketPricer,
      DiscountCurve discountCurve,
      double toleranceF,
      double toleranceX
      );

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
    public abstract CorrelationObject GetCorrelations(
      SyntheticCDO cdo,
      string[] names,
      Dt[] dates,
      BasketPricer basket,
      DiscountCurve discountCurve,
      double toleranceF,
      double toleranceX
      );

		/// <summary>
		///   Imply tranche correlation from base correlation
		/// </summary>
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
		/// <param name="copula">The Copula object</param>
		/// <param name="gridSize">The grid used to update probabilities (default to reasonable guess if zero)</param>
 		/// <param name="integrationPointsFirst">Integration points used in numerical integration
 		///   for the primary factor</param>
 		/// <param name="integrationPointsSecond">Integration points used in numerical integration
 		///   for the secondary factor (if applicable)</param>
		/// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
		/// <param name="toleranceX">The accuracy of implied correlations</param>
		///
		public double
		TrancheCorrelation(
      SyntheticCDO cdo,
      Dt asOf,
      Dt settle,
      SurvivalCurve [] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double [] principals,
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
			return TrancheCorrelation(cdo, asOf, settle, survivalCurves, recoveryCurves, discountCurve,
																principals, stepSize, stepUnit, 0.0, 0.0, copula, gridSize,
																integrationPointsFirst, integrationPointsSecond,
																toleranceF, toleranceX);
		}

    /// <summary>
    ///   Find entity names from an array of base correlations
    /// </summary>
    /// <param name="baseCorrelations">Array of base correlations</param>
    /// <returns>Entity names</returns>
    /// <exclude />
    public static string[] FindEntityNames(BaseCorrelationObject[] baseCorrelations)
    {
      // Find the entity names.
      // Assuming all base correlation have the same names,
      // we return thr first non-empty names list.
      foreach (BaseCorrelation bc in baseCorrelations)
      {
        if (bc.EntityNames != null)
          return bc.EntityNames;
      }

      // not found
      return null;
    }

    /// <summary>
    ///   Get embeded term structure dates
    /// </summary>
    /// <returns>Dates or null if not applicable</returns>
    internal virtual Dt[] GetTermStructDates()
    {
      return null;
    }
    #endregion // Methods
      
    #region BaseCorrelationBump

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
    /// <param name="bumpSizes">
    ///   Array of the bump sizes applied to the correlations on the selected detachment
    ///   points.  If the array is null or empty, no bump is performed.  Else if it
    ///   contains only a single number, the number is applied to all detachment points.
    ///   Otherwise, the array is required to have the same length as the array of
    ///   detachments.
    /// </param>
    /// <param name="relative">
    ///   Boolean value indicating if a relative bump is required.
    /// </param>
    /// <param name="lowerBound">
    ///   The lower bound of the bumped correlations.  If any bumped value is below
    ///   the bound, it is adjust up to the bound.  Normally this should be 0.
    /// </param>
    /// <param name="upperBound">
    ///   The upper bound of the bumped correlations.  If any bumped value is above
    ///   this, the value is adjust down to the bound.  Normally this should be 1.
    /// </param>
    /// 
    /// <returns>
    ///   The average of the absolute changes in correlations, which may be different
    ///   than the bump size requested due to the restrictions on lower bound and upper
    ///   bound.
    /// </returns>
    /// 
    public double BumpCorrelations(
      string[] selectComponents,
      Dt[] selectTenorDates,
      double[] selectDetachments,
      double[] bumpSizes,
      bool relative,
      double lowerBound, double upperBound)
    {
      return BumpCorrelations(
        selectComponents, selectTenorDates, selectDetachments,
        BumpSize.CreatArray(bumpSizes, BumpUnit.None,lowerBound,upperBound),
        null, relative, false, null);
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
    public abstract double BumpCorrelations(
      string[] selectComponents,
      Dt[] selectTenorDates,
      double[] selectDetachments,
      BumpSize[] trancheBumps,
      BumpSize indexBump,
      bool relative, bool onquotes,
      ArrayList hedgeInfo);

    /// <summary>
    ///   Bump base correlations selected by detachments, tenor dates and components.
    /// </summary>
    /// <param name="selectComponents">
    ///   Array of names of the selected components to bump.  This parameter applies
    ///   to mixed base correlation objects and it is ignored for non-mixed single
    ///   object.  A null value means bump all components.
    /// </param>
    /// <param name="bumps">
    ///   Array of BaseCorrelationBump objects.
    ///   It must either match the length of selectComponents,
    ///   or be of length 1 in which case the same object applies to all components.
    /// </param>
    /// <param name="hedgeInfo">
    ///   A object to receive Hedge deltss
    /// </param>
    /// <returns>Average bump size</returns>
    public double BumpCorrelations(
      string[] selectComponents,
      BaseCorrelationBump[] bumps,
      ArrayList hedgeInfo)
    {
      BaseCorrelationTermStruct[] bcts =
        FindComponentsByNames(this, selectComponents);
      return BaseCorrelationBump.Bump(bcts, bumps, hedgeInfo);
    }

    /// <summary>
    ///   Get an array of component names inside the <paramref name="correlation"/>.
    /// </summary>
    /// <param name="correlation">Base correlation to examine</param>
    /// <returns>
    ///   An array of names if the object is a <see cref="BaseCorrelationMixed"/> and contains
    ///  any component; null oetherwise.
    /// </returns>
    public static string[] FindComponentNames(
      BaseCorrelationObject correlation)
    {
      UniqueSequence<string> componentNames = new UniqueSequence<string>();
      
      if (correlation is BaseCorrelationMixed)
      {
        BaseCorrelationObject[] coms =
          ((BaseCorrelationMixed)correlation).BaseCorrelations;
        if (coms == null)
          return null;
        //string[] names = new string[coms.Length];
        for (int i = 0; i < coms.Length; ++i)
        {
          //names[i] = coms[i].Name;
          componentNames.Add(FindComponentNames(coms[i]));
        }
        return componentNames.ToArray();
      }
      if (correlation is BaseCorrelationTermStruct || correlation is BaseCorrelation)
      {
        string name = correlation.Name;
        return name == null ? null : new string[] { name };
      }
      return null;
    }

    /// <summary>
    ///   Find all the components in the base correlation object
    ///   and return them as an array of term structure objects
    /// </summary>
    /// <param name="correlation">The base correlation object</param>
    /// <param name="componentNames">A list of component names,
    /// or null (meaning to extract all components) </param>
    /// <returns>Components as a array of term structures</returns>
    public static BaseCorrelationTermStruct[] FindComponentsByNames(
      BaseCorrelationObject correlation, string[] componentNames)
    {
      List<BaseCorrelationTermStruct> list =
        new List<BaseCorrelationTermStruct>();
      FindComponents(correlation, list);
      if (componentNames == null || componentNames.Length == 0)
        return list.ToArray();
      Dictionary<string, BaseCorrelationTermStruct> dict =
        new Dictionary<string, BaseCorrelationTermStruct>();
      foreach (BaseCorrelationTermStruct b in list)
        if (!dict.ContainsKey(b.Name)) dict.Add(b.Name, b);
      BaseCorrelationTermStruct[] result =
        new BaseCorrelationTermStruct[componentNames.Length];
      for (int i = 0; i < componentNames.Length; ++i)
        result[i] = dict[componentNames[i]];
      return result;
    }

    /// <summary>
    ///   Helper functions find all components recursively
    /// </summary>
    /// <param name="correlation"></param>
    /// <param name="list"></param>
    private static void FindComponents(
      BaseCorrelationObject correlation, List<BaseCorrelationTermStruct> list)
    {
      if (correlation is BaseCorrelationMixed)
      {
        BaseCorrelationObject[] coms =
          ((BaseCorrelationMixed)correlation).BaseCorrelations;
        if (coms == null)
          return;
        foreach (BaseCorrelationObject bco in coms)
          FindComponents(bco, list);
        return;
      }
      if (correlation is BaseCorrelationTermStruct)
      {
        list.Add((BaseCorrelationTermStruct)correlation);
        return;
      }
      if (correlation is BaseCorrelation)
      {
        BaseCorrelationTermStruct bct = new BaseCorrelationTermStruct(
          new Dt[] { Dt.Empty }, new BaseCorrelation[] { (BaseCorrelation)correlation });
        bct.Name = correlation.Name;
        list.Add(bct);
        return;
      }
      throw new NotImplementedException(String.Format(
        "Unknown correlation type {0}", correlation.GetType().FullName));
    }

    /// <summary>
    ///   Get an array of tenor dates inside the <paramref name="correlation"/>
    /// </summary>
    /// <param name="correlation">Base correlation to examine.
    /// </param>
    /// <returns>
    ///   A sorted array of tenor dates if the object contains any
    ///   <see cref="BaseCorrelationTermStruct"/>; null otherwise.
    /// </returns>
    public static Dt[] FindTenorDates(
      BaseCorrelationObject correlation)
    {
      UniqueSequence<Dt> list = new UniqueSequence<Dt>();
      FindTenorDates(correlation, list);
      if(list.Count==0)
        return null;
      return list.ToArray();
    }

    /// <summary>
    ///   Build a mapping from tenor dates to tenor names.
    /// </summary>
    /// <remarks>
    ///   The map is built using the infomation from BaseCorreltionTermStruct.TenorNames
    ///   property.  If no tenor name exists for a date, the entry for the date
    ///   will not be in the map.
    /// </remarks>
    /// <param name="correlation">Base correlation object to examine.
    /// </param>
    /// <returns>
    ///   A mapping from dates to tenor names
    /// </returns>
    internal static Dictionary<Dt,string> BuildTenorMap(
      BaseCorrelationObject correlation)
    {
      Dictionary<Dt, string> dict = new Dictionary<Dt, string>();
      BuildTenorMap(correlation, dict);
      return dict;
    }

    /// <summary>
    ///   Get an array of the detachment points inside the <paramref name="correlation"/>.
    /// </summary>
    /// <param name="correlation">Base correlation to examine.
    /// </param>
    /// <returns>
    ///   A sorted array of detachment points if the object contains
    ///   any <see cref="BaseCorrelationTermStruct"/>;
    ///   null oetherwise.
    /// </returns>
    public static double[] FindDetachments(
      BaseCorrelationObject correlation)
    {
      UniqueSequence<double> list = new UniqueSequence<double>();
      FindDetachments(correlation, list);
      if (list.Count == 0)
        return null;
      return list.ToArray();
    }

    /// <summary>
    ///   Find all the tenor dates and put them in a <paramref name="list"/>
    /// </summary>
    /// <param name="correlation">Correlation to examine</param>
    /// <param name="list">List to receive the dates</param>
    private static void FindTenorDates(
      BaseCorrelationObject correlation,
      UniqueSequence<Dt> list
      )
    {
      if (correlation is BaseCorrelationMixed)
      {
        BaseCorrelationObject[] coms =
          ((BaseCorrelationMixed)correlation).BaseCorrelations;
        if (coms == null)
          return;
        foreach (BaseCorrelationObject bco in coms)
          FindTenorDates(bco, list);
      }
      else if (correlation is BaseCorrelationTermStruct)
        list.Add(((BaseCorrelationTermStruct)correlation).Dates);
      return;
    }

    /// <summary>
    ///   Build a mapping from tenor dates to tenor names.
    /// </summary>
    /// <remarks>
    ///   The map is built using the infomation from BaseCorreltionTermStruct.TenorNames
    ///   property.  If no tenor name exists for the date, the entry will not be in the map.
    /// </remarks>
    /// <param name="correlation">Base correlation to examine</param>
    /// <param name="dict">A mapping from dates to tenor names</param>
    public static void BuildTenorMap(
      BaseCorrelationObject correlation,
      Dictionary<Dt,string> dict
      )
    {
      if (correlation is BaseCorrelationMixed)
      {
        BaseCorrelationObject[] coms =
          ((BaseCorrelationMixed)correlation).BaseCorrelations;
        if (coms == null)
          return;
        foreach (BaseCorrelationObject bco in coms)
          BuildTenorMap(bco, dict);
      }
      else if (correlation is BaseCorrelationTermStruct)
      {
        BaseCorrelationTermStruct bct = correlation as BaseCorrelationTermStruct;
        string[] tenors = bct.TenorNames;
        if (tenors != null)
        {
          bct.Validate();
          Dt[] dates = bct.Dates;
          for (int i = 0; i < tenors.Length; ++i)
            if (!dict.ContainsKey(dates[i]))
              dict.Add(dates[i], tenors[i]);
        }
      }
      return;
    }

    /// <summary>
    ///   Find all the detachment points and put them in a list.
    /// </summary>
    /// <param name="correlation">Correlation to examine</param>
    /// <param name="list">List to receive the dates</param>
    private static void FindDetachments(
      BaseCorrelationObject correlation,
      UniqueSequence<double> list
      )
    {
      if (correlation is BaseCorrelationMixed)
      {
        BaseCorrelationObject[] coms =
          ((BaseCorrelationMixed)correlation).BaseCorrelations;
        if (coms == null)
          return;
        foreach (BaseCorrelationObject bco in coms)
          FindDetachments(bco, list);
      }
      else if (correlation is BaseCorrelationTermStruct)
      {
        BaseCorrelationObject[] coms =
          ((BaseCorrelationTermStruct)correlation).BaseCorrelations;
        if (coms == null)
          return;
        foreach (BaseCorrelationObject bco in coms)
          FindDetachments(bco, list);
      }
      else
      {
        BaseCorrelation bc = (BaseCorrelation)correlation;
        list.Add(bc.Detachments);
      }
      return;
    }
    #endregion // BaseCorrelationBump

    #region Walker

    /// <summary>
    ///   Visitor function
    /// </summary>
    /// <param name="bco">Base correlation object to visit</param>
    /// <returns>It returns false if no need to visit any of the sub-objects, i.e.,
    ///   the base correlation objects contained inside <paramref name="bco"/>;
    ///   Otherwise, it returns true.
    /// </returns>
    internal delegate bool VisitFn(BaseCorrelationObject bco);

    /// <summary>
    ///   Object tree walker
    /// </summary>
    /// <param name="visit">Visitor function</param>
    /// <remarks>
    ///   The implementation should first call <c>visit(this)</c>
    ///   and check the return value.  If the return value is true,
    ///   it should proceed to walk through all the sub-objects;
    ///   otherwise, the function should return immediately.
    /// </remarks>
    internal abstract void Walk(VisitFn visit);

    /// <summary>
    /// Sets the minimum and maximum allowable correlations.
    /// </summary>
    internal void SetMinAndMaxCorrelations()
    {
      bool set = false;
      double min = Double.MaxValue, max = Double.MinValue;
      Walk(delegate(BaseCorrelationObject bco)
      {
        if (min > bco.MinCorrelation)
          min = bco.MinCorrelation;
        if (max < bco.MaxCorrelation)
          max = bco.MaxCorrelation;
        set = true;
        return true;
      });
      if (!set)
      {
        min = 0.0; max = 1.0;
      }
      this.MinCorrelation = min;
      this.MaxCorrelation = max;
    }
    #endregion Walker

    #region Properies

    /// <summary>
    ///   Entity names associated with this object
    /// </summary>
    /// <exclude />
    public string[] EntityNames { get; set; }

    /// <summary>
    ///   Indicate if the correlation can be extended to larger than 1.
    ///   For internal use only.
    ///   <preliminary/>
    /// </summary>
    /// <exclude />
    [Browsable(false)]
    public bool Extended
    {
      get { return ModelChoice.ExtendedCorreltion; }
      set { RecoveryCorrelationModel.SetExtendedCorrelation(value); }
    }

    /// <summary>
    ///   Whether the surface is calibrated with recovery correlation
    /// </summary>
    public bool WithRecoveryCorrelation
    {
      get { return ModelChoice.WithCorrelatedRecovery; }
      set { RecoveryCorrelationModel.SetWithCorrelatedRecovery(value); }
    }

    /// <summary>
    ///   Basket model choice.
    ///   For internal use only.
    ///   <preliminary/>
    /// </summary>
    /// <exclude/>
    [Browsable(false)]
    public BasketModelChoice ModelChoice
    {
      get { return RecoveryCorrelationModel.ModelChoice; }
    }

    /// <summary>
    ///   Recovery correlation model.
    ///   For internal use only.
    ///   <preliminary/>
    /// </summary>
    /// <exclude/>
    [Browsable(false)]
    internal RecoveryCorrelationModel RecoveryCorrelationModel { get; set; }
       = RecoveryCorrelationModel.Default;

    /// <summary>
    ///   Base correlation calibrator used to calibrate this object.
    ///   For internal use only.
    ///   <preliminary/>
    /// </summary>
    /// <exclude/>
    public BaseCorrelationCalibrator Calibrator { get; set; }

    /// <summary>
    ///   Calibration Time
    /// </summary>
    public double CalibrationTime { get; internal set; }

    #endregion Properties
  }
}
