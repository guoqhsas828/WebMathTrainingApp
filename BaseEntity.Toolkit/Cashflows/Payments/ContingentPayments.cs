
/*
 * ContingentPayment.cs
 * Copyright(c)   2002-2018. All rights reserved.
*/



using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows.Payments
{
  /// <summary>
  ///  A payment which is made only when some events happen
  ///  in the specified reference period.
  /// </summary>
  public abstract class ContingentPayment : Payment
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="ContingentPayment" /> class.
    /// </summary>
    /// <param name="beginDate">The reference period begin date</param>
    /// <param name="endDate">The reference period end date</param>
    /// <param name="ccy">The currency</param>
    protected ContingentPayment(Dt beginDate, Dt endDate, Currency ccy)
      : base(endDate, ccy)
    {
      BeginDate = beginDate;
      CreditRiskEndDate = endDate;
    }

    /// <summary>
    /// The begin date of the reference period
    /// </summary>
    public Dt BeginDate { get; }

    /// <summary>
    /// The end date of the reference period
    /// </summary>
    public Dt EndDate => GetCreditRiskEndDate();
  }

  /// <summary>
  ///  A payment of $1 continent on that a credit default event
  ///  happens in the specified reference period.
  /// </summary>
  public class CreditContingentPayment : ContingentPayment
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="CreditContingentPayment"/> class.
    /// </summary>
    /// <param name="beginDate">The begin date.</param>
    /// <param name="endDate">The end date.</param>
    /// <param name="ccy">The currency</param>
    public CreditContingentPayment(Dt beginDate, Dt endDate, Currency ccy)
      : base(beginDate, endDate, ccy)
    {
    }

    #region Properties

    /// <summary>
    /// Gets or sets the time grids for integration
    /// </summary>
    /// <value>The time grids</value>
    internal IReadOnlyList<Dt> TimeGrids { get; set; }

    internal bool IncludeEndDateProtection { get; set; }

    #endregion

    #region Methods

    /// <summary>
    ///  The present value of one unit payment contingent
    ///  on that a credit event happens in the period.
    /// </summary>
    /// <param name="discountFunction">Discount curve</param>
    /// <param name="survivalFunction">Survive probability curve</param>
    /// <returns>Risky discounted price</returns>
    public override double RiskyDiscount(
      Func<Dt, double> discountFunction,
      Func<Dt, double> survivalFunction)
    {
      if (survivalFunction == null) return 0.0;

      var calculator = survivalFunction.Target as DefaultRiskCalculator;
      if (calculator != null)
      {
        return calculator.Protection(this, discountFunction);
      }

      return ProtectionPv(
        BeginDate, EndDate, TimeGrids,
        discountFunction, survivalFunction);
    }

    /// <summary>
    /// When Amount is derived from other fields implement this
    /// </summary>
    /// <returns>System.Double.</returns>
    protected override double ComputeAmount()
    {
      return 1;
    }

    public static double ProtectionPv(Dt begin, Dt end,
      Func<Dt, double> discountFunction,
      Func<Dt, double> survivalFunction)
    {
      Debug.Assert(survivalFunction != null);

      double sp0 = survivalFunction(begin),
        sp1 = survivalFunction(end),
        df0 = discountFunction(begin),
        df1 = discountFunction(end);
      return (sp0 - sp1)*(df0 + df1)/2;
    }

    public static double ProtectionPv(
      Dt beginDate, Dt endDate,
      IEnumerable<Dt> timeGrids,
      Func<Dt, double> discountFunction,
      Func<Dt, double> survivalFunction)
    {
      Debug.Assert(endDate > beginDate);

      if (timeGrids == null)
      {
        return ProtectionPv(beginDate, endDate,
          discountFunction, survivalFunction);
      }

      var pv = 0.0;
      Dt begin = beginDate;
      foreach (var dt in timeGrids)
      {
        if (dt <= begin) continue;
        var cmp = Dt.Cmp(dt, endDate);
        if (cmp >= 0)
        {
          // This is for the exact time consistency,
          // such that for t1 < t2 < t3, the additive equation
          //   P(t1, t2) + P(t2, t3) = P(t1, t3)
          // always holds if the same time grids are used.
          var beginPv = ProtectionPv(begin, dt,
            discountFunction, survivalFunction);
          var endPv = cmp == 0 ? 0.0 : ProtectionPv(
            endDate, dt, discountFunction, survivalFunction);
          pv += beginPv - endPv;
          return pv;
        }
        pv += ProtectionPv(begin, dt,
          discountFunction, survivalFunction);
        begin = dt;
      }
      if (endDate > begin)
      {
        pv += ProtectionPv(begin, endDate,
          discountFunction, survivalFunction);
      }
      return pv;
    }

    #endregion
  }

  public class RecoveryPayment : CreditContingentPayment
  {
    public RecoveryPayment(Dt beginDate, Dt endDate,
      double recoveryRate, Currency ccy)
      : base(beginDate, endDate, ccy)
    {
      RecoveryRate = recoveryRate;
    }

    #region Properties

    /// <summary>
    /// Is funded effects the sign on the amount, and whether it is accounted as amount or loss in the cashflow object
    /// </summary>
    public bool IsFunded { get; set; }

    /// <summary>
    /// Notional 
    /// </summary>
    public double Notional { get; set; }

    /// <summary>
    /// Recovery rate
    /// </summary>
    public double RecoveryRate { get; }

    #endregion

    #region Methods

    protected override double ComputeAmount()
    {
      return IsFunded ? Notional*RecoveryRate : Notional*(RecoveryRate - 1.0);
    }

    #endregion
  }
}
