/*
 *   2014. All rights reserved.
 * Definition of all Scenario Shifts
 */

using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using BaseEntity.Shared;
using BaseEntity.Shared.Dynamic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Calibrators.Volatilities.Bump;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  /// Scenario shift type
  /// </summary>
  public enum ScenarioShiftType
  {
    /// <summary>
    /// No shift
    /// </summary>
    None = 0,

    /// <summary>
    /// Relative shift (in percentage)
    /// </summary>
    /// <remarks>
    /// <para>The Value to bump is set to Value * shift.</para>
    /// </remarks>
    Relative,

    /// <summary>
    /// Absolute shift
    /// </summary>
    /// <para>The Value to bump is set to Value + shift.</para>
    Absolute,

    /// <summary>
    /// Shift is specified value
    /// </summary>
    /// <para>The Value to bump is set to shift.</para>
    Specified
  }

  /// <summary>
  /// Scenario shift value
  /// </summary>
  public sealed class ScenarioValueShift
  {
    /// <summary>
    /// Constructor for scenario value shift object
    /// </summary>
    /// <param name="type">Scenario shift type, such as
    /// Absolute, Relative and Specified</param>
    /// <param name="value">Shift value</param>
    public ScenarioValueShift(ScenarioShiftType type, double value)
    {
      ShiftType = type;
      ShiftSize = value;
    }

    /// <summary>
    /// Scenario shift type, such as Absolute, Relative and Specified
    /// </summary>
    public ScenarioShiftType ShiftType { get; }

    /// <summary>
    /// Shift size
    /// </summary>
    public double ShiftSize { get; }
  }

  /// <summary>
  /// Scenario shift changing the properties of a Pricer or a Product
  /// </summary>
  /// <remarks>
  /// <para>Performs a scenario shift by setting the the parameters (properties) of <see cref="IPricer">Pricers</see>
  /// or terms (properties) of the underlying <see cref="IProduct">Products</see>.</para>
  /// <para>If a pricer does not have the specified parameter then the product's terms are searched. If the term is not
  /// found then it is ignored for that pricer.</para>
  /// <para><b>Notes:</b></para>
  /// <para>Any date can also be set to a number or a string. A number is interpreted as the
  /// number of days from the pricer asOf date. A string is interpreted as a period (eg 5 Days, 1 Year) from the
  /// pricer asOf date.</para>
  /// <para>Any DiscountCurve can be set to a double which is interpreted as a constant forward rate.</para>
  /// <para>Any SurvivalCurve can be set to a double which is interpreted as a constant hazard rate.</para>
  /// <para>Any VolatilitySurface can be set to a double which is interpreted as a flat volatility.</para>
  /// <para>Any StockCurve can be set to a double which is interpreted as the stock price.</para>
  /// <para>For convenience, a single name can be delimited by a semi-colon to indidate multiple terms
  /// to be set to the specified value. This is useful when the same conceptional value has different names in
  /// different pricers. E.g. StockFutureOptionBlackPricer.QuotedFuturePrice and StockFuturePricer.QuotedPrice
  /// are both the futures quoted price.</para>
  /// </remarks>
  /// <seealso cref="IScenarioShift"/>
  /// <seealso cref="Scenarios.CalcScenario(BaseEntity.Toolkit.Pricers.IPricer[],string,IScenarioShift[],bool,bool,System.Data.DataTable)"/>
  public class ScenarioShiftPricerTerms : IScenarioShift
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(ScenarioShiftPricerTerms));

    #region Constructors

    /// <summary>
    /// Create a shift to the properties of an pricer
    /// </summary>
    /// <param name="propertyNames">List of properties to shift</param>
    /// <param name="propertyValues">New values for properties</param>
    public ScenarioShiftPricerTerms(string[] propertyNames, object[] propertyValues)
    {
      if (propertyNames.Length == 1 && propertyNames[0].IndexOf(';') >= 0)
        // Single name with properties separated by semi-colons
        PropertyNames = propertyNames[0].Split(new[] {';'});
      else
        PropertyNames = propertyNames;
      if (PropertyNames.Length > 1 && propertyValues.Length == 1)
      {
        // Single property for multiple names
        PropertyValues = new object[PropertyNames.Length];
        for (var i = 0; i < PropertyNames.Length; i++)
          PropertyValues[i] = propertyValues[0];
      }
      else
        PropertyValues = propertyValues;
    }

    #endregion Constructors

    #region IScenarioShift

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
      if (!PropertyNames.IsNullOrEmpty() && (PropertyValues.IsNullOrEmpty() || PropertyNames.Length != PropertyValues.Length))
        throw new ArgumentException(String.Format("Number of property names ({0}) does not match number of property values ({1})",
          PropertyNames.Length, PropertyValues == null ? 0 : PropertyValues.Length));
    }

    /// <summary>
    /// Save state
    /// </summary>
    public void SaveState(PricerEvaluator[] evaluators)
    {
      SavedState = new object[evaluators.Length, PropertyValues.Length];
      for (var j = 0; j < evaluators.Length; j++)
      {
        for (var k = 0; k < PropertyNames.Length; ++k)
        {
          var name = PropertyNames[k];
          if (String.IsNullOrEmpty(name)) continue;
          // Figure out which object we are looking at
          var source = SourceObject(evaluators[j].Pricer, name);
          if (source == null) continue;
          // Save value, cloning any objects we may modify in-place
          var value = source.GetValue<object>(name);
          if (value is Curve)
            SavedState[j, k] = CloneUtil.Clone(value as Curve);
          else
            SavedState[j, k] = value;
        }
      }
    }

    /// <summary>
    /// Restore state then clear saved state
    /// </summary>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
      if (SavedState == null || SavedState.GetLength(0) != evaluators.Length)
        return;
      for (var j = 0; j < evaluators.Length; j++)
      {
        for (var k = 0; k < PropertyNames.Length; ++k)
        {
          var name = PropertyNames[k];
          if (String.IsNullOrEmpty(name)) continue;
          // Figure out which object we are looking at
          var source = SourceObject(evaluators[j].Pricer, name);
          if (source == null) continue;
          // Restore value, replacing and curves in-place
          var ovalue = SavedState[j, k];
          if (ovalue is Curve)
          {
            var nvalue = source.GetValue<object>(name);
            CurveUtil.CurveSet(new[] {nvalue as Curve}, new[] {ovalue as Curve});
          }
          else
            source.SetValue(name, ovalue);
        }
      }
      // Clear saved state
      SavedState = null;
    }

    /// <summary>
    /// Perform scenario shift
    /// </summary>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      foreach (var pricer in evaluators.Select(t => t.Pricer))
      {
        for (var k = 0; k < PropertyNames.Length; ++k)
        {
          var name = PropertyNames[k];
          if (String.IsNullOrEmpty(name)) continue;
          // Figure out which object we are looking at
          var source = SourceObject(pricer, name);
          if (source == null) continue;
          // Set value, handling special Dt cases and replacing any curves in-place
          var ovalue = source.GetValue<object>(name);
          var value = PropertyValues[k];
          var shift = value as ScenarioValueShift;
          if (shift != null)
          {
            if (!(ovalue is double))
              throw new ArgumentException("invalid operation.The property value is not double");
            var bumped = Scenarios.Bump((double) ovalue, shift.ShiftType, shift.ShiftSize);
            source.SetValue(name, bumped);
          }
          else if ((ovalue is Dt) && (value is int)) {
            // Dt assigned from a int is days from Pricer.AsOf date
            var nvalue = Dt.Add(pricer.AsOf, (int) value);
            logger.DebugFormat("Setting {0} {1} to Pricer.AsOf ({2}) + {3} days = {4}", source, name, pricer.AsOf, value, nvalue);
            source.SetValue(name, nvalue);
          } else if ((ovalue is Dt) && (value is double)) {
            // Dt assigned from a double is days from Pricer.AsOf date
            var nvalue = Dt.Add(pricer.AsOf, Convert.ToInt32((double)value));
            logger.DebugFormat("Setting {0} {1} to Pricer.AsOf ({2}) + {3} days = {4}", source, name, pricer.AsOf, value, nvalue);
            source.SetValue(name, nvalue);
          } else if ((ovalue is Dt) && (value is string)) {
            // Dt assigned from a string is tenor from Pricer.AsOf date
            var nvalue = Dt.Add(pricer.AsOf, (string)value);
            logger.DebugFormat("Setting {0} {1} to Pricer.AsOf ({2}) + {3} = {4}", source, name, pricer.AsOf, value, nvalue);
            source.SetValue(name, nvalue);
          } else if (ovalue is DiscountCurve) {
            // DiscountCurve set to flat forward rate
            logger.DebugFormat("Setting {0} {1} to DiscountCurve with flat forward rate {2}", source, name, value);
            ((DiscountCurve)ovalue).Copy(new DiscountCurve(pricer.AsOf, (double)value));
          } else if (ovalue is SurvivalCurve) {
            // SurvivalCurve set to flat hazard rate
            logger.DebugFormat("Setting {0} {1} to SurvivalCurve with flat hazard rate {2}", source, name, value);
            ((SurvivalCurve)ovalue).Copy(new SurvivalCurve(pricer.AsOf, (double)value));
          } else if (ovalue is StockCurve) {
            // StockCurve underlying price set
            logger.DebugFormat("Setting {0} {1} SpotPrice to {2}", source, name, value);
            ((StockCurve)ovalue).SpotPrice = (double)value;
          } else if (ovalue is VolatilitySurface) {
            // VolatilitySurface set to flat volatility, not done in-place for now
            logger.DebugFormat("Setting {0} {1} to VolatilitySurface with flat vol {2}", source, name, value);
            source.SetValue(name, CalibratedVolatilitySurface.FromFlatVolatility(pricer.AsOf, (double)value));
          }
          else
          {
            try
            {
              logger.DebugFormat("Setting {0} {1} to {2}", source, name, value);
              source.SetValue(name, value);
            }
            catch (Exception ex)
            {
              if (ovalue.GetType() == value.GetType())
                throw new ArgumentException(String.Format("Unable to set {0} to value {1} - {2}", name, value, ex));
              // Type mismatch. Note TypeCode is used to get a nicer ouput format
              throw new ArgumentException(
                String.Format("Unable to set {0} to value {1}. {0} can only be set to a {2}, not a {3}",
                  name, value, Type.GetTypeCode(ovalue.GetType()), Type.GetTypeCode(value.GetType())));
            }
          }
        }
        pricer.Reset();
      }
    }

    /// <summary>
    /// Refit all calibrated objects effected by the shift
    /// </summary>
    /// <param name="evaluators">Pricer evaluators to shift</param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    {}

    #endregion IScenarioShift

    #region Methods

