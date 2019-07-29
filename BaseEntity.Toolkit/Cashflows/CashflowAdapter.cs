using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows
{

  #region  CashflowAdapter

  [Serializable]
  public class CashflowAdapter
  {
    #region Constructors
    //constructor using legacy cash flow
    public CashflowAdapter(Cashflow cf)
    {
      _data = cf;
    }

    //Constructor using payment schedule
    public CashflowAdapter(PaymentSchedule ps)
    {
      _ps = ps;
      _data = ps?.OfType<InterestPayment>().OrderBy(p => p.PayDt).ToList();
    }

    //Constructor
    public CashflowAdapter(PaymentSchedule ps, double originalPrincipal)
    {
      _ps = ps;
      _data = ps?.OfType<InterestPayment>().OrderBy(p => p.PayDt).ToList();
      _originalPrincipal = originalPrincipal;
    }

    #endregion Constructors

    #region Properties
    /// <summary>
    /// Data. Either Payment schedule or legacy cashflow 
    /// </summary>
    public object Data => _data;

    /// <summary>
    /// Payment schedule
    /// </summary>
    public PaymentSchedule Ps => _ps;


    /// <summary>
    /// Princile exchange payment amounts extracting from the payment schedule. 
    /// </summary>
    public double[] PeAmounts
    {
      get
      {
        if (_peAmounts == null)
        {
          var ips = (List<InterestPayment>) _data;
          _peAmounts = new double[ips.Count];
          var i = 0;
          foreach (var ip in ips)
          {
            var notional = 0.0;
            IEnumerable<Payment> payments = _ps.GetPaymentsOnDate(ip.PayDt);
            var pes = new List<PrincipalExchange>();
            foreach (var p in payments)
            {
              var pe = p as PrincipalExchange;
              if (pe != null) pes.Add(pe);
            }
            if (pes.Count == 1)
            {
              notional = pes[0].Notional;
            }
            else if (pes.Count > 1)
            {
              foreach (var pe in pes)
              {
                notional += pe.Notional;
              }
            }
            _peAmounts[i++] = notional;
          }
        }
        return _peAmounts;
      }
    }

 
    //Recovery payments from payment schedule
    public List<RecoveryPayment> RecoveryPayments
    {
      get
      {
        if (_recoveryPayments == null)
        {
          _recoveryPayments = new List<RecoveryPayment>();
          var rps = _ps.OfType<RecoveryPayment>().OrderBy(p => p.PayDt).ToList();
          var ips = (List<InterestPayment>) _data;
          var iCount = ips.Count;
          if (iCount == 0)
          {
            _recoveryPayments = rps;
            return _recoveryPayments;
          }

          foreach (var ip in ips)
          {
            bool getMatched = false;
            int index = 0;
            for (int i = 0; i < rps.Count; i++)
            {
              if (Matched(ip, rps[i]))
              {
                getMatched = true;
                index = i;
                break;
              }
            }

            if (getMatched)
            {
              _recoveryPayments.Add(rps[index]);
            }
            else
            {
              _recoveryPayments.Add(new RecoveryPayment(ip.AccrualStart, ip.AccrualEnd,
                0.0, ip.Ccy)
              {
                Notional = ip.Notional,
                IsFunded = true
              });
            }
          }

          if (iCount != _recoveryPayments.Count)
            throw new ArgumentException("The counts of " +
                                        "recovery and interest payments are not matched");
        }
        return _recoveryPayments;
      }
    }

    public int Count =>
      (_data as Cashflow)?.Count
      ?? (((List<InterestPayment>) _data)?.Count ?? 0);

    public Dt Effective =>
      (_data as Cashflow)?.Effective
      ?? ((List<InterestPayment>) _data)[0].AccrualStart;

    public double OriginalPrincipal =>
      (_data as Cashflow)?.OriginalPrincipal
      ?? (_originalPrincipal > 0.0 ? _originalPrincipal : 1.0);

    public bool IsCashflow => (Data as Cashflow) != null;


    #endregion Properties

    #region PropertyHelpers

    public Dt GetStartDt(int index) =>
      (_data as Cashflow)?.GetStartDt(index)
      ?? ((List<InterestPayment>)_data)[index].AccrualStart;

    public Dt GetEndDt(int index) =>
      (_data as Cashflow)?.GetEndDt(index)
      ?? ((List<InterestPayment>)_data)[index].AccrualEnd;

    public Dt GetDt(int index) =>
      (_data as Cashflow)?.GetDt(index)
      ?? ((List<InterestPayment>)_data)[index].PayDt;

    public double GetPrincipalAt(int index) =>
      (_data as Cashflow)?.GetPrincipalAt(index)
      ?? ((List<InterestPayment>)_data)[index].Notional;

    public double GetAmount(int index) =>
      (_data as Cashflow)?.GetAmount(index) ?? PeAmounts[index];

    public double GetAccrued(int index) =>
      (_data as Cashflow)?.GetAccrued(index)
      ?? (((List<InterestPayment>)_data)[index].DomesticAmount
        / OriginalPrincipal);

    public double GetDefaultAmount(int index) =>
      (_data as Cashflow)?.GetDefaultAmount(index)
      ?? RecoveryPayments[index].Amount;

    public double GetDefaultTiming() =>
      (_data as Cashflow)?.GetDefaultTiming() ?? 0.5;

    public DayCount GetDayCount(int index) =>
      (_data as Cashflow)?.DayCount
      ?? ((List<InterestPayment>)_data)[index].DayCount;

    public double GetPeriodFraction(int index) =>
      (_data as Cashflow)?.GetPeriodFraction(index)
      ?? ((List<InterestPayment>)_data)[index].AccrualFactor;

    //For the cash flow we have a unique payment frequency.
    //In term of payment schedule, it make more sense to
    //assign each payment a frequency. 
    public Frequency GetFrequency(int index) =>
      (_data as Cashflow)?.Frequency
      ?? ((List<InterestPayment>)_data)[index]
      .CompoundingFrequency;

    //this function is to set the amount of the cashflowadapter.
    //for example, if the call price is not 1.0 for loan, we need to
    //set amount for the loan.
    public void SetAmount(int index, double factor)
    {
      var cf = _data as Cashflow;
      if (cf != null)
      {
        cf.Set(index, cf.GetAmount(index) * factor,
          cf.GetAccrued(index), cf.GetDefaultAmount(index));
      }
      else
      {
        PeAmounts[index] *= factor;
      }
    }

    public double GetRemainingNotional(Dt date)
    {
      if (date >= Effective)
      {
        int n = Count;
        for (int i = 0; i < n; ++i)
        {
          if (date <= GetEndDt(i))
            return GetPrincipalAt(i);
        }
      }
      return 0.0;
    }

    #endregion PropertyHelpers

    #region Pv
    /// <summary>
    /// This pv function is the main funtion to calculate pv
    /// when using the cashflow adapter. It includes both the legacy
    /// cashflow pv calculation and the payment schedule pv
    /// calculation.
    /// </summary>
    /// <param name="asOf">Asof date</param>
    /// <param name="settle">settle date</param>
    /// <param name="discountCurve">discount curve</param>
    /// <param name="survivalCurve">survival curve</param>
    /// <param name="counterpartyCurve">counterparty curve</param>
    /// <param name="correlation">correlation</param>
    /// <param name="stepSize">step size</param>
    /// <param name="stepUnit">step unit</param>
    /// <param name="flags">cashflow model flags</param>
    /// <returns></returns>
    public double Pv(Dt asOf, Dt settle, DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, SurvivalCurve counterpartyCurve,
      double correlation, int stepSize, TimeUnit stepUnit, CashflowModelFlags flags)
    {
      double pv;
      var cf = _data as Cashflow;
      if (cf != null)
      {
        pv = cf.CashflowPv(asOf, settle, discountCurve, survivalCurve,
          counterpartyCurve, correlation, stepSize, stepUnit, flags, cf.Count);
      }
      else
      {
        pv = _ps.CalculatePv(asOf, settle, discountCurve, survivalCurve,
            counterpartyCurve, correlation, stepSize, stepUnit, flags)
          /OriginalPrincipal;
      }
      return pv;
    }

    /// <summary>
    /// This function to calculate pv with adjusted the cashflow model flags.
    /// </summary>
    /// <remarks>
    /// In the legacy cash flow pv, there are two functions, Cashflow.Pv, Cashflow.Price,
    /// to calculate the pv, the difference between them is the cashflow model flags.
    /// If the _data is payment schedule, this pv function is the same as above Pv(...) function.
    /// </remarks>
    /// <param name="asOf">Asof date</param>
    /// <param name="settle">settle date</param>
    /// <param name="discountCurve">discount curve</param>
    /// <param name="survivalCurve">survival curve</param>
    /// <param name="counterpartyCurve">counterparty curve</param>
    /// <param name="correlation">correlation</param>
    /// <param name="stepSize">step size</param>
    /// <param name="stepUnit">step unit</param>
    /// <param name="flags">cashflow model flags</param>
    /// <returns></returns>
    public double BackwardCompatiblePv(Dt asOf, Dt settle, DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, SurvivalCurve counterpartyCurve,
      double correlation, int stepSize, TimeUnit stepUnit, CashflowModelFlags flags)
    {
      var cf = _data as Cashflow;
      if (cf != null)
      {
        cf.DefaultPayment = null;
        flags |= (CashflowModelFlags.IncludeFees
                  | CashflowModelFlags.IncludeProtection
                  | CashflowModelFlags.IgnoreAccruedInProtection
                  | CashflowModelFlags.IncludeAccruedOnSettlementDefault)
                 | (survivalCurve != null && survivalCurve.Stressed
                   ? CashflowModelFlags.LogLinearApproximation : 0);
      }
      return Pv(asOf, settle, discountCurve, survivalCurve, counterpartyCurve,
        correlation, stepSize, stepUnit, flags);
    }

    #endregion Pv

    #region Legacy cashflow adapter, to be removed later

    //Right now this block will deal with four functions 
    //in the RateVolatilityUtil.cs for legacy cashflow method
    public Dt AsOf
    {
      set { CantBeNull(_data as Cashflow).AsOf = value; }
    }

    public DayCount DayCount
    {
      set { CantBeNull(_data as Cashflow).DayCount = value; }
    }


    private static Cashflow CantBeNull(Cashflow cf)
    {
      if (cf == null)
        throw new ToolkitException("Data is not a cashflow");
      return cf;
    }

    public static implicit operator CashflowAdapter(Cashflow cf)
    {
      return new CashflowAdapter(cf);
    }

    public static implicit operator Cashflow(CashflowAdapter cf)
    {
      return CantBeNull(cf._data as Cashflow);
    }

    #endregion

    #region Helpers
    /// <summary>
    /// Helper to clean the notional payment for cashflow adapter
    /// </summary>
    /// <returns></returns>
    public CashflowAdapter ClearNotionalPayment()
    {
      var cf = _data as Cashflow;
      if (cf != null)
      {
        var cfClone = cf.clone();
        int n = cfClone?.Count ?? 0;
        for (int i = 0; i < n; ++i)
          cfClone.ClearAmount(i);
        return new CashflowAdapter(cfClone);
      }

      var payments = new PaymentSchedule();
      foreach (var payment in _ps)
      {
        var otp = payment as PrincipalExchange;
        if (otp != null) continue;
        payments.AddPayment(payment);
      }
      return new CashflowAdapter(payments);
    }

    /// <summary>
    /// Helper to create the unit cashflow/payment for cashflow adapter
    /// </summary>
    /// <returns></returns>
    public CashflowAdapter CreateUnitCashflow()
    {
      var cf = _data as Cashflow;
      if (cf != null)
      {
        return new CashflowAdapter(cf.CreateUnitCashflow());
      }

      var payments = new PaymentSchedule();
      foreach (var ip in (List<InterestPayment>) _data)
      {
        payments.AddPayment(new FixedInterestPayment(ip.PreviousPaymentDate, ip.PayDt,
          ip.Ccy, ip.CycleStartDate, ip.CycleEndDate, ip.PeriodStartDate,
          ip.PeriodEndDate, ip.ExDivDate, ip.Notional, 1.0,
          ip.DayCount, ip.CompoundingFrequency)
        {
          CreditRiskEndDate = ip.GetCreditRiskEndDate(),
          AccruedFractionAtDefault = ip.AccruedFractionAtDefault,
          AccrueOnCycle = ip.AccrueOnCycle,
          FXCurve = ip.FXCurve,
          Amount = ip.AccrualFactor
        });
      }
      payments.AddPayments(RecoveryPayments);
      return new CashflowAdapter(payments);
    }

    /// <summary>
    /// Helper to create the loss cashflow/payment for cashflow adapter
    /// </summary>
    /// <returns></returns>
    public CashflowAdapter CreateLossCashflow()
    {
      var cf = _data as Cashflow;
      if (cf != null)
      {
        return new CashflowAdapter(cf.CreateLossCashflow());
      }

      var payments = new PaymentSchedule();
      if (_ps == null || _ps.Count == 0)
        return new CashflowAdapter(payments);
      var lossPayments = RecoveryPayments.Select(rp =>
        {
          Debug.Assert(rp.IsFunded);
          var lossRate = 1 - rp.Amount / rp.Notional;
          return new RecoveryPayment(rp.BeginDate, rp.EndDate, lossRate, rp.Ccy)
          {
            Notional = rp.Notional,
            IsFunded = true
          };
        })
        .ToList();

      payments.AddPayments(lossPayments);
      return new CashflowAdapter(payments);
    }

    private static bool Matched(InterestPayment ip, RecoveryPayment rp)
    {
      if (rp.BeginDate != ip.AccrualStart || rp.EndDate != ip.AccrualEnd)
      {
        return rp.BeginDate == ip.PreviousPaymentDate
               && rp.EndDate == ip.PayDt;
      }
      return true;
    }
    #endregion Helpers

    #region Data

    private readonly object _data;
    private readonly PaymentSchedule _ps;
    private List<RecoveryPayment> _recoveryPayments;
    private double[] _peAmounts;
    private readonly double _originalPrincipal;

    #endregion Data
  }

  #endregion CashflowAdpater

  #region AdapterUtil
  public static class AdapterUtil
  {
    /// <summary>
    /// This function is to calculate the legay cash flow pv for the cashflow adapter
    /// Because cashflow adapter also adapts the legacy cash flow, this function is mainly
    /// for the back-ward compatible. At some point in future, we can retire this function.
    /// </summary>
    /// <param name="cf">cash flow</param>
    /// <param name="asOf">asof date</param>
    /// <param name="settle">settle date</param>
    /// <param name="discountCurve">discount curve</param>
    /// <param name="survivalCurve">survival curve</param>
    /// <param name="counterpartyCurve">counterparty curve</param>
    /// <param name="correlation">correlation</param>
    /// <param name="stepSize">step size</param>
    /// <param name="stepUnit">step unit</param>
    /// <param name="flags">cashflow model flags</param>
    /// <param name="last">The index of the last cash flow</param>
    /// <returns></returns>
    public static double CashflowPv(this Cashflow cf,
      Dt asOf, Dt settle, DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, SurvivalCurve counterpartyCurve,
      double correlation, int stepSize, TimeUnit stepUnit,
      CashflowModelFlags flags, int last = -1)
    {
      if (cf == null)
        return 0.0;

      double pv = CashflowModel.Price(cf, asOf, settle, discountCurve,
        survivalCurve, counterpartyCurve, correlation,
        (int)flags, stepSize, stepUnit, last < 0 ? cf.Count : last);

      if (cf.DefaultPayment != null)
      {
        // WillDefault means we are in the middle of VOD/JTD computation
        bool includeDefaultPaymentOnSettle
          = (survivalCurve != null && survivalCurve.Defaulted == Defaulted.WillDefault);
        pv += DefaultPv(cf.DefaultPayment, asOf, settle, discountCurve,
          includeDefaultPaymentOnSettle);
      }
      return pv;
    }

    /// <summary>
    /// To calculate the default pv for the legacy cash flow.
    /// </summary>
    /// <param name="defaultPayment"></param>
    /// <param name="asOf"></param>
    /// <param name="settle"></param>
    /// <param name="dc"></param>
    /// <param name="includeDefaultPaymentOnSettle"></param>
    /// <returns></returns>
    public static double DefaultPv(
      this Cashflow.ScheduleInfo defaultPayment,
      Dt asOf, Dt settle, DiscountCurve dc, bool includeDefaultPaymentOnSettle)
    {
      if (defaultPayment == null) return 0;

      if (defaultPayment.Date < settle
          || (defaultPayment.Date == settle && !includeDefaultPaymentOnSettle))
      {
        return 0;
      }
      double ret = defaultPayment.Accrual + (defaultPayment.Amount)
                   * dc.DiscountFactor(asOf, defaultPayment.Date);
      return ret;
    }

    /// <summary>
    /// The helper to create the unit cash flow for legacy cashflow
    /// </summary>
    /// <param name="cf"></param>
    /// <returns></returns>
    public static Cashflow CreateUnitCashflow(this Cashflow cf)
    {
      var unitCf = cf.clone();
      int count = cf.Count;
      for (int i = 0; i < count; ++i)
      {
        double frac = cf.GetPeriodFraction(i);
        unitCf.Set(i, cf.GetPrincipalAt(i), 0.0, frac, 1.0,
          cf.GetDefaultAmount(i));
      }
      return unitCf;
    }

    /// <summary>
    /// The helper to create the loss cash flow for legacy cashflow
    /// </summary>
    /// <param name="cf"></param>
    /// <returns></returns>
    public static Cashflow CreateLossCashflow(this Cashflow cf)
    {
      var lossCf = cf.clone();
      int count = cf.Count;
      for (int i = 0; i < count; ++i)
      {
        double principal = cf.GetPrincipalAt(i);
        double recovery = cf.GetDefaultAmount(i);
#if DEBUG
        if (recovery > principal)
        {
          throw new ToolkitException(String.Format(
            "Recovery {0} > Principal {1} at cashflow {2}",
            recovery, principal, i));
        }
#endif
        lossCf.Set(i, cf.GetPrincipalAt(i), 0.0, 0.0, 0.0,
          principal - recovery);
      }
      return lossCf;
    }
   
    public static CashflowModelFlags CreateFlags(bool includeSettlement,
      bool includeMaturityProtection, bool fullFirstCoupon)
    {
      return CashflowModelFlags.IncludeFees | CashflowModelFlags.IncludeProtection |
             (includeSettlement ? CashflowModelFlags.IncludeSettlePayments : 0) |
             (includeMaturityProtection ? CashflowModelFlags.IncludeMaturityProtection : 0) |
             (fullFirstCoupon ? CashflowModelFlags.FullFirstCoupon : 0);
    }

    public static bool IsNullOrEmpty(this CashflowAdapter cfa)
    {
      return cfa?.Data == null || cfa.Count == 0;
    }
  }

  #endregion AdapterUtil

} //namespace
