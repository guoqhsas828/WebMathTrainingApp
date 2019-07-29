/*
 * BaseCorrelationTermStruct.cs
 *
 * A class for base correlation data with term structure
 *
 *  . All rights reserved.
 *
 */
#define Include_Old_Constructors

using System;
using System.ComponentModel;
using System.Collections;
using System.Data;
using BaseEntity.Toolkit.Calibrators.BaseCorrelation;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Base
{
  ///
  /// <summary>
  ///   A class for base correlation data with term structure
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
  public partial class BaseCorrelationTermStruct : BaseCorrelationObject, ICorrelationBump, ICorrelationBumpTermStruct
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BaseCorrelationTermStruct));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="dates">Array of dates matching each base correlation object</param>
    /// <param name="baseCorrelations">Array of base correlation objects matching dates</param>
    ///
    /// <returns>Created base correlation term structure</returns>
    ///
    public BaseCorrelationTermStruct(
      Dt[] dates,
      BaseCorrelation[] baseCorrelations
      )
    {
      if (dates == null)
        throw new ArgumentException(String.Format("Null date array"));
      if (baseCorrelations == null)
        throw new ArgumentException(String.Format("Null base correlation array"));
      if (baseCorrelations.Length != dates.Length)
        throw new ArgumentException(String.Format("Length of dates ({0}) and base correlations {2} not match", dates.Length, baseCorrelations.Length));

      // Initialize data members
      dates_ = dates;
      baseCorrelations_ = baseCorrelations;

      // Time interpolation method
      this.Interp = new Linear();
      this.CalibrationMethod = BaseCorrelationCalibrationMethod.MaturityMatch;

      // Entity names
      this.EntityNames = FindEntityNames(baseCorrelations);

      return;
    }


    /// <summary>
    ///   Construct base correlation term structure from Synthetic CDO tranche quotes
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This function uses the basket pricer supplied by the caller.</para>
    /// </remarks>
    ///
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="calibrationMethod">Term structure calibrating method</param>
    /// <param name="cdos">CDO tranches in detachment point order</param>
    /// <param name="basket">Basket pricer</param>
    /// <param name="principals">Principals (face values) associated with individual names</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    /// <param name="strikeInterp">Interpolation method between strikes (default is linear)</param>
    /// <param name="strikeExtrap">Extrapolation method for strikes (default is const)</param>
    /// <param name="tenorInterp">Interpolation method between tenors (default is linear)</param>
    /// <param name="tenorExtrap">Extrapolation method for tenors (default is const)</param>
    /// <param name="min">Minimum return value if Smooth extrapolation is chosen</param>
    /// <param name="max">Maximum return value if Smooth extrapolation is chosen</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelationTermStruct(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      BaseCorrelation.IStrikeEvaluator strikeEvaluator,
      BaseCorrelationCalibrationMethod calibrationMethod,
      SyntheticCDO[][] cdos,
      BasketPricer basket,
      double[][] principals,
      DiscountCurve discountCurve,
      double toleranceF,
      double toleranceX,
      InterpMethod strikeInterp,
      ExtrapMethod strikeExtrap,
      InterpMethod tenorInterp,
      ExtrapMethod tenorExtrap,
      double min, double max
      )
    {
      // Validate
      if (method == BaseCorrelationMethod.ProtectionMatching && calibrationMethod == BaseCorrelationCalibrationMethod.TermStructure)
        throw new System.NotSupportedException("Protection matching not yet supported for boostraping term structure.");
      if (cdos == null || cdos.Length < 1)
        throw new System.ArgumentException("Must specify cdos");
      if (principals != null)
      {
        if (cdos.Length != principals.Length)
          throw new System.ArgumentException(String.Format(
            "cdos (Length={0}) and principals (Length={1}) not match",
            cdos.Length, principals.Length));
        for (int i = 0; i < principals.Length; ++i)
          if (principals[i].Length != basket.Principals.Length)
            throw new System.ArgumentException(String.Format(
              "Number of principals of {0}th tenor ({1}) does not match the basket size ({2})",
              i + 1, principals[0].Length, basket.Principals.Length));
      }

      // Number of tenor dates
      int nSets = cdos.Length;

      // Number of tranches
      int nTranches = cdos[0].Length;
      for (int i = 1; i < nSets; ++i)
        if (cdos[i].Length != nTranches)
          throw new ArgumentException("Number of tranches not match");

      // Allocate arrays
      baseCorrelations_ = new BaseCorrelation[nSets];
      dates_ = new Dt[nSets];

      // Initialize dates
      for (int i = 0; i < nSets; ++i)
        dates_[i] = cdos[i][0].Maturity;

      // Construct correlation term structures
      string[] names = Utils.GetCreditNames(basket.SurvivalCurves);
      CorrelationTermStruct[] baseCorrs = new CorrelationTermStruct[nTranches];
      for (int j = 0; j < nTranches; ++j)
        baseCorrs[j] = CreateCorrelation(calibrationMethod, names, dates_);

      // tranche correlations are needed when using the ProtectionMatching method
      CorrelationTermStruct[] trancheCorrs = null;
      if (method == BaseCorrelationMethod.ProtectionMatching)
      {
        trancheCorrs = new CorrelationTermStruct[nTranches];
        for (int j = 0; j < nTranches; ++j)
          trancheCorrs[j] = CreateCorrelation(calibrationMethod, names, dates_);
      }

      // Construct base correlations for each maturity date
      for (int i = 0; i < nSets; ++i)
      {
        // Construct base pricers
        SyntheticCDOPricer[] basePricers = CreateCDOPricers(
          CreateBaseTranches(cdos[i]), dates_[i], principals == null ? null : principals[i],
          basket, discountCurve, baseCorrs);
        SyntheticCDOPricer[] tranchePricers = null;
        if (method == BaseCorrelationMethod.ProtectionMatching)
          tranchePricers = CreateCDOPricers(
            cdos[i], dates_[i], principals == null ? null : principals[i],
            basket, discountCurve, trancheCorrs);

        BaseCorrelation bc = new BaseCorrelation(
          strikeMethod, strikeEvaluator, basePricers, tranchePricers, toleranceF, toleranceX, names
          );
        bc.MinCorrelation = min;
        bc.MaxCorrelation = max;
        bc.Extended = (max > 1);
        bc.Interp = InterpFactory.FromMethod(strikeInterp, strikeExtrap, min, max);
        baseCorrelations_[i] = bc;
        if (calibrationMethod == BaseCorrelationCalibrationMethod.TermStructure)
          UpdateCorrelations(i, baseCorrs, trancheCorrs, bc);
      }

      // Time interpolation method
      this.Extended = (max > 1);
      this.Interp = InterpFactory.FromMethod(tenorInterp, tenorExtrap, min, max);
      this.CalibrationMethod = calibrationMethod;
      this.EntityNames = names;
      this.MinCorrelation = min;
      this.MaxCorrelation = max;
      return;
    }

    /// <summary>
    ///   Construct base correlation term structure from Synthetic CDO tranche quotes
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Constructs a base correlation object from a sequence of synthetic CDO tranches.</para>
    /// </remarks>
    ///
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="calibrationMethod">Term structure calibrating method</param>
    /// <param name="cdos">CDO tranches in detachment point order</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
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
    /// <param name="strikeInterp">Interpolation method between strikes (default is linear)</param>
    /// <param name="strikeExtrap">Extrapolation method for strikes (default is const)</param>
    /// <param name="tenorInterp">Interpolation method between tenors (default is linear)</param>
    /// <param name="tenorExtrap">Extrapolation method for tenors (default is const)</param>
    /// <param name="min">Minimum return value if Smooth extrapolation is chosen</param>
    /// <param name="max">Maximum return value if Smooth extrapolation is chosen</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelationTermStruct(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      BaseCorrelation.IStrikeEvaluator strikeEvaluator,
      BaseCorrelationCalibrationMethod calibrationMethod,
      SyntheticCDO[][] cdos,
      Dt asOf,
      Dt settle,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[][] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond,
      double toleranceF,
      double toleranceX,
      InterpMethod strikeInterp,
      ExtrapMethod strikeExtrap,
      InterpMethod tenorInterp,
      ExtrapMethod tenorExtrap,
      double min, double max
      )
      : this(method, strikeMethod, strikeEvaluator, calibrationMethod, cdos,
        CreateBasket(asOf, settle, GetMaturity(cdos), survivalCurves, recoveryCurves,
          principals[0], copula, stepSize, stepUnit, gridSize,
          integrationPointsFirst, integrationPointsSecond),
        principals, discountCurve, toleranceF, toleranceX,
        strikeInterp, strikeExtrap, tenorInterp, tenorExtrap, min, max)
    {
    }

    /// <summary>
    ///   Construct base correlation term structure from a sequence of correlations
    /// </summary>
    /// 
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="calibrationMethod">Term structure calibrating method</param>
    /// <param name="cdos">CDO tranches in detachment point order</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="corr">Base correlations at the detachment points</param>
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
    /// <param name="strikeInterp">Interpolation method between strikes (default is linear)</param>
    /// <param name="strikeExtrap">Extrapolation method for strikes (default is const)</param>
    /// <param name="tenorInterp">Interpolation method between tenors (default is linear)</param>
    /// <param name="tenorExtrap">Extrapolation method for tenors (default is const)</param>
    /// <param name="min">Minimum return value if Smooth extrapolation is chosen</param>
    /// <param name="max">Maximum return value if Smooth extrapolation is chosen</param>
    /// 
    /// <returns>Calculated strikes</returns>
    /// 
    /// <exclude />
    public BaseCorrelationTermStruct(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      BaseCorrelation.IStrikeEvaluator strikeEvaluator,
      BaseCorrelationCalibrationMethod calibrationMethod,
      SyntheticCDO[][] cdos,
      Dt asOf,
      Dt settle,
      double[][] corr,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[][] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond,
      InterpMethod strikeInterp,
      ExtrapMethod strikeExtrap,
      InterpMethod tenorInterp,
      ExtrapMethod tenorExtrap,
      double min, double max
      )
    {
      Initialize(method, strikeMethod, strikeEvaluator, calibrationMethod, cdos,
        asOf, settle, corr, survivalCurves, recoveryCurves, discountCurve, principals,
        stepSize, stepUnit, copula, gridSize, integrationPointsFirst, integrationPointsSecond,
        strikeInterp, strikeExtrap, tenorInterp, tenorExtrap, min, max);
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      BaseCorrelationTermStruct obj = (BaseCorrelationTermStruct)base.Clone();
      obj.dates_ = CloneUtil.Clone(dates_);
      obj.baseCorrelations_ = CloneUtil.Clone(baseCorrelations_);
      return obj;
    }

    #region Old_Constructors