#if USEBETTERDESCRIPTION // Wait to see if this needed. It may be an overkill. RTD Feb'14
    /// <summary>
    /// ToString
    /// </summary>
    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.Append("ScenarioShiftPricer:");
      var i = 0;
      while (i < PropertyNames.Length && i < PropertyValues.Length && i < 5)
      {
        if (i > 0) sb.Append(", ");
        sb.Append(PropertyNames[i]).Append("=").Append(PropertyValues[i]);
        ++i;
      }
      if (i < PropertyNames.Length || i < PropertyValues.Length)
        sb.Append("...");
      return sb.ToString();
    }
#endif

    /// <summary>
    /// Return Pricer or Product containing field
    /// </summary>
    private static object SourceObject(IPricer pricer, string name)
    {
      if (pricer.HasPropertyOrField(name))
        return pricer;
      if (pricer.Product.HasPropertyOrField(name))
        return pricer.Product;
      return null;
    }

    #endregion Methods

    #region Properties

    /// <summary>Pricer term (property) names</summary>
    public string[] PropertyNames { get; private set; }

    /// <summary>Pricer term (property) values</summary>
    public object[] PropertyValues { get; private set; }

    /// <summary>Saves state</summary>
    private object[,] SavedState { get; set; }

    #endregion Properties
  }

  /// <summary>
  /// Scenario shift bumping a set of curves
  /// </summary>
  /// <remarks>
  /// <para>Perform a shift by bumping a set of curves by a parallel shift.</para>
  /// <para>The shift may be absolute or relative and is in terms of the market
  /// quotes that were used to calibrate the curve. I.e. the
  /// market quotes are shifted then the curve is recalibrated.</para>
  /// <para>If <see cref="RefitDependentCurves"/> is set then any curves that depend
  /// on the shifted curves are automatically refitted after bumping. For example,
  /// any interest rate projection curves dependent on a
  /// OIS discount curve are refitted after the OIS curve is bumped.</para>
  /// <para>Absolute shifts are specified in basis points.</para>
  /// <para>This scenario works on any <see cref="CalibratedCurve"/>.</para>
  /// </remarks>
  /// <seealso cref="IScenarioShift"/>
  /// <seealso cref="Scenarios.CalcScenario(BaseEntity.Toolkit.Pricers.IPricer[],string,IScenarioShift[],bool,System.Data.DataTable)"/>
  public class ScenarioShiftCurves : IScenarioShift
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(ScenarioShiftCurves));

    #region Constructors

    /// <summary>
    /// Shift Curves
    /// </summary>
    /// <param name="curves">List if curves to shift</param>
    /// <param name="shifts">Size of shift for each curve (Absolute in bp)</param>
    /// <param name="shiftType">Type of shift</param>
    /// <param name="refitDependentCurves">Refit any dependent curves after bumping</param>
    public ScenarioShiftCurves(CalibratedCurve[] curves, double[] shifts, ScenarioShiftType shiftType, bool refitDependentCurves)
    {
      Curves = curves;
      Shifts = shifts;
      ShiftType = shiftType;
      RefitDependentCurves = refitDependentCurves;
    }

    #endregion Constructors

    #region IScenarioShift

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
      if (ShiftType == ScenarioShiftType.Specified)
        throw new ArgumentException("Specified shift value is not yet supported");
      if (!Curves.IsNullOrEmpty() && Shifts.IsNullOrEmpty())
        throw new ArgumentException("Shifts must be specified if curves are specified");
      if (!Shifts.IsNullOrEmpty() && Shifts.Length != 1 && Shifts.Length != Curves.Length)
        throw new ArgumentException(String.Format("Must specify one shift or a shift for each curve"));
    }

    /// <summary>
    /// Save state
    /// </summary>
    public void SaveState(PricerEvaluator[] evaluators)
    {
      if (NothingToDo) return;
      if (RefitDependentCurves)
      {
        // Find all the curves in the evaluator which depend on the curves to bump.
        var curves = evaluators.GetDependentCurves(Curves);
        AllCurves = GetComponentCurves(curves)
            .ToDependencyGraph(c => c.EnumeratePrerequisiteCurves()).ToArray();
      }
      else
      {
        AllCurves = GetComponentCurves(Curves).
            ToDependencyGraph(c => c.EnumeratePrerequisiteCurves()).ToArray();
      }
      // Make clones of the all curves.
      SavedAllCurves = AllCurves.IsNullOrEmpty() ? null : CloneUtil.Clone(AllCurves);
      SavedQuotes = AllCurves.IsNullOrEmpty()
        ? null
        : AllCurves.Select(c => c.Tenors.Select(GetQuote).ToList()).ToArray();
    }


    private static IList<CalibratedCurve> GetComponentCurves(
      IList<CalibratedCurve> curves)
    {
      var list = new List<CalibratedCurve>();
      foreach (var curve in curves)
      {
        if (!list.Contains(curve)) list.Add(curve);
        var fpCurve = curve as ForwardPriceCurve;
        if (fpCurve != null)
          fpCurve.GetComponentCurves<CalibratedCurve>(list);
      }
      return list;
    }

    private static IMarketQuote GetQuote(CurveTenor tenor)
    {
      return tenor.QuoteHandler != null
        ? tenor.QuoteHandler.GetCurrentQuote(tenor)
        : null;
    }

    /// <summary>
    /// Restore state then clear saved state
    /// </summary>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
      if (SavedAllCurves == null) return;
      AllCurves.SetQuotes(SavedQuotes).SetPoints(SavedAllCurves);
      SavedQuotes = null;
      SavedAllCurves = null;
    }

    /// <summary>
    /// Perform shift
    /// </summary>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      // Nothing to shift if no curve specified.
      if (NothingToDo) return;
      // Bump curves.
      for (var i = 0; i < Curves.Length; ++i)
      {
        if (Curves[i] == null) continue;
        var bump = (Shifts.Length == 1 ? Shifts[0] : Shifts[i]);
        if (bump.IsAlmostSameAs(0.0) || ShiftType == ScenarioShiftType.None) continue;
        logger.DebugFormat("Shifting Curve {0} by {1} {2}",
          Curves[i].Name, bump, ShiftType);
        CurveUtil.CurveBump(new[] {Curves[i]}, null, new[] {bump},
          ScenarioUtil.GetBumpFlags(true, ShiftType == ScenarioShiftType.Relative),
          false, null);
      }
    }

    /// <summary>
    /// Refit all calibrated objects effected by the shift
    /// </summary>
    /// <param name="evaluators">Pricer evaluators to shift</param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    {
      // Refit the curves.
      // AllCurves are arranged in the dependency order so the dependent curves
      // are fitted AFTER their prerequisites are fitted.
      if (NothingToDo) return;
      foreach (var curve in AllCurves.Where(curve => curve != null))
        curve.Calibrator?.ReFit(curve, 0);
    }

    #endregion IScenarioShift

