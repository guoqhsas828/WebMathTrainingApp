// 
//  -2012. All rights reserved.
// 
// Todo: Convert to inherit from SingleAssetOption. RTD Nov'12

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Products
{

  /// <summary>
  ///  FxOptionConfig.
  /// </summary>
  /// <exclude />
  public class FxOptionConfig
  {
    /// <summary>
    /// Map call/put to up/down for no-touch options.
    /// </summary>
    /// <remarks>
    /// Introduced in 15.2. <c>false</c> for backward compatible behavior,
    ///  where the touch options, if not have up/down directions specified,
    ///  will map call/put to Up-In/Down-In.
    ///  This works for one-touch, but not for no-touch options.
    ///  The later should map to Up-Out/Down-Out.
    ///  To get the correct new behavior, set this flag to <c>true</c>.
    /// </remarks>
    [ToolkitConfig("Consistently map call/put to up/down for touch options")]
    public readonly bool MapCallPutToUpDownForNoTouchOptions = true;

    /// <summary>
    /// Perform the explicit over hedge costs adjustment for all options
    /// </summary>
    /// <rematks>
    /// Introduced in 15.2. <c>false</c> for backward compatible behavior,
    /// where only the single barrier options perform the explicit over-hedge
    /// cost adjustment, and others make adjustments through volatility
    /// interpolation, essentially the same approach as the
    /// <c>VolatilityInterpolation</c> method.  To apply the explicit adjustment
    /// to all the options, set this to <c>true</c>.
    /// </rematks>
    [ToolkitConfig("Apply the consistent over-hedge costs adjustment across vanilla, single barrier and double barrier options")]
    public readonly bool ConsistentOverHedgeAdjustmentAcrossOptions = false;

    /// <summary>
    /// Whether to use the exact analytic formula instead of approximation
    ///  for double barrier no-touch probability
    /// </summary>
    [ToolkitConfig("Use the exact analytic formula instead of approximation for double barrier no-touch probability")]
    public readonly bool ExactDoubleBarrierProbability = true;
  }

  /// <summary>
  /// Types of double barriers
  /// </summary>
  public enum DoubleBarrierType
  {
    /// <summary>Double Knock In</summary>
    DoubleKnockIn,

    /// <summary>Double Knock Out</summary>
    DoubleKnockOut
  }

  /// <summary>
  /// Option on FX rates
  /// </summary>
  /// <remarks>
  /// 
  /// <para>FX options are options where the underlying asset is spot FX rate.</para>
  /// 
  /// <para>For example a GBPUSD contract could give the owner the right to sell £1,000,000 and
  /// buy $2,000,000 on December 31. In this case the pre-agreed exchange rate, or strike price,
  /// is 2.0000 USD per GBP (or GBP/USD 2.00 as it is typically quoted) and the notional amounts
  /// (notional) are £1,000,000 and $2,000,000.</para>
  /// 
  /// <para>This type of contract is both a call on dollars and a put on sterling, and is typically
  /// called a GBPUSD put, as it is a put on the exchange rate; although it could equally be called
  /// a USDGBP call.</para>
  /// 
  /// <para>If the rate is lower than 2.0000 on December 31 (say at 1.9000), meaning that the dollar
  /// is stronger and the pound is weaker, then the option is exercised, allowing the owner to sell
  /// GBP at 2.0000 and immediately buy it back in the spot market at 1.9000, making a profit of
  /// (2.0000 GBPUSD – 1.9000 GBPUSD)*1,000,000 GBP = 100,000 USD in the process. If they immediately
  /// convert the profit into GBP this amounts to 100,000/1.9000 = 52,631.58 GBP.</para>
  /// <para><i>Source: Wikipedia</i></para>
  /// 
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="SingleAssetOptionBase" />
  /// 
  /// <example>
  /// 
  /// <para>The following example builds a regular, call option.</para>
  /// <code source="..\toolkit\BaseEntity.Toolkit.Tests\Pricers\FxOptionPricerExamples.cs"  region="RegularCall" language="c#" />
  /// 
  /// <para>The following example creates a digital, put option.</para>
  /// <code source="..\toolkit\BaseEntity.Toolkit.Tests\Pricers\FxOptionPricerExamples.cs"  region="DigitalPut" language="c#" />
  /// 
  /// <para>The following example builds a single barrier, up and in, regular call option.</para>
  /// <code source="..\toolkit\BaseEntity.Toolkit.Tests\Pricers\FxOptionPricerExamples.cs"  region="SingleBarrierUpInCall" language="c#" />
  ///
  /// <para>The following example builds a single barrier, down and out, digital put option.</para>
  /// <code source="..\toolkit\BaseEntity.Toolkit.Tests\Pricers\FxOptionPricerExamples.cs"  region="SingleBarrierDownOutDigitalPut" language="c#" />
  /// 
  /// <para>The following example constructs a single barrier, one-touch, up and in, option.</para>
  /// <code source="..\toolkit\BaseEntity.Toolkit.Tests\Pricers\FxOptionPricerExamples.cs"  region="OneTouchUpIn" language="c#" />
  /// 
  /// <para>The following example constructs a single barrier, no-touch, down and out, option.</para>
  /// <code source="..\toolkit\BaseEntity.Toolkit.Tests\Pricers\FxOptionPricerExamples.cs"  region="NoTouchDownOut" language="c#" />
  ///
  /// <para>The following example creates a double barrier, one-touch, knock-in, option.</para>
  /// <code source="..\toolkit\BaseEntity.Toolkit.Tests\Pricers\FxOptionPricerExamples.cs"  region="OneTouchDoubleKnockIn" language="c#" />
  /// 
  /// <para>The following example creates a double barrier, no-touch, knock-out, option.</para>
  /// <code source="..\toolkit\BaseEntity.Toolkit.Tests\Pricers\FxOptionPricerExamples.cs"  region="NoTouchDoubleKnockOut" language="c#" />
  /// </example>
  /// 
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.FxOptionVanillaPricer"/>
  [Serializable]
  [ReadOnly(true)]
  public class FxOption : Product, IBasicExoticOption
  {
    #region Constructors

    /// <summary>
    /// Default Constructor.
    /// </summary>
    public FxOption()
    {
      Barriers = new List<Barrier>();
      Underlying = new Fx(Currency.None, Currency.None);
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // Validate Maturity
      if (!Maturity.IsEmpty() && !Maturity.IsValid())
        InvalidValue.AddError(errors, this, "Expiration", String.Format("Invalid expiration date. Must be empty or valid date, not {0}", Maturity));

      // Validate BarrierWindowEnd data
      if (!BarrierWindowEnd.IsEmpty() && (BarrierWindowEnd > Maturity || BarrierWindowEnd < Effective))
        InvalidValue.AddError(errors, this, "BarrierWindowEnd", String.Format(
          "Invalid window end date. Must be empty or a date between effective and expiration, not {0}", BarrierWindowEnd));

      if (!IsTouchOption)
      {
        // Invalid Option Type
        if (Type == OptionType.None)
          InvalidValue.AddError(errors, this, "Type", $"Invalid Option Type. Can not be {Type}");

        // Invalid Option Style
        if (Style == OptionStyle.None)
          InvalidValue.AddError(errors, this, "Style", $"Invalid Option Style. Can not be {Style}");
      }

      //  Strike >= 0 
      if (Strike < 0)
        InvalidValue.AddError(errors, this, "Strike", $"Invalid Strike. Must be +Ve, Not {Strike}");

      // Check PayAtBarrierHit flag
      if ((Flags & OptionBarrierFlag.PayAtBarrierHit) != 0 && !IsOneTouch)
        InvalidValue.AddError(errors, this, "Flags", String.Format("Barrier type {0} does not support PayAtBarrierHit", Barriers[0].BarrierType));

      int count = Barriers.Count;

      // Check touch option consistency
      if (IsTouchOption)
      {
        if (count != 1 && count != 2)
          InvalidValue.AddError(errors, this, "Flags", "Only single/double barrier option supports touch options");
        else if ((Flags & OptionBarrierFlag.NoTouch) != 0 && (Flags & OptionBarrierFlag.OneTouch) != 0)
          InvalidValue.AddError(errors, this, "Flags","Cannot be both OneTouch and NoTouch at the same time.");
        else if (IsOneTouch)
        {
          var bt = Barriers[0].BarrierType;
          if (count == 1)
          {
            if (bt != OptionBarrierType.UpIn && bt != OptionBarrierType.DownIn && bt != OptionBarrierType.OneTouch)
              InvalidValue.AddError(errors, this, "Flags", $"OneTouch option does not support {bt}");
          }
          else
          {
            // double barrier one-touch
            var ut = Barriers[1].BarrierType;
            if ((ut != OptionBarrierType.UpIn && ut != OptionBarrierType.OneTouch) ||(bt != OptionBarrierType.DownIn && bt != OptionBarrierType.OneTouch))
              InvalidValue.AddError(errors, this, "Flags", $"OneTouch option does not support ({bt},{ut})");
          }
        }
        else if (IsNoTouch)
        {
          var bt = Barriers[0].BarrierType;
          if (count == 1)
          {
            if (bt != OptionBarrierType.UpOut && bt != OptionBarrierType.DownOut && bt != OptionBarrierType.NoTouch)
              InvalidValue.AddError(errors, this, "Flags", $"NoTouch option does not support {bt}");
          }
          else
          {
            // double barrier no-touch
            var ut = Barriers[1].BarrierType;
            if ((ut != OptionBarrierType.UpOut && ut != OptionBarrierType.NoTouch) || (bt != OptionBarrierType.DownOut && bt != OptionBarrierType.NoTouch))
              InvalidValue.AddError(errors, this, "Flags", $"NoTouch option does not support ({bt},{ut})");
          }
        }
      }
      else if (count == 1)
      {
        // Check other barrier options
        var bt = Barriers[0].BarrierType;
        if (bt == OptionBarrierType.NoTouch || bt == OptionBarrierType.OneTouch)
          InvalidValue.AddError(errors, this, "Flags", String.Format("Need specify UpIn or DonwIn for touch options", bt));
      }

      //If there are two barriers 
      if (count == 2)
      {
        var bt = Barriers[0].BarrierType;
        var ut = Barriers[1].BarrierType;
        if (!((bt == OptionBarrierType.DownOut && ut == OptionBarrierType.UpOut) ||
              (bt == OptionBarrierType.DownIn && ut == OptionBarrierType.UpIn)))
          InvalidValue.AddError(errors, this, "Barrier", $"{bt} and {ut} is an invalid combination of lower and upper barriers");
        if (Barriers[0].Value > Barriers[1].Value)
          InvalidValue.AddError(errors, this, "Barrier", String.Format("Lower Barrier {0} must be <= Upper Barrier {1}", Barriers[0].Value, Barriers[1].Value));
      }

      // Underlying Product
      if (Underlying == null)
        InvalidValue.AddError(errors, this, "Underlying", String.Format("Invalid Underlying {0} ", Underlying));
      else
      {
        // Pay and receive currencies differ
        if (ReceiveCcy == PayCcy)
          InvalidValue.AddError(errors, this, "Ccy", String.Format("Receive currency {0} must differ from pay currency {1}", ReceiveCcy, PayCcy));
        Underlying.Validate(errors);
      }
    }

    /// <summary>
    /// For single barrier option with the barrier type being OneTouch
    /// or NoTouch, maps the call option to up-in and the put option to
    /// down-in.  Otherwise, do nothing.
    /// </summary>
    public void MapTouchOption()
    {
      if (Barriers.Count != 1) return;
      var bt = Barriers[0].BarrierType;
      if (bt == OptionBarrierType.NoTouch)
      {
        Flags |= OptionBarrierFlag.NoTouch;
        Flags &= ~OptionBarrierFlag.Digital;
      }
      else if (bt == OptionBarrierType.OneTouch)
      {
        Flags |= OptionBarrierFlag.OneTouch;
        Flags &= ~OptionBarrierFlag.Digital;
      }
      else
      {
        // Not a touch option
        return;
      }
      // Now do the mapping:  Call => Up, Put => Down
      var mapNoTouch = Settings.MapCallPutToUpDownForNoTouchOptions;
      if (Type == OptionType.Call)
      {
        Barriers[0].BarrierType = IsNoTouch && mapNoTouch
          ? OptionBarrierType.UpOut : OptionBarrierType.UpIn;
      }
      else if (Type == OptionType.Put)
      {
        Barriers[0].BarrierType = IsNoTouch && mapNoTouch
          ? OptionBarrierType.DownOut : OptionBarrierType.DownIn;
      }
      else
      {
        throw new ToolkitException("Must specify the call/put type.");
      }
    }

    /// <summary>
    /// Clone
    /// </summary>
    /// <returns></returns>
    public override object Clone()
    {
      var clone = (FxOption)base.Clone();
      // Barriers
      clone.Barriers = CloneUtil.CloneToGenericList(Barriers);
      // Done
      return clone;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Receive currency
    /// </summary>
    [Category("Option")]
    public Currency ReceiveCcy
    {
      get { return Underlying.ReceiveCcy; }
      set { Underlying.ReceiveCcy = Ccy = value; }
    }

    /// <summary>
    /// Pay currency
    /// </summary>
    [Category("Option")]
    public Currency PayCcy
    {
      get { return Underlying.PayCcy; }
      set { Underlying.PayCcy = value; }
    }

    /// <summary>
    /// Underlying FX rate
    /// </summary>
    [Category("Option")]
    public Fx Underlying { get; private set; }

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
    /// Characteristics of FX option barrier(s)
    /// </summary>
    [Category("Option")]
    public OptionBarrierFlag Flags { get; set; }

    /// <summary>
    /// Gets the barrier window begin date.
    /// </summary>
    [Category("Barrier")]
    public Dt BarrierWindowBegin { get; set; }

    /// <summary>
    /// Gets or sets the barrier window end date.
    /// </summary>
    [Category("Barrier")]
    public Dt BarrierWindowEnd { get; set; }

    /// <summary>
    /// List of barriers.
    /// </summary>
    [Category("Barrier")]
    public IList<Barrier> Barriers { get; set; }

    #region Informational

    /// <summary>
    /// Indicating whether this is not a digital nor touch option.
    /// </summary>
    [Category("Informational")]
    internal bool IsRegular => (Flags & (OptionBarrierFlag.Digital
      | OptionBarrierFlag.NoTouch | OptionBarrierFlag.OneTouch)) == 0;

    /// <summary>
    /// Indicating whether this is a digital option.
    /// </summary>
    /// <remarks>
    ///  A digital option is associated with a strike value.
    ///  If the option is active at the expiry time and
    ///  it turns out in the money, then it pays the fixed
    ///  amount of one unit currency;  otherwise, it pays nothing.
    /// </remarks>
    [Category("Informational")]
    public bool IsDigital => (Flags & OptionBarrierFlag.Digital) != 0;

    /// <summary>
    /// True if option has one or more barriers
    /// </summary>
    [Category("Informational")]
    public bool IsBarrier => (Barriers != null && Barriers.Count > 0);

    /// <summary>
    /// True if option has a single barrier
    /// </summary>
    [Category("Informational")]
    public bool IsSingleBarrier => (Barriers != null && Barriers.Count == 1);

    /// <summary>
    /// True if option has a double barrier
    /// </summary>
    [Category("Informational")]
    public bool IsDoubleBarrier => (Barriers != null && Barriers.Count == 2);

    /// <summary>
    /// Indicating whether this is a single barrier touch option.
    /// </summary>
    /// 
    /// <remarks>
    ///  <para>The single barrier touch options include both the <c>one-touch</c>
    ///  and the <c>no-touch</c> options.</para>
    /// 
    ///  <para>A <c>one-touch</c> option pays a fixed unit amount
    ///  if the FX rate hits the barrier sometime during its life-time;
    ///  otherwise, it pays nothing.</para>
    /// 
    ///  <para>A <c>no-touch</c> option pays a fixed unit amount
    ///  if the FX rate never hits the barrier during its life-time; 
    ///  otherwise, it pays nothing.</para>
    /// 
    /// </remarks>
    [Category("Informational")]
    public bool IsTouchOption =>
      (Flags & (OptionBarrierFlag.NoTouch | OptionBarrierFlag.OneTouch)) != 0;

    /// <summary>
    /// Indicating whether this is a single barrier one-touch option.
    /// </summary>
    /// <remarks>
    ///  <para>A <c>one-touch</c> option pays a fixed unit amount
    ///  if the FX rate hits the barrier sometime during its life-time;
    ///  otherwise, it pays nothing.  The payment can be made at either
    ///  the barrier hit time or at the expiry.</para>
    /// </remarks>
    [Category("Informational")]
    internal bool IsOneTouch => (Flags & OptionBarrierFlag.OneTouch) != 0;

    /// <summary>
    /// Indicating whether this is a single barrier no-touch option.
    /// </summary>
    /// <remarks>
    ///  <para>A <c>no-touch</c> option pays a fixed unit amount
    ///  if the FX rate never hits the barrier during its life-time; 
    ///  otherwise, it pays nothing.  The payment is always made at
    ///  the expiry.</para>
    /// </remarks>
    [Category("Informational")]
    internal bool IsNoTouch => (Flags & OptionBarrierFlag.NoTouch) != 0;

    /// <summary>
    /// True if pays in domestic (home) currency
    /// </summary>
    [Category("Informational")]
    internal bool IsPayInHomeCurrency =>
      (Flags & OptionBarrierFlag.PayInForeignCurrency) == 0;

    /// <summary>
    /// True if pays at barrier hit
    /// </summary>
    [Category("Informational")]
    internal bool IsPayAtBarrierHit =>
      (Flags & OptionBarrierFlag.PayAtBarrierHit) != 0;

    internal static FxOptionConfig Settings => ToolkitConfigurator.Settings.FxOption;

    #endregion Informational

    #endregion Properties

    #region IOption Members

    Dt IOptionProduct.Expiration
    {
      get { return Maturity; }
    }

    IProduct IOptionProduct.Underlying
    {
      get { return null; }
    }

    #endregion

    #region Explicit IBasicExoticOption members

    SettlementType IBasicExoticOption.SettlementType
    {
      get { return SettlementType.Cash; }
    }

    OptionPayoffType IBasicExoticOption.PayoffType
    {
      get { return IsDigital ? OptionPayoffType.Digital : OptionPayoffType.Regular; }
    }

    double IBasicExoticOption.Rebate
    {
      get { return 0; }
    }

    Frequency IBasicExoticOption.BarrierMonitoringFrequency
    {
      get
      {
        return Barriers.IsNullOrEmpty() || Barriers[0] == null
          ? Frequency.Continuous : Barriers[0].MonitoringFrequency;
      }
    }

    BarrierOptionPayoffTime IBasicExoticOption.BarrierPayoffTime
    {
      get
      {
        return IsPayAtBarrierHit ? BarrierOptionPayoffTime.AtBarrierHit
          : BarrierOptionPayoffTime.Default;
      }
    }

    #endregion
  }
}
