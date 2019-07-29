/*
 *  -2014. All rights reserved.
 */
using System;
using System.Collections.Generic;
using System.Xml;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.Serialization;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  /// Helpers for write toolkit objects to and read them from XML representations.
  /// </summary>
  public static class XmlSerialization
  {
    #region Read and write XML files

    /// <summary>
    /// Reads the object from the specified XML file.
    /// </summary>
    /// <typeparam name="T">The type of the object to read</typeparam>
    /// <param name="xmlFilePath">The XML file path</param>
    /// <returns>T.</returns>
    public static T ReadXmlFile<T>(string xmlFilePath)
    {
      var serializer = GetSerializer(typeof (T));
      using (var xmlReader = XmlReader.Create(xmlFilePath))
      {
        return (T) serializer.ReadObject(xmlReader);
      }
    }

    /// <summary>
    /// Reads the object from the specified XML file.
    /// </summary>
    /// <param name="xmlFilePath">The XML file path</param>
    /// <param name="rootType">Type of the object to read (the default is <see cref="object"/>)</param>
    /// <returns>System.Object.</returns>
    public static object ReadXmlFile(string xmlFilePath,
      Type rootType = null)
    {
      if (rootType == null) rootType = typeof(object);
      var serializer = GetSerializer(rootType);
      using (var xmlReader = XmlReader.Create(xmlFilePath))
      {
        return serializer.ReadObject(xmlReader);
      }
    }

    /// <summary>
    /// Writes the object to the specified XML file.
    /// </summary>
    /// <typeparam name="T">The type of the object to write</typeparam>
    /// <param name="data">The object to write</param>
    /// <param name="xmlFilePath">The XML file path</param>
    public static void WriteXmlFile<T>(T data, string xmlFilePath)
    {
      WriteXmlFile(data, xmlFilePath, typeof(T));
    }

    /// <summary>
    /// Writes the object to the specified XML file.
    /// </summary>
    /// <param name="data">The object to write</param>
    /// <param name="xmlFilePath">The XML file path</param>
    /// <param name="rootType">The declared type of the object to write
    ///   (the default is <see cref="object"/>)</param>
    public static void WriteXmlFile(
      object data, string xmlFilePath,
      Type rootType = null)
    {
      if (rootType == null) rootType = typeof (object);
      var settings = new XmlWriterSettings
      {
        OmitXmlDeclaration = false,
        ConformanceLevel = ConformanceLevel.Document,
        Indent = true,
      };
      var serializer = GetSerializer(rootType);
      using (var xmlWriter = XmlWriter.Create(xmlFilePath, settings))
      {
        serializer.WriteObject(xmlWriter, data);
      }
      return;
    }

    #endregion

    #region Create serializer

    /// <summary>
    /// Gets the serializer.
    /// </summary>
    /// <param name="rootType">Type of the root.</param>
    /// <returns>SimpleXmlSerializer.</returns>
    internal static SimpleXmlSerializer GetSerializer(Type rootType)
    {
      var serializer = new SimpleXmlSerializer(rootType, null)
      {
        TrackObjectReferences = true
      };
      MapFields(serializer);
      serializer.MapCollectionType(
        typeof (CurveTenorCollection), typeof (IList<CurveTenor>));
      serializer.MapCollectionType(
        typeof (AssetTermList), typeof (IDictionary<string, AssetCurveTerm>));
      return serializer;
    }

    private static readonly Dictionary<Type, FieldMap> NativeFieldMaps
      = new Dictionary<Type, FieldMap>
      {
        {
          Type.GetType("BaseEntity.Toolkit.Curves.Native.Curve+NativeRef, BaseEntity.Toolkit.NativeInterop"),
          new FieldMap
          {
            {"asOf_", "AsOf", typeof (Dt)},
            {"spread_", "Spread", typeof (double)},
            {"dayCount_", "DayCount", typeof (DayCount)},
            {"freq_", "Freq", typeof (Frequency)},
            {"x_", "X", typeof (double[])},
            {"y_", "Y", typeof (double[])},
            {"dt_", "Dt", typeof (Dt[])},
            {"name_", "Name", typeof (string)},
            {"category_", "Category", typeof (string)},
            {"ccy_", "Ccy", typeof (Currency)},
            {"flags_", "Flags", typeof (int)},
            {"jumpDate_", "JumpDate", typeof (Dt)},
            {"interp_", "Interp", typeof (InterpScheme)},
          }
        },
        //
        // Extrapolate
        //
        {
          typeof (Smooth),
          new FieldMap
          {
            {"h_", "H", typeof (double)},
            {"rightSlope_", "RightSlope", typeof (double)},
            {"leftSlope_", "LeftSlope", typeof (double)},
            {"max_", "Max", typeof (double)},
            {"min_", "Min", typeof (double)},
          }
        },
        //
        // Interpolate
        //
        {
          typeof (Cubic),
          new FieldMap
          {
            {"lowerExtrap_", "LowerExtrap", typeof (Extrap)},
            {"upperExtrap_", "UpperExtrap", typeof (Extrap)},
            {"lowerEnd_", "LowerEnd", typeof (Interp.EndCondition)},
            {"upperEnd_", "UpperEnd", typeof (Interp.EndCondition)},
            {"y2_", "Y2", typeof (double[])},
          }
        },
        {
          typeof (Flat),
          new FieldMap
          {
            {"lowerExtrap_", "LowerExtrap", typeof (Extrap)},
            {"upperExtrap_", "UpperExtrap", typeof (Extrap)},
            {"lowerEnd_", "LowerEnd", typeof (Interp.EndCondition)},
            {"upperEnd_", "UpperEnd", typeof (Interp.EndCondition)},
          }
        },
        {
          typeof(Interpolator),
          new FieldMap
          {
            {"x_", "X", typeof(double[]) },
            {"y_", "Y", typeof(double[]) },
            {"interp_", "Interp", typeof(InterpScheme) },
          }
        },
        {
          typeof (Linear),
          new FieldMap
          {
            {"lowerExtrap_", "LowerExtrap", typeof (Extrap)},
            {"upperExtrap_", "UpperExtrap", typeof (Extrap)},
            {"lowerEnd_", "LowerEnd", typeof (Interp.EndCondition)},
            {"upperEnd_", "UpperEnd", typeof (Interp.EndCondition)},
          }
        },
        {
          typeof (Weighted),
          new FieldMap
          {
            {"lowerExtrap_", "LowerExtrap", typeof (Extrap)},
            {"upperExtrap_", "UpperExtrap", typeof (Extrap)},
            {"lowerEnd_", "LowerEnd", typeof (Interp.EndCondition)},
            {"upperEnd_", "UpperEnd", typeof (Interp.EndCondition)},
          }
        },
        {
          typeof (Tension),
          new FieldMap
          {
            {"lowerExtrap_", "LowerExtrap", typeof (Extrap)},
            {"upperExtrap_", "UpperExtrap", typeof (Extrap)},
            {"lowerEnd_", "LowerEnd", typeof (Interp.EndCondition)},
            {"upperEnd_", "UpperEnd", typeof (Interp.EndCondition)},
            {"flags_", "Flags", typeof (int)},
            {"tension_", "Tension", typeof (double[])},
            {"storage_", "Storage", typeof (double[])},
          }
        },
      };

    /// <summary>
    /// Map field names for known Toolkit types
    /// </summary>
    private static readonly Dictionary<Type, string[,]> FieldMaps
      = new Dictionary<Type, string[,]>
      {
        //
        // Base
        //
        {
          typeof (RateResets),
          new[,] {{"allResets_", "AllResets"}}
        },
        {
          typeof (PutPeriod),
          new[,]
          {
            {"startDate_", "StartDate"},
            {"endDate_", "EndDate"},
            {"price_", "Price"},
            {"style_", "Style"},
          }
        },
        {
          typeof (CallPeriod),
          new[,]
          {
            {"startDate_", "StartDate"},
            {"endDate_", "EndDate"},
            {"price_", "Price"},
            {"style_", "Style"},
            {"trigger_", "Trigger"},
            {"grace_", "Grace"},
          }
        },
        //
        // Curves and calibrators
        //
        {
          typeof (CalibratedCurve),
          new[,]
          {
            {"tenors_", "Tenors"},
            {"calibrator_", "Calibrator"},
            {"dependentCurves_", "DependentCurves"},
            {"gradientsWrtQuotes_", Ignore},
            {"hessiansWrtQuotes_", Ignore},
            {"initDerivativesWrtQuotes_", Ignore},
          }
        },
        {
          typeof (Calibrator),
          new[,]
          {
            {"asOf_", "AsOf"},
            {"settle_", "Settle"},
            {"calibrationTime_", Ignore},
          }
        },
        {
          typeof (Curve),
          new[,]
          {
            {"native_", "Native"},
          }
        },
        {
          typeof (BaseEntity.Toolkit.Curves.Native.Curve),
          new[,]
          {
            {"native_", "Data"},
            {"overlay_", "Overlay"},
            {"customInterp_", "CustomInterp"},
          }
        },
        {
          typeof (CurvePointHolder),
          new[,] {{"value_", "Value"}}
        },
        {
          typeof(DelegateSpotCurve),
          new[,] { {"_fn", "Fn" }}
        },
        {
          typeof (DiscountCurveFitCalibrator),
          new[,]
          {
            {"_targetIndices", "TargetIndices"},
          }
        },
        {
          typeof (FactorLoadingCollection),
          new[,]
          {
            {"_data", "Data"},
            {"_marketFactorNames", "FactorNames"},
            {"_tenors", "Tenors"},
          }
        },
        {
          typeof (VolatilityCollection),
          new[,]
          {
            {"_data", "Data"},
            {"_tenors", "Tenors"},
          }
        },
        {
          typeof (FxCurve),
          new[,]
          {
            {"_fxInterpolator", "FxInterpolator"},
          }
        },
        {
          typeof (FxCalibrator),
          new[,]
          {
            {"fi_", "FxInterpolator"},
          }
        },
        {
          typeof (CurveTenor),
          new[,]
          {
            {"name_", "Name"},
            {"product_", "Product"},
            {"marketPv_", "MarketPv"},
            {"weight_", "Weight"},
            {"originalQuote_", "OriginalQuote"},
            {"quoteHandler_", "QuoteHandler"},
            {"curveDate_", "CurveDate"},
            {"quoteKey_", "QuoteKey"},
            {"_q", "TenorQuote"},
            {"modelPv_", Ignore},
          }
        },
        {
          typeof (CurveTenor.Quote),
          new[,]
          {
            {"type_", "Type"},
            {"value_", "Value"},
          }
        },
        {
          typeof (CurveFitSettings),
          new[,]
          {
            {"method_", "Method"},
            {"curvatureWeight_", "CurvatureWeight"},
            {"curveAsOf_", "CurveAsOf"},
            {"pricingDate_", "PricingDate"},
            {"futureWeight_", "FutureWeight"},
            {"interpScheme_", "InterpScheme"},
            {"overlapTreatmentOrder_", "OverlapTreatmentOrder"},
            {"slopeWeight_", "SlopeWeight"},
            {"tolerance_", "Tolerance"},
            {"weightFactor_", "WeightFactor"},
            {"maximumIterations_", "MaximumIterations"},
            {"_flags", "Flags"},
          }
        },
        //
        // Interpolator
        //
        {
          typeof (InterpScheme),
          new[,]
          {
            {"_flags", "Flags"},
            {"upperExtrap_", "UpperExtrap"},
            {"lowerExtrap_", "LowerExtrap"},
          }
        },
        {
          typeof (Interp.EndCondition),
          new[,]
          {
            {"type_", "Type"},
            {"value_", "Value"},
          }
        },
        {
          typeof(SplineInterpolationSmile),
          new[,]
          {
            {"_interpolator", "Interpolator" },
            {"_forward", "Forward" },
            {"_interpSpace", "SmileInputKind" },
          }
        },
        //
        // Correlations
        //
        {
          typeof(Copula),
          new[,]
          {
            {"type_", "CopulaType" },
            {"dfCommon_", "DfCommon" },
            {"dfIdiosyncratic_", "DfIdiosyncratic" },
            {"data_", "Data" },
          }
        },
        //
        // Volatilities
        //
        {
          typeof(BlackScholesSurfaceCalibrator),
          new[,]
          {
            {"_model", "SmileModel" },
            {"_smileInterp", "SmileInterp" },
            {"_timeInterp", "TimeInterp" },
          }
        },
        {
          typeof(GenericBlackScholesCalibrator),
          new[,]
          {
            {"_spot", "Spot" },
            {"_curve1", "Curve1" },
            {"_curve2", "Curve2" },
          }
        },
        {
          typeof (RateVolatilityCalibrator),
          new[,]
          {
            {"asOf_", "AsOf"},
            {"rateProjectionCurve_", "RateProjectionCurve"},
            {"rateIndex_", "RateIndex"},
            {"discountCurve_", "DiscountCurve"},
            {"volatilityType_", "VolatilityType"},
          }
        },
        {
          typeof (FxOptionVannaVolgaCalibrator),
          new[,]
          {
            {"settle_", "Settle"},
            {"volCurveInterp_", "VolCurveInterp"},
            {"fwdAsAtmAfterYears_", "FwdAsAtmAfterYears"},
            {"fwdDeltaAfterYears_", "FwdDeltaAfterYears"},
            {"anchorStrikesCurves_", "AnchorStrikesCurves"},
            {"anchorVolsCurves_", "AnchorVolsCurves"},
            {"flags_", "Flags"},
            {"_underlying", "Underlying"},
          }
        },
        {
          typeof (ForwardVolatilityCube),
          new[,]
          {
            {"native_", "Native"},
            {"dates_", "Dates"},
            {"expiries_", "Expiries"},
            {"strikes_", "Strikes"},
            {"expiryTenors_", "ExpiryTenors"},
          }
        },
        {
          typeof (RateVolatilityCube),
          new[,]
          {
            {"fwdVols_", "FwdVols"},
          }
        },
        {
          typeof (RateVolatilitySurface),
          new[,]
          {
            {"_volatilityType", "VolatilityType"},
          }
        },
        {
          typeof (CalibratedVolatilitySurface),
          new[,]
          {
            {"calibrator_", "Calibrator"},
            {"tenors_", "Tenors"},
          }
        },
        {
          typeof (VolCubeInterpolator),
          new[,]
          {
            {"strikeInterp_", "StrikeInterp"},
            {"timeInterp_", "TimeInterp"},
          }
        },
        {
          typeof (VolatilityTenor),
          new[,]
          {
            {"_name", "Name"},
            {"_maturity", "Maturity"},
          }
        },
        {
          typeof (BasicVolatilityTenor),
          new[,]
          {
            {"_values", "Values"},
          }
        },
        {
          typeof (VolatilitySkewHolder),
          new[,]
          {
            {"delta_", "Delta"}
          }
        },
        {
          typeof (RateVolatilityParametricSabrCalibrator),
          new[,]
          {
            {"alphaDates_", "AllphaDates"},
            {"alphaValues_", "AllphaValues"},
            {"betaDates_", "BetaDates"},
            {"betaValues_", "BetaValues"},
            {"nuDates_", "NuDates"},
            {"nuValues_", "NuValues"},
            {"rhoDates_", "RhoDates"},
            {"rhoValues_", "RhoValues"},
          }
        },
        {
          Type.GetType(
            "BaseEntity.Toolkit.Calibrators.RateVolatilityParametricSabrCalibrator+VolatilityList, BaseEntity.Toolkit"),
          new[,]
          {
            {"_cal", "Calibrator"},
            {"_dateIndex", "DateIndex"},
          }
        },
        {
          typeof (RateVolatilitySabrCalibrator),
          new[,]
          {
            {"alphaCurve_", "AlphaCurve"},
            {"betaCurve_", "BetaCurve"},
            {"nuCurve_", "NuCurve"},
            {"rhoCurve_", "RhoCurve"},
          }
        },
        {
          typeof (View2D<RateVolatilityTenor>),
          new[,]
          {
            {"data_", "Data"},
            {"nrow_", "Rows"},
            {"ncol_", "Cols"},
          }
        },
        //
        // Payments
        //
        {
          typeof (Amortization),
          new[,]
          {
            {"amount_", "Amount"},
            {"date_", "Date"},
            {"type_", "Type"},
          }
        },
        {
          typeof (CouponPeriod),
          new[,]
          {
            {"coupon_", "Coupon"},
            {"date_", "Date"},
          }
        },
        {
          typeof (FloatingInterestPayment),
          new[,]
          {
            {"_indexMultiplier", "IndexMultiplier"},
            {"RateProjector", Ignore},
            {"ForwardAdjustment", Ignore},
          }
        },
        {
          typeof (CapletPayment),
          new[,]
          {
            {"_indexMultiplier", "IndexMultiplier"},
          }
        },
        //
        // Pricers
        //
        {
          typeof (PricerBase),
          new[,]
          {
            {"product_", "Product"},
            {"asOf_", "AsOf"},
            {"settle_", "Settle"},
            {"pricerFlags_", "PricerFlags"},
            {"origNotional_", "Notional"},
            {"payment_", "Payment"},
            {"paymentPricer_", "PaymentPricer"},
            {"isTerminated_", "IsTerminated"},
          }
        },
        {
          typeof (CapFloorPricerBase),
          new[,]
          {
            {"resets_", "Resets"},
            {"referenceCurve_", "ReferenceCurve"},
            {"discountCurve_", "DiscountCurve"},
            {"volatilityType_", "VolatilityType"},
            {"volatilityCube_", "VolatilityObject"},
            {"caplets_", "Caplets"},
            {"currentRate_", "CurrentRate"},
            {"lastExpiry_", "LastExpiry"},
          }
        },
        {
          typeof(CreditIndexOptionPricer),
          new[,]
          {
            {"_basketSize", "BasketSize" },
            {"_cdxSettleDate", "IndexSettleDate" },
            {"_currentFactor", "CurrentFactor" },
            {"_data", "ModelData"},
            {"_discountCurve", "DiscountCurve"},
            {"_includedPastLosses", "ExistingLoss" },
            {"_initialFactor", "InitialFactor" },
            {"_marketPv", "MarketPv" },
            {"_marketQuote", "MarketQuote" },
            {"_modelBasis", "ModelBasis" },
            {"_recoveryCurve", "MarketRecovery" },
            {"_requestedSettleDate", "RequestedSettleDate" },
            {"_volatility", "Volatility" },
            {"_volatilitySurface", "VolatilitySurface" },
          }
        },
        {
          typeof(CdxVolatilityUnderlying),
          new[,]
          {
            {"_data", "ModelData"},
            {"_flags", "Flags" },
            {"_discountCurve", "DiscountCurve"},
            {"_factor", "Factor"},
            {"_initialFactor", "InitialFactor" },
            {"_losses", "ExistingLoss" },
            {"_pricingDate", "PricingDate" },
            {"_protectStart", "ProtectStart" },
          }
        },
        {
          typeof (FxForwardPricer),
          new[,] {{"_valuationCurrency", "ValuationCurrency"}}
        },
        {
          typeof (FxOptionPricerBase),
          new[,]
          {
            {"volatilitySurface_", "VolatilitySurface"},
            {"discountCurve_", "DiscountCurve"},
            {"foreignRateCurve_", "ForeignRateCurve"},
            {"fxCurve_", "FxCurve"},
            {"smileAdjustment_", "SmileAdjustment"},
            {"spotFxRate_", "SpotFxRate"},
            {"fwdFxRate_", Ignore},
            {"blackVolatilityCurve_", Ignore},
            {"exerciseDate_", "ExerciseDate"},
            {"vvflags_", "VvFlags"},
          }
        },
        {
          typeof (FxOptionVanillaPricer),
          new[,]
          {
            {"isCalculated_", Ignore},
            {"pv_", Ignore},
            {"delta_", Ignore},
            {"gamma_", Ignore},
            {"vega_", Ignore},
            {"vanna_", Ignore},
            {"volga_", Ignore},
            {"vol_", Ignore},
            {"theta_", Ignore},
          }
        },
        {
          typeof (FxOptionSingleBarrierPricer),
          new[,]
          {
            {"adjustedBarrier_", "AdjustedBarrier"},
            {"flags_", "Flags"},
          }
        },
        {
          typeof (FxOptionDoubleBarrierPricer),
          new[,]
          {
            {"adjustedBarriers_", "AdjustedBarriers"},
            {"flags_", "Flags"},
          }
        },
        {
          typeof (MultiLeggedSwapPricer),
          new[,] {{"_swapLegPricers", "SwapLegPricers"}}
        },
        {
          typeof (SwaptionBlackPricer),
          new[,]
          {
            {"referenceCurve_", "ReferenceCurve"},
            {"discountCurve_", "DiscountCurve"},
            {"volatilityObject_", "VolatilityObject"},
            {"rateResets_", "RateResets"},
          }
        },
        {
          typeof (SwapBermudanBgmTreePricer),
          new[,]
          {
            {"_volatilityObject", "VolatilityObject"},
            {"_discountCurve", "DiscountCurve"},
            {"_referenceCurve", "ReferenceCurve"},
            {"_rateResets", "RateResets"},
            {"_swaption", "Swaption"},
          }
        },
        //
        // Products
        //
        {
          typeof (Product),
          new[,]
          {
            {"effective_", "Effective"},
            {"maturity_", "Maturity"},
            {"description_", "Description"},
            {"ccy_", "Ccy"},
            {"notional_", "Notional"}
          }
        },
        {
          typeof (ProductWithSchedule),
          new[,]
          {
            {"firstCpn_", "FirstCoupon"},
            {"lastCpn_", "LastCoupon"},
            {"freq_", "Freq"},
            {"bdc_", "BDConvention"},
            {"cal_", "Calendar"},
            {"rule_", "CycleRule"},
            {"flags_", "CashflowFlag"},
            {"schedule_", Ignore},
            {"cashflowFactorySchedule_", Ignore},
          }
        },
        {
          typeof (CDS),
          new[,]
          {
            {"recoveryCcy_", "RecoveryCcy"},
            {"amortSched_", "AmortizationSchedule"},
            {"premiumSched_", "PremiumSchedule"},
          }
        },
        {
          typeof (Bond),
          new[,]
          {
            {"amortSched_", "AmortizationSchedule"},
          }
        },
        {
          typeof (SwapLeg),
          new[,]
          {
            {"ptype_", "ProjectionType"},
            {"amortSched_", "AmortizationSchedule"},
            {"couponSched_", "CouponSchedule"},
            {"indexMultiplierSched_", "IndexMultiplierSchedule"},
          }
        },
        {
          typeof (MultiLeggedSwap),
          new[,] {{"_swapLegs", "SwapLegs"}}
        },
        {
          typeof (Swaption),
          new[,]
          {
            {"_swap", "Swap"},
            {"_expiry", "Expiry"},
          }
        },
        {
          typeof (InterestPayment),
          new[,] {{"_fixedCoupon", "FixedCoupon"}}
        },
      };

    /// <summary>
    /// Mapped name for the fields ignored in serialization
    /// </summary>
    private const string Ignore = "";

    /// <summary>
    /// Maps the fields.
    /// </summary>
    /// <param name="settings">The settings.</param>
    private static void MapFields(SimpleXmlSerializer settings)
    {
      foreach (var map in FieldMaps)
      {
        var s = map.Value;
        for (int i = 0, n = s.GetLength(0); i < n; ++i)
          settings.AddFieldNameMap(map.Key, s[i, 0], s[i, 1]);
      }

      foreach (var pair in NativeFieldMaps)
      {
        var type = pair.Key;
        foreach (var e in pair.Value)
        {
          settings.AddFieldNameMap(type, e.OriginalName, e.FieldType, e.MappedName);
        }
      }
    }

    #endregion
  }
}