#if USEBETTERDESCRIPTION // Wait to see if this needed. It may be an overkill. RTD Feb'14
    #region Methods

    /// <summary>
    /// ToString
    /// </summary>
    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.Append("ScenarioShiftPricer:");
      string asg = null;
      switch (ShiftType)
      {
        case ScenarioShiftType.Relative: asg = "*="; break;
        case ScenarioShiftType.Absolute: asg = "+="; break;
        case ScenarioShiftType.Specified: asg = "="; break;
      }
      var i = 0;
      while (i < Curves.Length && i < Shifts.Length && i < 5)
      {
        if (i > 0) sb.Append(", ");
        sb.Append(Curves[i].Name).Append(asg).Append(Shifts[i]);
        ++i;
      }
      if (i < Curves.Length || i < Shifts.Length)
        sb.Append("...");
      return sb.ToString();
    }

    #endregion Methods
#endif

    #region Properties

    /// <summary>
    /// Curves to bump
    /// </summary>
    public CalibratedCurve[] Curves { get; private set; }

    /// <summary>
    /// Shifts
    /// </summary>
    public double[] Shifts { get; private set; }

    /// <summary>
    /// Type of shift
    /// </summary>
    public ScenarioShiftType ShiftType { get; private set; }

    /// <summary>
    /// Refit any dependent curves after bumping
    /// </summary>
    public bool RefitDependentCurves { get; private set; }

    /// <summary>
    /// All curves effected by the shifts in dependency order
    /// </summary>
    private CalibratedCurve[] AllCurves { get; set; }

    /// <summary>
    /// Saved dependent curve states
    /// </summary>
    private CalibratedCurve[] SavedAllCurves { get; set; }

    /// <summary>
    /// Saved dependent curve states
    /// </summary>
    private IList<IMarketQuote>[] SavedQuotes { get; set; }

    /// <summary>
    /// No shift to perform
    /// </summary>
    private bool NothingToDo
    {
      get { return Curves.IsNullOrEmpty(); }
    }

    #endregion Properties
  }

  /// <summary>
  /// Scenario shift bumping credit curves
  /// </summary>
  /// <remarks>
  /// <para>Perform a shift by bumping a set of credit curves with a parallel shift of CDS spreads
  /// and a simultaneous parallel shift in recovery rates.</para>
  /// <para>CDS Spreads can be bumped in relative or absolute terms.
  /// Recoveries rates can be set to a specific recover rate or bumped in absolute terms.</para>
  /// <para>Shifts are in terms of the market quotes that were used to
  /// calibrate the curve. I.e. the market quotes are shifted then the curve is recalibrated.</para>
  /// <para>Credit curves can be defaulted using <see cref="ScenarioShiftDefaults"/>.</para>
  /// </remarks>
  /// <seealso cref="IScenarioShift"/>
  /// <seealso cref="Scenarios.CalcScenario(BaseEntity.Toolkit.Pricers.IPricer[],
  /// string,BaseEntity.Toolkit.Sensitivity.IScenarioShift[],bool,bool,System.Data.DataTable)"/>
  /// <seealso cref="ScenarioShiftDefaults"/>
  public class ScenarioShiftCreditCurves : IScenarioShift
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(ScenarioShiftCreditCurves));

    #region Constructors

    /// <summary>
    /// Shift Credit Curves only
    /// </summary>
    /// <param name="curves">List if curves to shift</param>
    /// <param name="spreadShifts">Spread shift for each curve (Absolute in bp)</param>
    /// <param name="spreadShiftType">Type of shift</param>
    public ScenarioShiftCreditCurves(SurvivalCurve[] curves, double[] spreadShifts, 
      ScenarioShiftType spreadShiftType)
      : this(curves, spreadShifts, spreadShiftType, null, ScenarioShiftType.None)
    {}

    /// <summary>
    /// Shift Credit Curves and recoveries
    /// </summary>
    /// <param name="curves">List if curves to shift</param>
    /// <param name="spreadShifts">Spread shift for each curve (Absolute in bp)</param>
    /// <param name="spreadShiftType">Type of shift</param>
    /// <param name="recoveryShifts">Recovery rate shifts for each curve</param>
    /// <param name="recoveryShiftType">Type of recovery rate shift</param>
    public ScenarioShiftCreditCurves(SurvivalCurve[] curves, double[] spreadShifts, 
      ScenarioShiftType spreadShiftType,
      double[] recoveryShifts, ScenarioShiftType recoveryShiftType)
    {
      Curves = curves;
      SpreadShifts = spreadShifts;
      SpreadShiftType = spreadShiftType;
      RecoveryShifts = recoveryShifts;
      RecoveryShiftType = recoveryShiftType;
    }

    #endregion Constructors

    #region IScenarioShift

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
      if (!SpreadShifts.IsNullOrEmpty() && SpreadShifts.Length != 1 
        && SpreadShifts.Length != Curves.Length)
        throw new ArgumentException(
          String.Format("If spread shifts specified, must have just one or one for each curve"));
      if (!RecoveryShifts.IsNullOrEmpty() && RecoveryShifts.Length != 1 
        && RecoveryShifts.Length != Curves.Length)
        throw new ArgumentException(
          String.Format("If recovery shifts specified, must have just one or one for each curve"));
    }

    /// <summary>
    /// Save state
    /// </summary>
    public void SaveState(PricerEvaluator[] evaluators)
    {
      if (NothingToDo) return;
      SavedState = (RecoveryShifts.IsNullOrEmpty()) 
        ? CloneUtil.Clone(Curves) : CurveUtil.CurveCloneWithRecovery(Curves);
    }

    /// <summary>
    /// Restore state then clear saved state
    /// </summary>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
      if (SavedState == null) return;
      if (RecoveryShifts.IsNullOrEmpty())
        CurveUtil.CurveSet(Curves, SavedState);
      else
        CurveUtil.CurveRestoreWithRecovery(Curves, SavedState);
      SavedState = null;
    }

    /// <summary>
    /// Perform shift
    /// </summary>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      if (NothingToDo) return;
      RefitCurve = new bool[Curves.Length];
      for (var i = 0; i < Curves.Length; ++i)
      {
        if (Curves[i] == null) continue;
        var curve = Curves[i];
        var calibrator = curve.SurvivalCalibrator;
        if (calibrator == null)
          throw new ArgumentException(
            String.Format("The curve '{0}' is not a calibrated curve", curve.Name));
        var isAlive = (curve.Defaulted == Defaulted.NotDefaulted);
        if (!RecoveryShifts.IsNullOrEmpty() && RecoveryShiftType != ScenarioShiftType.None)
        {
          var rBump = (RecoveryShifts.Length == 1 ? RecoveryShifts[0] : RecoveryShifts[i]);
          var rc = calibrator.RecoveryCurve;
          if (rc != null && !rBump.AlmostEquals(0.0) &&
            (isAlive || curve.Defaulted == Defaulted.WillDefault 
            || rc.Recovered == Recovered.WillRecover))
          {
            // Bump recovery rate ONLY when one of the following holds:
            //  (1) It is alive;
            //  (2) It will default in the future (as in hypothetical scenario analysis);
            //  (3) It is explicitly marked that the default will settle in the future.
            // Note: point (3) excludes the case where the Default Settle Date is empty.
            logger.DebugFormat("Shifting SurvivalCurve {0} Recovery by {1} {2}", 
              Curves[i].Name, rBump, RecoveryShiftType);
            switch (RecoveryShiftType)
            {
              case ScenarioShiftType.Absolute:
                rc.Spread += rBump;
                break;
              case ScenarioShiftType.Relative:
                // Convert relative shift to spread
                rc.Spread = rc.Interpolate(rc.AsOf) * rBump;
                break;
              case ScenarioShiftType.Specified:
                if (rc.Count > 1) rc.Shrink(1);
                rc.SetVal(0, rBump);
                break;
            }
            RefitCurve[i] = isAlive; // Refit only when the curve is alive.
          }
        }

        // Bump the curve or set it defaulted ONLY when it is alive.
        if (!SpreadShifts.IsNullOrEmpty() && isAlive && SpreadShiftType != ScenarioShiftType.None)
        {
          var bump = (SpreadShifts.Length == 1 ? SpreadShifts[0] : SpreadShifts[i]);
          if (bump.AlmostEquals(0.0)) continue;
          logger.DebugFormat("Shifting SurvivalCurve {0} Spreads by {1} {2}", 
            Curves[i].Name, bump, SpreadShiftType);
          CurveUtil.CurveBump(new[] {curve}, null, new[] {bump},
            ScenarioUtil.GetBumpFlags(true,
              SpreadShiftType == ScenarioShiftType.Relative), false, null);
          // Indicate that we need to refit
          RefitCurve[i] = true;
        }
      }
    }

    /// <summary>
    /// Refit all calibrated objects effected by the shift
    /// </summary>
    /// <param name="evaluators">Pricer evaluators to shift</param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    {
      if (NothingToDo) return;
      // Refit the curves.
      for( var i = 0; i < Curves.Length; ++i)
        if( RefitCurve[i] && Curves[i] != null )
          Curves[i].ReFit(0);
    }

    #endregion IScenarioShift

