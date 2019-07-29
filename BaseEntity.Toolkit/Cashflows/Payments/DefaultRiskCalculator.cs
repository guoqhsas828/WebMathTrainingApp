using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows.Utils;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using static System.Math;

namespace BaseEntity.Toolkit.Cashflows.Payments
{
  [Serializable]
  public class DefaultRiskCalculator
  {
    #region Constructor

    public DefaultRiskCalculator(
      Dt asOf, Dt riskBeginDate, Dt riskEndDate,
      Curve survivalCurve,
      Curve counterpartyCurve = null,
      double correlation = 0,
      bool accrualOnDefault = false,
      bool includeDefaultDateInAccrual = true,
      bool logLinearApproximation = false,
      int stepSize = 0,
      TimeUnit stepUnit = TimeUnit.None)
    {
      CreditRiskBeginDate = PartialAccrualBeginDate = riskBeginDate;

      // Find the default date
      bool isPrepaid;
      DefaultDate = GetDefaultDate(survivalCurve,
        counterpartyCurve, out isPrepaid);
      IsPrepaid = isPrepaid;

      if (counterpartyCurve == null)
      {
        CreditCurve = survivalCurve;
        var sp = survivalCurve?.Interpolate(riskBeginDate) ?? 1.0;
        InitialSurvival = sp <= 1E-14 ? 1.0 : sp;
      }
      else
      {
        var interp = new Weighted(new Const(), new Const());
        var credit = new SurvivalCurve(asOf) {Interp = interp};
        var prepay = new SurvivalCurve(asOf) {Interp = interp};
        CounterpartyRisk.TransformSurvivalCurves(
          asOf, riskEndDate, survivalCurve,
          counterpartyCurve, correlation, credit, prepay,
          stepSize, stepUnit);
        CreditCurve = credit;
        PrepayCurve = prepay;

        var creditSp = credit?.Interpolate(riskBeginDate) ?? 1.0;
        var prepaySp = prepay?.Interpolate(riskBeginDate) ?? 1.0;
        var sp = CombinedSurvival(creditSp, prepaySp, 1.0);
        InitialSurvival = sp <= 1E-14 ? 1.0 : sp;
      }

      AccrualPaidOnDefault = accrualOnDefault;
      IncludeDefaultDateInAccrual = includeDefaultDateInAccrual;
      UseLogLinearApproximation = logLinearApproximation;
      TimeGridBuilder = stepSize > 0
        ? new TimeGridBuilder(stepSize, stepUnit) : null;
    }

    #endregion

    #region Properties

    public double InitialSurvival { get; }

    public Curve CreditCurve { get; }

    public Curve PrepayCurve { get; }

    public Dt DefaultDate { get; }

    public bool IsPrepaid { get; }

    /// <summary>
    /// The date when the default risk exposure starts,
    /// usually the settlement date of the trade.
    /// </summary>
    public Dt CreditRiskBeginDate { get; }

    /// <summary>
    /// The date when the partial unwind accrual begins.
    /// Currently used only in the case where DiscountAccrued = false
    /// and the accrual period cross the settle date.
    /// </summary>
    public Dt PartialAccrualBeginDate { get; }

    public bool IncludeDefaultDateInAccrual { get; }

    public bool AccrualPaidOnDefault { get; }

    /// <summary>
    /// Gets or sets a value indicating whether to use log linear approximation.
    /// </summary>
    /// <value><c>true</c> if use log linear approximation; otherwise, <c>false</c>.</value>
    public bool UseLogLinearApproximation { get; }

    /// <summary>
    /// Gets or sets the time grids for integration
    /// </summary>
    /// <value>The time grids</value>
    public ITimeGridBuilder TimeGridBuilder { get; }

    #endregion

    #region Methods

    #region Survival calculation

    /// <summary>
    /// Calculate the probability that there is no
    ///  default till the specified date.
    /// </summary>
    /// <param name="date">The date</param>
    /// <returns>System.Double</returns>
    private double CreditSurvival(Dt date)
    {
      var sc = CreditCurve;
      if (sc == null) return 1.0;
      return date < CreditRiskBeginDate ? 1.0
        : (sc.Interpolate(date)/InitialSurvival);
    }