#if Include_Old_Constructors
    /// <summary>
    ///   Construct from strikes and base correlations.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Construct a base correlation set directly from strikes and base correlations.
    ///   Strikes may be calculated independently using the Strike function.</para>
    /// </remarks>
    ///
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="interpMethod">Interpolation method between strikes</param>
    /// <param name="extrapMethod">Extrapolation method for strikes</param>
    /// <param name="dates">Array of dates</param>
    /// <param name="strikes">Array of normalized detachment points</param>
    /// <param name="correlations">Array of (base) correlations matching strikes</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelationTermStruct(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      BaseCorrelation.IStrikeEvaluator strikeEvaluator,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      Dt[] dates,
      double[][] strikes,
      double[][] correlations
      )
    {
      if (dates == null)
        throw new ArgumentException(String.Format("Null date array"));
      if (strikes == null)
        throw new ArgumentException(String.Format("Null strike array"));
      if (correlations == null)
        throw new ArgumentException(String.Format("Null correlation array"));
      if (strikes.Length != dates.Length || correlations.Length != dates.Length)
        throw new ArgumentException(String.Format("Length of dates ({0}), strikes {1}, correlations {2} not match", dates.Length, strikes.Length, correlations.Length));

      // dates array
      dates_ = dates;

      // Time interpolation method
      this.Interp = new Linear();

      // Construct base correlation array
      baseCorrelations_ = new BaseCorrelation[dates.Length];
      for (int i = 0; i < dates.Length; ++i)
      {
        BaseCorrelation bc = new BaseCorrelation(method, strikeMethod, strikeEvaluator, strikes[i], correlations[i]);
        bc.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod, 0.0, 1.0);
        baseCorrelations_[i] = bc;
      }
      this.CalibrationMethod = BaseCorrelationCalibrationMethod.MaturityMatch;

      return;
    }


    /// <summary>
    ///   Construct base correlation term structure from Synthetic CDO tranche quotes
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Constructs a base correlation object from a sequence of synthetic CDO tranches.</para>
    /// </remarks>
    ///
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="cdos">CDO tranches in detachment point order</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
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
    /// <param name="interpMethod">Interpolation method between strikes (default is linear)</param>
    /// <param name="extrapMethod">Extrapolation method for strikes (default is const)</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelationTermStruct(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      BaseCorrelation.IStrikeEvaluator strikeEvaluator,
      SyntheticCDO[][] cdos,
      Dt asOf,
      Dt settle,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[][] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond,
      double toleranceF,
      double toleranceX,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod
      )
    {
      // Validate
      if (cdos == null || cdos.Length < 1)
        throw new ArgumentException("Must specify cdos");

      int nSets = cdos.Length;
      if (nSets != principals.Length)
        throw new ArgumentException(String.Format("cdos (Length={0}) and principals (Length={1}) not match", nSets, principals.Length));

      // Allocate arrays
      baseCorrelations_ = new BaseCorrelation[nSets];
      dates_ = new Dt[nSets];

      // Construct base correlations for each maturity date
      string[] entityNames = Utils.GetCreditNames(survivalCurves);
      for (int i = 0; i < nSets; ++i)
      {
        BaseCorrelation bc = new BaseCorrelation(
          method, strikeMethod, strikeEvaluator, cdos[i], asOf, settle,
          survivalCurves, recoveryCurves, discountCurve,
          principals[i], stepSize, stepUnit, copula, gridSize,
          integrationPointsFirst, integrationPointsSecond,
          toleranceF, toleranceX, entityNames);
        bc.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod, 0.0, 1.0);
        baseCorrelations_[i] = bc;
        dates_[i] = cdos[i][0].Maturity;
      }

      // Time interpolation method
      this.Interp = new Linear();
      this.CalibrationMethod = BaseCorrelationCalibrationMethod.MaturityMatch;
      this.EntityNames = entityNames;

      return;
    }


    /// <summary>
    ///   Construct from strikes and base correlations.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Construct a base correlation set directly from strikes and base correlations.
    ///   Strikes may be calculated independently using the Strike function.</para>
    /// </remarks>
    ///
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="interpMethod">Interpolation method between strikes</param>
    /// <param name="extrapMethod">Extrapolation method for strikes</param>
    /// <param name="dates">Array of dates</param>
    /// <param name="strikes">Array of normalized detachment points</param>
    /// <param name="correlations">Array of (base) correlations matching strikes</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelationTermStruct(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod,
      Dt[] dates,
      double[][] strikes,
      double[][] correlations
      )
    {
      if (dates == null)
        throw new ArgumentException(String.Format("Null date array"));
      if (strikes == null)
        throw new ArgumentException(String.Format("Null strike array"));
      if (correlations == null)
        throw new ArgumentException(String.Format("Null correlation array"));
      if (strikes.Length != dates.Length || correlations.Length != dates.Length)
        throw new ArgumentException(String.Format("Length of dates ({0}), strikes {1}, correlations {2} not match", dates.Length, strikes.Length, correlations.Length));

      // dates array
      dates_ = dates;

      // Time interpolation method
      this.Interp = new Linear();

      // Construct base correlation array
      baseCorrelations_ = new BaseCorrelation[dates.Length];
      for (int i = 0; i < dates.Length; ++i)
      {
        BaseCorrelation bc = new BaseCorrelation(method, strikeMethod, null, strikes[i], correlations[i]);
        bc.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod, 0.0, 1.0);
        baseCorrelations_[i] = bc;
      }
      this.CalibrationMethod = BaseCorrelationCalibrationMethod.MaturityMatch;

      return;
    }


    /// <summary>
    ///   Construct base correlation term structure from Synthetic CDO tranche quotes
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Constructs a base correlation object from a sequence of synthetic CDO tranches.</para>
    /// </remarks>
    ///
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="cdos">CDO tranches in detachment point order</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
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
    /// <param name="interpMethod">Interpolation method between strikes (default is linear)</param>
    /// <param name="extrapMethod">Extrapolation method for strikes (default is const)</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelationTermStruct(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      SyntheticCDO[][] cdos,
      Dt asOf,
      Dt settle,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[][] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond,
      double toleranceF,
      double toleranceX,
      InterpMethod interpMethod,
      ExtrapMethod extrapMethod
      )
    {
      // Validate
      if (cdos == null || cdos.Length < 1)
        throw new ArgumentException("Must specify cdos");

      int nSets = cdos.Length;
      if (nSets != principals.Length)
        throw new ArgumentException(String.Format("cdos (Length={0}) and principals (Length={1}) not match", nSets, principals.Length));

      // Allocate arrays
      baseCorrelations_ = new BaseCorrelation[nSets];
      dates_ = new Dt[nSets];

      // Construct base correlations for each maturity date
      string[] entityNames = Utils.GetCreditNames(survivalCurves);
      for (int i = 0; i < nSets; ++i)
      {
        BaseCorrelation bc = new BaseCorrelation(
          method, strikeMethod, null, cdos[i], asOf, settle,
          survivalCurves, recoveryCurves, discountCurve,
          principals[i], stepSize, stepUnit, copula, gridSize,
          integrationPointsFirst, integrationPointsSecond,
          toleranceF, toleranceX, entityNames);
        bc.Interp = InterpFactory.FromMethod(interpMethod, extrapMethod, 0.0, 1.0);
        baseCorrelations_[i] = bc;
        dates_[i] = cdos[i][0].Maturity;
      }

      // Time interpolation method
      this.Interp = new Linear();
      this.CalibrationMethod = BaseCorrelationCalibrationMethod.MaturityMatch;
      this.EntityNames = entityNames;

      return;
    }