#if USEBETTERDESCRIPTION // Wait to see if this needed. It may be an overkill. RTD Feb'14
    #region Methods

    /// <summary>
    /// ToString
    /// </summary>
    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.Append("ScenarioShiftCreditCurves:");
      string asg = null;
      switch (SpreadShiftType)
      {
        case ScenarioShiftType.Relative: asg = "*="; break;
        case ScenarioShiftType.Absolute: asg = "+="; break;
        case ScenarioShiftType.Specified: asg = "="; break;
      }
      var i = 0;
      while (i < Curves.Length && i < SpreadShifts.Length && i < 5)
      {
        if (i > 0) sb.Append(", ");
        sb.Append(Curves[i].Name).Append(asg).Append(SpreadShifts[i]);
        ++i;
      }
      if (i < Curves.Length || i < SpreadShifts.Length)
        sb.Append("...");
      return sb.ToString();
    }

    #endregion Methods
#endif

    #region Properties

    /// <summary>
    /// True if recovery rates shifted
    /// </summary>
    public bool RecoveriesBumped
    {
      get { return !RecoveryShifts.IsNullOrEmpty(); }
    }

    /// <summary>
    /// Curves to shift
    /// </summary>
    public SurvivalCurve[] Curves { get; private set; }

    /// <summary>
    /// Spread Shifts
    /// </summary>
    public double[] SpreadShifts { get; private set; }

    /// <summary>
    /// Type of spread shift
    /// </summary>
    public ScenarioShiftType SpreadShiftType { get; private set; }

    /// <summary>
    /// Recovery rate Shifts
    /// </summary>
    public double[] RecoveryShifts { get; private set; }

    /// <summary>
    /// Type of recovery shift
    /// </summary>
    public ScenarioShiftType RecoveryShiftType { get; private set; }

    /// <summary>
    /// Flags indicating curve needs to be refitted
    /// </summary>
    private bool[] RefitCurve { get; set; }

    /// <summary>
    /// No shift to perform
    /// </summary>
    private bool NothingToDo
    {
      get
      {
        return Curves.IsNullOrEmpty() ||
          (SpreadShiftType == ScenarioShiftType.None && RecoveryShiftType == ScenarioShiftType.None);
      }
    }

    /// <summary>
    /// Saved state
    /// </summary>
    private CalibratedCurve[] SavedState { get; set; }

    #endregion Properties
  }

  /// <summary>
  /// Scenario shift specifying credit defaults
  /// </summary>
  /// <remarks>
  /// <para>Perform a shift by marking a set of credit curves as defaulted and optionally
  /// shifting the realised recovery rate for those defaulted curves.</para>
  /// <para>The recovery rate can be specified directly or as a shift in absolute or relative terms.</para>
  /// </remarks>
  /// <seealso cref="IScenarioShift"/>
  /// <seealso cref="Scenarios.CalcScenario(BaseEntity.Toolkit.Pricers.IPricer[],
  /// string,BaseEntity.Toolkit.Sensitivity.IScenarioShift[],bool,System.Data.DataTable)"/>
  public class ScenarioShiftDefaults : IScenarioShift
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(ScenarioShiftDefaults));

    #region Constructors

    /// <summary>
    /// Credit defaults
    /// </summary>
    /// <param name="curves">Credit curves to mark as defaulted</param>
    public ScenarioShiftDefaults(SurvivalCurve[] curves)
    {
      Curves = curves;
      RecoveryShifts = null;
      RecoveryShiftType = ScenarioShiftType.Absolute;
    }

    /// <summary>
    /// Credit defaults with recovery rate shifts
    /// </summary>
    /// <param name="curves">Credit curves to default</param>
    /// <param name="recoveryShifts">Recovery rate shifts for each curve</param>
    /// <param name="recoveryShiftType">Type of recovery rate shift</param>
    public ScenarioShiftDefaults(SurvivalCurve[] curves, double[] recoveryShifts, ScenarioShiftType recoveryShiftType)
    {
      Curves = curves;
      RecoveryShifts = recoveryShifts;
      RecoveryShiftType = recoveryShiftType;
    }

    #endregion Constructors

    #region IScenarioShift

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
      if (RecoveryShiftType == ScenarioShiftType.Relative)
        throw new ArgumentException("Relative recovery shift is not yet supported");
      if (!RecoveryShifts.IsNullOrEmpty() && RecoveryShifts.Length != 1 && RecoveryShifts.Length != Curves.Length)
        throw new ArgumentException(String.Format("If recoveries specified, must be one or one for each curve"));
    }

    /// <summary>
    /// Save state
    /// </summary>
    public void SaveState(PricerEvaluator[] evaluators)
    {
      if (NothingToDo) return;
      SavedState = (RecoveryShifts.IsNullOrEmpty()) ? CloneUtil.Clone(Curves) : CurveUtil.CurveCloneWithRecovery(Curves);
    }

    /// <summary>
    /// Restore state then clear saved state
    /// </summary>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
      if (SavedState == null) return;
      if (RecoveryShifts.IsNullOrEmpty())
        CurveUtil.CurveSet(Curves, SavedState);
      else
        CurveUtil.CurveRestoreWithRecovery(Curves, SavedState);
      SavedState = null;
    }

    /// <summary>
    /// Perform scenario shift
    /// </summary>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      if (NothingToDo) return;
      var settle = LastSettle(evaluators);
      for (var i = 0; i < Curves.Length; ++i)
      {
        if (Curves[i] == null) continue;
        var curve = Curves[i];
        if (curve.SurvivalCalibrator == null)
          throw new ArgumentException(String.Format("The curve '{0}' is not a calibrated curve", curve.Name));
        // Bump the curve or set it defaulted ONLY when it is alive.
        if (curve.Defaulted != Defaulted.NotDefaulted) continue;
        logger.DebugFormat("Marking SurvivalCurve {0} as defaulted", Curves[i].Name);
        curve.DefaultDate = settle;
        curve.Defaulted = Defaulted.WillDefault;
        if (RecoveryShifts.IsNullOrEmpty()) continue;
        var rBump = (RecoveryShifts.Length == 1 ? RecoveryShifts[0] : RecoveryShifts[i]);
        logger.DebugFormat("Shifting SurvivalCurve {0} Recoveries by {1} {2}", Curves[i].Name, rBump, RecoveryShiftType);
        var rc = curve.SurvivalCalibrator.RecoveryCurve;
        switch (RecoveryShiftType)
        {
          case ScenarioShiftType.Relative:
            // Convert relative shift to spread
            rc.Spread += rc.Interpolate(rc.AsOf) * rBump;
            break;
          case ScenarioShiftType.Absolute:
            rc.Spread += rBump;
            break;
          case ScenarioShiftType.Specified:
            if (rc.Count > 1) rc.Shrink(1);
            rc.SetVal(0, rBump);
            break;
          case ScenarioShiftType.None:
            break;
        }
      }
    }

    /// <summary>
    /// Refit all calibrated objects effected by the shift
    /// </summary>
    /// <param name="evaluators">Pricer evaluators to shift</param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    {}

    /// <summary>Utility to find the latest settlement date</summary>
    private static Dt LastSettle(PricerEvaluator[] evaluators)
    {
      var settle = new Dt(1, 1, 1990);
      foreach (var e in evaluators.Where(e => Dt.Cmp(settle, e.Settle) < 0))
        settle = e.Settle;
      return settle;
    }

    #endregion IScenarioShift