    /// <summary>
    /// Calculate the probability that there is no
    ///  default and no prepay till the specified date.
    /// </summary>
    /// <param name="date">The date</param>
    /// <returns>System.Double</returns>
    public double SurvivalProbability(Dt date)
    {
      if (date <= CreditRiskBeginDate)
        return 1.0;

      var creditSp = CreditCurve?.Interpolate(date) ?? 1.0;
      if (PrepayCurve == null)
        return creditSp/InitialSurvival;

      var prepaySp = PrepayCurve.Interpolate(date);
      return CombinedSurvival(creditSp, prepaySp, InitialSurvival);
    }

    private static double CombinedSurvival(
      double creditSurvival,
      double prepaySurvival,
      double initialSurvival)
    {
      // deal with round-off errors.
      if (prepaySurvival > 0.5)
        return creditSurvival/initialSurvival + (prepaySurvival - 1)/initialSurvival;
      if (creditSurvival > 0.5)
        return (creditSurvival - 1)/initialSurvival + prepaySurvival/initialSurvival;
      return (creditSurvival + prepaySurvival - 1)/initialSurvival;
    }

    #endregion

  /// <summary>
  /// Risky discount interest payment
  /// </summary>
  /// <param name="interestPayment">The interest payment</param>
  /// <param name="discountFunction">The discount function</param>
  /// <returns>risky discount factor</returns>
  /// <remarks>The <em>Risky Discount</em> for an interest period
  /// <m>(t_0, t_1]</m> is defined as<math>
  /// \mathrm{RiskyDiscount} \equiv \left(1-F(t_e)\right)\,D(t_p)
  /// + \phi\int_{t_b}^{t_e}{\frac{\tau - t_0}{t_1 - t_0}D(\tau)}\,d{F(\tau)}
  /// </math>where
  /// <m>F(\cdot)</m> is the cumulative default probability function,
  /// <m>D(\cdot)</m> the risk-less discount function,
  /// <m>t_b \in [t_0, t_1]</m> is the credit risk begin date doe this period,
  /// <m>t_e \in [t_0, t_1]</m>, <m>t_e \geq t_b</m>, the credit risk end date,
  /// <m>\phi = 1</m> if the accrued on default is included
  /// and <m>\phi = 0</m> otherwise.</remarks>
  public double RiskyDiscount(
      InterestPayment interestPayment,
      Func<Dt, double> discountFunction)
    {
      double df = discountFunction(interestPayment.PayDt);
      double delta = interestPayment.AccruedFractionAtDefault;
      if (delta.AlmostEquals(0.0))
      {
        var end = interestPayment.GetCreditRiskEndDate();
        return SurvivalProbability(interestPayment
                 .IncludeEndDateProtection ? end + 1 : end) * df;
      }
      double s1 = GetOverallSurvival(interestPayment);
      return s1*df + AccrualOnDefault(interestPayment, discountFunction);
    }

    #region Accrual on default calculation

    public double AccrualOnDefault(
      InterestPayment ip,
      Func<Dt, double> discountFunction)
    {
      var ratio = GetAccrualRatio(ip);
      if (ratio <= 0) return 0;

      var dayCount = ip.DayCount;
      var accrualOnDefaultFn = GetAccrualOnDefaultFn();
      var includeDefaultdate = IncludeDefaultDateInAccrual;
      var timeFraction = ip.AccruedFractionAtDefault;
      var accrualFraction = ip.AccruedFractionAtDefault;
      var accrualDays = GetAccrualPeriodDays(ip);

      Dt begin = GetAccrualRiskBeginDate(ip.AccrualStart),
        accrualStart = begin,
        end = GetAccrualRiskEndDate(ip, ip.AccrualEnd);
      var beginDf = discountFunction(begin);
      var beginSurvival = SurvivalProbability(begin);

      var timeGrids = TimeGridBuilder?.GetTimeGrids(begin, end);
      if (timeGrids == null || timeGrids.Count == 0)
      {
        double endDf = discountFunction(end);
        var endSurvival = SurvivalProbability(
          ip.IncludeEndDateProtection ? (end + 1) : end);
        return accrualOnDefaultFn(begin, end, beginDf, endDf,
          beginSurvival, endSurvival, accrualStart, dayCount,
          1.0, accrualDays, includeDefaultdate,
          timeFraction, accrualFraction)*ratio;
      }

      var pv = 0.0;
      var includeLast = ip.IncludeEndDateProtection;
      var stepBegin = begin;
      foreach (Dt date in timeGrids)
      {
        if (date <= stepBegin) continue;

        Dt stepEnd = date, protectionDate = date;
        if (stepEnd >= end)
        {
          stepEnd = end;
          protectionDate = includeLast ? (date + 1) : date;
          includeLast = false; // indicate that last is already included.
        }
        var probSurv = SurvivalProbability(protectionDate);
        var df = discountFunction(stepEnd);
        pv += accrualOnDefaultFn(stepBegin, stepEnd,
          beginDf, df, beginSurvival, probSurv, accrualStart,
          dayCount, 1.0, accrualDays, includeDefaultdate,
          timeFraction, accrualFraction);

        beginSurvival = probSurv;
        beginDf = df;
        stepBegin = stepEnd;
      }
      return pv*ratio;
    }

