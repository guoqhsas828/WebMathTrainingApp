// 
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
  /// Option on a stock basket
  /// </summary>
  /// <remarks>
  /// <para>A variety of vanilla and exotic options are supported.</para>
  /// <para><b>Vanilla Option</b></para>
  /// <para>An option is a contract that gives the buyer the right but not the obligation to trade an
  /// underlying asset at a pre-determined price (strike <seealso cref="StockBasketOption.Strike"/>)</para>
  /// <para>There are a variety of types <seealso cref="OptionType"/> of options. A Call option gives the
  /// right to buy the underlying asset. A Put option gives the right to sell the underlying asset.</para>
  /// <para>The buyer of the option pays an upfront fee or Premium.</para>
  /// <para>Options may be cash settled or physically settled. For physically settled options,
  /// the underlying asset is traded at the strike price. For cash settled options, the difference
  /// between the current price of the underlying asset and the strike is exchanged.
  /// <seealso cref="SettlementType"/></para>
  /// <para>Options come in a variety of different Styles <seealso cref="Style"/>. European
  /// options can be exercised at the expiration date. American options can be exercised any time
  /// up till the expiration date.</para>
  /// <para>Options are both exchange traded and traded in the OTC market.</para>
  /// <para><b>Digital Option</b></para>
  /// <para>Digital options pay a fixed amount or nothing. They are also known as binary, all-or-nothing or fixed return options. 
  /// Two main variants are cash-or-nothing or asset-or-nothing options. Cash-or-nothing options pay one dollar if the option is in the money.
  /// Asset-or-nothing options pay one stock if the option is in the money.</para>
  /// <para>To specify a digital option, set <seealso cref="PayoffType"/> to Digital, specify
  /// the <seealso cref="Rebate"/> amount and specify a cash or asset option using the
  /// <seealso cref="SettlementType"/>.</para>
  /// <para><b>One Touch Option</b></para>
  /// <para>One-touch options pay a fixed amount immediately if a barrier is breached. A no-touch options pay a fixed
  /// amount if a barrier is not reached.</para>
  /// <para>To specify a one touch option, set <seealso cref="PayoffType"/> to Digital, specify
  /// the <seealso cref="Rebate"/> amount and specify the barriers <seealso cref="Barriers"/>.</para>
  /// <para><b>Barrier Option</b></para>
  /// <para>Barrier options are options with specified barrier levels that if breached by the underlying price, either activate or
  /// deactivate the option.</para>
  /// <para>Barrier options that are activated by the underlying hitting the barrier are up-and-in and down-and-in options.
  /// Barrier options that are deactivated by the underlying hitting the barrier are up-and-out and down-and-out options.</para>
  /// <para>Barrier options may also have a Rebate <seealso cref="Rebate"/> that is paid if the option is not activated.</para>
  /// <para>Barrier options may also be digital. A Barrier Digital Knock-in turns into a regular digital option if the
  /// barrier is reached. A One touch option is similar but do not have a strike and pay immediately the barrier is reached.</para>
  /// <para>Barrier options typically have an upper and a lower barrier. Each barrier has some characteristics:</para>
  /// <list>
  ///   <item><b>Type</b> - Type of barrier</item>
  ///   <item><b>Level</b> - Level of barrier</item>
  /// </list>
  /// <para>To specify a barrier option, set <seealso cref="PayoffType"/> to Regular and specify the upper and lower barriers.</para>
  /// <para>To specify a digital knock-in option, set <seealso cref="PayoffType"/> to Digital and specify the upper and lower
  /// barriers.</para>
  /// </remarks>
  [Serializable]
  public class StockBasketOption : Product
  {
    #region Constructors

    /// <summary>
    /// Constructor for vanilla option
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="amounts">Underlying stock amounts</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public StockBasketOption(Dt effective, Currency ccy, double[] amounts,
      Dt expiration, OptionType type, OptionStyle style, double strike)
      : base(effective, expiration, ccy)
    {
      Amounts = amounts;
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
    /// <param name="effective">Effective date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="amounts">Underlying stock amounts</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    /// <param name="barrier1Type">First barrier type</param>
    /// <param name="barrier1Level">First barrier level</param>
    /// <param name="barrier2Type">Second barrier type</param>
    /// <param name="barrier2Level">Second barrier level</param>
    public StockBasketOption(
      Dt effective, Currency ccy, double[] amounts,
      Dt expiration, OptionType type, OptionStyle style, double strike,
      OptionBarrierType barrier1Type, double barrier1Level,
      OptionBarrierType barrier2Type, double barrier2Level
      )
      : base(effective, expiration, ccy)
    {
      Amounts = amounts;
      Type = type;
      Style = style;
      Strike = strike;
      PayoffType = OptionPayoffType.Regular;
      BarrierWindowBegin = effective;
      BarrierWindowEnd = expiration;
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
    /// <param name="effective">Effective date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="amounts">Underlying stock amounts</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    /// <param name="rebate">Rebate</param>
    public StockBasketOption(
      Dt effective, Currency ccy, double[] amounts,
      Dt expiration, OptionType type, OptionStyle style, double strike,
      double rebate
      )
      : base(effective, expiration, ccy)
    {
      Amounts = amounts;
      Type = type;
      Style = style;
      Strike = strike;
      PayoffType = OptionPayoffType.Digital;
      Rebate = rebate;
      StrikeDetermination = OptionStrikeDeterminationMethod.Fixed;
      UnderlyingDetermination = OptionUnderlyingDeterminationMethod.Regular;
      Barriers = new List<Barrier>();
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
      if (!Expiration.IsEmpty() && !Expiration.IsValid())
        InvalidValue.AddError(errors, this, "Expiration", String.Format("Invalid expiration date. Must be empty or valid date, not {0}", Expiration));
      if (Type == OptionType.None)
        InvalidValue.AddError(errors, this, "Type", String.Format("Invalid Option Type. Can not be {0}", Type));
      if (Style == OptionStyle.None)
        InvalidValue.AddError(errors, this, "Style", String.Format("Invalid Option Style. Can not be {0}", Style));
      if (Strike < 0)
        InvalidValue.AddError(errors, this, "Strike", String.Format("Invalid Strike. Must be +Ve, Not {0}", Strike));
      if (Amounts != null)
      {
        foreach (var a in Amounts)
          if (a < 0.0) InvalidValue.AddError(errors, this, "Amounts", String.Format("Invalid Amount. Must be +Ve, Not {0}", a));
      }
      if (Barriers != null && Barriers.Count > 0)
      {
        if (Barriers.Count > 2)
          InvalidValue.AddError(errors, this, "Barriers", String.Format("At most 2 barriers are currently supported. Not {0}", Barriers.Count));
        if( Barriers.Count == 2 && Barriers[0].BarrierType == Barriers[1].BarrierType )
          InvalidValue.AddError(errors, this, "Barriers", String.Format("Two barriers can't have the same type {0} and {1}", Barriers[0].BarrierType, Barriers[1].BarrierType));
      }
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Expiration date of option.
    /// </summary>
    [Category("Option")]
    public Dt Expiration { get { return Maturity; } }

    /// <summary>
    /// Fixing date
    /// </summary>
    [Category("Option")]
    public Dt FixingDate { get; set; }

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
    /// Underlying stock amounts
    /// </summary>
    [Category("Underlying")]
    public double[] Amounts { get; set; }

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

    /// <summary>
    /// Option payoff type
    /// </summary>
    [Category("Option")]
    public OptionPayoffType PayoffType { get; set; }

    /// <summary>
    /// Rebate  amount (for digital options)
    /// </summary>
    [Category("Digital")]
    public double Rebate { get; set; }

    /// <summary>
    /// Settlement type
    /// </summary>
    [Category("Option")]
    public SettlementType SettlementType { get; set; }

    /// <summary>
    /// Barrier start date
    /// </summary>
    [Category("Barrier")]
    public Dt BarrierWindowBegin { get; set; }

    /// <summary>
    /// Barier end date
    /// </summary>
    [Category("Barrier")]
    public Dt BarrierWindowEnd { get; set; }

    /// <summary>
    /// Barrier monitoring frequency.
    /// </summary>
    [Category("Barrier")]
    public Frequency BarrierMonitoringFrequency { get; set; }

    /// <summary>
    /// List of barriers.
    /// </summary>
    public IList<Barrier> Barriers { get; set; }

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