#if USEBETTERDESCRIPTION // Wait to see if this needed. It may be an overkill. RTD Feb'14
    #region Methods

    /// <summary>
    /// ToString
    /// </summary>
    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.Append("ScenarioShiftDefaults:");
      string asg = null;
      switch (RecoveryShiftType)
      {
        case ScenarioShiftType.Relative: asg = "*="; break;
        case ScenarioShiftType.Absolute: asg = "+="; break;
        case ScenarioShiftType.Specified: asg = "="; break;
      }
      var i = 0;
      while (i < Curves.Length && i < RecoveryShifts.Length && i < 5)
      {
        if (i > 0) sb.Append(", ");
        sb.Append(Curves[i].Name).Append(".Recovery").Append(asg).Append(RecoveryShifts[i]);
        ++i;
      }
      if (i < Curves.Length || i < RecoveryShifts.Length)
        sb.Append("...");
      return sb.ToString();
    }

    #endregion Methods
#endif

    #region Properties

    /// <summary>
    /// True if recovery rates shifted
    /// </summary>
    public bool RecoveriesBumped
    {
      get { return !RecoveryShifts.IsNullOrEmpty(); }
    }

    /// <summary>
    /// credit curves
    /// </summary>
    public SurvivalCurve[] Curves { get; private set; }

    /// <summary>
    /// Recovery rate Shifts
    /// </summary>
    public double[] RecoveryShifts { get; private set; }

    /// <summary>
    /// Type of recovery shift
    /// </summary>
    public ScenarioShiftType RecoveryShiftType { get; private set; }

    /// <summary>
    /// No shift to perform
    /// </summary>
    private bool NothingToDo
    {
      get { return Curves.IsNullOrEmpty(); }
    }
    
    /// <summary>
    /// Saved state
    /// </summary>
    private CalibratedCurve[] SavedState { get; set; }

    #endregion Properties
  }

  /// <summary>
  /// Scenario shift bumping fx curves
  /// </summary>
  /// <remarks>
  /// <para>Perform a shift by shifting fx rates and fx basis rates.</para>
  /// <para>The shifts in fx rates and fx basis rates may be absolute or relative
  /// and are in terms of the market quotes that were used to calibrate the curve. I.e. the
  /// market quotes are shifted then the curve is recalibrated.</para>
  /// </remarks>
  /// <seealso cref="IScenarioShift"/>
  /// <seealso cref="Scenarios.CalcScenario(BaseEntity.Toolkit.Pricers.IPricer[],
  /// string,BaseEntity.Toolkit.Sensitivity.IScenarioShift[],bool,System.Data.DataTable)"/>
  public class ScenarioShiftFxCurves : IScenarioShift
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(ScenarioShiftFxCurves));

    #region Constructors

    /// <summary>
    /// Shift fx rates
    /// </summary>
    /// <param name="fxCurves">List if fx curves to shift</param>
    /// <param name="fxShifts">Fx shifts for each fx curve</param>
    /// <param name="fxShiftType">Type of fx rate shift</param>
    /// <param name="basisShifts">Fx basis shifts for each fx curve</param>
    /// <param name="basisShiftType">Type of basis shift</param>
    public ScenarioShiftFxCurves(FxCurve[] fxCurves, double[] fxShifts, ScenarioShiftType fxShiftType,
      double[] basisShifts, ScenarioShiftType basisShiftType)
    {
      FxCurves = fxCurves;
      FxShifts = fxShifts;
      FxShiftType = fxShiftType;
      BasisShifts = basisShifts;
      BasisShiftType = basisShiftType;
    }

    #endregion Constructors

    #region IScenarioShift

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
      if (FxCurves.IsNullOrEmpty()) return;
      if (FxShiftType == ScenarioShiftType.Specified)
        throw new ArgumentException("Specified fx shift is not yet supported");
      if (BasisShiftType == ScenarioShiftType.Specified)
        throw new ArgumentException("Specified fx basis shift is not yet supported");
      if (!FxShifts.IsNullOrEmpty() && FxShifts.Length != 1 
        && FxShifts.Length != FxCurves.Length)
        throw new ArgumentException(
          String.Format("If fx shifts specified, must have just one or one for each curve"));
      if (!BasisShifts.IsNullOrEmpty() && BasisShifts.Length != 1 
        && BasisShifts.Length != FxCurves.Length)
        throw new ArgumentException(
          String.Format("If fx basis shifts specified, must have just one or one for each curve"));
    }

    /// <summary>
    /// Save state
    /// </summary>
    public void SaveState(PricerEvaluator[] evaluators)
    {
      if (NothingToDo) return;
      SavedState = FxCurves.Select(FxCurveState.Create).ToArray();
    }

    /// <summary>
    /// Restore state then clear saved state
    /// </summary>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
      // Restore FxCurves
      if (SavedState == null) return;
      for (var i = 0; i < SavedState.Length; ++i)
      {
        SavedState[i]?.Restore();
      }
      SavedState = null;
    }

    /// <summary>
    /// Perform shift
    /// </summary>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      if (NothingToDo) return;
      if (FxCurves.IsNullOrEmpty()) return;
      for (var i = 0; i < FxCurves.Length; ++i)
      {
        var fxCurve = FxCurves[i];
        if (fxCurve == null) continue;
        if (FxShifts != null)
        {
          var spotBump = (FxShifts.Length == 1) ? FxShifts[0] : FxShifts[i];
          if (!spotBump.AlmostEquals(0.0) && FxShiftType != ScenarioShiftType.None)
          {
            logger.DebugFormat("Shifting FxCurve {0} Spot Rate by {1} {2}",
              FxCurves[i].Name, spotBump, FxShiftType);
            CurveUtil.CurveBump(new[] {fxCurve}, new[]
            {
              fxCurve.Tenors.Where(t => t.Product is FxForward).OrderBy(t => t.Maturity)
                .First().Name
            }, new[] {spotBump}, ScenarioUtil.GetBumpFlags(true,
              FxShiftType == ScenarioShiftType.Relative), false, null);
          }
        }
        if (fxCurve.BasisCurve == null || BasisShifts == null) continue;
        var basisBump = (BasisShifts.Length == 1) ? BasisShifts[0] : BasisShifts[i];
        if (basisBump.AlmostEquals(0.0) || BasisShiftType == ScenarioShiftType.None) continue;
        logger.DebugFormat("Shifting FxCurve {0} Basis Spreads by {1} {2}", 
          FxCurves[i].Name, basisBump, BasisShiftType);
        CurveUtil.CurveBump(new[] {fxCurve.BasisCurve}, null, new[] {basisBump},
          ScenarioUtil.GetBumpFlags(true, BasisShiftType == ScenarioShiftType.Relative),
          false, null);
      }
    }

    /// <summary>
    /// Refit all calibrated objects effected by the shift
    /// </summary>
    /// <param name="evaluators">Pricer evaluators to shift</param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    {
      if (NothingToDo) return;
      // Refit the curves.
      foreach (var fxCurve in FxCurves.Where(fxCurve => fxCurve != null))
      {
        fxCurve.ReFit(0);
        if (fxCurve.BasisCurve != null)
          fxCurve.BasisCurve.ReFit(0);
      }
    }

    #endregion IScenarioShift