    public static int GetAccrualPeriodDays(InterestPayment ip)
    {
      var days = Dt.Diff(ip.AccrualStart, ip.AccrualEnd, ip.DayCount);
      return days;
    }

    public Dt GetAccrualRiskBeginDate(Dt begin)
    {
      Dt date = CreditRiskBeginDate;
      return date <= begin ? begin : date;
    }

    public Dt GetAccrualRiskEndDate(Payment ip, Dt accrualEnd)
    {
      Dt date = ip.GetCreditRiskEndDate();
      Debug.Assert(!date.IsEmpty());
      return date < accrualEnd ? date : accrualEnd;
    }

    public double GetAccrualRatio(InterestPayment ip)
    {
      Dt settle = PartialAccrualBeginDate;
      if (ip.AccrualStart >= settle || ip.AccrualEnd <= settle)
        return 1;
      double accrual;
      double accrued = ip.Accrued(settle, out accrual);
      return accrual <= 0 ? 0 : (1 + accrued/accrual);
    }

    private double GetOverallSurvival(InterestPayment ip)
    {
      Dt end = GetAccrualRiskEndDate(ip, ip.AccrualEnd);
      return SurvivalProbability(ip.IncludeEndDateProtection ? (end + 1) : end);
    }

    private AccrualOnDefaultFn GetAccrualOnDefaultFn()
    {
      return UseLogLinearApproximation
        ? AccrualOnDefaultLogLinear
        : (AccrualOnDefaultFn) AccrualOnDefaultLinear;
    }

    delegate double AccrualOnDefaultFn(
      Dt stepBegin, Dt stepEnd,
      double dfBegin, double dfEnd,
      double spBegin, double spEnd,
      Dt accrualStart, DayCount dayCount,
      double accruedValue, int accrualPeriod,
      bool includeDefaultDate,
      double timeFraction, double accrualFraction);

    private static double AccrualOnDefaultLinear(
      Dt stepBegin, Dt stepEnd,
      double dfBegin, double dfEnd,
      double spBegin, double spEnd,
      Dt accrualStart, DayCount dayCount,
      double accruedValue, int accrualPeriod,
      bool includeDefaultDate,
      double timeFraction, double accrualFraction)
    {
      // For the first period, accrual starts with settle (the accrued before settle should be paid on settle)
      int days;
      if (accrualStart < stepBegin)
      {
        days = Dt.Diff(accrualStart, stepBegin, dayCount)
          + (int) (Dt.Diff(stepBegin, stepEnd, dayCount)*accrualFraction);
      }
      else
      {
        days = (int) (Dt.Diff(accrualStart, stepEnd, dayCount)*accrualFraction);
      }

      // Include default date if we need to
      if (includeDefaultDate)
        days++;

      // Calculate accrued
      double avgDf = (1.0 - timeFraction)*dfBegin + timeFraction*dfEnd;
      double accrued = ((double) days)/accrualPeriod*accruedValue;
      return avgDf*(spBegin - spEnd)*accrued;
    }

    private static double AccrualOnDefaultLogLinear(
      Dt stepBegin, Dt stepEnd,
      double dfBegin, double dfEnd,
      double spBegin, double spEnd,
      Dt accrualStart, DayCount dayCount,
      double accruedValue, int accrualPeriod,
      bool includeDefaultDate,
      double timeFraction, double accrualFraction)
    {
      // Estimate number of days between accrual start and stepEnd.
      int days = Dt.Diff(accrualStart, stepEnd, dayCount);

      // Calculate the initial accrual days and interval.
      //   a0 = days between accrual start and stepBegin.
      //   dt = days between stepBegin and stepEnd.
      double a0, dt;
      if (accrualStart < stepBegin)
      {
        a0 = Dt.Diff(accrualStart, stepBegin);
        dt = Dt.Diff(stepBegin, stepEnd);
      }
      else
      {
        a0 = 0;
        dt = Dt.Diff(accrualStart, stepEnd);
      }
      double coupon = days == 0 ? 0.0
        : accruedValue*days/accrualPeriod/(a0 + dt);

      // Adjust for discrete payments in continuous integral.
      // The adjustment depends on the flag includeDefaultDate.
      if (includeDefaultDate)
        a0 += 1.0; // start with extra 1 day accrual
      else
        a0 += 0.5; // start with extra 0.5 day accrual

      // Calculate accrued.
      return coupon*AccrualOnDefaultIntegral(
        0.0, dt, a0, dfBegin, dfEnd, spBegin, spEnd);
    }

