/*
 * ABSCashFlowGenerator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Class used to generate cash flows of ABS securities
  /// </summary>
  [Serializable]
  public class ABSCashFlowGenerator : BaseEntityObject
  {
    #region Data
    private Dt settleDate_;
    private double notional_;
    private CashflowStream cashflows_;
    #endregion // Data

    #region Properties
    /// <summary>
    /// Return the CashFlowStream of securitity that was passed to the CashFlowGenerator
    /// </summary>
    public CashflowStream Cashflows
    {
      get { return cashflows_; }
    }
    /// <summary>
    /// Return the Notional of the Cash Flows
    /// </summary>
    public double Notional
    {
      get { return notional_; }
    }

    /// <summary>
    /// Return the Settle Date (i.e. Valuation Date) of the Cash Flows
    /// </summary>
    public Dt SettleDate
    {
      get { return settleDate_; }
    }

    #endregion // Properties

    #region Contructors
    /// <summary>
    /// Constructor that generates the base cash flows (e.g. no prepayments or defaults) of an ABS security
    /// </summary>
    /// <param name="settle">"As of" date of cash flows</param>
    /// <param name="absec">Asset-backed security</param>
    /// <param name="Z0t">Discount curve</param>
    public ABSCashFlowGenerator(Dt settle, ABS absec, DiscountCurve Z0t)
    {
      settleDate_ = settle;
      notional_ = absec.OutstandingBalance;
      generateCF(absec, Z0t);
    }

    /// <summary>
    /// Class object that generates cash flows for Asset-Backed Securities using a Prepayment Model
    /// </summary>
    /// <param name="settle">"As of" date of cash flows</param>
    /// <param name="absec">Asset-backed security</param>
    /// <param name="pm">Prepayment model</param>
    /// <param name="Z0t">Discount curve</param>
    public ABSCashFlowGenerator(Dt settle, ABS absec, DiscountCurve Z0t, PrepaymentModel pm)
    {
      settleDate_ = settle;
      notional_ = absec.OutstandingBalance;
      generateCF(absec, Z0t, pm); // set memebers of ABSCashFlowGenerator object
    }


    /// <summary>
    /// Constructor for setting the CashflowStream data member of an ABSCashFlowGenerator object
    /// </summary>
    /// <param name="settle">"As of" date of cash flows</param>
    /// <param name="Bal">Outstanding balance as of "settle"</param>
    /// <param name="cfs">CashflowStream</param>
    public ABSCashFlowGenerator(Dt settle, double Bal, CashflowStream cfs)
    {
      settleDate_ = settle;
      notional_ = Bal;
      cashflows_ = cfs;
    }

    /// <summary>
    /// Clone class object
    /// </summary>
    /// <returns>Cloned object</returns>
    public override object Clone()
    {
      ABSCashFlowGenerator obj= (ABSCashFlowGenerator)base.Clone();
      obj.cashflows_ = this.cashflows_.clone();
      return obj;
    }

    #endregion // Constructors

    #region Methods

    // Find first cashflow on or after settlement (depending on includeSettle flag)
    private int findSettle(Dt settle, Schedule sched)
    {
      int firstIdx;
      for( firstIdx = 0; firstIdx < sched.Count; firstIdx++ )
        if( sched.GetPaymentDate(firstIdx) >= settle)
          break;
      return firstIdx ;
    }
    
    /// <summary>
    /// Given a cash product, generate its cash flows
    /// </summary>
    /// <param name="absec">Asset-backed security</param>
    /// <param name="Z0t">Discount curve</param>
    private void generateCF(ABS absec, DiscountCurve Z0t)
    {
      int amortOffset = 0;
      int amortizationTerm = absec.AmortizationSchedule.Length;
      int bulletTerm = absec.PaySchedule.Count - amortizationTerm;
      if (bulletTerm < 0)
      {
        bulletTerm = 0;
        amortOffset = amortizationTerm - absec.PaySchedule.Count;
      }
      cashflows_ = new CashflowStream();
      //      cashflows_.Add(absec.Effective, 0, 0, absec.OriginalFactor);
      double expectedFactor = computeExpectedFactor(amortOffset, absec.AmortizationSchedule);
      double totalPrincipal = 0;
      double balance = 0;
      double interest =0;
      double accrued = 0;
      double LIBOR =0;
      double survivalFactor = 0;
      int firstIdx = findSettle(this.settleDate_, absec.PaySchedule);
      for (int t = firstIdx; t < bulletTerm; t++)
      {
        if (t == firstIdx)
        {
          LIBOR = Z0t.F(this.settleDate_, absec.PaySchedule.GetPeriodEnd(t), DayCount.Actual360, Frequency.None);
          accrued = absec.GetCoupon(LIBOR) * Dt.Fraction(absec.PaySchedule.GetPeriodStart(t), this.settleDate_, absec.DayCount) * absec.OriginalFactor;
          interest = absec.GetCoupon(LIBOR) * Dt.Fraction(this.settleDate_, absec.PaySchedule.GetPeriodEnd(t), absec.DayCount) * absec.OriginalFactor;
          cashflows_.Add(absec.PaySchedule.GetPaymentDate(t), 0, accrued, interest, absec.OriginalFactor);
        }
        else
        {
          LIBOR = Z0t.F(absec.PaySchedule.GetPeriodStart(t), absec.PaySchedule.GetPeriodEnd(t), DayCount.Actual360, Frequency.None);
          accrued = 0;
          interest = absec.GetCoupon(LIBOR) * Dt.Fraction(absec.PaySchedule.GetPeriodStart(t), absec.PaySchedule.GetPeriodEnd(t), absec.DayCount) * cashflows_.GetNotional(t - firstIdx - 1);
          cashflows_.Add(absec.PaySchedule.GetPaymentDate(t), 0, accrued, interest, cashflows_.GetNotional(t - 1));
        }
      }
      for (int t = Math.Max(bulletTerm, firstIdx); t < absec.PaySchedule.Count; t++)
      {
        if (t == firstIdx)
        {
          survivalFactor = absec.OriginalFactor / expectedFactor;
          totalPrincipal = survivalFactor * absec.AmortizationSchedule[t + amortOffset - bulletTerm];
          expectedFactor -= absec.AmortizationSchedule[t + amortOffset - bulletTerm];
          balance = absec.OriginalFactor - totalPrincipal;
          LIBOR = Z0t.F(this.settleDate_, absec.PaySchedule.GetPeriodEnd(t), DayCount.Actual360, Frequency.None);
          accrued = absec.GetCoupon(LIBOR) * Dt.Fraction(absec.PaySchedule.GetPeriodStart(t), this.settleDate_, absec.DayCount) * absec.OriginalFactor;
          interest = absec.GetCoupon(LIBOR) * Dt.Fraction(this.settleDate_, absec.PaySchedule.GetPeriodEnd(t), absec.DayCount) * absec.OriginalFactor;
        }
        else
        {
          survivalFactor = cashflows_.GetNotional(t - 1) / expectedFactor;
          totalPrincipal = survivalFactor * absec.AmortizationSchedule[t + amortOffset - bulletTerm];
          expectedFactor -= absec.AmortizationSchedule[t + amortOffset - bulletTerm];
          balance = cashflows_.GetNotional(t - firstIdx - 1) - totalPrincipal;
          LIBOR = Z0t.F(absec.PaySchedule.GetPeriodStart(t), absec.PaySchedule.GetPeriodEnd(t), DayCount.Actual360, Frequency.None);
          interest = absec.GetCoupon(LIBOR) * Dt.Fraction(absec.PaySchedule.GetPeriodStart(t), absec.PaySchedule.GetPeriodEnd(t), absec.DayCount) * cashflows_.GetNotional(t - firstIdx - 1);
          accrued = 0;
        }
        cashflows_.Add(absec.PaySchedule.GetPaymentDate(t), totalPrincipal, accrued, interest, balance);
      }
      /*
      for (int t = 1; t <= bulletTerm; t++)
      {
        cashflows_.Add(absec.PaySchedule.GetPaymentDate(t - 1), 0, 0, cashflows_.GetNotional(t - 1));
      }
      for (int t = bulletTerm + 1; t <= absec.PaySchedule.Count; t++)
      {
        survivalFactor = cashflows_.GetNotional(t - 1) / expectedFactor;
        totalPrincipal = survivalFactor * absec.AmortizationSchedule[t + amortOffset - (bulletTerm + 1)];
        expectedFactor -= absec.AmortizationSchedule[t + amortOffset - (bulletTerm + 1)];
        balance = cashflows_.GetNotional(t - 1) - totalPrincipal;
        cashflows_.Add(absec.PaySchedule.GetPaymentDate(t - 1), totalPrincipal, 0, balance);
      }
 */
    }


    /// <summary>
    /// Take a cash product and prepayment model and generate its cash flows
    /// </summary>
    /// <param name="absec">Asset-backed security</param>
    /// <param name="Z0t">Discount curve</param>
    /// <param name="pm">Prepayment model</param>
    private void generateCF(ABS absec, DiscountCurve Z0t, PrepaymentModel pm) 
    {
      int amortOffset = 0;
      int amortizationTerm = absec.AmortizationSchedule.Length;
      int bulletTerm = absec.PaySchedule.Count - amortizationTerm;
      if (bulletTerm < 0)
      {
        bulletTerm = 0;
        amortOffset = amortizationTerm - absec.PaySchedule.Count;
      }
      cashflows_ = new CashflowStream();
      //        cashflows_.Add(absec.Effective, 0, 0, absec.OriginalFactor);
      double survivalFactor = 0;
      double scheduledPrincipal = 0;
      double unscheduledPrincipal = 0;
      double expectedFactor = computeExpectedFactor(amortOffset, absec.AmortizationSchedule);
      double balance;
      double accrued;
      double interest;
      double LIBOR;
      int firstIdx = findSettle(this.settleDate_, absec.PaySchedule);
      for (int t = firstIdx; t < bulletTerm; t++)
      {
        if (t == firstIdx)
        {
          unscheduledPrincipal = absec.OriginalFactor * pm.PrepaymentRates[t];
          balance = absec.OriginalFactor - unscheduledPrincipal;
          LIBOR = Z0t.F(this.settleDate_, absec.PaySchedule.GetPeriodEnd(t), DayCount.Actual360, Frequency.None);
          accrued = absec.GetCoupon(LIBOR) * Dt.Fraction(absec.PaySchedule.GetPeriodStart(t), this.settleDate_, absec.DayCount) * absec.OriginalFactor;
          interest = absec.GetCoupon(LIBOR) * Dt.Fraction(this.settleDate_, absec.PaySchedule.GetPeriodEnd(t), absec.DayCount) * absec.OriginalFactor;
        }
        else
        {
          unscheduledPrincipal = cashflows_.GetNotional(t - firstIdx - 1) * pm.PrepaymentRates[t];
          balance = cashflows_.GetNotional(t - firstIdx - 1) - unscheduledPrincipal;
          LIBOR = Z0t.F(absec.PaySchedule.GetPeriodStart(t), absec.PaySchedule.GetPeriodEnd(t), DayCount.Actual360, Frequency.None);
          interest = absec.GetCoupon(LIBOR) * Dt.Fraction(absec.PaySchedule.GetPeriodStart(t), absec.PaySchedule.GetPeriodEnd(t), absec.DayCount) * cashflows_.GetNotional(t - firstIdx - 1);
          accrued = 0;
        }
        cashflows_.Add(absec.PaySchedule.GetPaymentDate(t), unscheduledPrincipal, accrued, interest, balance);
      }
      for (int t = Math.Max(bulletTerm, firstIdx); t < absec.PaySchedule.Count; t++)
      {
        if (t == firstIdx)
        {
          unscheduledPrincipal = absec.OriginalFactor * pm.PrepaymentRates[t];
          survivalFactor = (absec.OriginalFactor - unscheduledPrincipal) / expectedFactor;
          scheduledPrincipal = survivalFactor * absec.AmortizationSchedule[t + amortOffset - bulletTerm];
          expectedFactor -= absec.AmortizationSchedule[t + amortOffset - bulletTerm];
          balance = absec.OriginalFactor - (scheduledPrincipal + unscheduledPrincipal);
          LIBOR = Z0t.F(this.settleDate_, absec.PaySchedule.GetPeriodEnd(t), DayCount.Actual360, Frequency.None);
          accrued = absec.GetCoupon(LIBOR) * Dt.Fraction(absec.PaySchedule.GetPeriodStart(t), this.settleDate_, absec.DayCount) * absec.OriginalFactor;
          interest = absec.GetCoupon(LIBOR) * Dt.Fraction(this.settleDate_, absec.PaySchedule.GetPeriodEnd(t), absec.DayCount) * absec.OriginalFactor;
        }
        else
        {
          unscheduledPrincipal = cashflows_.GetNotional(t - firstIdx - 1) * pm.PrepaymentRates[t];
          survivalFactor = (cashflows_.GetNotional(t - firstIdx - 1) - unscheduledPrincipal) / expectedFactor;
          scheduledPrincipal = survivalFactor * absec.AmortizationSchedule[t + amortOffset - bulletTerm];
          expectedFactor -= absec.AmortizationSchedule[t + amortOffset - bulletTerm];
          LIBOR = Z0t.F(absec.PaySchedule.GetPeriodStart(t), absec.PaySchedule.GetPeriodEnd(t), DayCount.Actual360, Frequency.None);
          interest = absec.GetCoupon(LIBOR) * Dt.Fraction(absec.PaySchedule.GetPeriodStart(t), absec.PaySchedule.GetPeriodEnd(t), absec.DayCount) * cashflows_.GetNotional(t - firstIdx - 1);
          accrued = 0;
          balance = cashflows_.GetNotional(t - firstIdx - 1) - (scheduledPrincipal + unscheduledPrincipal);
        }
        cashflows_.Add(absec.PaySchedule.GetPaymentDate(t), scheduledPrincipal + unscheduledPrincipal, accrued, interest, balance);
      }
      /*
                for (int t = 1; t <= bulletTerm; t++)
                {
                  unscheduledPrincipal = cashflows_.GetNotional(t-1) * pm.PrepaymentRates[t];
                  balance = cashflows_.GetNotional(t-1) - unscheduledPrincipal;
                  cashflows_.Add(absec.PaySchedule.GetPaymentDate(t - 1), unscheduledPrincipal, 0, balance);
                }
                for (int t = bulletTerm+1; t <= absec.PaySchedule.Count; t++)
                {
                  unscheduledPrincipal = cashflows_.GetNotional(t-1) * pm.PrepaymentRates[t];
                  survivalFactor = (cashflows_.GetNotional(t-1) - unscheduledPrincipal) / expectedFactor;
                  scheduledPrincipal = survivalFactor * absec.AmortizationSchedule[t + amortOffset - (bulletTerm + 1)];
                  expectedFactor -= absec.AmortizationSchedule[t + amortOffset - (bulletTerm + 1)];
                  balance = cashflows_.GetNotional(t - 1) - (scheduledPrincipal + unscheduledPrincipal);
                  cashflows_.Add(absec.PaySchedule.GetPaymentDate(t - 1), scheduledPrincipal + unscheduledPrincipal, 0, balance);
                }
        */
    }

    /// <summary>
    /// Method for computing the balance at each period given the asset-backed security's scheduled amortization
    /// </summary>
    /// <param name="offset">Max(Amortization periods - Remaining payment periods (from pricing date), 0) and </param>
    /// <param name="amortSched">array of schedule amortization payments as a percentage of original balance</param>
    /// <returns>Scheduled balance of security on pricing date</returns>
    private double computeExpectedFactor(int offset, double[] amortSched)
    {
      double cumAmort = 0;
      for (int i = 0; i < offset; i++)
      {
        cumAmort += amortSched[i];
      }
      return (1 - cumAmort);
    }
    #endregion // Methods
  }
}