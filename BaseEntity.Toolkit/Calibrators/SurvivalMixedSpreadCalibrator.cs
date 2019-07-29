using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Use to construct a mixed credit curve from multi credit curves,
  /// and also allow clients to bump either mixed curve or component curves.
  /// </summary>
  [Serializable]
  public class SurvivalMixedSpreadCalibrator : SurvivalFitCalibrator
  {
    #region Constructor

    private SurvivalMixedSpreadCalibrator(Dt asOf,
      Dt settle,
      RecoveryCurve recoveryCurve,
      DiscountCurve discountCurve,
      SurvivalCurve[] componentCurves,
      double[] scalingFactors,
      double initialSpread,
      string[] tenorNames,
      Dt[] tenorDates,
      bool enableDirectBump)
      : base(asOf, settle, recoveryCurve, discountCurve)
    {
      _componentCurves = componentCurves;
      _scalingFactors = scalingFactors;
      _initialSpread = initialSpread;
      _tenorNames = tenorNames;
      _tenorDates = tenorDates;
      _enableDirectBump = enableDirectBump;
    }

    #endregion Constructor

    #region Methods

    /// <summary>
    /// Constructs a credit curve based on a mixture of existing credit curves. 
    /// We can either bump mixed curve or component curves.
    /// </summary>
    /// <remarks>
    ///   <para>Create a survival curve by mixing par CDS levels CDS from existing credit curves.</para>
    ///   <para>Each CDS is calculated as:</para>
    ///   <math>CDS_{i} = \sum_{j=1}^{n} CDS_{i}^{j} * \beta_{j} + spread</math>
    ///   <para>Where:</para>
    ///   <list type="bullet">
    ///     <item><description><m>CDS_{i}</m> is the calulated ith CDS</description></item>
    ///     <item><description><m>CDS_{i}^{j}</m> is the implied par CDS at the ith tenor from the jth survivalCurve</description></item>
    ///     <item><description><m>\beta_{j}</m> is the jth factor</description></item>
    ///     <item><description><m>spread</m> is the spread</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="asOf">Pricing as-of date or pricing date of first survival curve if not specified</param>
    /// <param name="settle">Effective date or effective date of first survival curve if not specified</param>
    /// <param name="ccy">Currency of curve or currency of first survival curve if not specified</param>
    /// <param name="category">Category for curve</param>
    /// <param name="discountCurve">Discount curve ir discount curve of first survival curve if not specified</param>
    /// <param name="tenorNames">Tenor names</param>
    /// <param name="tenorDates">Tenor dates. If not specified, market standard (IMM roll) dates are calculated from the tenors</param>
    /// <param name="survivalCurves">Survival curves</param>
    /// <param name="scalingFactors">Scaling factors to apply to each survival curve par CDS</param>
    /// <param name="spread">Final spread to add to mixed curves in basis points</param>
    /// <param name="recoveryRate">Recovery rate or recovery rate of first survival curve if not specified</param>
    /// <param name="directBumpEnabled">Flag to bump mixed curve directly or component curves</param>
    /// <param name="curveName">The name of the mixed curve</param>
    /// <returns>Mixed survival curve</returns>
    public static SurvivalCurve Mixed(
      Dt asOf,
      Dt settle,
      Currency ccy,
      string category,
      DiscountCurve discountCurve,
      string[] tenorNames,
      Dt[] tenorDates,
      SurvivalCurve[] survivalCurves,
      double[] scalingFactors,
      double spread,
      double recoveryRate,
      bool directBumpEnabled,
      string curveName = "")
    {
      if (survivalCurves == null || survivalCurves.Length < 1)
        throw new ArgumentException("Must specify at lease one survival curve");
      if (scalingFactors == null || scalingFactors.Length != survivalCurves.Length)
        throw new ArgumentException("Number of scaling factors must match number of survival curves");
      if (tenorDates != null && (tenorNames != null && tenorDates.Length != tenorNames.Length))
        throw new ArgumentException("Number of tenor dates not consistent with number of tenor names");
      var firstCalibrator = survivalCurves[0].Calibrator as SurvivalFitCalibrator;
      if (firstCalibrator == null)
        throw new ArgumentException("Component survival curves must be Calibrated");


      // Get defaults
      if (asOf.IsEmpty())
        asOf = survivalCurves[0].AsOf;
      if (settle.IsEmpty())
        settle = survivalCurves[0].Calibrator.Settle;
      if (discountCurve == null)
        discountCurve = firstCalibrator.DiscountCurve;
      if (recoveryRate < 0.0)
        recoveryRate = firstCalibrator.RecoveryCurve.RecoveryRate(settle);
      var recoveryCurve = new RecoveryCurve(asOf, recoveryRate);

      if (tenorNames == null || tenorNames.Length == 0)
      {
        if (tenorDates == null || tenorDates.Length == 0)
        {
          // No tenors specified, so use all tenors from first curve
          tenorNames = new string[survivalCurves[0].Tenors.Count];
          tenorDates = new Dt[survivalCurves[0].Tenors.Count];
          for (var i = 0; i < survivalCurves[0].Tenors.Count; i++)
          {
            tenorNames[i] = survivalCurves[0].Tenors[i].Name;
            tenorDates[i] = survivalCurves[0].Tenors[i].Maturity;
          }
        }
        else
        {
          tenorNames = new string[tenorDates.Length];
          for (int i = 0; i < tenorDates.Length; ++i)
          {
            tenorNames[i] = tenorDates[i].ToString();
          }
        }
      }
      else if (tenorDates == null || tenorDates.Length == 0)
      {
        // Tenor names specified but not tenor dates specified so imply 
        // from tenor names and first curve asOf date
        tenorDates = new Dt[tenorNames.Length];
        for (var i = 0; i < tenorNames.Length; i++)
          tenorDates[i] = Dt.CDSMaturity(survivalCurves[0].AsOf, tenorNames[i]);
      }

      // Create curve
      var calibrator = new SurvivalMixedSpreadCalibrator(asOf, settle,
        recoveryCurve, discountCurve, survivalCurves, scalingFactors,
        spread/10000.0, tenorNames, tenorDates, directBumpEnabled);
      var curve = calibrator.FitMixedCurve(category, ccy, curveName);
      curve.Calibrator = calibrator;
      if (!directBumpEnabled)
      {
        curve.Tenors = new CurveTenorCollection();
      }
      return curve;
    }

    /// <inheritdoc />
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      if (_enableDirectBump)
      {
        // Fit curve points using the tenors on the curve.
        base.FitFrom(curve, fromIdx);
        //update the quotes
        _quotes = curve.Tenors
          .Select(t => t.QuoteHandler.GetCurrentQuote(t).Value * 10000)
          .ToArray();
        return;
      }
      // Fit (by FitMixedCurve)and copy (by Set) curve points
      _quotes = CalculateMixedSpread();
      new OverlayWrapper(curve).Set(FitMixedCurve(
        curve.Category, curve.Ccy, curve.Name));
    }

    private SurvivalCurve FitMixedCurve(string category, Currency ccy, string name)
    {
      var parameters = SurvivalCurveParameters.GetDefaultParameters();
      var curve = SurvivalCurve.FitCDSQuotes(
        String.Empty, AsOf, Settle, ccy, category, false,
        CDSQuoteType.ParSpread, Double.NaN, parameters, DiscountCurve,
        _tenorNames, _tenorDates, MixedQuotes,
        new[] { RecoveryCurve.RecoveryRate(Settle) },
        0, null, null, 0, Double.NaN, null, false);
      curve.Name = name;
      return curve;
    }

    /// <inheritdoc />
    public override IEnumerable<CalibratedCurve> EnumerateParentCurves()
    {
      if (_enableDirectBump || _componentCurves == null || _componentCurves.Length == 0)
        return base.EnumerateParentCurves();
      return base.EnumerateParentCurves().Concat(_componentCurves);
    }

    private double[] CalculateMixedSpread()
    {
      var dates = _tenorDates;
      var tenors = _tenorNames;
      var quotes = new double[tenors.Length];
      for (var i = 0; i < tenors.Length; i++)
      {
        Dt maturity = dates[i];
        double spread = _initialSpread;
        for (var j = 0; j < _componentCurves.Length; j++)
        {
          spread += _componentCurves[j].ImpliedSpread(maturity)
            * _scalingFactors[j];
        }
        quotes[i] = spread * 10000;
      }
      return quotes;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///  Get the mixed spreads from the component survival curves.
    /// </summary>
    public double[] MixedQuotes
    {
      get { return _quotes ?? (_quotes = CalculateMixedSpread()); }
    }

    /// <summary>
    /// Flag of direct bump
    /// </summary>
    public bool EnableDirectBump
    {
      get { return _enableDirectBump; }
    }

    #endregion Properties

    #region Data

    private readonly SurvivalCurve[] _componentCurves;
    private readonly double[] _scalingFactors;
    private readonly double _initialSpread;
    private readonly string[] _tenorNames;
    private readonly Dt[] _tenorDates;
    private readonly bool _enableDirectBump;

    [Mutable]
    private double[] _quotes;

    #endregion Data


  }
}