    /// <summary>
    ///   Calculate the accrual on default.
    /// </summary>
    /// <remarks>
    /// Calculate the accrual on default as the integral
    /// <math>
    ///   V = - \int_{t_0}^{t_1} a(t)\, D(t)\, d S(t)
    ///   = \frac{h}{h+r} D_0\,S_0 \left(
    ///     a_0 - a_1 e^{-(h+r)\Delta}
    ///    + \frac{1 - e^{-(h+r)\Delta}}{h+r}\right)
    /// </math>
    /// where
    /// <math>
    ///   a(t) = a_0 + t - t_0,\quad
    ///   a_1 = a_0 + \Delta,\quad
    ///   \Delta = t_1 - t_0
    /// </math><math>
    ///   D(t) = D_0 \, e^{-r (t-t_0)},\qquad
    ///   S(t) = S_0 \, e^{-h (t-t_0)}
    /// </math>
    /// </remarks>
    private static double AccrualOnDefaultIntegral(
      double t0, double t1,
      double a0,
      double d0, double d1,
      double s0, double s1)
    {
      // Calculate time interval.
      double dt = t1 - t0;
      if (Abs(dt) < 1.0/256)
      {
        // Use linear approximation for small time interval.
        double a1 = a0 + dt;
        return 0.5*(a0*d0 + a1*d1)*(s1 - s0);
      }

      // If the initial survival probability
      // or discount factor is zero, return zero.
      if (s0 <= 0 || d0 <= 0)
        return 0.0;

      // Calculate the hazard rate.
      s1 /= s0;
      if (s1 <= 0)
      {
        // treat this as infinite hazard rate
        return s0*d0*a0;
      }
      double h = -Log(s1)/dt;

      // Calculate the interest rate
      d1 /= d0;
      if (d1 <= 0)
      {
        // treat this as infinite interest rate
        return 0.0;
      }
      double r = -Log(d1)/dt;

      // Calculate the integral in normal case.
      double lam = h + r;
      double v = (h/lam)*s0*d0*
        (a0 + (1.0 - (1 + (a0 + dt)*lam)*d1*s1)/lam);
      return v;
    }

    #endregion

    #region Protection calculation

    public double Protection(
      CreditContingentPayment rp,
      Func<Dt, double> discountFunction)
    {
      return Protection(
        GetAccrualRiskBeginDate(rp.BeginDate),
        GetAccrualRiskEndDate(rp, rp.EndDate),
        rp.IncludeEndDateProtection,
        discountFunction);
    }

    public double Protection(
      InterestPayment ip,
      Func<Dt, double> discountFunction)
    {
      return Protection(
        GetAccrualRiskBeginDate(ip.AccrualStart),
        GetAccrualRiskEndDate(ip, ip.AccrualEnd),
        ip.IncludeEndDateProtection,
        discountFunction);
    }

    public double Protection(
      Dt beginDate, Dt endDate, bool includeEndDateProtection,
      Func<Dt, double> discountFunction)
    {
      Debug.Assert(endDate >= beginDate);

      var protectionFn = GetProtectionFn();
      const double timeFraction = 0.5;

      var beginDf = discountFunction(beginDate);
      var beginSp = CreditSurvival(beginDate);

      var timeGrids = TimeGridBuilder?.GetTimeGrids(beginDate, endDate);
      if (timeGrids == null || timeGrids.Count == 0)
      {
        double endDf = discountFunction(endDate);
        var endSp = CreditSurvival(
          includeEndDateProtection ? (endDate + 1) : endDate);
        return protectionFn(beginDate, endDate,
          beginDf, endDf, beginSp, endSp, timeFraction);
      }

      var pv = 0.0;
      var includeLast = includeEndDateProtection;
      Dt begin = beginDate;
      foreach (var dt in timeGrids)
      {
        if (dt <= begin) continue;

        Dt end = dt, protectionDate = dt;
        if (end >= endDate)
        {
          end = endDate;
          protectionDate = includeLast ? Dt.Add(end, 1) : end;
          includeLast = false; // indicate that last is already included.
        }
        var sp = CreditSurvival(protectionDate);
        var df = discountFunction(end);

        pv += protectionFn(begin, end, beginDf, df, beginSp, sp, timeFraction);

        beginSp = sp;
        beginDf = df;
        begin = end;
      }

      return pv;
    }