#if USEBETTERDESCRIPTION // Wait to see if this needed. It may be an overkill. RTD Feb'14
    #region Methods

    /// <summary>
    /// ToString
    /// </summary>
    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.Append("ScenarioShiftFxCurves:");
      string asg = null;
      switch (FxShiftType)
      {
        case ScenarioShiftType.Relative: asg = "*="; break;
        case ScenarioShiftType.Absolute: asg = "+="; break;
        case ScenarioShiftType.Specified: asg = "="; break;
      }
      var i = 0;
      while (i < FxCurves.Length && i < FxShifts.Length && i < 5)
      {
        if (i > 0) sb.Append(", ");
        sb.Append(FxCurves[i].Name).Append(asg).Append(FxShifts[i]);
        ++i;
      }
      if (i < FxCurves.Length || i < FxShifts.Length)
        sb.Append("...");
      return sb.ToString();
    }

    #endregion Methods
#endif

    #region Properties

    /// <summary>
    /// FX curves to shift
    /// </summary>
    public FxCurve[] FxCurves { get; private set; }

    /// <summary>
    /// FX Shifts
    /// </summary>
    public double[] FxShifts { get; private set; }

    /// <summary>
    /// Type of fx shift
    /// </summary>
    public ScenarioShiftType FxShiftType { get; private set; }

    /// <summary>
    /// Fx Basis Shifts
    /// </summary>
    public double[] BasisShifts { get; private set; }

    /// <summary>
    /// Type of basis shift
    /// </summary>
    public ScenarioShiftType BasisShiftType { get; private set; }

    /// <summary>
    /// No shift to perform
    /// </summary>
    private bool NothingToDo
    {
      get
      {
        return FxCurves.IsNullOrEmpty() ||
          (FxShiftType == ScenarioShiftType.None && BasisShiftType == ScenarioShiftType.None);
      }
    }

    #endregion Properties

    #region FxCurveState

    /// <summary>
    /// Saved state
    /// </summary>
    private FxCurveState[] SavedState { get; set; }

    private class FxCurveState
    {
      private readonly double _savedSpotRate;
      private readonly FxCurve _fxCurve;
      private readonly Curve[] _saved, _original;

      private FxCurveState(FxCurve fxCurve)
      {
        _fxCurve = fxCurve;
        _savedSpotRate = fxCurve.SpotRate;
        if (fxCurve.IsSupplied)
        {
          var orig = fxCurve.GetComponentCurves<FxForwardCurve>(null).FirstOrDefault();
          if (orig == null) return;
          _original = new Curve[] { orig };
          _saved = new Curve[] { CloneUtil.Clone(orig) };
          return;
        }
        var basis = fxCurve.BasisCurve;
        var inverse = (basis.Calibrator as FxBasisFitCalibrator)?.InverseFxBasisCurve;
        _original = new Curve[] { basis, inverse };
        _saved = new Curve[] { CloneUtil.Clone(basis), CloneUtil.Clone(inverse) };
      }

      public static FxCurveState Create(FxCurve c)
      {
        return c == null ? null : new FxCurveState(c);
      }

      public void Restore()
      {
        _fxCurve.SpotFxRate.Rate = _savedSpotRate;
        _original?.CurveSet(_saved);
      }
    }

    #endregion FxCurveSate
  }

  /// <summary>
  /// Scenario shift bumping a set of stock curves
  /// </summary>
  /// <remarks>
  /// <para>Perform a shift by bumping a set of stock by a parallel shift 
  /// to the current stock price and a parallel shift
  /// to the dividend yield.</para>
  /// <para>The shift may be absolute or relative.</para>
  /// </remarks>
  /// <seealso cref="IScenarioShift"/>
  /// <seealso cref="Scenarios.CalcScenario(BaseEntity.Toolkit.Pricers.IPricer[],
  /// string,BaseEntity.Toolkit.Sensitivity.IScenarioShift[],bool,System.Data.DataTable)"/>
  public class ScenarioShiftStockCurves : IScenarioShift
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(ScenarioShiftStockCurves));

    #region Constructors

    /// <summary>
    /// Shift Curves
    /// </summary>
    /// <param name="curves">List if StockCurves to shift</param>
    /// <param name="stockPriceShifts">Size of shift for each stock price</param>
    /// <param name="stockPriceShiftType">Type of stock price shift</param>
    /// <param name="dividendYieldShifts">Size of shift for each dividend yield</param>
    /// <param name="dividendYieldShiftType">Type of dividend yield shift</param>
    public ScenarioShiftStockCurves(StockCurve[] curves, double[] stockPriceShifts, 
      ScenarioShiftType stockPriceShiftType,
      double[] dividendYieldShifts, ScenarioShiftType dividendYieldShiftType)
    {
      Curves = curves;
      StockPriceShifts = stockPriceShifts;
      StockPriceShiftType = stockPriceShiftType;
      DividendYieldShifts = dividendYieldShifts;
      DividendYieldShiftType = dividendYieldShiftType;
    }

    #endregion Constructors

    #region IScenarioShift

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
      if (Curves.IsNullOrEmpty()) return;
      if (DividendYieldShiftType != ScenarioShiftType.Absolute 
        && DividendYieldShiftType != ScenarioShiftType.None)
        throw new ArgumentException(
          "Only absolute dividend yield shift are currently supported");
      if (!StockPriceShifts.IsNullOrEmpty() && StockPriceShifts.Length != 1 
        && StockPriceShifts.Length != Curves.Length)
        throw new ArgumentException(
          String.Format("Must specify one shift or a shift for each curve"));
      if (!DividendYieldShifts.IsNullOrEmpty() && DividendYieldShifts.Length != 1 
        && DividendYieldShifts.Length != Curves.Length)
        throw new ArgumentException(
          String.Format("Must specify one shift or a shift for each curve"));
    }

    /// <summary>
    /// Save state
    /// </summary>
    public void SaveState(PricerEvaluator[] evaluators)
    {
      if (NothingToDo) return;
      SavedStockPrices = new double[Curves.Length];
      SavedDividendSpreads = new double[Curves.Length];
      for (var i = 0; i < Curves.Length; ++i)
      {
        SavedStockPrices[i] = Curves[i].SpotPrice;
        if (DividendYieldShifts.IsNullOrEmpty() || Curves[i].TargetCurve == null 
          || DividendYieldShiftType == ScenarioShiftType.None) continue;
        SavedDividendSpreads[i] = Curves[i].TargetCurve.Spread;
      }
    }

    /// <summary>
    /// Restore state then clear saved state
    /// </summary>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
      if (SavedStockPrices == null) return;
      for (var i = 0; i < Curves.Length; ++i)
      {
        var stockCurve = Curves[i];
        if (stockCurve == null) continue;
        stockCurve.SpotPrice = SavedStockPrices[i];
        foreach (CurveTenor tenor in stockCurve.Tenors)
        {
          var spot = tenor?.Product as SpotAsset;
          if (spot == null) continue;
          tenor.MarketPv = SavedStockPrices[i];
          spot.SpotPrice = SavedStockPrices[i];
        }
        stockCurve.SpotPriceCurve?.ReFit(0);
        if (DividendYieldShifts.IsNullOrEmpty() || stockCurve.TargetCurve == null 
          || DividendYieldShiftType == ScenarioShiftType.None) continue;
        stockCurve.TargetCurve.Spread = SavedDividendSpreads[i];
      }
      SavedStockPrices = null;
      SavedDividendSpreads = null;
    }

    /// <summary>
    /// Perform shift
    /// </summary>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      // Nothing to shift if no curve specified.
      if (NothingToDo) return;

      // Bump curves.
      for (var i = 0; i < Curves.Length; ++i)
      {
        var stockCurve = Curves[i];
        if (stockCurve == null) continue;
        var stockBump = StockPriceShifts.IsNullOrEmpty()
          ? 0.0
          : (StockPriceShifts.Length == 1
            ? StockPriceShifts[0]
            : StockPriceShifts[i]);
        if (!stockBump.IsAlmostSameAs(0.0))
        {
          logger.DebugFormat("Shifting StockCurve {0} Stock Price by {1} {2}", 
            stockCurve.Name, stockBump, StockPriceShiftType);

          var bumpAmount = GetStockCurveBumpAmount(stockCurve, stockBump, StockPriceShiftType);
          stockCurve.SpotPrice += bumpAmount;

          foreach (CurveTenor tenor in stockCurve.Tenors)
          {
            var spot = tenor?.Product as SpotAsset;
            if (spot == null) continue;
            var bumpedSpotPrice = stockCurve.SpotPrice;
            tenor.MarketPv = bumpedSpotPrice;
            spot.SpotPrice = bumpedSpotPrice;
          }
          stockCurve.SpotPriceCurve?.ReFit(0);
        }
        if (DividendYieldShifts.IsNullOrEmpty()) continue;
        var divBump = (DividendYieldShifts.Length == 1
          ? DividendYieldShifts[0]
          : DividendYieldShifts[i]);
        if (divBump.IsAlmostSameAs(0.0) || Curves[i].TargetCurve == null 
          || DividendYieldShiftType == ScenarioShiftType.None) continue;
        logger.DebugFormat("Shifting StockCurve {0} Dividend Yield by {1} {2}", 
          Curves[i].Name, divBump, DividendYieldShiftType);
        Curves[i].TargetCurve.Spread += divBump;
      }
    }


    private static double GetStockCurveBumpAmount(StockCurve stockCurve, 
      double bumpSize, ScenarioShiftType shiftType)
    {
      var originalQuote = stockCurve.SpotPrice;
      var bumpAmount = 0.0;
      switch (shiftType)
      {
        case ScenarioShiftType.Relative:
          bumpAmount = originalQuote*bumpSize;
          break;
        case ScenarioShiftType.Absolute:
          bumpAmount = bumpSize;
          break;
        case ScenarioShiftType.Specified:
          bumpAmount = bumpSize - originalQuote;
          break;
      }
      return bumpAmount;
    }



    /// <summary>
    /// Refit all calibrated objects effected by the shift
    /// </summary>
    /// <param name="evaluators">Pricer evaluators to shift</param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    {}

    #endregion IScenarioShift

