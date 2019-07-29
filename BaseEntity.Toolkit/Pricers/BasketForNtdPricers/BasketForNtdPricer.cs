/*
 * BasketForNtdPricer.cs
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using log4net;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Pricers.BasketForNtdPricers
{

  #region Config

  /// <exclude />
  [Serializable]
  public class NtdPricerConfig
  {
    /// <exclude />
    [ToolkitConfig("The threshold to consider NTD basket a heterogeneous portfolio.")]
    public readonly double HeterogeneousNtdModelThreshold = 0;
  }

  #endregion Config

  ///
  /// <summary>
  ///   Base class for basket underlying NTD products
  /// </summary>
  ///
  /// <remarks>
  ///   This helper class sets up a basket and pre-calculates anything specific to the basket but
  ///   independent of the product.
  /// </remarks>
  ///
  [Serializable]
  public abstract class BasketForNtdPricer : BaseEntityObject
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (BasketForNtdPricer));

    #region Constructors

    /// <summary>
    ///   constructor
    /// </summary>
    protected
      BasketForNtdPricer()
    {
      EffectiveDigits = 9;
    }


    /// <summary>
    ///   constructor
    /// </summary>
    ///
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names</param>
    /// <param name="principals">Principals of individual names</param>
    /// <param name="copula">Copula for the correlation structure</param>
    /// <param name="correlation">Correlation data</param>
    /// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
    /// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
    ///
    protected BasketForNtdPricer(Dt asOf,
                              Dt settle,
                              Dt maturity,
                              SurvivalCurve[] survivalCurves,
                              RecoveryCurve[] recoveryCurves,
                              double[] principals,
                              Copula copula,
                              Correlation correlation,
                              int stepSize,
                              TimeUnit stepUnit)
    {
      // Validate
      if (Dt.Cmp(asOf, settle) > 0)
        throw new ArgumentException(String.Format("Settlement date {0} must be on or after asof {1} date", settle, asOf));

      // Save data using Properties to include validation
      AsOf = asOf;
      PortfolioStart = Dt.Empty;
      Settle = settle;
      Maturity = maturity;
      StepSize = stepSize;
      StepUnit = stepUnit;
      Copula = copula;
      EffectiveDigits = 9;
      HeteroGeneousThreshold = ToolkitConfigurator.Settings.NtdPricer.HeterogeneousNtdModelThreshold;
      // Set the portfolio data and validate curves/principals.
      OriginalBasket = new CreditPool(principals, survivalCurves, recoveryCurves, null, null, false, null);
      OriginalCorrelation = correlation;
    }

    #endregion

    #region Validate

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// 
    /// <param name="errors">Array of resulting errors</param>
    /// 
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // Invalid AsOf date
      if (!AsOf.IsEmpty() && !AsOf.IsValid())
        InvalidValue.AddError(errors, this, "AsOf",
                              String.Format("Invalid AsOf. Must be empty or valid date, not {0}", AsOf));

      // Invalid Settle date
      if (!Settle.IsEmpty() && !Settle.IsValid())
        InvalidValue.AddError(errors, this, "Settle",
                              String.Format("Invalid Settle. Must be empty or valid date, not {0}", Settle));

      // Invalid Maturity date
      if (!Maturity.IsEmpty() && !Maturity.IsValid())
        InvalidValue.AddError(errors, this, "AsOf",
                              String.Format("Invalid Maturity. Must be empty or valid date, not {0}", Maturity));

      // AsOf can not be after settle
      if (AsOf > Settle)
        InvalidValue.AddError(errors, this, "Settle",
                              String.Format("Settlement {0} must be on or after pricing AsOf date {1}", Settle, AsOf));

      // Settle can not be after maturity
      if (Settle > Maturity)
        InvalidValue.AddError(errors, this, "Settle",
                              String.Format("Maturity {0} must be on or after pricing Settle date {1}", Maturity,
                                            Settle));

      // Invalid correlation
      if (OriginalCorrelation == null)
        InvalidValue.AddError(errors, this, "Correlation", String.Format("Correlation cannot be null"));

      // Invalid copula
      if (Copula == null)
        InvalidValue.AddError(errors, this, "Copula", String.Format("Copula cannot be null"));

      // Invalid original basket
      if (OriginalBasket == null)
        InvalidValue.AddError(errors, this, "OrginalBasket", String.Format("Survival Curves cannot be null"));
      else
        OriginalBasket.Validate(errors);

      // Invalid StepSize
      if (StepSize <= 0.0)
        InvalidValue.AddError(errors, this, "StepSize",
                              String.Format("Invalid stepsize. Must be +Ve, Not {0}", StepSize));

      // Invalid Integration Points Second
      if (EffectiveDigits < 0)
        InvalidValue.AddError(errors, this, "EffectiveDigits", String.Format("Effective Digits is not positive"));
      // Invalid Correlation
      if (OriginalCorrelation is ExternalFactorCorrelation)
      {
#if DEBUG
        if (Copula != null && Copula.CopulaType == CopulaType.Gauss)
          Copula.CopulaType = CopulaType.ExternalGauss;
#endif
        InvalidValue.AddError(errors, this, "CopulaType",
                              String.Format("CopulaType cannot be ExternalFactorCorrelation"));
      }
      return;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (BasketForNtdPricer) base.Clone();
      obj.Copula = (Copula) Copula.Clone();
      // Clone the original basket and the original correlation.
      var sc = CloneUtil.Clone(OriginalBasket.SurvivalCurves);
      var rc = CloneUtil.Clone(OriginalBasket.RecoveryCurves);
      var prins = CloneUtil.Clone(OriginalBasket.Participations);
      obj.OriginalBasket = new CreditPool(prins, sc, rc, null, null, false, null);
      obj.OriginalCorrelation = (Correlation) OriginalCorrelation.Clone();
      // When survivalCurves_ is null, a call to UpdateCurves()
      // will set up survival curves, recovery curves, principals,
      // as well as correlation, from the orginal basket and original
      // correlation.
      if (survivalCurves_ != null)
      {
        obj.survivalCurves_ = null;
        obj.UpdateCurves();
      }
      return obj;
    }

    /// <summary>
    ///  Check if the basket is homogeneous or not
    /// </summary>
    /// <returns>True for homogeneous basket</returns>
    protected bool CheckHomogeneous(out double[] lossGivenDefaults)
    {
      var scaledPrins = new double[principals_.Length];
      {
        double scale = Principals.Length / TotalPrincipal;
        for (int i = 0; i < scaledPrins.Length; i++)
          scaledPrins[i] = principals_[i] * scale;
      }
      bool homo = true;
      int count = scaledPrins.Length;
      lossGivenDefaults = new double[count];
      double[] rc = RecoveryRates;
      double l0 = lossGivenDefaults[0] = scaledPrins[0] * (1 - rc[0]);
      {
        int i;
        for (i = 1; i < count; i++)
        {
          double lgd = lossGivenDefaults[i] = scaledPrins[i] * (1 - rc[i]);
          if (Math.Abs(l0 - lgd) > HeteroGeneousThreshold)
          {
            homo = false;
            break;
          }
        }
        for (++i; i < count; ++i)
          lossGivenDefaults[i] = scaledPrins[i] * (1 - rc[i]);
      }
      return homo;
    }

    /// <summary>
    ///   calculate and save total principal
    /// </summary>
    /// <exclude />
    private static double CalcTotalPrincipal(double[] principals)
    {
      double totalPrincipal = 0.0;
      if (null != principals)
      {
        for (int j = 0; j < principals.Length; j++)
          totalPrincipal += principals[j];
      }
      return totalPrincipal;
    }

    /// <summary>
    /// get recovery curves fro survival curves
    /// </summary>
    /// <exclude />
    private static RecoveryCurve[]
      GetRecoveryCurves(SurvivalCurve[] sc)
    {
      var rc = new RecoveryCurve[sc.Length];
      for (int i = 0; i < sc.Length; ++i)
      {
        SurvivalCalibrator cal = sc[i].SurvivalCalibrator;
        if (cal == null)
          throw new ArgumentException(String.Format("null calibrator in survival curve {0}", i));
        rc[i] = cal.RecoveryCurve;
        if (rc[i] == null)
          throw new ArgumentException(String.Format("null recovery curve in survival curve {0}", i));
      }
      return rc;
    }


    ///
    /// <summary>
    ///   Compute the survival curve for the nth default
    /// </summary>
    ///
    /// <remarks>
    ///  The survival probability is defined as one minus the probability
    ///  that the nth default occurs.
    /// </remarks>
    ///
    /// <param name="nth">The index of default</param>
    ///
    public abstract SurvivalCurve NthSurvivalCurve(int nth);

    ///
    /// <summary>
    ///   Compute the expected loss curve for the nth default
    /// </summary>
    ///
    /// <remarks>
    ///   This curve represents the expected cumulative losses over time.
    /// </remarks>
    ///
    /// <param name="nth">The index of default</param>
    /// 
    public abstract Curve NthLossCurve(int nth);

    ///
    /// <summary>
    ///   Reset the pricer
    /// </summary>
    ///
    /// <remarks>
    ///   This method reset the basket pricer  such that in the next request
    ///   for NthSurvivalCurve() or NthLossCurve(), it will recompute everything.
    /// </remarks>
    ///
    public virtual void Reset()
    {
      survivalCurves_ = null;
      principals_ = null;
      recoveryCurves_ = null;
    }

    ///
    /// <summary>
    ///   Set the correlation between any pair of credits to be the same number
    /// </summary>
    ///
    /// <param name="factor">factor to set</param>
    ///
    /// <remarks>
    ///   The correlation between pairs are set to the square of the factor.
    /// </remarks>
    public void SetFactor(double factor)
    {
      Correlation corr = Correlation;
      if (corr is GeneralCorrelation)
        ((GeneralCorrelation) corr).SetCorrelation(factor*factor);
      else
        corr.SetFactor(factor);
    }

    /// <summary>
    ///   Calculate number of days in accrual period
    /// </summary>
    /// <param name="begin">Accrual begin date</param>
    /// <param name="end">Accrual end date</param>
    /// <param name="dayCount">Day count</param>
    /// <param name="first">First default to protect</param>
    /// <param name="covered">Number of defaults covered</param>
    /// <returns>Number of days in accrual period</returns>
    public int AccrualFractionDays(Dt begin, Dt end, DayCount dayCount,
                                   int first, int covered)
    {
      UpdateCurves();
      // Leverages the logic in the Accrued(...) method
      if (covered <= 0) return 0;
      if (defaultInfo_ == null || PrevDefaults < first)
        return Dt.FractionDays(begin, end, dayCount);
      --first; // convert to zero-based index.
      int predflts = 0, survival = covered;
      Dt[] dates = defaultInfo_.Dates;
      for (int i = 0; i < dates.Length; ++i)
      {
        Dt defaultDate = dates[i];
        if (defaultDate >= end)
          break; // we do the last date outside the loop
        if (defaultDate > begin && predflts >= first)
        {
          survival = covered - (predflts - first);
          if (survival <= 0) break;
          begin = defaultDate;
        }
        predflts = (int) Math.Round(defaultInfo_.CumulativeAmorts[i]
                                    + defaultInfo_.CumulativeLosses[i], 0);
      }
      // include the last default date
      survival = covered - Math.Max(0, predflts - first);
      // Dt.Diff(...) is the numerator for *most* daycounts.  OneOne and Monthly might be off here; it's too complex to worry about now.
      return (survival <= 0) ? 0 : Dt.Diff(begin, end);
    }

    /// <summary>
    ///   Calculate accrual fraction with the relevant defaulted names included
    /// </summary>
    /// <param name="begin">Accrual begin date</param>
    /// <param name="end">Accrual end date</param>
    /// <param name="dayCount">Day count</param>
    /// <param name="freq">Coupon frequency</param>
    /// <param name="first">First default to protect</param>
    /// <param name="covered">Number of defaults covered</param>
    /// <param name="accruedOnDefault">Is accrued paid on default</param>
    /// <returns>Accrual fraction for unit (original) notional</returns>
    public double AccrualFraction(Dt begin, Dt end, DayCount dayCount, Frequency freq,
                                  int first, int covered, bool accruedOnDefault)
    {
      UpdateCurves();
      if (covered <= 0) return 0;
      if (defaultInfo_ == null || PrevDefaults < first)
        return Dt.Fraction(begin, end, begin, end, dayCount, freq);
      --first; // convert to zero-based index.
      double accrual = 0;
      int predflts = 0, survival = covered;
      var dates = defaultInfo_.Dates;
      for (int i = 0; i < dates.Length; ++i)
      {
        Dt defaultDate = dates[i];
        if (defaultDate >= end)
          break; // we do the last date outside the loop
        if (defaultDate > begin && predflts >= first && accruedOnDefault)
        {
          survival = covered - (predflts - first);
          if (survival <= 0) break;
          // assume same behavior as CashflowFactory.FillFixed with IsAccruedIncludingDefaultDate()
          Dt accrueTo = Dt.Add(defaultDate, 1);
          accrual += survival*Dt.Fraction(begin, end, begin, accrueTo, dayCount, freq);
          begin = accrueTo;
        }
        predflts = (int) Math.Round(defaultInfo_.CumulativeAmorts[i]
                                    + defaultInfo_.CumulativeLosses[i], 0);
      }
      // include the last default date
      survival = covered - Math.Max(0, predflts - first);
      // survival may be negative for cases with defaults
      // Under same case the EffectiveNotional is already 0
      // which is multiplied to the AccrualFraction, so it's
      // OK to set survival = 0
      survival = Math.Max(survival, 0);
      accrual += survival*Dt.Fraction(begin, end, begin, end, dayCount, freq);
      return accrual/covered;
    }

    /// <summary>
    ///   Calculate the pv of the values from the names
    ///   which have defaulted but need to be settled after the pricer settle date.
    /// </summary>
    /// <param name="settle">Pricer settle date</param>
    /// <param name="maturity">Pricer maturity date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="first">First default to protect</param>
    /// <param name="covered">Number of defaults covered</param>
    /// <param name="includeLoss">True if include the (negative) loss values</param>
    /// <param name="includeRecovery">True if include the (positive) recovery values</param>
    /// <returns>The settlement values discounted to the settle date for unit (original) notional</returns>
    public double DefaultSettlementPv(
      Dt settle, Dt maturity,
      DiscountCurve discountCurve,
      int first, int covered,
      bool includeLoss, bool includeRecovery)
    {
      UpdateCurves();
      if (covered <= 0 || PrevDefaults < first || defaultInfo_ == null || defaultInfo_.SettlementInfo == null ||
          defaultInfo_.SettlementInfo.IsEmpty)
        return 0;
      DefaultSettleInfo info = defaultInfo_.SettlementInfo;
      Dt[] dates = info.SettleDates;
      bool[] includesSettle = info.IncludeStartDates;
      // find the first date
      int N = dates.Length;
      int start = N;
      for (int i = 0; i < N; ++i)
      {
        var cmp = Dt.Cmp(dates[i], settle);
        if (cmp > 0 || (cmp == 0 && (includesSettle[i] 
          // Include the default date before the pricing settle date,
          // yet default settlement date is pricing settle date.
          || info.DefaultDates[i] < settle)))
        {
          start = i;
          break;
        }
      }
      // work through all the relevant settle dates
      bool settleOnMaturity = settle == Maturity;
      double pv = 0;
      Dt preDfltDate = Dt.Empty;
      int last = first + covered;
      for (int i = start; i < N; ++i)
      {
        // In the special case defaultSettle == pricerSettle == maturity,
        // we don't include the loss, in order to be consistent with CDO pricing
        // where it hard codes includeSettle to false and the price in the special
        // case is always zero.
        if (dates[i] > maturity || (dates[i] == settle && settleOnMaturity))
        {
          // default settled after maturity is excluded only when
          // it is defaulted on or after maturity
          if (info.DefaultDates[i] >= maturity) break;
        }
        int cmp = Dt.Cmp(dates[i], settle);
        if (cmp < 0) continue;
        if (cmp == 0 && !info.IncludeStartDates[i]) continue;
        if (preDfltDate == info.DefaultDates[i]) continue;
        preDfltDate = info.DefaultDates[i];
        // Find index of the default date in the Dates array
        // for retrieving the cumulative loss/amortization infomations
        int pos = Array.BinarySearch(defaultInfo_.Dates, info.DefaultDates[i]);
        if (pos < 0)
          throw new InvalidOperationException(
            "Corrupted or badly constructed BasketDefaultInfo");
        double preLoss = 0, preAmor = 0;
        int predflts = 0;
        if (pos > 0)
        {
          predflts = defaultInfo_.CumulativeDefaultCounts[pos - 1];
          if (predflts >= last) break;
          preLoss = defaultInfo_.CumulativeLosses[pos - 1];
          preAmor = defaultInfo_.CumulativeAmorts[pos - 1];
        }
        double newLoss = defaultInfo_.CumulativeLosses[pos] - preLoss;
        double newAmor = defaultInfo_.CumulativeAmorts[pos] - preAmor;
        int newdflts = defaultInfo_.CumulativeDefaultCounts[pos] - predflts;
        int dfltscovered = DefaultsCovered(first, covered, predflts, newdflts);
        if (dfltscovered <= 0) continue;

        // Calculate the unsettled loss/recoveries to the tranche
        double v = 0;
        if (includeLoss)
          v -= dfltscovered*newLoss/newdflts;
        if (includeRecovery && info.Recoveries[i] != 0)
          v += dfltscovered*newAmor/newdflts;
        pv += v*discountCurve.DiscountFactor(settle, dates[i]);
      }

      // normalized to be based on unit tranche (original) notional
      return pv/covered;
    }

    /// <summary>
    ///   Calculate the survival on the settle date
    /// </summary>
    /// <remarks>
    ///   Effective survival is the remaining notional per unit
    ///   original notional on the settle date
    /// </remarks>
    /// <param name="first">First default to covered</param>
    /// <param name="covered">Number of defaults covered</param>
    /// <param name="includeUnsettled">Include unsettled default in survival.</param>
    /// <returns>Effective survival</returns>
    public double EffectiveSurvival(int first, int covered, bool includeUnsettled)
    {
      UpdateCurves();
      if (covered <= 0) return 0.0;
      int prevDefaults = includeUnsettled ? SettledDefaults : PrevDefaults;
      if (first > prevDefaults) return 1.0;
      int dfltscovered = prevDefaults - first + 1;
      if (dfltscovered >= covered) return 0.0;
      return 1.0 - ((double) dfltscovered)/covered;
    }

    /// <summary>
    ///   Create an array of dates as time grid
    /// </summary>
    protected Dt[] CreateDateArray(Dt from)
    {
      var retVal = new List<Dt>();
      if (StepSize == 0 || StepUnit == TimeUnit.None)
      {
        StepSize = 3;
        StepUnit = TimeUnit.Months;
      }
      Dt current = from;
      for (;;)
      {
        if (current >= Maturity)
          break;
        retVal.Add(current);
        current = Dt.Add(current, StepSize, StepUnit);
      }
      retVal.Add(Maturity);
      return retVal.ToArray();
    }

    /// <summary>
    ///   Set the underlying curves and participations
    /// </summary>
    /// 
    /// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
    /// <param name="recoveryCurves">Recovery curves of individual names (or null to use survivalCurve recoveries</param>
    /// <param name="principals">Principals of individual names</param>
    /// <param name="correlation">Correlation data</param>
    /// <exclude />
    private void Set(
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      double[] principals,
      Correlation correlation)
    {
      // Validate
      if ((survivalCurves == null) || (survivalCurves.Length < 1))
        throw new ArgumentException("Missing survival curves");
      if (principals.Length != survivalCurves.Length)
      {
        throw new ArgumentException(String.Format(
                                      "Number of principals {0} must match the number of names {1}",
                                      principals.Length, survivalCurves.Length));
      }
      if (!(correlation is Correlation || correlation is CorrelationTermStruct))
      {
        throw new ArgumentException(String.Format(
                                      "The Argument correlation must be either Correlation or CorrelationTermStruct, not {0}",
                                      correlation.GetType()));
      }
      // If recoveryCurves is null, we get them from the survival curves
      if (recoveryCurves == null)
      {
        recoveryCurves = GetRecoveryCurves(survivalCurves);
      }
      else if (recoveryCurves.Length != survivalCurves.Length)
      {
        throw new ArgumentException(String.Format(
                                      "Number of recovery curves {0} must be no less than the number of names {1}",
                                      recoveryCurves.Length, survivalCurves.Length));
      }
      // calculate the total principal
      TotalPrincipal = CalcTotalPrincipal(principals);
      // Initialize local variables
      double[] prin = principals;
      SurvivalCurve[] sc = survivalCurves;
      RecoveryCurve[] rc = recoveryCurves;
      Correlation corr = correlation;
      // Count the defaulted credits
      int count = PrevDefaults = SettledDefaults = 0;
      foreach (SurvivalCurve s in survivalCurves)
        if (s.Defaulted != Defaulted.NotDefaulted)
          ++count;
      // Removed defaulted credits
      var defaultInfoBuilder = new BasketDefaultInfo.Builder();
      if (count > 0)
      {
        PrevDefaults = SettledDefaults = count;
        SurvivalCurve[] sc0 = sc;
        // Convert count to be the number of survivals
        count = sc0.Length - count;
        // Create arrays of active credits and calculate prev losses
        prin = new double[count];
        sc = new SurvivalCurve[count];
        rc = new RecoveryCurve[count];
        var picks = new double[sc0.Length];
        for (int i = 0, idx = 0; i < sc0.Length; ++i)
        {
          if (survivalCurves[i].Defaulted != Defaulted.NotDefaulted)
          {
            picks[i] = 0;
            double recoveryRate = recoveryCurves[i].RecoveryRate(Maturity);
            double loss = 1 - recoveryRate, amor = recoveryRate;
            Dt defaultDate = survivalCurves[i].DefaultDate;
            defaultInfoBuilder.Add(defaultDate, loss, amor);
            if (survivalCurves[i].SurvivalCalibrator == null)
              continue;
            RecoveryCurve rcurve = survivalCurves[i].SurvivalCalibrator.RecoveryCurve;
            if (rcurve != null && !rcurve.JumpDate.IsEmpty())
            {
              Dt date = rcurve.JumpDate;
              if (date > Settle) --SettledDefaults;
              defaultInfoBuilder.AddSettle(date, defaultDate, amor, loss);
            }
            else if (survivalCurves[i].Defaulted == Defaulted.WillDefault)
            {
              --SettledDefaults; // WillDefault is never settled.
              defaultInfoBuilder.AddSettle(defaultDate, defaultDate, amor, loss, true);
            }
          }
          else
          {
            picks[i] = 1;
            prin[idx] = principals[i];
            sc[idx] = survivalCurves[i];
            rc[idx] = recoveryCurves[i];
            ++idx;
          }
        }
        if (sc.Length > 0)
        {
          // Create a new correlation object
          if (corr is SingleFactorCorrelation)
            corr = CorrelationFactory.CreateSingleFactorCorrelation(corr, picks);
          else if (corr is FactorCorrelation)
            corr = CorrelationFactory.CreateFactorCorrelation(corr, picks);
          else if (corr is GeneralCorrelation)
            corr = CorrelationFactory.CreateGeneralCorrelation(corr, picks);
          else if (corr is CorrelationTermStruct)
            corr = CorrelationFactory.CreateCorrelationTermStruct(corr, picks);
          else
            throw new ToolkitException("Unknown correlation type");
        }
        else
          corr = null;
      }
      // Set the basket properties
      principals_ = prin;
      survivalCurves_ = sc;
      recoveryCurves_ = rc;
      correlation_ = corr;
      // Loss and amortization must be in percentage
      defaultInfo_ = defaultInfoBuilder.ToDefaultInfo(1.0);
      // done
      return;
    }

    /// <summary>
    /// Update environment
    /// </summary>
    protected void UpdateCurves()
    {
      if (survivalCurves_ == null)
      {
        Set(OriginalBasket.SurvivalCurves, GetRecoveryCurves(),
            OriginalBasket.Participations, OriginalCorrelation);
      }
    }

    private RecoveryCurve[] GetRecoveryCurves()
    {
      if (fixedRecovery_ == null || fixedRecovery_.Length == 0)
        return OriginalBasket.RecoveryCurves;
      int count = OriginalBasket.SurvivalCurves.Length;
      if (fixedRecovery_.Length != 1 && fixedRecovery_.Length != count)
      {
        throw new ToolkitException("Fixed recoveries and basket size not match");
      }
      var rc = new RecoveryCurve[count];
      for (int i = 0; i < count; ++i)
      {
        rc[i] = new RecoveryCurve(AsOf,
                                  fixedRecovery_[fixedRecovery_.Length == 1 ? 0 : i]);
      }
      return rc;
    }

    /// <summary>
    /// Survival curve of nth defaulted name if nth default already happened
    /// </summary>
    /// <param name="nth"></param>
    /// <returns></returns>
    protected SurvivalCurve DefaultedSurvivalCurve(int nth)
    {
      SurvivalCurve curve = null;
      if (defaultInfo_ != null)
      {
        Dt[] dates = defaultInfo_.Dates;
        for (int i = 0; i < dates.Length; ++i)
        {
          var n = (int) Math.Round(defaultInfo_.CumulativeAmorts[i]
                                   + defaultInfo_.CumulativeLosses[i], 0);
          if (n >= nth)
          {
            curve = new SurvivalCurve(Dt.Add(dates[i], -1));
            curve.DefaultDate = dates[i];
            break;
          }
        }
      }
      if (curve == null)
      {
        curve = new SurvivalCurve(Dt.Add(AsOf, -1));
        curve.Add(AsOf, 1E-10);
      }
      return curve;
    }

    /// <summary>
    /// </summary>
    /// <param name="nth"></param>
    /// <returns></returns>
    protected Curve DefaultedLossCurve(int nth)
    {
      Curve curve = null;
      if (defaultInfo_ != null)
      {
        Dt[] dates = defaultInfo_.Dates;
        for (int i = 0; i < dates.Length; ++i)
        {
          var n = (int) Math.Round(defaultInfo_.CumulativeAmorts[i]
                                   + defaultInfo_.CumulativeLosses[i], 0);
          if (n >= nth)
          {
            curve = new SurvivalCurve(Dt.Add(dates[i], -1));
            curve.Add(dates[i], defaultInfo_.CumulativeLosses[i]/n);
            break;
          }
        }
      }
      if (curve == null)
      {
        curve = new Curve(Dt.Add(AsOf, -1));
        curve.Add(AsOf, AverageRecoveryRate);
      }
      curve.JumpDate = Dt.Add(AsOf, -1);
      return curve;
    }

    private static int DefaultsCovered(int first, int covered, int predflts, int newdflts)
    {
      --first;
      int last = first + covered;
      if (first >= predflts)
      {
        first -= predflts;
        last -= predflts;
      }
      else if (last > predflts)
      {
        first = 0;
        last -= predflts;
      }
      else
        return 0;

      if (first >= newdflts)
        return 0;

      covered = last - first;
      int dfltscovered = newdflts - first;
      return dfltscovered < covered ? dfltscovered : covered;
    }

    #endregion // Implementation

    #region Properties

    /// <summary>
    /// Original basket
    /// </summary>
    public CreditPool OriginalBasket { get; private set; }

    /// <summary>
    ///   Number of names (read only)
    /// </summary>
    public int Count
    {
      get { return OriginalBasket.CreditCount; }
    }


    /// <summary>
    ///   Portfolio start date
    /// </summary>
    public Dt PortfolioStart { get; set; }

    /// <summary>
    ///   As of date
    /// </summary>
    public Dt AsOf { get; set; }


    /// <summary>
    ///   Settlement date
    /// </summary>
    public Dt Settle { get; set; }


    /// <summary>
    ///   Maturity date
    /// </summary>
    public Dt Maturity { get; set; }


    /// <summary>
    ///   Step size for pricing grid
    /// </summary>
    public int StepSize { get; set; }

    /// <summary>
    ///   Step units for pricing grid
    /// </summary>
    public TimeUnit StepUnit { get; set; }

    /// <summary>
    /// Original correlation
    /// </summary>
    public Correlation OriginalCorrelation { get; private set; }

    /// <summary>
    ///   Correlation structure of the basket
    /// </summary>
    public Correlation Correlation
    {
      get
      {
        UpdateCurves();
        return correlation_;
      }
    }

    /// <summary>
    ///   Copula structure
    /// </summary>
    public Copula Copula { get; set; }

    /// <summary>
    ///   Copula type used for pricing
    /// </summary>
    public CopulaType CopulaType
    {
      get
      {
        if (Correlation is ExternalFactorCorrelation)
        {
#if DEBUG
          if (Copula.CopulaType == CopulaType.Gauss)
            return CopulaType.ExternalGauss;
#endif
        }
        return Copula.CopulaType;
      }
    }


    /// <summary>
    ///   Degrees of freedom for common factor
    /// </summary>
    public int DfCommon
    {
      get { return Copula.DfCommon; }
    }


    /// <summary>
    ///   Degrees of freedom for idiosyncratic factor
    /// </summary>
    public int DfIdiosyncratic
    {
      get { return Copula.DfIdiosyncratic; }
    }


    /// <summary>
    ///  Survival curves from curves
    /// </summary>
    public SurvivalCurve[] SurvivalCurves
    {
      get { return OriginalBasket.SurvivalCurves; }
    }


    /// <summary>
    ///  Recovery curves from curves
    /// </summary>
    public RecoveryCurve[] RecoveryCurves
    {
      get
      {
        if (OriginalBasket.RecoveryCurves != null)
          return OriginalBasket.RecoveryCurves;
        return GetRecoveryCurves(OriginalBasket.SurvivalCurves);
      }
    }

    /// <summary>
    ///   Principal or face values for each name in the basket
    /// </summary>
    public double[] Principals
    {
      get { return OriginalBasket.Participations; }
    }

    /// <summary>
    ///    The total original principal/notional in the basket
    /// </summary>
    public double TotalPrincipal
    {
      get
      {
        if (survivalCurves_ == null)
          UpdateCurves();
        return totalPrincipal_;
      }
      set { totalPrincipal_ = value; }
    }

    /// <summary>
    ///    Effective decimal points
    /// </summary>
    public int EffectiveDigits { get; private set; }

    /// <summary>
    ///  Get the threshold for heterogeneous NTD basket
    /// </summary>
    public double HeteroGeneousThreshold
    {
      get; set;

    }

    /// <summary>
    ///   Recovery rates from curves
    /// </summary>
    protected double[] RecoveryRates
    {
      get
      {
        UpdateCurves();
        return Array.ConvertAll(recoveryCurves_, rc => rc.RecoveryRate(Maturity));
      }
    }

    /// <summary>
    ///   Average recovery rate
    /// </summary>
    protected double AverageRecoveryRate
    {
      get
      {
        // use the average recovery rate
        double sum = 0;
        double[] recoveryRates = RecoveryRates;
        for (int i = 0; i < recoveryRates.Length; ++i)
          sum += recoveryRates[i];
        return (sum/recoveryRates.Length);
      }
    }

    /// <summary>
    ///   Recovery dispersions from curves
    /// </summary>
    protected double[] RecoveryDispersions
    {
      get
      {
        UpdateCurves();
        return Array.ConvertAll(recoveryCurves_, rc => rc.RecoveryDispersion);
      }
    }

    /// <summary>
    /// Previous defaults
    /// </summary>
    protected int PrevDefaults { get; private set; }

    /// <summary>
    /// Settled defaults
    /// </summary>
    protected int SettledDefaults { get; private set; }

    /// <summary>
    /// Fixed recoveries (different from those in survival curves)
    /// </summary>
    internal double[] FixedRecovery
    {
      get { return fixedRecovery_; }
      set
      {
        fixedRecovery_ = value;
        Reset();
      }
    }

    /// <summary>
    ///   Return IEnumerator for basket survival curves
    /// </summary>
    public IEnumerator GetEnumerator()
    {
      return OriginalBasket.SurvivalCurves.GetEnumerator();
    }

    #endregion // Properties

    #region Data

    private double totalPrincipal_;
    private Correlation correlation_;
    private BasketDefaultInfo defaultInfo_;
    private double[] fixedRecovery_;
    /// <summary>Principles</summary>
    protected double[] principals_; //alive principals
    /// <summary>Recovery curves</summary>
    protected RecoveryCurve[] recoveryCurves_; //alive recoveries
    /// <summary>Survival curves</summary>
    protected SurvivalCurve[] survivalCurves_; //alive survivals

    #endregion Data
  } // class BasketForNtdPricer
}