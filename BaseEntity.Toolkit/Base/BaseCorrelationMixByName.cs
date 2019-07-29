/*
 * BaseCorrelationMixByName.cs
 *
 * A class for mixing base correlation data
 *
 *
 */
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Base correlations mixed by names
  /// </summary>
  [Serializable]
	public class BaseCorrelationMixByName : BaseCorrelationMixed
	{
    #region Constructors
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="baseCorrelations">Array of base correlations</param>
    /// <param name="names">Array of names</param>
    /// <param name="associates">Array associating each name with a base correlation index</param>
    private BaseCorrelationMixByName(
      BaseCorrelationObject[] baseCorrelations,
      string[] names,
      int[] associates
      )
      : base(baseCorrelations)
    {
      this.EntityNames = names;
      associates_ = associates;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    /// <returns>Cloned object</returns>
    public override object Clone()
    {
      BaseCorrelationMixByName obj = (BaseCorrelationMixByName)base.Clone();
      obj.associates_ = CloneUtil.Clone(associates_);
      return obj;
    }

    /// <summary>
    ///   Create a copy of the base correlation object with the selected entity names
    /// </summary>
    /// <param name="entityNames">Selected names</param>
    /// <returns>A copy of the orginal correlation</returns>
    /// <exclude />
    public override BaseCorrelationObject Create(string[] entityNames)
    {
      if (this.EntityNames != null && entityNames != null)
        return CreateSubsetCorrelation(entityNames);
      return base.Create(entityNames);
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
      if (BaseCorrelations.Length != 1)
        throw new System.InvalidOperationException("GetCorrelation() is not defined for BaseCorrelationMixByName object with more than one base correlations");

      return BaseCorrelations[0].GetCorrelation(dp, asOf, settle, maturity,
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
      if (BaseCorrelations.Length != 1)
        throw new System.InvalidOperationException("GetCorrelation() is not defined for BaseCorrelationMixByName object with more than one base correlations");

      return BaseCorrelations[0].GetCorrelation(cdo, basketPricer, discountCurve, toleranceF, toleranceX);
    }

    /// <summary>
    ///   Interpolate base correlation at detachment point for an array of dates.
    /// </summary>
    /// <remarks>
    ///   <para>When the parameter <paramref name="names"/> is null, the name list is
    ///    taken from the names of the survival curves inside the <paramref name="basket"/>.
    ///   </para>
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
    /// <exception cref="ArgumentException">Number of names and the basket size not match.</exception>
    /// <exception cref="NullReferenceException">Either <paramref name="cdo"/>, <paramref name="basket"/> or
    ///   <paramref name="discountCurve"/> are null.</exception>
    /// <returns>A <see cref="CorrelationObject"/> object containing the interpolated correlations.</returns>
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
      if (names == null)
        names = this.EntityNames;
      if (names == null)
        names = basket.EntityNames;
      if (names.Length != basket.Count)
        throw new ArgumentException(String.Format("Names (len={0}) and basket size ({1}) not match",
          names.Length, basket.Count));

      string[] entityNames = this.EntityNames;

      if (dates == null || dates.Length == 0)
      {
        dates = GetTermStructDates();
        if (dates == null || dates.Length == 0)
          dates = new Dt[] { cdo.Maturity };
      }

      int nDates = dates.Length;
      int nNames = names.Length;
      CorrelationTermStruct cot = new CorrelationTermStruct(
        names, new double[nDates * nNames], dates, MinCorrelation, MaxCorrelation);
      basket.Correlation = cot;
      basket.RawLossLevels = new UniqueSequence<double>(0.0, cdo.Detachment);
      for (int i = 0; i < dates.Length; ++i)
      {
        cdo.Maturity = basket.Maturity = dates[i];
        basket.Reset();
        FactorCorrelation corr = GetCorrelations(cdo, names, associates_,
          basket, discountCurve, toleranceF, toleranceX);
        SetFactorsFromDate(cot, i, corr);
      }
      return cot;
    }

    /// <summary>
    ///   Helper function to set factors from a date index
    /// </summary>
    /// <param name="cot">Correlation term structure</param>
    /// <param name="iDate">Start date</param>
    /// <param name="fcorr">Factor correlations</param>
    private void SetFactorsFromDate(
      CorrelationTermStruct cot, int iDate, FactorCorrelation fcorr)
    {
      int nNames = cot.BasketSize;
      double[] data = cot.Correlations;
      double[] fdata = fcorr.Correlations;
      Dt[] dates = cot.Dates;
      for (int i = iDate; i < dates.Length; ++i)
      {
        int baseIdx = i * nNames;
        for (int j = 0; j < nNames; ++j)
          data[baseIdx + j] = fdata[j];
      }
      return;
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

    /// <summary>
    ///   Create a base correlation object from a subset of names
    /// </summary>
    /// <param name="subsetNames">A subset of names</param>
    /// <returns>BaseCorrelationMixByName object</returns>
    public BaseCorrelationMixByName CreateSubsetCorrelation(string[] subsetNames)
    {
      if (subsetNames == null || subsetNames.Length == 0)
        return null;

      // Hash table of full set of correlations
      Dictionary<string,BaseCorrelationObject> ht = collection_;

      // create an array associating names with base correlations
      BaseCorrelationObject[] bcByNames = new BaseCorrelationObject[subsetNames.Length];
      for (int i = 0; i < subsetNames.Length; ++i)
      {
        if (!ht.ContainsKey(subsetNames[i]))
          throw new System.ArgumentException(String.Format(
            "Name '{0}' at index {1} not found in complete entity list", subsetNames[i], i));
        bcByNames[i] = (BaseCorrelationObject)ht[subsetNames[i]];
      }

      BaseCorrelationMixByName bcm = CreateCorrelationByNames(bcByNames, subsetNames);
      bcm.collection_ = collection_;
      return bcm;
    }

    /// <summary>
    ///   Create a base correlation object from a subset of names
    /// </summary>
    /// <param name="additionalNames">A additional set of names</param>
    /// <param name="additionalCorrelationByNames">Correlations by additional names</param>
    /// <returns>BaseCorrelationMixByName object</returns>
    public BaseCorrelationMixByName CreateSupersetCorrelation(
      string[] additionalNames,
      BaseCorrelationObject[] additionalCorrelationByNames
      )
    {
      // Hash table of full set of correlations
      Dictionary<string, BaseCorrelationObject> ht = collection_;

      if (additionalNames != null && additionalNames.Length != 0)
      {
        // Add names and correlations
        for (int i = 0; i < additionalNames.Length; ++i)
          if (additionalNames[i] != null && additionalNames[i].Length > 0)
          {
            if (ht == null)
              ht = new Dictionary<string, BaseCorrelationObject>();

            BaseCorrelationObject bco = additionalCorrelationByNames[i];
            if (bco == null)
              throw new System.ArgumentException(String.Format(
                "Null base correlation in position {0} for name {1}", i, additionalNames[i]));
            ht[additionalNames[i]] = bco;
          }
      }

      if (ht == null)
        return null;

      // create an array associating names with base correlations
      int count = ht.Count;
      string[] names = new string[count];
      BaseCorrelationObject[] bcByNames = new BaseCorrelationObject[count];
      count = 0;
      foreach(object obj in ht.Keys)
      {
        string name = (string)obj;
        names[count] = name;
        bcByNames[count] = (BaseCorrelationObject)ht[name];
        ++count;
      }

      BaseCorrelationMixByName bcm = CreateCorrelationByNames(bcByNames, names);
      bcm.collection_ = ht;
      return bcm;
    }
    
    /// <summary>
    ///   Constructing an object based on name by name correlations
    /// </summary>
    /// <param name="baseCorrelations">Base correlations, one for each name</param>
    /// <param name="names">Array of names</param>
    /// <returns>BaseCorrelationMixByName object</returns>
    public static BaseCorrelationMixByName FromCorrelationByNames(
      BaseCorrelationObject[] baseCorrelations,
      string[] names
      )
    {
      BaseCorrelationMixByName bcm = CreateCorrelationByNames(baseCorrelations, names);
      bcm.collection_ = BuildHashtable(bcm.BaseCorrelations, names, bcm.associates_);
      return bcm;
    }

    /// <summary>
    ///   Create an object based on correlations with disjoint set of entity names
    /// </summary>
    /// <param name="baseCorrelations">Array of base correlations</param>
    /// <returns>BaseCorrelationMixByName object</returns>
    public static BaseCorrelationMixByName FromDisjointCorrelations(
      BaseCorrelationObject[] baseCorrelations
      )
    {
      int nameCount = 0;
      for (int i = 0; i < baseCorrelations.Length; ++i)
        if (baseCorrelations[i] != null)
        {
          string[] names = baseCorrelations[i].EntityNames;
          if (names == null || names.Length == 0)
            throw new System.ArgumentException(String.Format(
              "BaseCorrelation '{0}' at index {1} contains no entity names",
              baseCorrelations[i].Name, i));
          nameCount += names.Length;
        }

      string[] fullSetNames = new string[nameCount];
      int[] associates = new int[nameCount];
      for(int i = 0, idx = 0;  i < baseCorrelations.Length; ++i)
        if (baseCorrelations[i] != null)
        {
          string[] names = baseCorrelations[i].EntityNames;
          for (int j = 0; j < names.Length; ++idx, ++j)
          {
            fullSetNames[idx] = names[j];
            associates[idx] = i;
          }
        }

      BaseCorrelationMixByName bcm =
        new BaseCorrelationMixByName(baseCorrelations, fullSetNames, associates);
      bcm.collection_ = BuildHashtable(baseCorrelations, fullSetNames, associates);

      return bcm;
    }

     /// <summary>
    ///   Constructing an object based on name by name correlations
    /// </summary>
    /// 
    /// <remarks>This function does not set the hash table of full collection.</remarks>
    /// 
    /// <param name="baseCorrelations">Base correlations, one for each name</param>
    /// <param name="names">Array of names</param>
    /// <returns>BaseCorrelationMixByName object</returns>
    internal static BaseCorrelationMixByName CreateCorrelationByNames(
      BaseCorrelationObject[] baseCorrelations,
      string[] names
      )
    {
      int N = names.Length;

      // create associate array and initialized to -1
      int[] associates = new int[N];
      for (int i = 0; i < N; ++i)
        associates[i] = -1;

      // find the number of distinguished base correlations
      int bcCount = 0;
      for (int i = 0; i < N; ++i)
      {
        if (associates[i] >= 0)
          continue; // already found
        BaseCorrelationObject bc = baseCorrelations[i];
        associates[i] = bcCount;
        baseCorrelations[bcCount] = bc;
        for (int j = i + 1; j < N; ++j)
          if (baseCorrelations[j] == bc)
            associates[j] = bcCount;
        ++bcCount;
      }

      // create base correlation array
      BaseCorrelationObject[] bcs = new BaseCorrelationObject[bcCount];
      for (int i = 0; i < bcCount; ++i)
        bcs[i] = baseCorrelations[i];

      return new BaseCorrelationMixByName(bcs, names, associates);
    }
    
    /// <summary>
    ///   Create a hash table of full collection
    /// </summary>
    /// <param name="bcs"></param>
    /// <param name="names"></param>
    /// <param name="associates"></param>
    /// <returns></returns>
    private static Dictionary<string, BaseCorrelationObject> BuildHashtable(
      BaseCorrelationObject[] bcs,
      string[] names,
      int[] associates
      )
    {
      // Check names
      if (names == null || names.Length == 0)
        throw new System.NullReferenceException("No names given in BaseCorrelationMixByName.BuildHashTable()");

      // Check associates
      if (associates == null || associates.Length != names.Length)
        throw new System.NullReferenceException("Associates and names not match in BaseCorrelationMixByName.BuildHashTable()");

      // Base correlations
      if (bcs == null || bcs.Length == 0)
        throw new System.NullReferenceException("No base correlations in BaseCorrelationMixByName.BuildHashTable()");

      // build a hash table and a bool array
      Dictionary<string, BaseCorrelationObject> ht = new Dictionary<string, BaseCorrelationObject>();
      int N = names.Length;
      for (int i = 0; i < N; ++i)
      {
        if (associates[i] >= bcs.Length)
          throw new System.OverflowException("assciate index outside legitimate base correlation range");
        ht[names[i]] = bcs[associates[i]];
      }

      return ht;
    }
    #endregion // Methods

    #region Properies
    /// <summary>
    ///   Associated base correlation index by names
    /// </summary>
    public int[] Associates
    {
      get { return associates_; }
      set { associates_ = value; }
    }

    /// <summary>
    ///   Full collection of names and base correlations
    /// </summary>
    internal Dictionary<string, BaseCorrelationObject> FullCollection
    {
      get { return collection_; }
      set { collection_ = value; }
    }
    #endregion // Properites

    #region Data
    private int[] associates_;

    // Full set of collections.
    // This member is always copied by reference for efficiency.
    private Dictionary<string,BaseCorrelationObject> collection_;
    #endregion // Data
  } // Class BaseCorrelationMixByName
}
