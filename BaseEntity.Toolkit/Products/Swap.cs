// 
//  -2013. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Swap contract composed of two swap legs. Could be both floating, fixed or any combination thereof. 
  /// </summary>
  /// <remarks>
  ///   <para>An interest rate swap is an OTC contract between two counterparties to exchange a set
  ///   of cashflows over a defined period of time. Typically one 'leg' of the swap is a fixed
  ///   rate and the other is some variable rate relative to a floating interest rate such as LIBOR
  ///   or a currency. The two most common forms of swaps are vanilla interest rate swaps and
  ///   currency swaps.</para>
  /// 
  ///   <p><h2>Vanilla Interest Rate Swap</h2></p>
  ///   <para>The most common form of swap is the 'plain vanilla' interest rate swap. In this case
  ///   Party A agrees to pay Party B a fixed rate of interest on specific dates for an agreed period of
  ///   time. Party B agrees to pay Party A a floating rate of interest for the same specified dates and
  ///   period in the same currency.</para>
  ///   <h1 align="center"><img src="IRSwap.png" width="80%"/></h1>
  ///   <para>For example, Company A and Company B enter into a five-year swap with the following terms:</para>
  ///   <list type="bullet">
  ///     <item><description>Company A pays Company B an amount equal to 5% per annum on $10 million notional.</description></item>
  ///     <item><description>Company B pays Company A an amount equal to 6 month LIBOR per annum on a $10 million notional.</description></item>
  ///   </list>
  ///   <para>For vanilla interest rate swaps the floating rate is determined at the beginning of the settlement
  ///   period and payments are usually netted. There is no exchange of principal.</para>
  ///   <para>ISDA provides standard documentation for Interest Rate Swaps. Definitions of the
  ///   accrual conventions, business day conventions, etc. are defined by ISDA.</para>
  /// 
  ///   <p><h2>Currency Swaps</h2></p>
  ///   <para>For a vanilla currency swap, Part A agrees to pay Party B a fixed rate of interested on a
  ///   principal in one currency in exchange for Party B paying Party A a fixed rate of interest on a
  ///   principal in another currency. In this case the principal amounts will be exchanged at the swap's
  ///   inception and maturity.</para>
  ///   <h1 align="center"><img src="CCSwap.png" width="80%"/></h1>
  ///   <note>LIBOR (London Interbank Offer Rate) is the interest rate offered by London banks to other
  ///   banks for eurodollar deposits. There are similar rates for other currencies. The swaps market
  ///   often sets floating payments off these indices.</note>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.SwapPricer"/>
  /// <seealso cref="SwapLeg"/>
  [Serializable]
  public class Swap : Product, ICallable
  {
    #region Constructors

    /// <summary>
    /// Swap constructor from two swap legs
    /// </summary>
    /// <param name="receiverLeg">SwapLeg object</param>
    /// <param name="payerLeg">SwapLeg object</param>
    public Swap(SwapLeg receiverLeg, SwapLeg payerLeg)
      : base(Dt.Min(receiverLeg.Effective, payerLeg.Effective),
             (Dt.Cmp(receiverLeg.Maturity, payerLeg.Maturity) > 0) ? receiverLeg.Maturity : payerLeg.Maturity,
             (payerLeg.Ccy == receiverLeg.Ccy) ? receiverLeg.Ccy : Currency.None)
    {
      ReceiverLeg = receiverLeg;
      PayerLeg = payerLeg;
    }

    /// <summary>
    /// Vanilla Swap constructor
    /// </summary>
    /// <remarks>
    ///   <para>Creates a vanilla fixed/floating (pay fixed, receive floating) swap.</para>
    /// </remarks>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="premium">Premium for fixed leg</param>
    /// <param name="ccy">Currency for fixed leg</param>
    /// <param name="dayCount">Daycount convention for fixed leg</param>
    /// <param name="fixedLegFreq">Payment frequency of payment for fixed leg</param>
    /// <param name="roll">Roll convention for fixed leg</param>
    /// <param name="calendar">Calendar for fixed leg</param>
    /// <param name="floatLegFreq">Payment frequency for floating leg</param>
    /// <param name="index">Reference index for floating leg</param>
    public Swap(Dt effective, Dt maturity, double premium, Currency ccy, DayCount dayCount,
                Frequency fixedLegFreq, BDConvention roll, Calendar calendar,
                Frequency floatLegFreq, ReferenceIndex index
      )
      : this(effective, maturity, premium, ccy, dayCount, fixedLegFreq, roll, calendar, Frequency.None,
             floatLegFreq, index, ProjectionType.SimpleProjection, CompoundingConvention.None,
             Frequency.None, false)
    {}

    /// <summary>
    /// Swap constructor
    /// </summary>
    /// <remarks>
    ///   <para>Creates a fixed/floating (pay fixed, receive floating) swap.</para>
    /// </remarks>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="premium">Premium for fixed leg</param>
    /// <param name="ccy">Currency for fixed leg</param>
    /// <param name="dayCount">Daycount convention for fixed leg</param>
    /// <param name="fixedLegFreq">Payment frequency of payment for fixed leg</param>
    /// <param name="roll">Roll convention for fixed leg</param>
    /// <param name="calendar">Calendar for fixed leg</param>
    /// <param name="zeroCouponCompoundingFreq">Compounding frequency for zero coupon fixed leg</param>
    /// <param name="floatLegFreq">Payment frequency for floating leg</param>
    /// <param name="index">Reference index for floating leg</param>
    /// <param name="indexType">Index projection type for floating leg</param>
    /// <param name="compoundingConvention">Compounding convention for floating leg</param>
    /// <param name="compoundingFreq">Compounding frequency for floating leg</param>
    /// <param name="principalExchange">Principal exchange occurs</param>
    public Swap(Dt effective, Dt maturity, double premium, Currency ccy, DayCount dayCount,
                Frequency fixedLegFreq, BDConvention roll, Calendar calendar, Frequency zeroCouponCompoundingFreq,
                Frequency floatLegFreq, ReferenceIndex index, ProjectionType indexType,
                CompoundingConvention compoundingConvention, Frequency compoundingFreq,
                bool principalExchange
      )
      : base(effective, maturity, ccy)
    {
      // Fixed payer leg
      PayerLeg = new SwapLeg(effective, maturity, premium, ccy, dayCount, fixedLegFreq, roll, calendar,
                             zeroCouponCompoundingFreq, principalExchange);
      // Floating receiver leg
      ReceiverLeg = new SwapLeg(effective, maturity, 0.0, floatLegFreq, index,
                                indexType, compoundingConvention, compoundingFreq, principalExchange);
    }

    /// <summary>
    /// Vanilla basis swap constructor
    /// </summary>
    /// <remarks>
    ///   <para>Creates a vanilla basis (float/float) swap.</para>
    /// </remarks>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="premium">Basis swap spread</param>
    /// <param name="receiverLegFreq">Frequency of payment for receiver leg, default is index frequency</param>
    /// <param name="payerLegFreq">Frequency of payment for payer leg, default is index frequency</param>
    /// <param name="receiverIndex">Reference index for receiver leg</param>
    /// <param name="payerIndex">Reference index for payer leg</param>
    /// <param name="spreadOnReceiverIndexLeg">Basis swap spread payed on receiver leg</param>
    /// <param name="principalExchange">Principal exchange occurs</param>
    public Swap(Dt effective, Dt maturity, double premium, Frequency receiverLegFreq, Frequency payerLegFreq,
                ReferenceIndex receiverIndex, ReferenceIndex payerIndex, bool spreadOnReceiverIndexLeg, bool principalExchange
      )
      : this(effective, maturity, premium, receiverLegFreq, payerLegFreq,
             receiverIndex, payerIndex, ProjectionType.SimpleProjection, ProjectionType.SimpleProjection,
             CompoundingConvention.None, Frequency.None, Frequency.None,
             principalExchange, spreadOnReceiverIndexLeg)
    {}

    /// <summary>
    /// Vanilla basis swap constructor
    /// </summary>
    /// <remarks>
    ///   <para>By default receiver is on target index and payer is on projection index.</para>
    /// </remarks>
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="premium">Premium over projected rate</param>
    /// <param name="receiverLegFreq">Frequency of payment for leg paying target index (receiver)</param>
    /// <param name="payerLegFreq">Frequency of payment for leg paying projection index (payer)</param>
    /// <param name="receiverIndex">Reference index for target discount curve</param>
    /// <param name="payerIndex">Reference index for provided projection curve</param>
    /// <param name="receiverIndexType">Floating index projection type of leg paying target index (receiver)</param>
    /// <param name="payerIndexType">Floating index projection type of leg paying projection index (payer)</param>
    /// <param name="compoundingConvention">Compounding convention</param>
    /// <param name="receiverIndexLegCompoundingFreq">Compounding frequency of leg paying target index (receiver)</param>
    /// <param name="payerIndexLegCompoundingFreq">Compounding frequency of leg paying projection index (payer)</param>
    /// <param name="spreadOnReceiverIndexLeg">Basis swap spread payed on target index leg (receiver)</param>
    /// <param name="principalExchange">Principal exchange occurs</param>
    public Swap(Dt effective, Dt maturity, double premium, Frequency receiverLegFreq, Frequency payerLegFreq,
                ReferenceIndex receiverIndex, ReferenceIndex payerIndex, ProjectionType receiverIndexType, ProjectionType payerIndexType,
                CompoundingConvention compoundingConvention, Frequency receiverIndexLegCompoundingFreq, Frequency payerIndexLegCompoundingFreq,
                bool principalExchange, bool spreadOnReceiverIndexLeg
      )
      : base(effective, maturity)
    {
      ReceiverLeg = new SwapLeg(effective, maturity, spreadOnReceiverIndexLeg ? premium : 0.0, receiverLegFreq,
                                receiverIndex,
                                receiverIndexType, compoundingConvention, receiverIndexLegCompoundingFreq,
                                principalExchange);
      PayerLeg = new SwapLeg(effective, maturity, spreadOnReceiverIndexLeg ? 0.0 : premium, payerLegFreq, payerIndex,
                             payerIndexType, compoundingConvention, payerIndexLegCompoundingFreq, principalExchange);
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (Swap)base.Clone();
      if (ReceiverLeg != null)
        obj.ReceiverLeg = (SwapLeg)this.ReceiverLeg.Clone();
      if (PayerLeg != null)
        obj.PayerLeg = (SwapLeg)this.PayerLeg.Clone();
      return obj;
    }

    /// <summary>
    /// Validate product
    /// </summary>
    /// <remarks>
    /// This tests only relationships between fields of the product that
    /// cannot be validated in the property methods.
    /// </remarks>
    /// <param name="errors"></param>
    /// <exception cref="System.ArgumentOutOfRangeException">if product not valid</exception>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (ExerciseSchedule != null)
        ExerciseSchedule.Validate("Exercise Schedule");
    }

    #endregion

    #region Properties

    /// <summary>
    /// Underlying projection indices
    /// </summary>
    public IEnumerable<ReferenceIndex> ReferenceIndices
    {
      get
      {
        if (ReceiverLeg.ReferenceIndex != null)
          yield return ReceiverLeg.ReferenceIndex;
        if (PayerLeg.ReferenceIndex != null)
          yield return PayerLeg.ReferenceIndex;
      }
    }

    /// <summary>
    /// Swap receiver leg. 
    /// </summary>
    public SwapLeg ReceiverLeg { get; set; }

    /// <summary>
    /// Swap payer leg. 
    /// </summary>
    public SwapLeg PayerLeg { get; set; }

    /// <summary>
    /// Swap exercise schedule.
    /// </summary>
    public IList<IOptionPeriod> ExerciseSchedule { get; set; }

    /// <summary>
    /// Swap option rights
    /// </summary>
    public OptionRight OptionRight { get; set; }

    /// <summary>
    /// Swap option style
    /// </summary>
    public OptionStyle OptionStyle { get; set; }

    /// <summary>
    /// When to start/stop the swap if the embedded options are exercised
    /// </summary>
    public SwapStartTiming OptionTiming { get; set; }

    /// <summary>
    /// Swap exercise notification days.
    /// </summary>
    public int NotificationDays { get; set; }

    /// <summary>
    /// Payer leg is fixed
    /// </summary>
    public bool IsPayerFixed
    {
      get { return !PayerLeg.Floating; }
    }

    /// <summary>
    /// Receiver leg is fixed
    /// </summary>
    public bool IsReceiverFixed
    {
      get { return !ReceiverLeg.Floating; }
    }

    /// <summary>
    /// True if the swap has one fixed and one floating leg
    /// </summary>
    public bool IsFixedAndFloating
    {
      get
      {
        if (PayerLeg == null || ReceiverLeg == null)
          return false;
        return ((IsPayerFixed && !IsReceiverFixed) || (!IsPayerFixed && IsReceiverFixed));
      }
    }

    /// <summary>
    /// Spread paid on receiver (if floating)
    /// </summary>
    public bool IsSpreadOnReceiver
    {
      get { return ReceiverLeg.Floating && Math.Abs(ReceiverLeg.Coupon) > double.Epsilon; }
    }

    /// <summary>
    /// Spread paid on payer (if floating)
    /// </summary>
    public bool IsSpreadOnPayer
    {
      get { return PayerLeg.Floating && Math.Abs(PayerLeg.Coupon) > double.Epsilon; }
    }

    /// <summary>
    /// Payer is a zero coupon
    /// </summary>
    public bool IsPayerZeroCoupon
    {
      get { return PayerLeg.IsZeroCoupon; }
    }

    /// <summary>
    /// Receiver is a zero coupn
    /// </summary>
    public bool IsReceiverZeroCoupon
    {
      get { return ReceiverLeg.IsZeroCoupon; }
    }

    /// <summary>
    /// Swap is a Basis swap
    /// </summary>
    public bool IsBasisSwap
    {
      get { return PayerLeg.Floating && ReceiverLeg.Floating; }
    }

    /// <summary>
    /// True if payer has the right to cancel
    /// </summary>
    public bool? HasOptionRight { get; set; }

    /// <summary>
    /// Check whether Swap is cancelable
    /// </summary>
    public bool Cancelable
    {
      get { return (ExerciseSchedule != null) && ExerciseSchedule.Count > 0; }
    }

    /// <summary>
    /// Check whether Swap is callable
    /// </summary>
    public bool Callable
    {
      get { return Cancelable && (ExerciseSchedule[0] is PutPeriod); }
    }

    /// <summary>
    /// Check whether Swap is puttable
    /// </summary>
    public bool Puttable
    {
      get { return Cancelable && (ExerciseSchedule[0] is CallPeriod); }
    }

    /// <summary>
    /// Non standard exercise dates
    /// </summary>
    public IList<Dt> ExerciseDates { get; set; }

    /// <summary>
    /// Amortization schedule
    /// </summary>
    public IList<Amortization> AmortizationSchedule
    {
      set
      {
        foreach (var a in value)
        {
          ReceiverLeg.AmortizationSchedule.Add(a);
          PayerLeg.AmortizationSchedule.Add(a);
        }
      }
    }

    bool ICallable.FullExercisePrice => false;

    #endregion Properties
  }

  /// <summary>
  /// when to start the underlying swap if the swaption exercised.
  /// </summary>
  public enum SwapStartTiming
  {
    /// <summary>
    /// Default value. Not specified. If a swaption is American, start immediately.
    /// </summary>
    None = 0,

    /// <summary>
    /// Starts immediately. 
    /// </summary>
    Immediate,

    /// <summary>
    /// Starts from the next coupon period. 
    /// </summary>
    NextPeriod
  }

}
