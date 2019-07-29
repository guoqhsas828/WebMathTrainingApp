using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// MMD (Municipal Market Data) Rate Lock
  /// </summary>
  /// 
  /// <remarks>
  /// <para>A MMD Rate Lock is a contract between two counterparties 
  /// who agree to make payments to each other on a notional amount, 
  /// contingent upon whether the underlying municipal market data index scale
  /// is above or below a specified level on a given date in the contract. 
  /// For example, one counterparty(called "A"), who may participate in issuing 
  /// bond in future and try to lock the specified level of interest rate 
  /// such as today's rate on AAA MMD index, buys a MMD Rate Lock and 
  /// at the time of bond issue, if the interest rate has increased from the specified level,  
  /// "A" will receive a payment that equals to the difference between the specified level and
  /// actual level of the interest rate, multiplied by the notional amount and the DV01 specified 
  /// in the contract, from the other counterparty to the contract(called "B"); 
  /// If the interest rate has decreased from the specified level then the counterpart "A"
  /// makes a payment to "B" equal to the difference between the specified level and
  /// actual level of the interest rate, multiplied by the notional amount and the DV01 
  /// specified in the contract. </para>
  /// 
  /// <para>
  /// MMD rate lock contract is similar to a FRA agreement. The MMD Rate Lock 
  /// corresponds to the yield of the AAA MMD index with given maturity while 
  /// a FRA corresponds to a rate curve with a given index.
  /// </para>
  /// 
  /// </remarks>
  [Serializable]
  public class MmdRateLock : Product
  {
    #region Constructors

    /// <summary>
    /// Constructor for a MMD rate lock
    /// </summary>
    /// <param name="effective">effective date </param>
    /// <param name="determinationDate">determination date</param>
    /// <param name="contractTenor">contract tenor, such as 3m in 3mx5y</param>
    /// <param name="bondTenor">Bond tenor, such as 5y in 3mx5y</param>
    /// <param name="fixedRate">Fixed rate</param>
    /// <param name="dv01">Dv01 (in raw number)</param>
    /// <param name="ccy">Currency</param>
    public MmdRateLock(Dt effective, Dt determinationDate, Tenor contractTenor,
      Tenor bondTenor, double fixedRate, double dv01, Currency ccy)
      : base(effective, determinationDate, ccy)
    {
      ContractTenor = contractTenor;
      BondTenor = bondTenor;
      FixedRate = fixedRate;
      Dv01 = dv01;
      ContractName = String.Format("{0}x{1}", contractTenor.ToString("s", null), 
        bondTenor.ToString("s", null));
    }

    /// <summary>
    /// Constructor of MMD rate lock
    /// </summary>
    /// <param name="effective">settle date</param>
    /// <param name="determinationDate">determination date</param>
    /// <param name="contractName">contract/tenor term(string type)</param>
    /// <param name="fixedRate">Quoted fixed rate</param>
    /// <param name="dv01">Dv01 (in raw number)</param>
    /// <param name="ccy">Currency</param>
    public MmdRateLock(Dt effective, Dt determinationDate, 
      string contractName, double fixedRate, double dv01, Currency ccy)
      : base(effective, determinationDate, ccy)
    {
      Tenor contractTenor, bondTenor;
      if (Tenor.TryParseComposite(contractName, out contractTenor, out bondTenor))
      {
        ContractTenor = contractTenor;
        BondTenor = bondTenor;
      }
      FixedRate = fixedRate;
      Dv01 = dv01;
      ContractName = contractName;
    }
    #endregion Constructors

    #region Methods

    /// <summary>
    /// Generates the payment schedule for a given date.
    /// </summary>
    /// <param name="asOf">AsOf date</param>
    /// <param name="resets">Rate resets</param>
    /// <param name="referenceIndex">Reference index</param>
    /// <returns></returns>
    public PaymentSchedule GetPaymentSchedule(Dt asOf, RateResets resets, ReferenceIndex referenceIndex)
    {
      var pmtSchedule = new PaymentSchedule();
      // Calc expiry
      // rateReset date is the date the RateReset is physically published 
      Dt forwardDate = Maturity;
      Dt contractMaturity = Dt.Add(forwardDate, ContractTenor);
      Dt payDate = forwardDate;
      Dt resetDate = RateResetUtil.ResetDate(forwardDate, referenceIndex, Tenor.FromDays(ResetOffsetDays));
      RateResetState state;
      double rate = RateResetUtil.FindRate(resetDate, asOf, resets, true, out state);
      if (state == RateResetState.Missing && RateResetUtil.ProjectMissingRateReset(resetDate, asOf, forwardDate))
      {
        // prefer a reset for expiry when expiry == asOf (or in the time window up to start of period)
        // but will take a projection otherwise
        state = RateResetState.IsProjected;
      }

      var projectionParams = new ProjectionParams
      {
        ProjectionType = ProjectionType.SimpleProjection,
        ResetLag = Tenor.FromDays(ResetOffsetDays)
      };

      var rateProjector = CouponCalculator.Get(asOf, referenceIndex, projectionParams);

      var pmt = new FloatingInterestPayment(Dt.Empty, payDate, Ccy, forwardDate, contractMaturity, forwardDate, contractMaturity, Dt.Empty, Notional, 0.0, DayCount,
        Frequency.None, CompoundingConvention.None, rateProjector, null) {AccrualFactor = 1.0};

      if (state == RateResetState.ResetFound || state == RateResetState.ObservationFound)
      {
        pmt.EffectiveRate = rate;
      }

      // Add
      pmtSchedule.AddPayment(pmt);

      // Done
      return pmtSchedule;
    }

    /// <summary>
    /// Access resets information for cashflow generation
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="rateResets">Historical resets</param>
    /// <param name="refIndex">Reference index</param>
    /// <returns>Dictionary containing past and projected resets indexed by date</returns>
    public IDictionary<Dt, RateResets.ResetInfo> GetResetInfo(Dt asOf, RateResets rateResets, ReferenceIndex refIndex)
    {
      IDictionary<Dt, RateResets.ResetInfo> allInfo = new SortedDictionary<Dt, RateResets.ResetInfo>();
      PaymentSchedule ps = GetPaymentSchedule(asOf, rateResets, refIndex);
      foreach (FloatingInterestPayment ip in ps.GetPaymentsByType<FloatingInterestPayment>())
      {
        RateResetState state;
        Dt reset = ip.ResetDate;
        double rate;
        double? effectiveRate = null;
        if (ip.IsProjected)
        {
          rate = 0;
          state = RateResetState.IsProjected;
        }
        else
        {
          rate = RateResetUtil.FindRateAndReportState(reset, asOf, rateResets, out state);
          if (ip.RateResetState == RateResetState.ResetFound || ip.RateResetState == RateResetState.ObservationFound)
            effectiveRate = ip.EffectiveRate;
        }

        var rri = new RateResets.ResetInfo(reset, rate, state)
        {
          AccrualStart = ip.AccrualStart,
          AccrualEnd = ip.AccrualEnd,
          Frequency = ip.CompoundingFrequency,
          PayDt = ip.PayDt,
          IndexTenor = new Tenor(ip.CompoundingFrequency),
          ResetInfos = ip.GetRateResetComponents()
        };
        if (effectiveRate.HasValue)
          rri.EffectiveRate = effectiveRate;

        allInfo[reset] = rri;
      }
      return allInfo;
    }

    #endregion

    #region Properties

    /// <summary>
    /// The day count of a MMD rate lock.
    /// </summary>
    public DayCount DayCount
    {
      get { return DayCount.Thirty360; }
    }

    /// <summary>
    /// The determination date of a MMD rate lock
    /// </summary>
    public Dt DeterminationDate
    {
      get { return Maturity; }
    }


    /// <summary>
    /// The contract lock tenor in a MMD rate lock, 
    /// such as 3m in 3mx5y.
    /// </summary>
    public Tenor ContractTenor { get; private set; }

    /// <summary>
    /// The underlying bond tenor in a MMD rate lock, 
    /// such as 5y in 3mx5y.
    /// </summary>
    public Tenor BondTenor { get; private set; }

    /// <summary>
    /// The number of days implicit in the Contract lock tenor.
    ///  For example:
    /// "3m" is 90 days;
    /// "6m" is 180 days;
    /// "9m" is 270 days
    /// </summary>
    public int ContractDays
    {
      get { return (int) ContractTenor.Months*30; }
    }

    /// <summary>
    /// The Dv01 of the trade.
    /// </summary>
    public double Dv01 { get; private set; }

    /// <summary>
    /// Quoted fixed rate.
    /// </summary>
    public double FixedRate { get; private set; }

    /// <summary>
    /// String type of MMD rate lock Contract/Tenor term, such as 3mx5y.
    /// </summary>
    public string ContractName { get; private set; }

    /// <summary>
    /// Reset offset days
    /// </summary>
    public int ResetOffsetDays { get; set; }

    #endregion Properties
  }
}