#endif // Include_Old_Constructors

    /// <summary>
    ///   Construct base correlation term structure from Synthetic CDO tranche quotes
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Constructs a base correlation object from a sequence of synthetic CDO tranches.</para>
    /// </remarks>
    ///
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="calibrationMethod">Term structure calibrating method</param>
    /// <param name="cdos">CDO tranches in detachment point order</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
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
    /// <param name="strikeInterp">Interpolation method between strikes (default is linear)</param>
    /// <param name="strikeExtrap">Extrapolation method for strikes (default is const)</param>
    /// <param name="tenorInterp">Interpolation method between tenors (default is linear)</param>
    /// <param name="tenorExtrap">Extrapolation method for tenors (default is const)</param>
    /// <param name="min">Minimum return value if Smooth extrapolation is chosen</param>
    /// <param name="max">Maximum return value if Smooth extrapolation is chosen</param>
    ///
    /// <returns>Created base correlation object</returns>
    ///
    public BaseCorrelationTermStruct(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      BaseCorrelationCalibrationMethod calibrationMethod,
      SyntheticCDO[][] cdos,
      Dt asOf,
      Dt settle,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[][] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond,
      double toleranceF,
      double toleranceX,
      InterpMethod strikeInterp,
      ExtrapMethod strikeExtrap,
      InterpMethod tenorInterp,
      ExtrapMethod tenorExtrap,
      double min, double max
      )
      : this(method, strikeMethod, null, calibrationMethod, cdos, asOf, settle,
        survivalCurves, recoveryCurves, discountCurve, principals,
        stepSize, stepUnit, copula, gridSize, integrationPointsFirst, integrationPointsSecond,
        toleranceF, toleranceX, strikeInterp, strikeExtrap, tenorInterp, tenorExtrap, min, max)
    {
    }

    /// <summary>
    ///   Construct base correlation term structure from a sequence of correlations
    /// </summary>
    /// 
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="calibrationMethod">Term structure calibrating method</param>
    /// <param name="cdos">CDO tranches in detachment point order</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="corr">Base correlations at the detachment points</param>
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
    /// <param name="strikeInterp">Interpolation method between strikes (default is linear)</param>
    /// <param name="strikeExtrap">Extrapolation method for strikes (default is const)</param>
    /// <param name="tenorInterp">Interpolation method between tenors (default is linear)</param>
    /// <param name="tenorExtrap">Extrapolation method for tenors (default is const)</param>
    /// <param name="min">Minimum return value if Smooth extrapolation is chosen</param>
    /// <param name="max">Maximum return value if Smooth extrapolation is chosen</param>
    /// 
    /// <returns>Calculated strikes</returns>
    /// 
    /// <exclude />
    public BaseCorrelationTermStruct(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      BaseCorrelationCalibrationMethod calibrationMethod,
      SyntheticCDO[][] cdos,
      Dt asOf,
      Dt settle,
      double[][] corr,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[][] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond,
      InterpMethod strikeInterp,
      ExtrapMethod strikeExtrap,
      InterpMethod tenorInterp,
      ExtrapMethod tenorExtrap,
      double min, double max
      )
    {
      Initialize(method, strikeMethod, null, calibrationMethod, cdos,
        asOf, settle, corr, survivalCurves, recoveryCurves, discountCurve, principals,
        stepSize, stepUnit, copula, gridSize, integrationPointsFirst, integrationPointsSecond,
        strikeInterp, strikeExtrap, tenorInterp, tenorExtrap, min, max);
    }
    #endregion // Old_Constructors

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
      int idx = checkDates(dates_, maturity);
      if (idx >= 0)
      {
        double corr = baseCorrelations_[idx].GetCorrelation(
                    dp, asOf, settle, maturity,
                    survivalCurves, recoveryCurves,
                    discountCurve, principals,
                    stepSize, stepUnit, copula, gridSize,
                    integrationPointsFirst, integrationPointsSecond,
                    toleranceF, toleranceX);
        return corr;
      }

      // We use a curve to do time interpolation
      Curve curve = new Curve(asOf);
      curve.Interp = this.Interp;

      bool empty_curve = true;
      for (int i = 0; i < dates_.Length; ++i)
      {
        Dt date = dates_[i];
        if (asOf < date)
          continue;
        double corr = baseCorrelations_[i].GetCorrelation(
                    dp, asOf, settle, maturity,
                    survivalCurves, recoveryCurves,
                    discountCurve, principals,
                    stepSize, stepUnit, copula, gridSize,
                    integrationPointsFirst, integrationPointsSecond,
                    toleranceF, toleranceX);
        curve.Add(dates_[i], corr);
        empty_curve = false;
      }
      if (empty_curve)
      {
        // if all tenor dates are before the asOf, use the correlation
        // determined by the last tenor
        int lastIdx = baseCorrelations_.Length - 1;
        double corr = baseCorrelations_[lastIdx].GetCorrelation(
          dp, asOf, settle, maturity,
          survivalCurves, recoveryCurves,
          discountCurve, principals,
          stepSize, stepUnit, copula, gridSize,
          integrationPointsFirst, integrationPointsSecond,
          toleranceF, toleranceX);
        curve.Add(asOf, corr);
      }

      return curve.Interpolate(maturity);
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
      double toleranceX)
    {
      Dt savedMaturity = basketPricer.Maturity;
      Dt maturity = cdo.Maturity;
      int idx = checkDates(dates_, maturity);
      double result = Double.NaN;

      try
      {
        basketPricer.Maturity = maturity;
        if (idx >= 0)
        {
          result = baseCorrelations_[idx].CalcCorrelation(
            cdo, basketPricer, discountCurve,
            toleranceF, toleranceX, MinCorrelation, MaxCorrelation);
        }
        else
        {
          // We use a curve to do time interpolation
          Curve curve = new Curve(basketPricer.AsOf);
          curve.Interp = this.Interp;

          for (int i = 0; i < dates_.Length; ++i)
          {
            double tmp = baseCorrelations_[i].CalcCorrelation(
              cdo, basketPricer, discountCurve,
              toleranceF, toleranceX, MinCorrelation, MaxCorrelation);
            curve.Add(dates_[i], tmp);
          }

          result = curve.Interpolate(maturity);
        }
      }
      finally
      {
        basketPricer.Maturity = savedMaturity;
      }

      return result;
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
        names = basket.EntityNames;
      if (dates == null)
      {
        dates = GetTermStructDates();
        if (dates == null || dates.Length == 0)
          dates = new Dt[] { cdo.Maturity };
      }

      if (this.Calibrator != null && this.Calibrator.TrancheTerm!=null)
      {
        // For EquitySpread and SeniorSpread strikes,
        //   we want to use the original effective, first premium,
        //   day count and frequency for strike calculations.
        //   Otherwise, we will fail the round trip check.
        SyntheticCDO myCDO = this.Calibrator.TrancheTerm.Clone() as SyntheticCDO;
        myCDO.Attachment = 0;
        myCDO.Detachment = cdo.Detachment;
        myCDO.Maturity = cdo.Maturity;
        cdo = myCDO;
      }

      CorrelationTermStruct cot = new CorrelationTermStruct(
        names, new double[dates.Length], dates, MinCorrelation, MaxCorrelation);
      basket.Correlation = cot;
      basket.RawLossLevels =
        new UniqueSequence<double>(0.0, cdo.Detachment);
      for (int i = 0; i < dates.Length; ++i)
      {
        cdo.Maturity = basket.Maturity = dates[i];
        basket.Reset();
        double corr = GetCorrelation(cdo, basket,
          discountCurve, toleranceF, toleranceX);
        cot.SetFactorAtDate(i, Math.Sqrt(corr));
      }
      return cot;
    }

    /// <summary>
    ///   Get embeded term structure dates
    /// </summary>
    /// <returns>Dates or null if not calibrated by term structure method</returns>
    internal override Dt[] GetTermStructDates()
    {
      if (this.CalibrationMethod == BaseCorrelationCalibrationMethod.MaturityMatch)
        return null;
      return dates_;
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
    public override double
    TrancheCorrelation(
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
      int idx = checkDates(dates_, cdo.Maturity);
      if (idx >= 0)
      {
        double corr = baseCorrelations_[idx].TrancheCorrelation(
                    cdo, asOf, settle,
                    survivalCurves, recoveryCurves,
                    discountCurve, principals,
                    stepSize, stepUnit,
                    apBump, dpBump,
                    copula, gridSize,
                    integrationPointsFirst,
                    integrationPointsSecond,
                    toleranceF, toleranceX);
        return corr;
      }

      // We use a curve to do time interpolation
      Curve curve = new Curve(asOf);
      curve.Interp = this.Interp;

      // Create a copy of CDO used to calculate tranche correlation for each date
      SyntheticCDO cdoDup = (SyntheticCDO)cdo.Clone();
      for (int i = 0; i < dates_.Length; ++i)
      {
        cdoDup.Maturity = dates_[i];
        double corr = baseCorrelations_[i].TrancheCorrelation(
                    cdoDup, asOf, settle,
                    survivalCurves, recoveryCurves,
                    discountCurve, principals,
                    stepSize, stepUnit,
                    apBump, dpBump,
                    copula, gridSize,
                    integrationPointsFirst,
                    integrationPointsSecond,
                    toleranceF, toleranceX);
        curve.Add(dates_[i], corr);
      }

      double result = curve.Interpolate(cdo.Maturity);

      return result;
    }

    /// <summary>
    ///   Construct a base correlation at a specific date
    /// </summary>
    /// <exclude />
    // For internal use only at this moment
    public BaseCorrelation GetBaseCorrelation(Dt date)
    {
      int idx = checkDates(dates_, date);
      if (idx >= 0)
        return baseCorrelations_[idx];

      // Since we do not know the as-of date, we do an implicit const extrapolation
      // if the date to interpolate is before the first maturity date.
      // TODO: need to revisit this later
      if (date <= dates_[0])
        return baseCorrelations_[0];

      // We have to construct a new base correlation

      // First check if this is possible
      for (int i = 1; i < baseCorrelations_.Length; ++i)
        if (baseCorrelations_[i].Method != baseCorrelations_[0].Method ||
            baseCorrelations_[i].StrikeMethod != baseCorrelations_[0].StrikeMethod)
          throw new System.ArgumentException(
            "Not all the base correlations have the same method and strike method");

      // Find the combines strikes
      double[] strikes = combinedStrikes();

      // Find the corresponding base correlation numbers
      double[] baseCorrs = new double[strikes.Length];
      for (int i = 0; i < strikes.Length; ++i)
        baseCorrs[i] = interpolate(strikes[i], date);

      // Construct a new base correlation and return
      BaseCorrelation bc = new BaseCorrelation(
        baseCorrelations_[0].Method,
        baseCorrelations_[0].StrikeMethod,
        baseCorrelations_[0].StrikeEvaluator,
        strikes, baseCorrs);
      bc.MinCorrelation = this.MinCorrelation;
      bc.MaxCorrelation = this.MaxCorrelation;
      bc.Extended = this.Extended;
      bc.Interp = this.Interp;
      bc.EntityNames = this.EntityNames;
      return bc;
    }

    /// <summary>
    ///   Construct base correlation term structure from a sequence of correlations
    /// </summary>
    /// 
    /// <param name="method">Method used for constructing the base correlations</param>
    /// <param name="strikeMethod">Method used for calculating base correlation strikes</param>
    /// <param name="strikeEvaluator">User defined strike evaluator (ignored if strikeMethod is not UserDefined)</param>
    /// <param name="calibrationMethod">Term structure calibrating method</param>
    /// <param name="cdos">CDO tranches in detachment point order</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="corr">Base correlations at the detachment points</param>
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
    /// <param name="strikeInterp">Interpolation method between strikes (default is linear)</param>
    /// <param name="strikeExtrap">Extrapolation method for strikes (default is const)</param>
    /// <param name="tenorInterp">Interpolation method between tenors (default is linear)</param>
    /// <param name="tenorExtrap">Extrapolation method for tenors (default is const)</param>
    /// <param name="min">Minimum return value if Smooth extrapolation is chosen</param>
    /// <param name="max">Maximum return value if Smooth extrapolation is chosen</param>
    /// 
    /// <exclude />
    private void Initialize(
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      BaseCorrelation.IStrikeEvaluator strikeEvaluator,
      BaseCorrelationCalibrationMethod calibrationMethod,
      SyntheticCDO[][] cdos,
      Dt asOf,
      Dt settle,
      double[][] corr,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[][] principals,
      int stepSize,
      TimeUnit stepUnit,
      Copula copula,
      double gridSize,
      int integrationPointsFirst,
      int integrationPointsSecond,
      InterpMethod strikeInterp,
      ExtrapMethod strikeExtrap,
      InterpMethod tenorInterp,
      ExtrapMethod tenorExtrap,
      double min, double max
      )
    {
      // Validate
      if (cdos == null || cdos.Length < 1)
        throw new System.ArgumentException("Must specify cdos");

      // Number of tenor dates
      int nSets = cdos.Length;
      if (nSets != principals.Length)
        throw new System.ArgumentException(String.Format(
          "cdos (Length={0}) and principals (Length={1}) not match",
          nSets, principals.Length));

      // Number of tranches
      int nTranches = cdos[0].Length;
      for (int i = 1; i < nSets; ++i)
        if (cdos[i].Length != nTranches)
          throw new System.ArgumentException("Number of tranches not match");

      // Check the number of correlations
      if (corr == null || corr.Length != nSets)
        throw new System.ArgumentException(String.Format(
          "cdos (Length={0}) and corr (Length={1}) not match",
          nSets, corr.Length));
      for (int i = 1; i < nSets; ++i)
        if (corr[i].Length != nTranches)
          throw new System.ArgumentException("Number of correlations not match");

      // Allocate arrays
      baseCorrelations_ = new BaseCorrelation[nSets];
      dates_ = new Dt[nSets];

      // Initialize dates
      for (int i = 0; i < nSets; ++i)
        dates_[i] = cdos[i][0].Maturity;

      // Construct correlation term structures
      string[] names = Utils.GetCreditNames(survivalCurves);
      CorrelationTermStruct[] baseCorrs = new CorrelationTermStruct[nTranches];
      for (int j = 0; j < nTranches; ++j)
        baseCorrs[j] = CreateCorrelation(calibrationMethod, names, dates_);

      // Calculate strikes for each maturity date
      BasketPricer basket = CreateBasket(asOf, settle, GetMaturity(cdos),
        survivalCurves, recoveryCurves, principals[0], copula,
        stepSize, stepUnit, gridSize, integrationPointsFirst, integrationPointsSecond);
      for (int i = 0; i < nSets; ++i)
      {
        // Construct base pricers
        SyntheticCDOPricer[] basePricers = CreateCDOPricers(
          CreateBaseTranches(cdos[i]), dates_[i], principals[i], basket, discountCurve, baseCorrs);
        double[] strikes = BaseCorrelation.Strike(
          basePricers, strikeMethod, strikeEvaluator, corr[i]);
        BaseCorrelation bc = new BaseCorrelation(
          method, strikeMethod, strikeEvaluator, strikes, corr[i]);
        bc.EntityNames = names;
        bc.MinCorrelation = min;
        bc.MaxCorrelation = max;
        bc.Extended = (max > 1);
        bc.Interp = InterpFactory.FromMethod(strikeInterp, strikeExtrap, min, max);
        baseCorrelations_[i] = bc;
        if (calibrationMethod == BaseCorrelationCalibrationMethod.TermStructure)
          UpdateCorrelations(i, baseCorrs, null, bc);
      }

      // Time interpolation method
      this.Extended = (max > 1);
      this.Interp = InterpFactory.FromMethod(tenorInterp, tenorExtrap, min, max);
      this.CalibrationMethod = calibrationMethod;
      this.EntityNames = names;
      this.MinCorrelation = min;
      this.MaxCorrelation = max;
      return;
    }


    private static Dt GetMaturity(SyntheticCDO[][] cdos)
    {
      return cdos[cdos.Length - 1][0].Maturity;
    }

    /// <summary>
    ///   Create a basket pricer
    /// </summary>
    ///
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settle date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="survivalCurves">Survival curves</param>
    /// <param name="recoveryCurves">Recovery curves</param>
    /// <param name="principals">Principals</param>
    /// <param name="copula">Copula</param>
    /// <param name="stepSize">Time grid size</param>
    /// <param name="stepUnit">Time grid unit</param>
    /// <param name="gridSize">Distribution grid size</param>
    /// <param name="integrationPointsFirst">Quadrature points</param>
    /// <param name="integrationPointsSecond">Quadrature points</param>
    /// <returns>Basket Pricer</returns>
    private static BasketPricer CreateBasket(
        Dt asOf,
        Dt settle,
        Dt maturity,
        SurvivalCurve[] survivalCurves,
        RecoveryCurve[] recoveryCurves,
        double[] principals,
        Copula copula,
        int stepSize,
        TimeUnit stepUnit,
        double gridSize,
        int integrationPointsFirst,
        int integrationPointsSecond
        )
    {
      // Set up basket pricer for calculations
      double[,] lossLevels = new double[1, 1];

      //- Create pricers
      SemiAnalyticBasketPricer basket = new SemiAnalyticBasketPricer(
        asOf, settle, maturity, survivalCurves, recoveryCurves, principals, copula,
        new CorrelationTermStruct(new string[survivalCurves.Length], new double[1], new Dt[] { maturity }),
        stepSize, stepUnit, lossLevels, false);
      if (gridSize > 0)
        basket.GridSize = gridSize;
      if (integrationPointsFirst > 0)
        basket.IntegrationPointsFirst = integrationPointsFirst;
      else
        basket.IntegrationPointsFirst =
          BasketPricerFactory.DefaultQuadraturePoints(copula, survivalCurves.Length);
      if (integrationPointsSecond > 0)
        basket.IntegrationPointsSecond = integrationPointsSecond;

      return basket;
    }

    /// <summary>
    ///   Create CDO pricers for calibrating base correlations
    /// </summary>
    /// <param name="cdos">Array of CDO tranches</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="principals">Participations by names</param>
    /// <param name="basket">Basket pricer</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="correlations">Correlation term struct</param>
    /// <returns>CDO Pricers</returns>
    private static SyntheticCDOPricer[] CreateCDOPricers(
      SyntheticCDO[] cdos,
      Dt maturity,
      double[] principals,
      BasketPricer basket,
      DiscountCurve discountCurve,
      CorrelationTermStruct[] correlations
      )
    {
      int nTranches = cdos.Length;

      // Set up basket pricer for calculations
      UniqueSequence<double> lossLevels = new UniqueSequence<double>();
      for (int i = 0; i < cdos.Length; i++)
        lossLevels.Add(cdos[i].Attachment, cdos[i].Detachment);

      //- Create pricers
      double totalPrincipal = 1.0E10; // 10 billions
      SyntheticCDOPricer[] pricers = new SyntheticCDOPricer[nTranches];
      for (int i = 0; i < nTranches; ++i)
      {
        BasketBootstrapCorrelationPricer bskt =
          new BasketBootstrapCorrelationPricer(basket, correlations[i]);
        if (principals != null)
          bskt.Principals = principals;
        bskt.RawLossLevels = lossLevels;
        bskt.Maturity = maturity;
        if (bskt.MaximumAmortizationLevel() <=
          BasketPricerFactory.MinimumAmortizationLevel(cdos[i]))
        {
          bskt.NoAmortization = true;
        }
        pricers[i] = new SyntheticCDOPricer(cdos[i], bskt, discountCurve,
          (cdos[i].Detachment - cdos[i].Attachment) * totalPrincipal, null);
      }
      return pricers;
    }

    /// <summary>
    ///   Check consistency of tranches
    /// </summary>
    /// <param name="cdos">CDO tranches</param>
    private static void CheckConsistency(SyntheticCDO[] cdos)
    {
      // Set up basket pricer loss levels for calculations
      if (cdos[0].Attachment != 0)
        throw new ArgumentException("The attachment of the first tranhce must be 0");
      for (int i = 1; i < cdos.Length; i++)
      {
        if (cdos[i].Attachment != cdos[i - 1].Detachment)
          throw new ArgumentException(String.Format(
              "The attachment of {0}th tranhce ({1}) must match the detachment of {2}th tranhce ({3})",
              i + 1, cdos[i].Attachment, i, cdos[i - 1].Detachment));
      }
    }

    private static SyntheticCDO[] CreateBaseTranches(SyntheticCDO[] cdos)
    {
      // Set up basket pricer loss levels for calculations
      SyntheticCDO[] baseCdos = new SyntheticCDO[cdos.Length];
      for (int i = 0; i < cdos.Length; i++)
      {
        baseCdos[i] = (SyntheticCDO)cdos[i].Clone();
        baseCdos[i].Attachment = 0;
      }
      return baseCdos;
    }

    private static void UpdateCorrelations(
        int tenorIdx,
        CorrelationTermStruct[] baseCorrs,
        CorrelationTermStruct[] trancheCorrs,
        BaseCorrelation bc)
    {
      for (int i = 0; i < baseCorrs.Length; ++i)
      {
        baseCorrs[i].Correlations[tenorIdx] = Math.Sqrt(bc.Correlations[i]);
        if (trancheCorrs != null)
          trancheCorrs[i].Correlations[tenorIdx] = Math.Sqrt(bc.TrancheCorrelations[i]);
      }
    }

    private static void UpdateCorrelations(
      int tenorIdx,
      CorrelationTermStruct[] baseCorrs,
      double[] corr )
    {
      for (int i = 0; i < baseCorrs.Length; ++i)
        baseCorrs[i].Correlations[tenorIdx] = Math.Sqrt(corr[i]);
    }

    // Create a correlation term structure
    private static CorrelationTermStruct CreateCorrelation(
        BaseCorrelationCalibrationMethod calibrationMethod,
        string[] names,
        Dt[] dates)
    {
      if (calibrationMethod == BaseCorrelationCalibrationMethod.TermStructure)
        return new CorrelationTermStruct(names, new double[dates.Length], dates);
      return new CorrelationTermStruct(names, new double[1], new Dt[1] { dates[0] });
    }

    // check if maturity is on one point
    private static int checkDates(Dt[] dates, Dt maturity)
    {
      if (1 == dates.Length)
        return 0; // only one date, no time interpolation

      for (int i = 0; i < dates.Length; ++i)
      {
        if (dates[i] == maturity)
          return i;
      }

      return -1;
    }

    // Find the combined strikes
    private double[] combinedStrikes()
    {
      ArrayList list = new ArrayList();
      for (int i = 0; i < baseCorrelations_.Length; ++i)
      {
        double[] si = baseCorrelations_[i].Strikes;
        for (int j = 0; j < si.Length; ++j)
        {
          double strike = si[j];
          if (!Double.IsNaN(si[j]))
          {
            int pos = list.BinarySearch(strike);
            if (pos < 0)
              list.Insert(~pos, strike);
          }
        }
      }
      double[] strikes = new double[list.Count];
      for (int i = 0; i < list.Count; ++i)
        strikes[i] = (double)list[i];
      return strikes;
    }

    // Interpolate base correlation number at a specific strike and date
    private double interpolate(double strike, Dt date)
    {
      // We use a curve to do time interpolation
      //Curve curve = new Curve(dates_[0]);
      Curve curve = new Curve(Dt.Cmp(date, dates_[0])<0 ? date : dates_[0]);
      curve.Interp = this.Interp;

      // Create a copy of CDO used to calculate tranche correlation for each date
      for (int i = 0; i < dates_.Length; ++i)
      {
        double corr = baseCorrelations_[i].GetCorrelation(strike);
        curve.Add(dates_[i], corr);
      }

      double result = curve.Interpolate(date);

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
    ///   Set the correlation data from another
    ///   correlation object of the same type.
    /// </summary>
    /// <param name="source">Source correlation object</param>
    internal override void SetCorrelations(CorrelationObject source)
    {
      if (source == null)
        throw new ArgumentException("The source object can not be null.");

      BaseCorrelationTermStruct other = source as BaseCorrelationTermStruct;
      if (other == null)
        throw new ArgumentException("The source object is not a base correlation term struct object.");

      if (this.baseCorrelations_ == null)
        throw new NullReferenceException("The base correlation array is null.");

      if (other.baseCorrelations_ == null
        || other.baseCorrelations_.Length != this.baseCorrelations_.Length)
      {
        throw new ArgumentException("The source correlation array does not match this data.");
      }

      for (int i = 0; i < baseCorrelations_.Length; ++i)
        this.baseCorrelations_[i].SetCorrelations(other.baseCorrelations_[i]);

      return;
    }

    /// <summary>
    ///   Validate the internal consistency
    /// </summary>
    /// <param name="errors">A list of errors</param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if (dates_ == null)
        InvalidValue.AddError(errors, this, "Dates", "Dates must not be null");
      if (baseCorrelations_ == null || baseCorrelations_.Length != dates_.Length)
        InvalidValue.AddError(errors, this, "BaseCorrelations",
          String.Format("Base correlations (len={0}) and dates (len={1}) not match",
          baseCorrelations_==null?0:baseCorrelations_.Length, dates_.Length));
      if (tenorNames_ != null && tenorNames_.Length != dates_.Length)
        InvalidValue.AddError(errors, this, "TenorNames",
          String.Format("Tenor names (len={0}) and dates (len={1}) not match",
          tenorNames_.Length, dates_.Length));

      return;
    }

    #endregion Methods

    #region Walker
    /// <summary>
    ///   Object tree walker
    /// </summary>
    /// <param name="visit">Visitor</param>
    internal override void Walk(VisitFn visit)
    {
      if (visit(this) && baseCorrelations_ != null)
      {
        foreach (BaseCorrelation bc in baseCorrelations_)
          bc.Walk(visit);
      }
      return;
    }
    #endregion Walker

    #region Properties

    /// <summary>
    ///   Maturity dates
    /// </summary>
    public Dt[] Dates
    {
      get { return dates_; }
    }

    /// <summary>
    ///   Base correlation objects
    /// </summary>
    public BaseCorrelation[] BaseCorrelations
    {
      get { return baseCorrelations_; }
    }

    /// <summary>
    ///   Maturity dates
    /// </summary>
    public string[] TenorNames
    {
      get { return tenorNames_; }
      set { tenorNames_ = value; }
    }

    /// <summary>
    ///   Interpolator for strikes
    /// </summary>
    public Interp Interp
    {
      get { return interp_; }
      set { interp_ = value; }
    }

    /// <summary>
    ///   Interpolation method for strikes
    /// </summary>
    public InterpMethod InterpMethod
    {
      get { return InterpFactory.ToInterpMethod(interp_); }
    }

    /// <summary>
    ///   Extrapolation method for strikes
    /// </summary>
    public ExtrapMethod ExtrapMethod
    {
      get { return InterpFactory.ToExtrapMethod(interp_); }
    }

    /// <summary>
    ///   BaseCorrelation Calibration Method
    /// </summary>
    public BaseCorrelationCalibrationMethod CalibrationMethod
    {
      get { return calibrationMethod_; }
      set { calibrationMethod_ = value; }
    }

    /// <summary>
    ///   Set interp on factors
    /// </summary>
    [Browsable(false)]
    public bool InterpOnFactors
    {
      get { return interpOnFactors_; }
      set {
        interpOnFactors_ = value;
        if (baseCorrelations_ != null)
        {
          foreach (BaseCorrelation bc in baseCorrelations_)
            bc.InterpOnFactors = value;
        }
      }
    }

    #endregion Properties

    #region Data

    private Dt[] dates_;
    private string[] tenorNames_;
    private BaseCorrelation[] baseCorrelations_;
    private Interp interp_;
    private BaseCorrelationCalibrationMethod calibrationMethod_;
    private bool interpOnFactors_;
    #endregion Data

    #region BaseCorrelationBumps

    /// <summary>
    ///   Bump base correlations selected by detachments and tenor dates.
    /// </summary>
    /// 
    /// <param name="selectTenorDates">
    ///   Array of the selected tenor dates to bump.  A null value means to bump
    ///   all tenors.
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
    /// <returns>The average the absolute changes in correlations.</returns>
    public double BumpCorrelations(
      Dt[] selectTenorDates,
      double[] selectDetachments,
      double[] bumpSizes,
      bool relative, 
      double lowerBound, double upperBound)
    {
      return BumpCorrelations(selectTenorDates, selectDetachments,
        BumpSize.CreatArray(bumpSizes, BumpUnit.None, lowerBound, upperBound),
        null, relative, false, null);
    }

    /// <summary>
    ///   Bump base correlations selected by detachments and tenor dates.
    ///   For internal use only.
    ///   <prelimninary/>
    /// </summary>
    /// 
    /// <param name="selectTenorDates">
    ///   Array of the selected tenor dates to bump.  A null value means to bump
    ///   all tenors.
    /// </param>
    /// <param name="selectDetachments">
    ///   Array of the selected base tranches to bump.  This should be an array
    ///   of detachments points associated with the strikes.  A null value means
    ///   to bump the correlations at all strikes.
    /// </param>
    /// <param name="trancheBumps">
    ///   Array of the bump sizes applied to the correlations on the selected detachment
    ///   points.  If the array is null or empty, no bump is performed.  Else if it
    ///   contains only a single number, the number is applied to all detachment points.
    ///   Otherwise, the array is required to have the same length as the array of
    ///   detachments.
    /// </param>
    /// <param name="indexBump">
    ///   Array of bump sizes applied to index quotes by tenors.
    /// </param>
    /// <param name="relative">
    ///   Boolean value indicating if a relative bump is required.
    /// </param>
    /// <param name="onquotes">
    ///   Bump on quotes instead of correlation themselves
    /// </param>
    /// <param name="hedgeInfo">
    ///   Hedge delta info.  Null if no head info is required.
    /// </param>
    /// 
    /// <returns>
    ///   The average of the absolute changes in correlations (for correlation bump)
    ///   or quotes (for quote bump).
    /// </returns>
    /// <exclude />
    public double BumpCorrelations(
      Dt[] selectTenorDates,
      double[] selectDetachments,
      BumpSize[] trancheBumps,
      BumpSize indexBump,
      bool relative, bool onquotes,
      ArrayList hedgeInfo)
    {
      BaseCorrelationBump bump = BaseCorrelationBump.Create(
        this, selectTenorDates, selectDetachments, trancheBumps, indexBump, relative, onquotes);
      if (bump == null) return 0.0;
      return BaseCorrelationBump.Bump(new BaseCorrelationTermStruct[] { this },
        new BaseCorrelationBump[] { bump }, hedgeInfo);
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
      // If this surface is selected to bump, we bump it; otherwise, do nothing.
      if (selectComponents == null || Array.IndexOf(selectComponents, this.Name) >= 0)
      {
        return BumpCorrelations(selectTenorDates, selectDetachments,
          trancheBumps, indexBump, relative, onquotes, hedgeInfo);
      }
      else
        return 0.0;
    }
    #endregion BaseCorrelationBumps

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
      for (int j = 0; j < baseCorrelations_.Length; j++)
        avg += (i < baseCorrelations_[j].Correlations.Length ?
          baseCorrelations_[j].BumpCorrelations(i, bump, relative, factor)
          : 0.0);
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
      for (int j = 0; j < baseCorrelations_.Length; j++)
        avg += baseCorrelations_[j].BumpCorrelations(bump, relative, factor);

      return avg / baseCorrelations_.Length;
    }

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
      if (tenor < 0 || tenor >= baseCorrelations_.Length)
        throw new ArgumentException(String.Format("Tenor {0} is out of range", tenor));
      if (i < baseCorrelations_[tenor].Correlations.Length)
        return baseCorrelations_[tenor].BumpCorrelations(i, bump, relative, factor);
      else
        return 0.0;
    }

    /// <summary>
    ///  Bump all the correlations simultaneously for a given tenor
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
      if (tenor < 0 || tenor >= baseCorrelations_.Length)
        throw new ArgumentException(String.Format("Tenor {0} is out of range", tenor));
      return baseCorrelations_[tenor].BumpCorrelations(bump, relative, factor);
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
          if (i < baseCorrelations_[iName].Correlations.Length)
            return baseCorrelations_[iName].GetName(i);
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
          if (count < baseCorrelations_[i].Correlations.Length)
            count = baseCorrelations_[i].Correlations.Length;
        return count;
      }
    }

    #endregion

    #region Calibrator
    /// <summary>
    ///   Create a base correlation term structure from market quotes
    /// </summary>
    /// <param name="quotes">Tranche quotes (spread in basis points and fee in percent)</param>
    /// <param name="runningPrem">Array of running premiums (if quote in price)</param>
    /// <param name="dp">Array of detachments</param>
    /// <param name="maturities">Array of tranche maturity dates</param>
    /// <param name="tenorNames">Array of tenor names for the maturity dates</param>
    /// <param name="indexTerm">Index term</param>
    /// <param name="cdoTerm">A SyntheticCDO object representing tranche terms</param>
    /// <param name="basket">Underlying basket for the tranches</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="calibrationMethod">Base correlation calibration method</param>
    /// <param name="method">Base correlation calculation method</param>
    /// <param name="strikeMethod">Base correlation strike calculation method</param>
    /// <param name="strikeEvaluator">User supplied strike evaluator (can be null)</param>
    /// <param name="toleranceF">Relative error allowed in PV when calculating implied correlations</param>
    /// <param name="toleranceX">The accuracy of implied correlations</param>
    /// <param name="strikeInterp">Interpolation method between strikes (default is linear)</param>
    /// <param name="strikeExtrap">Extrapolation method for strikes (default is const)</param>
    /// <param name="tenorInterp">Interpolation method between tenors (default is linear)</param>
    /// <param name="tenorExtrap">Extrapolation method for tenors (default is const)</param>
    /// <param name="min">Minimum return value if Smooth extrapolation is chosen</param>
    /// <param name="max">Maximum return value if Smooth extrapolation is chosen</param>
    /// <param name="bottomUp">True for bottom-up calibration, false for top-down</param>
    /// <returns>Calibrated base correlation term structure</returns>
    public static BaseCorrelationTermStruct FromMarketQuotes(
      double[,] quotes,
      double[,] runningPrem,
      double[] dp,
      Dt[] maturities,
      string[] tenorNames,
      IndexScalingCalibrator indexTerm,
      SyntheticCDO cdoTerm,
      BasketPricer basket,
      DiscountCurve discountCurve,
      BaseCorrelationCalibrationMethod calibrationMethod,
      BaseCorrelationMethod method,
      BaseCorrelationStrikeMethod strikeMethod,
      BaseCorrelation.IStrikeEvaluator strikeEvaluator,
      double toleranceF,
      double toleranceX,
      InterpMethod strikeInterp,
      ExtrapMethod strikeExtrap,
      InterpMethod tenorInterp,
      ExtrapMethod tenorExtrap,
      double min, double max, bool bottomUp)
    {
      BaseCorrelation.CheckTolerance(ref toleranceF, ref toleranceX, basket);

      BaseCorrelationCalibrator cal;
      if (method == BaseCorrelationMethod.ArbitrageFree)
        cal = new DirectArbitrageFreeCalibrator(calibrationMethod, cdoTerm, basket,
          discountCurve, maturities, dp, runningPrem, quotes, toleranceF, toleranceX, bottomUp);
      else // protection match method
      {
        if (!bottomUp)
          throw new ArgumentException("Protection match does not support top-down calibration.");
        cal = new DirectLossPvMatchCalibrator(
          cdoTerm, basket, discountCurve,
          maturities, dp, runningPrem, quotes, toleranceF, toleranceX);
      }
      if(indexTerm != null)
        cal.IndexTerm = indexTerm;

      BaseCorrelationTermStruct bco = cal.Fit(calibrationMethod, method,
        strikeMethod, strikeEvaluator,
        strikeInterp, strikeExtrap, tenorInterp, tenorExtrap, min, max);
      bco.RecoveryCorrelationModel = basket.RecoveryCorrelationModel;
      bco.Calibrator = cal;
      bco.tenorNames_ = tenorNames;
      bco.MinCorrelation = min;
      bco.MaxCorrelation = max;
      bco.EntityNames = basket.EntityNames;
      return bco;
    }

    /// <summary>
    ///   Fit the whole correlation to market quotes
    /// </summary>
    public void Fit()
    {
      BaseCorrelationCalibrator cal = Calibrator;
      if (cal == null)
        throw new System.NullReferenceException("Base correlation calibrator is null");
      cal.FitFrom(this, 0, 0, MinCorrelation, MaxCorrelation);
    }

    /// <summary>
    ///   Refit the correlations from the specific tenor and detachment
    /// </summary>
    /// <param name="tenorIndex">The start tenor index</param>
    /// <param name="dpIndex">The start detachment index</param>
    public void ReFit(int tenorIndex, int dpIndex)
    {
      BaseCorrelationCalibrator cal = Calibrator;
      if (cal == null)
        throw new System.NullReferenceException("Base correlation calibrator is null");
      cal.FitFrom(this, tenorIndex, dpIndex, MinCorrelation, MaxCorrelation);
    }

    /// <summary>
    ///   Calculate the price at given quotes
    /// </summary>
    /// <param name="cal">BaseCorrelation Calibrator</param>
    /// <param name="quotes">Quotes</param>
    /// <param name="tenorIndices">Tenor indices</param>
    /// <param name="dpIndices">Detachment indices</param>
    /// <returns>Array of prices</returns>
    internal double[,] PricesAt(
      BaseCorrelationCalibrator cal,
      MarketQuote[][] quotes,
      int[] tenorIndices, int[] dpIndices)
    {
      double[,] results = new double[dpIndices.Length, tenorIndices.Length];
      for (int c = 0; c < tenorIndices.Length; ++c)
      {
        int t = tenorIndices[c];
        if (t < 0) continue;
        double[] corr = baseCorrelations_[t].Correlations;
        for (int r = 0; r < dpIndices.Length; ++r)
        {
          int d = dpIndices[r];
          if (d < 0) continue;
          results[r, c] = cal.TranchePriceAt(t, d, quotes[t][d]);
        }
      }
      return results;
    }

    public static BaseCorrelationTermStruct Create(
      BaseCorrelationParam paramsObj,
      BaseCorrelationStrikeMethod strikeMethod,
      BaseCorrelationCalibrationMethod calibrationMethod,
      SyntheticCDO[][] cdos,
      BasketPricer basket,
      double[][] principals,
      DiscountCurve discountCurve)
    {
      var bco = new BaseCorrelationTermStruct(
        paramsObj.Method, strikeMethod, null, calibrationMethod,
        cdos, basket, null, discountCurve,
        paramsObj.ToleranceF, paramsObj.ToleranceX,
        paramsObj.StrikeInterp, paramsObj.StrikeExtrap,
        paramsObj.TenorInterp, paramsObj.TenorExtrap,
        paramsObj.Min, paramsObj.Max);
      bco.InterpOnFactors = paramsObj.InterpOnFactors;
      bco.RecoveryCorrelationModel = paramsObj.RecoveryCorrelationModel;
      return bco;
    }

    /// <summary>
    /// To create the base correlation term structure
    /// </summary>
    /// <param name="paramsObj">Base correlation param object</param>
    /// <param name="strikeMethod">strike methods, such as Unscaled, ExpectedLoss and so on</param>
    /// <param name="calibrationMethod">Calibration method, such as MaturityMatch, TermStructure </param>
    /// <param name="cdos">Cdos</param>
    /// <param name="asOf">asof</param>
    /// <param name="settle">settle</param>
    /// <param name="corr">correlation array</param>
    /// <param name="survivalCurves">survivial curves</param>
    /// <param name="recoveryCurves">recovery curves</param>
    /// <param name="discountCurve">discount curve</param>
    /// <param name="principals">principals array</param>
    /// <returns></returns>
    public static BaseCorrelationTermStruct Create(
      BaseCorrelationParam paramsObj,
      BaseCorrelationStrikeMethod strikeMethod,
      BaseCorrelationCalibrationMethod calibrationMethod,
      SyntheticCDO[][] cdos,
      Dt asOf,
      Dt settle,
      double[][] corr,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      DiscountCurve discountCurve,
      double[][] principals)
    {
      var copula = paramsObj.Copula;
      int quadraturePoints = paramsObj.QuadraturePoints > 0
        ? paramsObj.QuadraturePoints
        : BasketPricerFactory.DefaultQuadraturePoints(copula, survivalCurves.Length);
      var bco = new BaseCorrelationTermStruct(
        paramsObj.Method, strikeMethod, calibrationMethod, cdos,
        asOf, settle, corr, survivalCurves, recoveryCurves,
        discountCurve, principals, 
        paramsObj.StepSize, paramsObj.StepUnit, copula,
        paramsObj.GridSize, quadraturePoints, 0,
        paramsObj.StrikeInterp, paramsObj.StrikeExtrap,
        paramsObj.TenorInterp, paramsObj.TenorExtrap,
        paramsObj.Min, paramsObj.Max);
      bco.InterpOnFactors = paramsObj.InterpOnFactors;
      bco.RecoveryCorrelationModel = paramsObj.RecoveryCorrelationModel;
      return bco;
    }

    public static BaseCorrelationTermStruct Create(
      BaseCorrelationParam paramsObj,
      BaseCorrelationCalibrationMethod calibrtnMethod,
      Dt[] dates, BaseCorrelation[] baseCorrelations,
      string[] tenors, string[] entityNames)
    {
      if (entityNames != null && entityNames.Length == 0)
        entityNames = null;

      if (dates.Length < 1)
        throw new ArgumentException("Must specify at least one date");
      if (dates.Length != baseCorrelations.Length)
        throw new ArgumentException("Number of dates does not match number of base correlation curves");
      if (tenors.Length > 0 && tenors.Length != baseCorrelations.Length)
        throw new ArgumentException("Number of tenors does not match number of dates");

      // Remove empty base correlations
      int count = 0;
      for (int i = 0; i < baseCorrelations.Length; i++)
      {
        if (baseCorrelations[i] != null)
          count++;
      }
      if (count <= 0)
        throw new ArgumentException("Must specify at least one valid base correlation curve");

      string[] tenorNames = tenors.IsNullOrEmpty() ? null : new string[count];
      var dts = new Dt[count];
      var bcs = new BaseCorrelation[count];
      double min = paramsObj.Min, max = paramsObj.Max;
      bool extended = (max > 1);
      count = 0;
      for (int i = 0; i < baseCorrelations.Length; i++)
      {
        if (baseCorrelations[i] == null) continue;
        if (tenorNames != null) tenorNames[count] = tenors[i];
        dts[count] = dates[i];
        bcs[count++] = baseCorrelations[i];
        extended = extended || baseCorrelations[i].Extended;
      }

      // Construct base correlation term structure
      var bco = new BaseCorrelationTermStruct(dts, bcs);
      bco.TenorNames = tenorNames;
      bco.Extended = extended;
      bco.Interp = InterpFactory.FromMethod(
        paramsObj.TenorInterp, paramsObj.TenorExtrap, min, max);
      bco.CalibrationMethod = calibrtnMethod;
      bco.MinCorrelation = min;
      bco.MaxCorrelation = max;
      // Set entity names
      if (entityNames == null)
        entityNames = FindEntityNames(bcs);
      bco.EntityNames = entityNames;
      bco.InterpOnFactors = paramsObj.InterpOnFactors;
      bco.RecoveryCorrelationModel = paramsObj.RecoveryCorrelationModel;
      return bco;
    }
    #endregion Calibrator

  } // class BaseCorrelation

}
