// 
// SingleAssetOptionBase.cs
//  -2012. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Abstract parent class for single asset options.
  /// <para>Options need not derive from this class and it is simply provided as a helpful class for options with a single
  /// strike price on a single underlying product.</para>
  /// </summary>
  /// <remarks>
  ///   <para>An option is a financial contract between two parties that gives the right but not the obligation for one party
  ///   to buy an underlying asset a reference price (the strike) at a future date (the expiration). This right has a value
  ///   based on the likely difference between the reference price and the price of the underlying asset on the expiration
  ///   date. This value (the premium) is typically paid upfront by the buyer of the option to the seller of the option.</para>
  ///   <para>The option to buy the underlying asset at a specific price is termed a call option and the right to sell
  ///   the underying asset at a specific price is termed a put option.</para>
  ///   <para>On the expiration date the option may be exercised by the option buyer, otherwise it expires worthles. The
  ///   option will be exercised only if the underlying asset price is favorable relative to the strike and the option is
  ///   said to be in the money. On exercise, the underying asset may be traded at the strike (physically settled) or
  ///   the price differential may be exchanged (cash settled).</para>
  ///   <para>Standardised option contracts are traded on exchanges and called exchange-traded or listed options. Options are
  ///   also traded in the OTC market.</para>
  /// 
  ///   <para><h2>Common Terms</h2></para>
  ///   <para>Vanilla options share common terms:</para>
  ///   <list type="bullet">
  ///     <item><description><see cref="Expiration">Expiration date</see> is the last date that the option can be exercised</description></item>
  ///     <item><description><see cref="Strike">Strike or exercise price</see> is the price at which the underlying transaction will occur</description></item>
  ///     <item><description><see cref="Type">Option type</see> is whether the holder has the right to buy or sell the underlying</description></item>
  ///     <item><description><see cref="Style">Option style</see> is whether the option holder can exercise on or before the expiration date</description></item>
  ///   </list>
  ///   <para>A variety of exotic options are also supported.</para>
  /// 
  ///   <para><h2>Exotics</h2></para>
  ///   <para><i>Digital Option</i></para>
  ///   <para>Digital options pay a fixed amount or nothing. They are also known as binary, all-or-nothing or fixed return options. 
  ///   Two main variants are cash-or-nothing or asset-or-nothing options. Cash-or-nothing options pay one dollar if the option is in the money.
  ///   Asset-or-nothing options pay one stock if the option is in the money.
  ///   To specify a digital option, set <see cref="PayoffType"/> to Digital, specify the <see cref="Rebate"/> amount and
  ///   specify a cash or asset option using the <see cref="SettlementType"/>.</para>
  ///   <para><i>One Touch Option</i></para>
  ///   <para>One-touch options pay a fixed amount immediately if a barrier is breached. A no-touch options pay a fixed
  ///   amount if a barrier is not reached.
  ///   To specify a one touch option, set <see cref="PayoffType"/> to Digital, specify
  ///   the <see cref="Rebate"/> amount and specify the <see cref="Barriers"/>.</para>
  ///   <para><i>Barrier Option</i></para>
  ///   <para>Barrier options are options with specified barrier levels that if breached by the underlying price, either activate or
  ///   deactivate the option.
  ///   Barrier options that are activated by the underlying hitting the barrier are up-and-in and down-and-in options.
  ///   Barrier options that are deactivated by the underlying hitting the barrier are up-and-out and down-and-out options.
  ///   Barrier options may also have a <see cref="Rebate"/> that is paid if the option is not activated.
  ///   Barrier options may also be digital. A Barrier Digital Knock-in turns into a regular digital option if the
  ///   barrier is reached. A One touch option is similar but do not have a strike and pay immediately the barrier is reached.
  ///   Barrier options typically have an upper and a lower barrier. Each barrier has a barrier
  ///   <see cref="OptionBarrierType">Type</see> and barrier Level.
  ///   To specify a barrier option, set <seealso cref="PayoffType"/> to Regular and specify the upper and lower barriers.
  ///   To specify a digital knock-in option, set <seealso cref="PayoffType"/> to Digital and specify the upper and lower
  ///   barriers.</para>
  ///   <para><i>Lookback Options</i></para>
  ///   <para>Lookback or Hindsight options are path-dependent options where the payoff is dependent on the maximum or minimum
  ///   asset price over the life of the option. Lookback options come in two main variations:</para>
  ///   <list class="bullet">
  ///     <item><para>Fixed Strike options are cash settled options where the strike is set upfront and the payoff is the maximum difference between
  ///     the optimal price and the strike price.</para></item>
  ///     <item><para>Floating Strike options are cash or asset settled where the strike is the optimal value of the underlying asset. Note that
  ///     floating strike options are always exercised.</para></item>
  ///   </list>
  /// 
  ///   <para><h2>Common Strategies</h2></para>
  ///   <para>A long call strategy is buying a call option which gives the buyer the right to purchase the underlying asset at the
  ///   agreed strike. This is similar to purchasing the underlying asset but provides higher leverage. This strategy would typically
  ///   be done to take advantage of an expected appreciation in the underlying price.</para>
  ///   <h1 align="center"><img src="OptionStrategy_Long_Call.gif" align="middle"/></h1>
  ///   <para>A short call strategy is selling a call option which gives the seller the obligation to sell the underlying asset at the
  ///   agreed strike. This strategy would typically be done to take advantage of an expected depreciation in the underlying price
  ///   by recieving the premium and having the option expire worthless.</para>
  ///   <h1 align="center"><img src="OptionStrategy_Short_Call.gif" align="middle"/></h1>
  ///   <para>A long put strategy is buying a put option which gives the buyer the right to sell the underlying asset at the
  ///   agreed strike. This strategy would typically be done to take advantage of an expected depreciation in the underlying price.</para>
  ///   <h1 align="center"><img src="OptionStrategy_Long_Put.gif" align="middle"/></h1>
  ///   <para>A short call strategy is selling a put option which gives the seller the obligation to buy the underlying asset at the
  ///   agreed strike. This strategy would typically be done to take advantage of an expected appreciation in the underlying price
  ///   by recieving the premium and having the option expire worthless.</para>
  ///   <h1 align="center"><img src="OptionStrategy_Short_Put.gif" align="middle"/></h1>
  ///   <para>More complex strategies involve combinations of basic trades to give payoffs that match the desired payoff.</para>
  ///   <para>A butterfly spread is buying one call option at a low strike, selling two call options at a mid strike and
  ///   buying one call option at a high strike. This allows a trader to benefit from low volatility while minimising
  ///   downside risk.</para>
  ///   <h1 align="center"><img src="OptionStrategy_Long_Butterfly.gif" align="middle"/></h1>
  ///   <para>A straddle is selling both a put and a call option at the same exercise price. This also allows the trader to
  ///   benefit from low volatility but trades a higher potential payoff with unlimited downside risk.</para>
  ///   <h1 align="center"><img src="OptionStrategy_Short_Straddle.gif" align="middle"/></h1>
  ///   <para>Many other well known strategies exist including bear spreads, bull spreads, strangles and covered calls to
  ///   name just a few.</para>
  /// </remarks>
  // Docs note: remarks are inherited so only include docs suitable for derived classes. RD Mar'14
  [Serializable]
  [ReadOnly(true)]
  public abstract class SingleAssetOptionBase : Product, IBasicExoticOption
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="underlying">Underlying product</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    ///
    protected SingleAssetOptionBase(Dt effective, Currency ccy, IProduct underlying,
      Dt expiration, OptionType type, OptionStyle style, double strike)
      : base(effective, expiration, ccy)
    {
      Underlying = underlying;
      Type = type;
      Style = style;
      Strike = strike;
    }

    /// <summary>
    /// Constructor for vanilla option
    /// </summary>
    /// <param name="underlying">Underlying product</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    protected SingleAssetOptionBase(IProduct underlying, Dt expiration, OptionType type, OptionStyle style, double strike)
      : base(Dt.Empty, expiration, Currency.None)
    {
      Underlying = underlying;
      Type = type;
      Style = style;
      Strike = strike;
      PayoffType = OptionPayoffType.Regular;
      StrikeDetermination = OptionStrikeDeterminationMethod.Fixed;
      UnderlyingDetermination = OptionUnderlyingDeterminationMethod.Regular;
      Barriers = new List<Barrier>();
    }

    /// <summary>
    /// Constructor for barrier option
    /// </summary>
    /// <param name="underlying">Underlying product</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    /// <param name="barrier1Type">First barrier type</param>
    /// <param name="barrier1Level">First barrier level</param>
    /// <param name="barrier2Type">Second barrier type</param>
    /// <param name="barrier2Level">Second barrier level</param>
    protected SingleAssetOptionBase(
      IProduct underlying, Dt expiration, OptionType type, OptionStyle style, double strike,
      OptionBarrierType barrier1Type, double barrier1Level,
      OptionBarrierType barrier2Type, double barrier2Level
      )
      : base(Dt.Empty, expiration, Currency.None)
    {
      Underlying = underlying;
      Type = type;
      Style = style;
      Strike = strike;
      PayoffType = OptionPayoffType.Regular;
      BarrierStart = Dt.Empty;
      BarrierEnd = expiration;
      BarrierMonitoringFrequency = Frequency.Continuous;
      StrikeDetermination = OptionStrikeDeterminationMethod.Fixed;
      UnderlyingDetermination = OptionUnderlyingDeterminationMethod.Regular;
      Barriers = new List<Barrier>();
      if( barrier1Type != OptionBarrierType.None)
        Barriers.Add(new Barrier {BarrierType = barrier1Type, Value = barrier1Level});
      if (barrier2Type != OptionBarrierType.None)
        Barriers.Add(new Barrier { BarrierType = barrier2Type, Value = barrier2Level });
    }

    /// <summary>
    /// Constructor for digital option
    /// </summary>
    /// <param name="underlying">Underlying product</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    /// <param name="rebate">Rebate</param>
    protected SingleAssetOptionBase(
      IProduct underlying, Dt expiration, OptionType type, OptionStyle style, double strike,
      double rebate
      )
      : base(Dt.Empty, expiration, Currency.None)
    {
      Underlying = underlying;
      Type = type;
      Style = style;
      Strike = strike;
      PayoffType = OptionPayoffType.Digital;
      Rebate = rebate;
      StrikeDetermination = OptionStrikeDeterminationMethod.Fixed;
      UnderlyingDetermination = OptionUnderlyingDeterminationMethod.Regular;
      Barriers = new List<Barrier>();
    }

    /// <summary>
    /// Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (SingleAssetOptionBase)base.Clone();
      obj.Underlying = (IProduct)Underlying.Clone();
      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Validate product
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      //-- Disabled since the underlying can be forward CDS/CDO with effective after the expiration
      // Expiration date after effective date
      //if (Dt.Cmp(expiration_, underlying_.Effective) < 0)
      //  errors.Add(new InvalidValue(this, "Expiration", String.Format("Expiration {0} must be on or after effective date {1}", expiration_, underlying_.Effective)));
      if (!Expiration.IsEmpty() && !Expiration.IsValid())
        InvalidValue.AddError(errors, this, "Expiration", String.Format("Invalid expiration date. Must be empty or valid date, not {0}", Expiration));
      if (!Underlying.Maturity.IsEmpty() && Expiration > Underlying.Maturity)
        InvalidValue.AddError(errors, this, "Expiration", String.Format("Expiration {0} must be before underlying product maturity {1}", Expiration, Underlying.Maturity));
      if (Underlying == null)
        InvalidValue.AddError(errors, this, "Underlying", String.Format("Invalid Underlying {0} ", Underlying));
      if (Type == OptionType.None)
        InvalidValue.AddError(errors, this, "Type", String.Format("Invalid Option Type. Can not be {0}", Type));
      if (Style == OptionStyle.None)
        InvalidValue.AddError(errors, this, "Style", String.Format("Invalid Option Style. Can not be {0}", Style));
      if (Strike < 0)
        InvalidValue.AddError(errors, this, "Strike", String.Format("Invalid Strike. Must be +Ve, Not {0}", Strike));
      if (Barriers != null && Barriers.Count > 0)
      {
        if (Barriers.Count > 2)
          InvalidValue.AddError(errors, this, "Barriers", String.Format("At most 2 barriers are currently supported. Not {0}", Barriers.Count));
        if( Barriers.Count == 2 && Barriers[0].BarrierType == Barriers[1].BarrierType )
          InvalidValue.AddError(errors, this, "Barriers", String.Format("Two barriers can't have the same type {0} and {1}", Barriers[0].BarrierType, Barriers[1].BarrierType));
      }
      Underlying.Validate(errors);
      return;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Underlying product
    /// </summary>
    [Category("Underlying")]
    public IProduct Underlying { get; private set; }

    /// <summary>
    /// Expiration date of option.
    /// </summary>
    [Category("Option")]
    public Dt Expiration { get { return Maturity; } set { Maturity = value; } }

    /// <summary>
    /// Option type
    /// </summary>
    [Category("Option")]
    public OptionType Type { get; set; }

    /// <summary>
    /// Option style
    /// </summary>
    [Category("Option")]
    public OptionStyle Style { get; set; }

    /// <summary>
    /// Option strike price
    /// </summary>
    [Category("Option")]
    public double Strike { get; set; }

    /// <summary>
    ///  Settlement type
    /// </summary>
    [Category("Option")]
    public SettlementType SettlementType { get; set; }

    /// <summary>
    /// Option strike price determination method
    /// </summary>
    [Category("Option")]
    public OptionStrikeDeterminationMethod StrikeDetermination { get; set; }

    /// <summary>
    /// Option underlying price determination method
    /// </summary>
    [Category("Option")]
    public OptionUnderlyingDeterminationMethod UnderlyingDetermination { get; set; }

    #region Digital

    /// <summary>
    /// Option payoff type
    /// </summary>
    [Category("Digital")]
    public OptionPayoffType PayoffType { get; set; }

    /// <summary>
    /// Rebate  amount (for digital options)
    /// </summary>
    [Category("Digital")]
    public double Rebate { get; set; }

    #endregion Digital

    #region Barrier

    /// <summary>
    /// Barrier start date
    /// </summary>
    /// <remarks>Dt.Empty if not set.</remarks>
    [Category("Barrier")]
    public Dt BarrierStart { get; set; }

    /// <summary>
    /// Barier end date
    /// </summary>
    [Category("Barrier")]
    public Dt BarrierEnd { get; set; }

    /// <summary>
    /// Barrier monitoring frequency.
    /// </summary>
    [Category("Barrier")]
    public Frequency BarrierMonitoringFrequency { get; set; }

    /// <summary>
    /// Gets or sets the barrier payoff time.
    /// </summary>
    [Category("Barrier")]
    public BarrierOptionPayoffTime BarrierPayoffTime { get; set; }

    /// <summary>
    /// List of barriers.
    /// </summary>
    [Category("Barrier")]
    public IList<Barrier> Barriers { get; set; }

    #endregion Barrier

    #region Informational

    /// <summary>
    /// Is regular (vanilla) option
    /// </summary>
    [Category("Informational")]
    internal bool IsRegular
    {
      get { return (!IsDigital && !IsBarrier); }
    }

    /// <summary>
    /// True if option is digital
    /// </summary>
    [Category("Informational")]
    public bool IsDigital
    {
      get { return (PayoffType == OptionPayoffType.Digital); }
    }

    /// <summary>
    /// True if option has one or more barriers
    /// </summary>
    [Category("Informational")]
    public bool IsBarrier
    {
      get { return (Barriers != null && Barriers.Count > 0); }
    }

    /// <summary>
    /// True if option has two barriers
    /// </summary>
    [Category("Informational")]
    public bool IsDoubleBarrier
    {
      get { return (Barriers != null && Barriers.Count == 2); }
    }

    /// <summary>
    /// Is barrier touch option
    /// </summary>
    [Category("Informational")]
    public bool IsTouchOption
    {
      get
      {
        return Barriers != null &&
          ((Barriers.Count == 1 && Barriers[0].IsTouch) ||
          (Barriers.Count == 2 && (Barriers[0].IsTouch || Barriers[1].IsTouch)));
      }
    }

    #endregion Informational

    #endregion Properties
  }
}