    private ProtectionFn GetProtectionFn()
    {
      return UseLogLinearApproximation
        ? ProtectionLogLinear
        : (ProtectionFn)ProtectionLinear;
    }

    delegate double ProtectionFn(
      Dt stepBegin, Dt stepEnd,
      double dfBegin, double dfEnd,
      double spBegin, double spEnd,
      double timeFraction);

    private static double ProtectionLinear(
      Dt stepBegin, Dt stepEnd,
      double dfBegin, double dfEnd,
      double spBegin, double spEnd,
      double timeFraction)
    {
      double avgDf = (1.0 - timeFraction)*dfBegin + timeFraction*dfEnd;
      double prot = avgDf*(spBegin - spEnd);
      return prot;
    }

    private static double ProtectionLogLinear(
      Dt stepBegin, Dt stepEnd,
      double dfBegin, double dfEnd,
      double spBegin, double spEnd,
      double timeFraction)
    {
      double dt = Dt.Diff(stepBegin, stepEnd);
      double prot = ProtectionIntegral(0.0, dt,
        dfBegin, dfEnd, spBegin, spEnd);
      return prot;
    }

    /// <summary>
    ///   Calculate the protection Pv.
    /// </summary>
    /// <remarks>
    /// Calculate the protection PV as the integral
    /// <math>
    ///   V = - \int_{t_0}^{t_1} D(t)\, d S(t)
    ///     = \frac{h}{h+r} D_0\,S_0 \left(1 - e^{-(h+r)\Delta}\right)
    /// </math>
    /// where
    /// <math>
    ///   D(t) = D_0 \, e^{-r (t-t_0)},\qquad
    ///   S(t) = S_0 \, e^{-h (t-t_0)},\qquad
    ///   \Delta = t_1 - t_0
    /// </math>
    /// </remarks>
    /// <param name="t0"></param>
    /// <param name="t1"></param>
    /// <param name="d0"></param>
    /// <param name="d1"></param>
    /// <param name="s0"></param>
    /// <param name="s1"></param>
    /// <returns></returns>
    static double ProtectionIntegral(
      double t0, double t1,
      double d0, double d1,
      double s0, double s1)
    {
      // Calculate time interval.
      double dt = t1 - t0;
      if (Abs(dt) < 1.0/256)
      {
        // Use linear approximation for small time interval.
        return 0.5*(d0 + d1)*(s1 - s0);
      }

      // If the initial survival probability
      // or discount factor is zero, return zero.
      if (s0 <= 0 || d0 <= 0)
        return 0.0;

      // Calculate the hazard rate.
      s1 /= s0;
      if (s1 <= 0)
      {
        // treat this as infinite hazard rate
        return s0*d0;
      }
      double h = -Log(s1)/dt;

      // Calculate the interest rate
      d1 /= d0;
      if (d1 <= 0)
      {
        // treat this as infinite interest rate
        return 0.0;
      }
      double r = -Log(d1)/dt;

      // Calculate the integral in normal case.
      double lam = h + r;
      double v = (h/lam)*d0*s0*(1 - d1*s1);
      return v;
    }

    #endregion

    #region Default date
    private static Dt GetDefaultDate(Curve survivalCurve,
      Curve counterpartyCurve, out bool isCounterpartyDefaulted)
    {
      Dt defaultDate;
      if (survivalCurve == null)
      {
        defaultDate = counterpartyCurve?.JumpDate ?? Dt.Empty;
        isCounterpartyDefaulted = defaultDate.IsEmpty();
      }
      else
      {
        defaultDate = survivalCurve.JumpDate;
        isCounterpartyDefaulted = false;
        if (counterpartyCurve != null)
        {
          var date = counterpartyCurve.JumpDate;
          if (!date.IsEmpty() && date < defaultDate)
          {
            defaultDate = date;
            isCounterpartyDefaulted = true;
          }
        }
      }
      return defaultDate;
    }

    #endregion

    #endregion
  }
}