#if USEBETTERDESCRIPTION // Wait to see if this needed. It may be an overkill. RTD Feb'14
    #region Methods

    /// <summary>
    /// ToString
    /// </summary>
    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.Append("ScenarioShiftStockCurves:");
      string asg = null;
      switch (StockPriceShiftType)
      {
        case ScenarioShiftType.Relative: asg = "*="; break;
        case ScenarioShiftType.Absolute: asg = "+="; break;
        case ScenarioShiftType.Specified: asg = "="; break;
      }
      var i = 0;
      while (i < Curves.Length && i < StockPriceShifts.Length && i < 5)
      {
        if (i > 0) sb.Append(", ");
        sb.Append(Curves[i].Name).Append(asg).Append(StockPriceShifts[i]);
        ++i;
      }
      if (i < Curves.Length || i < StockPriceShifts.Length)
        sb.Append("...");
      return sb.ToString();
    }

    #endregion Methods
#endif

    #region Properties

    /// <summary>
    /// Stock Curves to shift
    /// </summary>
    public StockCurve[] Curves { get; private set; }

    /// <summary>
    /// Stock price shifts
    /// </summary>
    public double[] StockPriceShifts { get; private set; }

    /// <summary>
    /// Type of stock price shift
    /// </summary>
    public ScenarioShiftType StockPriceShiftType { get; private set; }

    /// <summary>
    /// Dividend yield shifts
    /// </summary>
    public double[] DividendYieldShifts { get; private set; }

    /// <summary>
    /// Type of dividend yield shift
    /// </summary>
    public ScenarioShiftType DividendYieldShiftType { get; private set; }

    /// <summary>
    /// No shift to perform
    /// </summary>
    private bool NothingToDo
    {
      get
      {
        return Curves.IsNullOrEmpty() ||
          (StockPriceShiftType == ScenarioShiftType.None 
          && DividendYieldShiftType == ScenarioShiftType.None);
      }
    }

    /// <summary>
    /// Saved state
    /// </summary>
    private double[] SavedStockPrices { get; set; }
    private double[] SavedDividendSpreads { get; set; }

    #endregion Properties
  }

  /// <summary>
  /// Scenario shift bumping correlations
  /// </summary>
  /// <remarks>
  /// <para>Perform a shift by bumping correlations with a uniform shift.</para>
  /// <para>The shift in correlations may be absolute or relative.</para>
  /// </remarks>
  /// <seealso cref="IScenarioShift"/>
  /// <seealso cref="Scenarios.CalcScenario(BaseEntity.Toolkit.Pricers.IPricer[],
  /// string,BaseEntity.Toolkit.Sensitivity.IScenarioShift[],bool,System.Data.DataTable)"/>
  public class ScenarioShiftCorrelation : IScenarioShift
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(ScenarioShiftCorrelation));

    #region Constructors

    /// <summary>
    /// Shift correlations
    /// </summary>
    /// <param name="correlations">Correlation surfaces to bump</param>
    /// <param name="shifts">Correlation shifts</param>
    /// <param name="shiftType">Type of shift</param>
    public ScenarioShiftCorrelation(CorrelationObject[] correlations, 
      double[] shifts, ScenarioShiftType shiftType)
    {
      Correlations = correlations;
      Shifts = shifts;
      ShiftType = shiftType;
    }

    #endregion Constructors

    #region IScenarioShift

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
      if (ShiftType == ScenarioShiftType.Specified)
        throw new ArgumentException("Specified correlation shift is not yet supported");
      if (!Shifts.IsNullOrEmpty() && Shifts.Length != 1 
        && Shifts.Length != Correlations.Length)
        throw new ArgumentException(
          String.Format("Must specify one shift or a shift for each correlation surface"));
    }

    /// <summary>
    /// Save state
    /// </summary>
    public void SaveState(PricerEvaluator[] evaluators)
    {
      if (NothingToDo) return;
      SavedState = CloneUtil.Clone(Correlations);
    }

    /// <summary>
    /// Restore state then clear saved state
    /// </summary>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
      if (SavedState == null) return;
      for (var i = 0; i < Correlations.Length; ++i)
      {
        if (Correlations[i] == null) continue;
        Correlations[i].SetCorrelations(SavedState[i]);
        Correlations[i].Modified = SavedState[i].Modified;
      }
      SavedState = null;
    }

    /// <summary>
    /// Perform scenario shift
    /// </summary>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      if (NothingToDo) return;
      for (var i = 0; i < Correlations.Length; ++i)
      {
        if (Correlations[i] == null) continue;
        double bump = (Shifts.Length == 1 ? Shifts[0] : Shifts[i]);
        if (bump.AlmostEquals(0.0) || ShiftType == ScenarioShiftType.None) continue;
        logger.DebugFormat("Shifting Correlations {0} by {1} {2}", 
          Correlations[i].Name, bump, ShiftType);
        Correlations[i].BumpCorrelations(bump, ShiftType == ScenarioShiftType.Relative, false);
        Correlations[i].Modified = true;
      }
    }

    /// <summary>
    /// Refit all calibrated objects effected by the shift
    /// </summary>
    /// <param name="evaluators">Pricer evaluators to shift</param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    {}

    #endregion IScenarioShift

