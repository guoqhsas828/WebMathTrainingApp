/*
 *  -2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Bump;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Models;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;
using Parameter = BaseEntity.Toolkit.Models.RateModelParameters.Param;
using Process = BaseEntity.Toolkit.Models.RateModelParameters.Process;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   Discount fit calibrator
  /// </summary>
  [Serializable]
  public class FxBasisFitCalibrator : DiscountCalibrator, IHasCashflowCalibrator, IInverseCurveProvider
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (FxBasisFitCalibrator));

    #region Static Constructors

    /// <summary>
    /// Fit a forward FX basis curve from basis swaps foreign projection Index vs. domestic projection Index
    /// </summary>
    /// <param name="name">The name of the curve</param>
    /// <param name="curveFitSettings">The curve fit settings</param>
    /// <param name="fxRate">FX rate</param>
    /// <param name="foreignProjection">Foreign projection curve</param>
    /// <param name="domesticProjection">Domestic projection curve</param>
    /// <param name="curveTenors">Curve tenor quotes</param>
    /// <returns>Calibrated FX basis curve</returns>
    /// <remarks>Calibrate FX basis curve to basis swap paying foreign projection index discounted
    /// at foreign funding rate vs. domestic projection index discounted at domestic funding rate.</remarks>
    public static DiscountCurve FxCurveFit(string name, CurveFitSettings curveFitSettings, FxRate fxRate,
      DiscountCurve foreignProjection, DiscountCurve domesticProjection, CurveTenorCollection curveTenors)
    {
      // Get projection indices and discount curves from projection curves if we need to
      DiscountCurve foreignDiscount, domesticDiscount;
      ReferenceIndex foreignIndex, domesticIndex;
      GetProjectionIndexAndDiscountCurve(foreignProjection, out foreignIndex, out foreignDiscount);
      GetProjectionIndexAndDiscountCurve(domesticProjection, out domesticIndex, out domesticDiscount);

      // Create curve and calibrator
      Dt asOf = curveFitSettings.CurveAsOf;
      var calibrator = new FxBasisFitCalibrator(asOf, foreignDiscount, domesticDiscount, foreignIndex,
        foreignProjection, domesticIndex, domesticProjection, fxRate,
        new CalibratorSettings(curveFitSettings));
      var curve = new DiscountCurve(calibrator)
      {
        Interp = curveFitSettings.GetInterp(),
        Category = "None",
        Ccy = foreignIndex.Currency,
        Name = name,
      };
      for (int i=0; i < curveTenors.Count(); i++)
      {
        var tenor = (CurveTenor)curveTenors[i].Clone();
        tenor.UpdateProduct(asOf);
        if (tenor.Product is FxForward)
        {
          tenor.QuoteHandler = new FxTenorQuoteHandler(tenor.Product.Maturity, fxRate.FromCcy, fxRate.ToCcy, tenor.CurrentQuote.Value);
        }
        curve.Tenors.Add(tenor);
      }

      var order = curveFitSettings.OverlapTreatmentOrder;
      if (order == null || order.Length == 0) order = DefaultOverlapTreatmentOrder;
      ResolveOverlap(curve.Tenors, order);
      if (logger.IsDebugEnabled) logger.DebugFormat("Creating FxBasisCurve {0}", curve.Name);
      curve.Fit();
      return curve;
    }

    /// <summary>
    /// Fit a forward FX basis curve from basis swaps foreign projection Index vs. domestic projection Index
    /// </summary>
    /// <param name="name">The name of the curve</param>
    /// <param name="curveFitSettings">The curve fit settings</param>
    /// <param name="fxRate">FX rate</param>
    /// <param name="foreignProjection">Foreign projection curve</param>
    /// <param name="domesticProjection">Domestic projection curve</param>
    /// <param name="swapSettle">Basis swap settle/effective date</param>
    /// <param name="swapCal">Calendar for basis swap leg settlement (default is calendar for each currency + LNB)</param>
    /// <param name="types">The types.</param>
    /// <param name="maturities">Xccy basis swap maturities</param>
    /// <param name="tenors">Xccy basis swap tenor names</param>
    /// <param name="quotes">Xccy basis swap quotes</param>
    /// <param name="weights">Xccy basis swap calibration weights (or null for no weights)</param>
    /// <param name="swapSpreadLeg">Swap leg paying the basis swap spread</param>
    /// <returns>Calibrated FX basis curve</returns>
    /// <remarks>Calibrate FX basis curve to basis swap paying foreign projection index discounted
    /// at foreign funding rate vs. domestic projection index discounted at domestic funding rate.</remarks>
    public static DiscountCurve FxCurveFit(
      string name, CurveFitSettings curveFitSettings, FxRate fxRate,
      DiscountCurve foreignProjection, DiscountCurve domesticProjection,
      Dt swapSettle, Calendar swapCal, InstrumentType[] types, Dt[] maturities, 
      string[] tenors, double[] quotes, double[] weights, BasisSwapSide swapSpreadLeg)
    {
      // Defaults we don't need to pass in for now.
      Frequency foreignIndexLegCompoundingFreq = Frequency.None;
      Frequency domesticIndexLegCompoundingFreq = Frequency.None;

      // Verify we have right number of tenors and dates
      if (maturities.Length != types.Length)
        throw new ArgumentException("Number of dates must match number of instrument types");
      if (maturities.Length != quotes.Length)
        throw new ArgumentException("Number of dates must match number of quotes");
      if (tenors.Length != quotes.Length)
        throw new ArgumentException("Number of tenors must match number of quotes");
      if (weights != null && weights.Length != quotes.Length)
        throw new ArgumentException("Number of weights must match number of quotes if specified");

      // Get projection indices and discount curves from projection curves if we need to
      DiscountCurve foreignDiscount, domesticDiscount;
      ReferenceIndex foreignIndex, domesticIndex;
      GetProjectionIndexAndDiscountCurve(foreignProjection, out foreignIndex, out foreignDiscount);
      GetProjectionIndexAndDiscountCurve(domesticProjection, out domesticIndex, out domesticDiscount);

      // Get swap spread leg flag
      bool spOnCcy2Leg;
      switch (swapSpreadLeg)
      {
      case BasisSwapSide.Ccy1:
        spOnCcy2Leg = false;
        break;
      case BasisSwapSide.Ccy2:
        spOnCcy2Leg = true;
        break;
      default:
        spOnCcy2Leg = (FxUtil.BasisSwapCcyLeg(fxRate.FromCcy, fxRate.ToCcy,
          types.Any(t => t == InstrumentType.BasisSwap)) == 2);
        break;
      }

      // Create curve and calibrator
      Dt asOf = curveFitSettings.CurveAsOf;
      var calibrator = new FxBasisFitCalibrator(asOf, foreignDiscount, domesticDiscount, foreignIndex,
        foreignProjection, domesticIndex, domesticProjection, fxRate,
        new CalibratorSettings(curveFitSettings));
      var curve = new DiscountCurve(calibrator)
        {
          Interp = curveFitSettings.GetInterp(),
          Category = "None",
          Ccy = foreignIndex.Currency,
          Name = name
        };

      // Add xccy basis swaps
      var xccy = String.Concat(fxRate.FromCcy, fxRate.ToCcy, '.');
      for (int i = 0; i < types.Length; ++i)
      {
        var type = types[i];
        if (type == InstrumentType.None) continue;
        if (type == InstrumentType.BasisSwap)
        {
          string tenorName = String.Format("BasisSwap.{0}", tenors[i]);
          double weight = (weights != null) ? weights[i] : 1.0;
          curve.AddSwap(tenorName, weight, swapSettle, maturities[i], quotes[i]*1e-4,
            foreignIndexLegCompoundingFreq,
            domesticIndexLegCompoundingFreq, foreignIndex, domesticIndex, swapCal,
            new PaymentSettings
              {
                SpreadOnReceiver = !spOnCcy2Leg,
                PrincipalExchange = true
              });
        }
        else if (type == InstrumentType.FxForward)
        {
          var maturity = maturities[i];
          var fxFwd = new FxForward(maturity, fxRate.FromCcy, fxRate.ToCcy, quotes[i])
            {
              Description = String.Format("FxForward.{0}", tenors[i])
            };
          curve.Tenors.Add(new CurveTenor(fxFwd.Description, fxFwd, 0.0, 0.0, 0.0, 1.0,
            new FxTenorQuoteHandler(maturity, fxRate.FromCcy, fxRate.ToCcy, quotes[i])));
        }
        else
        {
          throw new ToolkitException(String.Format(
            "Invalid instrument for FX curve: {0}", types[i]));
        }
        var lastTenor = curve.Tenors[curve.Tenors.Count - 1];
        lastTenor.QuoteKey = xccy + lastTenor.Name;
      }
      var order = curveFitSettings.OverlapTreatmentOrder;
      if(order==null||order.Length==0) order = DefaultOverlapTreatmentOrder;
      ResolveOverlap(curve.Tenors, order);
      if (logger.IsDebugEnabled) logger.DebugFormat("Creating FxBasisCurve {0}", curve.Name);
      curve.Fit();
      return curve;
    }

    private static void GetProjectionIndexAndDiscountCurve(DiscountCurve projectionCurve,
      out ReferenceIndex projectionIndex, out DiscountCurve discountCurve)
    {
      const string mustHaveIndex = "Projection/Discount curve must be calibrated with a projection index";
      var rateCalibrator = projectionCurve.Calibrator as IRateCurveCalibrator;
      if (rateCalibrator == null)
        throw new ArgumentException(mustHaveIndex);
      projectionIndex = rateCalibrator.ReferenceIndex;
      discountCurve = rateCalibrator.DiscountCurve ?? projectionCurve;
    }

    private static readonly InstrumentType[] DefaultOverlapTreatmentOrder
      = new[] { InstrumentType.FxForward, InstrumentType.BasisSwap };

    private static void ResolveOverlap(CurveTenorCollection tenors, InstrumentType[] order)
    {
      tenors.Sort();
      List<CurveTenor> newTenors = null;// new List<CurveTenor>();
      var lastTenor = tenors[0];
      for (int i = 1, n = tenors.Count; i < n; ++i)
      {
        var tenor = tenors[i];
        if (tenor.CurveDate > lastTenor.CurveDate)
        {
          if(newTenors!=null)newTenors.Add(tenor);
          lastTenor = tenor;
          continue;
        }
        if (newTenors == null)
        {
          newTenors = new List<CurveTenor>();
          for(int j = 0; j < i; ++j)
            newTenors.Add(tenors[j]);
        }
        CurveTenor chosen = lastTenor;
        for (int k = 0; k < order.Length; ++k)
        {
          var precedence = order[k];
          if (precedence == InstrumentType.FxForward)
          {
            if (chosen.Product is FxForward) break;
            if (tenor.Product is FxForward)
            {
              chosen = tenor;
              break;
            }
          }
          else if (precedence == InstrumentType.BasisSwap)
          {
            if (chosen.Product is Swap) break;
            if (tenor.Product is Swap)
            {
              chosen = tenor;
              break;
            }
          }
        }
        newTenors[newTenors.Count - 1] = lastTenor = chosen;
      }
      if (newTenors == null) return;
      tenors.Clear();
      foreach(var tenor in newTenors)
        tenors.Add(tenor);
    }

    /// <summary>
    /// Fit a forward FX basis curve from basis swaps foreign projection Index vs. domestic projection Index
    /// </summary>
    /// <remarks>
    ///   <para>Calibrate FX basis curve to basis swap paying foreign projection index discounted
    ///   at foreign funding rate vs. domestic projection index discounted at domestic funding rate.</para>
    /// </remarks>
    /// <param name="name">The name of the curve</param>
    /// <param name="curveFitSettings">The curve fit settings</param>
    /// <param name="fxRate">FX rate</param>
    /// <param name="foreignProjection">Foreign projection curve</param>
    /// <param name="domesticProjection">Domestic projection curve</param>
    /// <param name="swapSettle">Basis swap settle/effective date</param>
    /// <param name="swapCal">Calendar for basis swap leg settlement (default is calendar for each currency + LNB)</param>
    /// <param name="swapMaturities">Xccy basis swap maturities</param>
    /// <param name="swapTenors">Xccy basis swap tenor names</param>
    /// <param name="swapQuotes">Xccy basis swap quotes</param>
    /// <param name="swapWeights">Xccy basis swap calibration weights (or null for no weights)</param>
    /// <param name="swapSpreadLeg">Swap leg paying the basis swap spread</param>
    /// <returns>Calibrated FX basis curve</returns>
    public static DiscountCurve FxCurveFit(
      string name, CurveFitSettings curveFitSettings, FxRate fxRate,
      DiscountCurve foreignProjection, DiscountCurve domesticProjection,
      Dt swapSettle, Calendar swapCal, Dt[] swapMaturities, string[] swapTenors, double[] swapQuotes, double[] swapWeights,
      BasisSwapSide swapSpreadLeg
      )
    {
      // Defaults we don't need to pass in for now.
      Frequency foreignIndexLegCompoundingFreq = Frequency.None;
      Frequency domesticIndexLegCompoundingFreq = Frequency.None;

      // Verify we have right number of tenors and dates
      if (swapMaturities.Length != swapQuotes.Length)
        throw new ArgumentException("Number of swap dates must match number of swap quotes");
      if (swapTenors.Length != swapQuotes.Length)
        throw new ArgumentException("Number of swap tenors must match number of swap quotes");
      if (swapTenors.Length != swapQuotes.Length)
        throw new ArgumentException("Number of swap tenors must match number of swap quotes");
      if (swapWeights != null && swapWeights.Length != swapQuotes.Length)
        throw new ArgumentException("Number of swap weights must match number of swap quotes if specified");

      // Get projection indices and discount curves from projection curves if we need to
      DiscountCurve foreignDiscount, domesticDiscount;
      ReferenceIndex foreignIndex, domesticIndex;
      GetProjectionIndexAndDiscountCurve(foreignProjection, out foreignIndex, out foreignDiscount);
      GetProjectionIndexAndDiscountCurve(domesticProjection, out domesticIndex, out domesticDiscount);

      // Get swap spread leg flag
      bool spOnCcy2Leg;
      switch (swapSpreadLeg)
      {
        case BasisSwapSide.Ccy1: spOnCcy2Leg = false; break;
        case BasisSwapSide.Ccy2: spOnCcy2Leg = true; break;
        default: spOnCcy2Leg = (FxUtil.BasisSwapCcyLeg(fxRate.FromCcy, fxRate.ToCcy) == 2); break;
      }

      // Create curve and calibrator
      Dt asOf = curveFitSettings.CurveAsOf;
      var calibrator = new FxBasisFitCalibrator(asOf, foreignDiscount, domesticDiscount, foreignIndex,
                                                foreignProjection, domesticIndex, domesticProjection, fxRate,
                                                new CalibratorSettings(curveFitSettings));
      var curve = new DiscountCurve(calibrator)
      {
        Interp = curveFitSettings.GetInterp(),
        Category = "None",
        Ccy = foreignIndex.Currency,
        Name = name
      };

      // Add xccy basis swaps
      for (int i = 0; i < swapQuotes.Length; ++i)
      {
        string tenorName = String.Format("BasisSwap.{0}", swapTenors[i]);
        double weight = (swapWeights != null) ? swapWeights[i] : 1.0;
        curve.AddSwap(tenorName, weight, swapSettle, swapMaturities[i], swapQuotes[i] * 1e-4,
                      foreignIndexLegCompoundingFreq,
                      domesticIndexLegCompoundingFreq, foreignIndex, domesticIndex, swapCal,
                      new PaymentSettings
                      {
                        SpreadOnReceiver = !spOnCcy2Leg,
                        PrincipalExchange = true
                      });
      }
      if (logger.IsDebugEnabled) logger.DebugFormat("Creating FxBasisCurve {0}", curve.Name);
      curve.Fit();
      return curve;
    }

    #region Removed in 10.2
    /// <summary>
    /// Fit a forward FX basis curve from basis swaps foreign projection Index vs. domestic projection Index
    /// </summary>
    /// <param name = "name">The name of the curve.</param>
    /// <param name = "curveFitSettings">The curve fit settings.</param>
    /// <param name = "foreignDiscount">Foreign discount curve</param>
    /// <param name = "domesticDiscount">The discount curve.</param>
    /// <param name = "foreignProjection">Foreign projection curve</param>
    /// <param name = "domesticProjection">Domestic projection curve.</param>
    /// <param name = "foreignIndex">Index for foreign projection.</param>
    /// <param name = "domesticIndex">Index for domestic projection.</param>
    /// <param name = "category">The category of the curve.</param>
    /// <param name = "instrumentTypes">Instrument types</param>
    /// <param name = "spreadOnDomesticLeg">True if the spread is paid on the domestic side swap leg</param>
    /// <param name = "quotes">The quotes by instruments.</param>
    /// <param name = "settles">The settle dates of the instruments.</param>
    /// <param name = "maturities">The maturity dates of the instruments.</param>
    /// <param name = "tenors">The tenor names.</param>
    /// <param name = "weights">The weights by instruments.</param>
    /// <param name = "freqs">Frequencies of swap legs paying the basis spread.
    /// The payment frequency of the other leg is assumed to be driven by the tenor of the index (i.e. 6M Xibor is paid semiannualy)</param>
    /// <param name = "rolls">The roll conventions by instruments.</param>
    /// <param name = "calendars">The calendars.</param>
    /// <param name = "fxRate">FX rate</param>
    /// <returns>Calibrated FX basis curve.</returns>
    /// <remarks>
    /// Calibrate FX basis curve to basis swap paying foreign projection index discounted at foreign funding rate vs. domestic projection index discounted at domestic funding rate.
    /// </remarks>
    /// TBD: Remove. Replaced by FxCurveFit
    public static DiscountCurve FrwdFxCurveFit(string name, CalibratorSettings curveFitSettings,
      DiscountCurve foreignDiscount, DiscountCurve domesticDiscount,
      DiscountCurve foreignProjection, DiscountCurve domesticProjection,
      ReferenceIndex foreignIndex, ReferenceIndex domesticIndex,
      string category, InstrumentType[] instrumentTypes, bool[] spreadOnDomesticLeg,
      double[] quotes, Dt[] settles, Dt[] maturities, string[] tenors,
      double[] weights, Frequency[] freqs, BDConvention[] rolls,
      Calendar[] calendars, FxRate fxRate)
    {
      if (maturities == null || maturities.Length == 0) maturities = new Dt[quotes.Length];
      else if (maturities.Length != quotes.Length)
        throw new ArgumentException(String.Format("The numbers of quotes ({0}) and maturities ({1}) not match.",
                                                  quotes.Length, maturities.Length));
      if (tenors.Length != quotes.Length)
        throw new ArgumentException(String.Format("The numbers of quotes ({0}) and tenor names ({1}) not match.",
                                                  quotes.Length, tenors.Length));
      settles = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, settles, Dt.Empty, "quotes", "settles");
      weights = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, weights, 1.0, "quotes", "weights");
      freqs = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, freqs, Frequency.None, "quotes", "frequencies");
      rolls = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, rolls, BDConvention.None, "quotes", "frequencies");
      calendars = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, calendars, Calendar.None, "quotes",
                                                           "calendars");
      spreadOnDomesticLeg = DiscountCurveCalibrationUtils.CheckArray(quotes.Length, spreadOnDomesticLeg, true, "quotes",
                                                               "spreadOnDomestic");
      Dt asOf = curveFitSettings.CurveAsOf;
      var calibrator = new FxBasisFitCalibrator(asOf, foreignDiscount, domesticDiscount, foreignIndex,
                                                foreignProjection, domesticIndex, domesticProjection, fxRate,
                                                curveFitSettings);
      var curve = new DiscountCurve(calibrator)
                    {
                      Interp = curveFitSettings.GetInterp(),
                      Category = category ?? "None",
                      Ccy = foreignIndex.Currency,
                      Name = name
                    };
      int count = quotes.Length;
      for (int i = 0; i < count; ++i)
      {
        if (quotes[i] != 0)
        {
          InstrumentType itype = instrumentTypes[i];
          if (itype == InstrumentType.None)
            continue;
          Dt settle = settles[i].IsEmpty()
                        ? DiscountCurveCalibrationUtils.GetSettlement(itype, curveFitSettings.CurveAsOf, 0, calendars[i])
                        : settles[i];
          Dt maturity = maturities[i].IsEmpty()
                          ? DiscountCurveCalibrationUtils.GetMaturity(itype, settle, tenors[i], calendars[i], rolls[i])
                          : maturities[i];
          switch (itype)
          {
            case InstrumentType.BasisSwap:
              string tenorName = String.Format("{0}.{1}", Enum.GetName(typeof (InstrumentType), InstrumentType.BasisSwap), tenors[i]);
              curve.AddSwap(tenorName, weights[i], settle, maturity, quotes[i]*1e-4,
                            spreadOnDomesticLeg[i] ? Frequency.None : freqs[i],
                            spreadOnDomesticLeg[i] ? freqs[i] : Frequency.None, foreignIndex, domesticIndex, calendars[i],
                            new PaymentSettings
                              {
                                SpreadOnReceiver = !spreadOnDomesticLeg[i],
                                PrincipalExchange = true
                              });
              break;
            default:
              throw new ArgumentException(String.Format("Unknown instrument tyep: {0}.", instrumentTypes[i]));
          }
        }
      }
      if (logger.IsDebugEnabled) logger.DebugFormat("Creating FxBasisCurve {0}", curve.Name);
      curve.Fit();
      return curve;
    }
    #endregion

    #endregion Static Constructors

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name = "asOf">As of date</param>
    /// <param name = "foreignDiscount">Foreign discount curve</param>
    /// <param name = "domesticDiscount">Discount curve</param>
    /// <param name = "foreignIndex">Foreign projection index</param>
    /// <param name = "foreignProjection">Foreign projection curve</param>
    /// <param name = "domesticIndex">Domestic projection index</param>
    /// <param name = "domesticProjection">Domestic projection curve</param>
    /// <param name = "fxRate">Fx rate object </param>
    /// <param name = "curveFitSettings">Curve fit settings</param>
    public FxBasisFitCalibrator(Dt asOf, DiscountCurve foreignDiscount, DiscountCurve domesticDiscount,
                                ReferenceIndex foreignIndex, DiscountCurve foreignProjection,
                                ReferenceIndex domesticIndex, DiscountCurve domesticProjection, FxRate fxRate,
                                CalibratorSettings curveFitSettings)
      : base(fxRate.Spot, fxRate.Spot)
    {
      if (domesticDiscount == null || foreignDiscount == null)
        throw new ToolkitException("Domestic discount curve or foreign discount curve cannot be null");
      if (foreignIndex == null || domesticIndex == null)
        throw new ToolkitException("Domestic and foreign reference indices cannot be null");
      if (foreignProjection == null || domesticProjection == null)
        throw new ToolkitException("Domestic and foreign projection cannot be null");
      ForeignDiscount = foreignDiscount;
      DomesticDiscount = domesticDiscount;
      ForeignIndex = foreignIndex;
      DomesticIndex = domesticIndex;
      ForeignProjection = foreignProjection;
      DomesticProjection = domesticProjection;
      FxRate = fxRate;
      CurveFitSettings = curveFitSettings;
      if (CurveFitSettings != null)
        CurveFitSettings.CurveAsOf = fxRate.Spot;
      else
        CurveFitSettings = new CalibratorSettings(new CurveFitSettings(fxRate.Spot));
      CashflowCalibratorSettings = new CashflowCalibrator.CashflowCalibratorSettings();
      SetParentCurves(ParentCurves, DomesticDiscount, ForeignDiscount, DomesticProjection, ForeignProjection);
    }

    #endregion Constructors

    #region Calibration

    /// <summary>
    ///   Fit a curve from the specified tenor point
    /// </summary>
    /// <param name = "curve">Curve to calibrate</param>
    /// <param name = "fromIdx">Index to start fit from</param>
    /// <remarks>
    ///   <para>Derived calibrated curves implement this to do the work of the
    ///     fitting</para>
    ///   <para>Called by Fit() and Refit(). Child calibrators can assume
    ///     that the tenors have been validated and the data curve has
    ///     been cleared for a full refit (fromIdx = 0).</para>
    /// </remarks>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      FitCurve(curve, false);
      SetDependentCurves(curve, DomesticDiscount, ForeignDiscount, DomesticProjection, ForeignProjection);
      if (curve.ShiftOverlay == null)
      {
        // If not in the new sensitivities, we fit the inverse curve the old way.
        InverseFxBasisCurve = CalibrateInverseFxBasisCurve(InverseFxBasisCurve, curve, this);
      }
    }

    /// <summary>
    /// Parent curves
    /// </summary>
    public override IEnumerable<CalibratedCurve> EnumerateParentCurves()
    {
      if (DomesticDiscount != null)
        yield return DomesticDiscount;
      if (ForeignDiscount != null)
        yield return ForeignDiscount;
      if (DomesticProjection != null && DomesticProjection != DomesticDiscount)
        yield return DomesticProjection;
      if (ForeignProjection != null && ForeignProjection != ForeignDiscount)
        yield return ForeignProjection;
    }

    /// <summary>
    ///   Create a pricer equal to the one used for the basis curve calibration
    /// </summary>
    /// <param name = "curve">Calibrated curve</param>
    /// <param name = "product">Interest rate product</param>
    /// <returns>Instantianted pricer</returns>
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      return GetPricer((DiscountCurve) curve, product, false);
    }

    /// <summary>
    /// Calibrates the inverse fx basis curve.
    /// </summary>
    /// <remarks>
    /// <para>Consider a FX basis swap.  Let<ul>
    ///  <li><m>s_0</m> be the spot foreign exchange rate;</li>
    ///  <li><m>D_t</m> be the domestic discount factor;</li>
    ///  <li><m>F_t</m> be the foreign discount factor;</li>
    ///  <li><m>c_t</m> be the coupon paid on the domestic leg;</li>
    ///  <li><m>a_t</m> be the coupon paid on the foreign leg;</li>
    ///  <li><m>s_t</m> be the forward FX rates;</li>
    /// </ul>Assuming both legs have the same coupon dates.
    ///   The basis factors, <m>b_t</m>, are calibrated such that:
    /// <math>\mathrm{PV} \equiv \sum c_t\, D_t - \sum a_t\, s_t\, D_t = 0</math> where
    /// <math>s_t = s_0 \frac{b_t F_t}{D_t}</math>
    /// </para><para>
    /// An inverse swap is a swap with the evaluation currency swapped,
    ///  which has the present value given by<math>
    /// \mathrm{PV}_\mathrm{rev} \equiv \sum \frac{c_t}{s_t} F_t - \sum a_t F_t
    /// </math>Easy to see that<math>
    ///  \mathrm{PV}_\mathrm{rev} = \sum \left(\frac{c_t}{s_t} - a_t\right) F_t
    ///  = \sum \frac{ c_t - a_t s_t}{s_0\, b_t}\,D_t
    /// </math>Hence if <m>b_t</m> is not constant over time, then 
    /// <m>\mathrm{PV} = 0</m> does not necessarily implies that
    ///  <m>\mathrm{PV}_\mathrm{rev} = 0</m>.  We need to calibrate a new series
    ///  of basis factors, <m>\tilde{b}_t</m>, to match the inverse swap.
    /// </para>
    /// </remarks>
    /// <param name="targetCurve">The target fx curve</param>
    /// <param name="curve">The fx basis curve.</param>
    /// <param name="calibrator">The fx basis calibrator.</param>
    /// <returns>An inverse FX basis curve.</returns>
    private static DiscountCurve CalibrateInverseFxBasisCurve(
      DiscountCurve targetCurve,
      CalibratedCurve curve, FxBasisFitCalibrator calibrator)
    {
      // Create and fit the inverse curve
      if (targetCurve == null)
      {
        targetCurve = new DiscountCurve(new FxBasisInverseFitCalibrator(curve))
        {
          Interp = curve.Interp,
          Ccy = calibrator.DomesticIndex.Currency,
          Category = curve.Category,
          Name = curve.Name,
          Tenors = curve.Tenors,
          DependentCurves = curve.DependentCurves,
          ReferenceIndex = curve.ReferenceIndex
        };
      }
      calibrator.FitCurve(targetCurve, true);
      return targetCurve;
    }

    private static SwapPricer CreateSwapPricer(
      Dt asOf, Dt settle,
      SwapLeg payerLeg, SwapLeg receiverLeg,
      DiscountCurve domDiscount,
      DiscountCurve forDiscount,
      ReferenceIndex payIndex,
      ReferenceIndex recIndex,
      DiscountCurve payProjection,
      DiscountCurve recProjection,
      DiscountCurve fxBasisCurve,
      FxRate fxRate)
    {
      var fxCurve = new FxCurve(fxRate, fxBasisCurve, domDiscount, forDiscount, 
        String.Format("{0}/{1}-FxCurve", domDiscount.Ccy, forDiscount.Ccy));
      var payerPricer = new SwapLegPricer(payerLeg, asOf, settle, -fxRate.Rate,
        domDiscount, payIndex, payProjection, null, null, 
        domDiscount.Ccy != payProjection.Ccy ? fxCurve : null);
      var receiverPricer = new SwapLegPricer(receiverLeg, asOf, settle, 1.0,
        domDiscount, recIndex, recProjection, null, null, 
        domDiscount.Ccy != recProjection.Ccy ? fxCurve : null);
      var pricer = new SwapPricer(receiverPricer, payerPricer);
      pricer.Validate();
      return pricer;
    }

    private SwapPricer GetPricer(DiscountCurve curve,
      Dt asOf, Dt settle, Swap swap, bool inverse)
    {
      if (inverse)
      {
        var fx = FxRate.InverseFxRate();
        return CreateSwapPricer(asOf, settle, swap.ReceiverLeg, swap.PayerLeg,
          ForeignDiscount, DomesticDiscount, ForeignIndex, DomesticIndex,
          ForeignProjection, DomesticProjection, curve, fx);
      }
      return CreateSwapPricer(asOf, settle, swap.PayerLeg, swap.ReceiverLeg, 
        DomesticDiscount, ForeignDiscount, DomesticIndex, ForeignIndex,
        DomesticProjection, ForeignProjection, curve, FxRate);
    }

    private static Swap SwapFromSwapLeg(SwapLeg swapLeg,
      ReferenceIndex domesticIndex, ReferenceIndex foreignIndex)
    {
      SwapLeg receiver, payer;
      if (swapLeg.ReferenceIndex != null &&
          (swapLeg.ReferenceIndex == foreignIndex || swapLeg.ReferenceIndex.IndexName == foreignIndex.IndexName))
      {
        receiver = swapLeg;
        payer = new SwapLeg(receiver.Effective, receiver.Maturity, domesticIndex.IndexTenor.ToFrequency(), 0.0,
                            domesticIndex);
      }
      else
      {
        receiver = new SwapLeg(swapLeg.Effective, swapLeg.Maturity, foreignIndex.IndexTenor.ToFrequency(), 0.0,
                               foreignIndex);
        payer = swapLeg;
      }
      receiver.FinalExchange = payer.FinalExchange = swapLeg.FinalExchange;
      receiver.InitialExchange = payer.InitialExchange = swapLeg.InitialExchange;
      receiver.IntermediateExchange = payer.IntermediateExchange = swapLeg.IntermediateExchange;
      receiver.AccrueOnCycle = payer.AccrueOnCycle = true;
      return new Swap(receiver, payer);
    }

    private IPricer GetPricer(DiscountCurve curve, IProduct product, bool inverse)
    {
      if (product is Swap)
      {
        return GetPricer(curve, curve.AsOf, curve.AsOf, (Swap) product, inverse);
      }
      if (product is SwapLeg)
      {
        var swap = (SwapLeg) product;
        return GetPricer(curve, curve.AsOf, swap.Effective, SwapFromSwapLeg(
          swap, DomesticIndex, ForeignIndex), inverse);
      }
      if (product is FxForward || product is FxFuture)
      {
        var fx = !inverse ? FxRate : FxRate.InverseFxRate();
        var domDiscount = !inverse ? DomesticDiscount : ForeignDiscount;
        var forDiscount = !inverse ? ForeignDiscount : DomesticDiscount;
        var fxCurve = new FxCurve(fx, curve, domDiscount, forDiscount, $"{fx.FromCcy}/{fx.ToCcy}-FxCurve");
        var forward = product as FxForward;
        if (forward != null)
        {
          var fwd = forward;
          if (inverse)
          {
            fwd = new FxForward(fwd.Maturity, fx.FromCcy, fx.ToCcy, 1.0 / fwd.FxRate)
            {
              Description = product.Description
            };
          }
          return new FxForwardPricer(fwd, curve.AsOf, curve.AsOf, 1.0, fwd.PayCcy, domDiscount, fxCurve, null);
        }
        var fxFuture = product as FxFuture;
        if (fxFuture != null)
        {
           return new FxFuturePricer(fxFuture, curve.AsOf, curve.AsOf, fx.FromCcy, fxCurve, 1.0);  
        }
      }
      throw new ToolkitException("Product not supported");
    }

    private CashflowCalibrator FillData(CalibratedCurve curve, bool inverse)
    {
      curve.Tenors.UpdateProducts(curve.AsOf);
      DiscountCurveCalibrationUtils.SetCurveDates(curve.Tenors);
      var calibrator = new CashflowCalibrator(curve.AsOf);
      foreach (CurveTenor tenor in curve.Tenors)
      {
        if (tenor.Product is Swap || tenor.Product is SwapLeg)
        {
          var pricer = (SwapPricer)GetPricer((DiscountCurve)curve, tenor.Product, inverse);
          var payer = pricer.PayerSwapPricer;
          var receiver = pricer.ReceiverSwapPricer;
          var ps = receiver.GetPaymentSchedule(null, curve.AsOf);
          if (logger.IsDebugEnabled)
          {
            double psPv = payer.Pv(), cfPv = payer.CfPv(),
              diff = psPv.Equals(0.0) ? cfPv : (cfPv/psPv - 1);
            if (Math.Abs(diff) > 1E-15)
              logger.Debug($"PsPv = {psPv}, CfPv = {cfPv}, diff = {diff}");
          }
          calibrator.Add(-payer.Pv() / payer.DiscountCurve.Interpolate(payer.AsOf, payer.Settle), ps, pricer.Settle, receiver.DiscountCurve,
            tenor.CurveDate, tenor.Weight, true);
        }
        else if (tenor.Product is FxForward)
        {
          var pricer = (FxForwardPricer) GetPricer((DiscountCurve) curve, tenor.Product, inverse);
          var ps = pricer.GetPaymentSchedule(null, curve.AsOf);
          calibrator.Add(0.0, ps, pricer.Settle, pricer.DiscountCurve,
                         tenor.CurveDate, tenor.Weight, true);
        }
        else if (tenor.Product is FxFuture)
        {
          var pricer = (FxFuturePricer)GetPricer((DiscountCurve)curve, tenor.Product, inverse);
          var ps = pricer.GetPaymentSchedule(null, curve.AsOf);
          calibrator.Add(tenor.MarketPv, ps, Settle, null, tenor.CurveDate, tenor.Weight, true);
        }
        else
        {
          throw new ToolkitException(String.Format("Calibration to products of type {0} not handled",
                                                   tenor.Product.GetType()));
        }
      }
      return calibrator;
    }

    internal void FitCurve(CalibratedCurve curve, bool inverse)
    {
      CashflowCalibrator calibrator = FillData(curve, inverse);
      IModelParameter vol = null;
      if (CurveFitSettings.Method == CurveFitMethod.SmoothFutures && CurveFitSettings.FwdModelParameters != null)
        CurveFitSettings.FwdModelParameters.TryGetValue(Process.Projection, Parameter.Custom, out vol);
      if (CurveFitSettings.MaximumIterations >= 0)
      {
        CashflowCalibratorSettings.MaximumOptimizerIterations =
          CashflowCalibratorSettings.MaximumSolverIterations =
            CurveFitSettings.MaximumIterations;
      }
      double[] priceErrors;
      FittingErrorCode =
        calibrator.Calibrate(CurveFitSettings.Method, curve, CurveFitSettings.SlopeWeightCurve,
                             CurveFitSettings.CurvatureWeightCurve, vol, out priceErrors, CashflowCalibratorSettings);
      if (curve.Name == string.Empty)
        curve.Name = string.Concat("FxBasis", Enum.GetName(curve.Ccy.GetType(), curve.Ccy));
      if (logger.IsDebugEnabled) logger.DebugFormat("Fitted FxBasisCurve {0}", curve.Name);
    }

    #endregion Calibration

    #region Properties
    /// <summary>
    ///   Gets the fitting error code.
    /// </summary>
    /// <value>The fitting error code.</value>
    public CashflowCalibrator.OptimizerStatus FittingErrorCode { get; private set; }

    /// <summary>
    ///   The Domestic Discount Curve
    /// </summary>
    public DiscountCurve DomesticDiscount { get; private set; }

    /// <summary>
    ///   The Foreign Discount Curve
    /// </summary>
    public DiscountCurve ForeignDiscount { get; private set; }

    /// <summary>
    ///   Reference Index
    /// </summary>
    public ReferenceIndex ForeignIndex { get; private set; }

    /// <summary>
    ///   Domestic Index
    /// </summary>
    public ReferenceIndex DomesticIndex { get; private set; }

    /// <summary>
    ///   Domestic Projection Curve
    /// </summary>
    public DiscountCurve DomesticProjection { get; private set; }

    /// <summary>
    ///   Foreign projection curve
    /// </summary>
    public DiscountCurve ForeignProjection { get; private set; }

    /// <summary>
    ///   Fx Rate
    /// </summary>
    public FxRate FxRate { get; private set; }

    /// <summary>
    /// Settings for cashflow calibrator
    /// </summary>
    public CashflowCalibrator.CashflowCalibratorSettings CashflowCalibratorSettings { get; private set; }

    internal DiscountCurve InverseFxBasisCurve { get; private set; }
    #endregion Properties

    #region IInverseCurveProvider Members

    DiscountCurve IInverseCurveProvider.InverseCurve
    {
      get
      {
        // Make sure the inverse curve and the regular curve have the same tenors.
        var curve = InverseFxBasisCurve;
        if (curve == null) return curve;
        var cal = curve.Calibrator as FxBasisInverseFitCalibrator;
        if (cal == null) return curve;
        curve.Tenors = cal.RegularCurve.Tenors;
        return curve;
      }
    }

    #endregion
  }

  [Serializable]
  internal class FxBasisInverseFitCalibrator : DiscountCalibrator
  {
    internal CalibratedCurve RegularCurve { get; private set; }

    internal FxBasisInverseFitCalibrator(CalibratedCurve curve)
      : base(curve.Calibrator.AsOf, curve.Calibrator.Settle)
    {
      RegularCurve = curve;
    }

    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      var calibrator = (FxBasisFitCalibrator)RegularCurve.Calibrator;
      calibrator.FitCurve(curve, true);
    }

    /// <summary>
    /// Parent curves
    /// </summary>
    public override IEnumerable<CalibratedCurve> EnumerateParentCurves()
    {
      yield return RegularCurve;
    }

    /// <summary>
    /// Construct a pricer matching the model(s) used for calibration.
    /// </summary>
    /// <param name="curve">Curve to calibrate</param>
    /// <param name="product">Product to price</param>
    /// <returns>Constructed pricer for product</returns>
    /// <remarks></remarks>
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      var calibrator = (FxBasisFitCalibrator)RegularCurve.Calibrator;
      return calibrator.GetPricer(RegularCurve, product);
    }
  }

  internal interface IInverseCurveProvider
  {
    DiscountCurve InverseCurve { get; }
  }
}