#if USEBETTERDESCRIPTION // Wait to see if this needed. It may be an overkill. RTD Feb'14
    #region Methods

    /// <summary>
    /// ToString
    /// </summary>
    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.Append("ScenarioShiftCorrelation:");
      string asg = null;
      switch (ShiftType)
      {
        case ScenarioShiftType.Relative: asg = "*="; break;
        case ScenarioShiftType.Absolute: asg = "+="; break;
        case ScenarioShiftType.Specified: asg = "="; break;
      }
      var i = 0;
      while (i < Correlations.Length && i < Shifts.Length && i < 5)
      {
        if (i > 0) sb.Append(", ");
        sb.Append(Correlations[i].Name).Append(asg).Append(Shifts[i]);
        ++i;
      }
      if (i < Correlations.Length || i < Shifts.Length)
        sb.Append("...");
      return sb.ToString();
    }

    #endregion Methods
#endif

    #region Properties

    /// <summary>
    /// Correlations to shift
    /// </summary>
    public CorrelationObject[] Correlations { get; private set; }

    /// <summary>
    /// Correlation shifts
    /// </summary>
    public double[] Shifts { get; private set; }

    /// <summary>
    /// Type of correlation shift
    /// </summary>
    public ScenarioShiftType ShiftType { get; private set; }

    /// <summary>
    /// Saved state
    /// </summary>
    private CorrelationObject[] SavedState { get; set; }

    /// <summary>
    /// No shift to perform
    /// </summary>
    private bool NothingToDo
    {
      get { return Correlations.IsNullOrEmpty() || ShiftType == ScenarioShiftType.None; }
    }

    #endregion Properties
  }

  /// <summary>
  /// Scenario shift bumping volatilities
  /// </summary>
  /// <remarks>
  /// <para>Perform a shift by bumping volatilities with a uniform shift.</para>
  /// <para>The shift in volatilities may be absolute or relative.</para>
  /// </remarks>
  /// <seealso cref="IScenarioShift"/>
  /// <seealso cref="Scenarios.CalcScenario(BaseEntity.Toolkit.Pricers.IPricer[],
  /// string,BaseEntity.Toolkit.Sensitivity.IScenarioShift[],bool,System.Data.DataTable)"/>
  public class ScenarioShiftVolatilities : IScenarioShift
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (ScenarioShiftVolatilities));

    #region Constructors

    /// <summary>
    /// Shift volatilities
    /// </summary>
    /// <param name="volatilities">Volatility surfaces to bump</param>
    /// <param name="shifts">Volatility shift in bp</param>
    /// <param name="shiftType">Type of shift</param>
    /// <param name="bumpInterpolated"><c>true</c> to bump interpolated volatility</param>
    public ScenarioShiftVolatilities(CalibratedVolatilitySurface[] volatilities, double[] shifts,
      ScenarioShiftType shiftType, bool bumpInterpolated = false)
    {
      Volatilities = volatilities;
      Shifts = shifts;
      ShiftType = shiftType;
      BumpInterpolated = bumpInterpolated;
    }

    #endregion Constructors

    #region IScenarioShift

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
      if (ShiftType == ScenarioShiftType.Specified)
        throw new ArgumentException("Specified volatility shift is not yet supported");
      if (!Shifts.IsNullOrEmpty() && Shifts.Length != 1 
        && Shifts.Length != Volatilities.Count)
        throw new ArgumentException(
          String.Format("Must specify one shift or a shift for each volatility surface"));
    }

    /// <summary>
    /// Save state
    /// </summary>
    public void SaveState(PricerEvaluator[] evaluators)
    {
      if (NothingToDo) return;
      // Create the scenario selection for later use
      if (BumpInterpolated)
      {
        ScenarioSelection = Volatilities.SelectScenario("Scenario", Shifts);
        return;
      }
      Func<CalibratedVolatilitySurface, IVolatilityTenor, double> bumpFilter =
        (surface, tenor) => Volatilities.Contains(surface) 
        ? Shifts[Volatilities.IndexOf(surface)] : 0.0;
      ScenarioSelection = Volatilities.SelectScenario("Scenario", bumpFilter);
    }

    /// <summary>
    /// Restore state then clear saved state
    /// </summary>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
      if (ScenarioSelection == null) return;
      ScenarioSelection.Restore(GetFlags());
      ScenarioSelection = null;
    }

    /// <summary>
    /// Perform scenario shift
    /// </summary>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      if (NothingToDo) return;
      var flags = GetFlags();
      logger.DebugFormat("Shifting all Volatilties...");
      if (BumpInterpolated)
      {
        var result = new BumpAccumulator();
        for (int i = 0, n = Volatilities.Count; i < n; ++i)
        {
          var surface = Volatilities[i];
          surface.Interpolator = VolatilityBumpInterpolator
            .Create(surface.Interpolator, Shifts[i], flags, result);
        }
        return;
      }
      ScenarioSelection.Tenors.Zip(ScenarioSelection.Bumps, 
        (t, b) => new { Tenor = t, Bump = b })
        .Average(t => t.Tenor.Bump(t.Bump, flags));
    }

    /// <summary>
    /// Refit all calibrated objects effected by the shift
    /// </summary>
    /// <param name="evaluators">Pricer evaluators to shift</param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    {
      if (NothingToDo || BumpInterpolated) return;
      foreach (var surface in ScenarioSelection.Surfaces)
        surface.Fit();
    }

    private BumpFlags GetFlags()
    {
      var flags = (ShiftType == ScenarioShiftType.Relative)
        ? BumpFlags.BumpRelative : BumpFlags.None;
      if (BumpInterpolated) flags |= BumpFlags.BumpInterpolated;
      return flags;
    }

    #endregion IScenarioShift

#if USEBETTERDESCRIPTION // Wait to see if this needed. It may be an overkill. RTD Feb'14
    #region Methods

    /// <summary>
    /// ToString
    /// </summary>
    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.Append("ScenarioShiftCorrelation:");
      string asg = null;
      switch (ShiftType)
      {
        case ScenarioShiftType.Relative: asg = "*="; break;
        case ScenarioShiftType.Absolute: asg = "+="; break;
        case ScenarioShiftType.Specified: asg = "="; break;
      }
      var i = 0;
      while (i < Volatilities.Count && i < Shifts.Length && i < 5)
      {
        if (i > 0) sb.Append(", ");
        sb.Append(Volatilities[i].Name).Append(asg).Append(Shifts[i]);
        ++i;
      }
      if (i < Volatilities.Count || i < Shifts.Length)
        sb.Append("...");
      return sb.ToString();
    }

    #endregion Methods
#endif

    #region Properties

    /// <summary>
    /// VolatilitySurfaces to shift
    /// </summary>
    public IList<CalibratedVolatilitySurface> Volatilities { get; private set; }

    /// <summary>
    /// Volatility shift amounts
    /// </summary>
    public double[] Shifts { get; private set; }

    /// <summary>
    /// Type of volatility shift
    /// </summary>
    public ScenarioShiftType ShiftType { get; private set; }

    /// <summary>
    /// Gets a value indicating whether to bump interpolated volatility.
    /// </summary>
    /// <value><c>true</c> if bump the interpolated volatility; otherwise, <c>false</c>.</value>
    public bool BumpInterpolated { get; }

    /// <summary>
    /// Saved volatility scenario state
    /// </summary>
    private IVolatilityScenarioSelection ScenarioSelection { get; set; }

    /// <summary>
    /// No shift to perform
    /// </summary>
    private bool NothingToDo
    {
      get { return Volatilities == null || Volatilities.Count <= 0 
          || ShiftType == ScenarioShiftType.None; }
    }

    #endregion Properties

  }

  
  /// <summary>
  /// The utility class for scenario calculations.
  /// </summary>
  public static class ScenarioUtil
  {
    /// <summary>
    /// Get the bump flags
    /// </summary>
    /// <param name="up">is up bump</param>
    /// <param name="bumpRelative">is relative bump</param>
    /// <returns>bump flags which allow to cross zero when bumping</returns>
    public static BumpFlags GetBumpFlags(bool up, bool bumpRelative)
    {
      var bumpFlag = BumpFlags.None;
      if (bumpRelative) bumpFlag |= BumpFlags.BumpRelative;
      if (!up) bumpFlag |= BumpFlags.BumpDown;

      //Add in the default behavior that allows up and down crossing zero
      bumpFlag |= BumpFlags.AllowDownCrossingZero;

      return bumpFlag;
    }
  }

}  //namespace
