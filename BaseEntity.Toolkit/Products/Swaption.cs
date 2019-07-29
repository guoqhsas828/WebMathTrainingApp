//
// Swaption.cs
//  -2011. All rights reserved.
//

using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using System.Collections;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Option to enter an interest rate Swap.
  /// </summary>
  /// <remarks>
  /// <para>A swaption is an option granting its owner the right but not the obligation to enter into an
  /// underlying swap. Although options can be traded on a variety of swaps, the term "swaption" typically
  /// refers to options on interest rate swaps.</para>
  /// <para>There are two types of swaption contracts:</para>
  /// <para>A payer swaption gives the owner of the swaption the right to enter into a swap where they pay
  /// the fixed leg and receive the floating leg.</para>
  /// <para>A receiver swaption gives the owner of the swaption the right to enter into a swap in which
  /// they will receive the fixed leg, and pay the floating leg.</para>
  /// <para>In addition, a "straddle" refers to a combination of a receiver and a payer option on the same
  /// underlying swap.</para>
  /// <para>The buyer and seller of the swaption agree on:</para>
  /// <list type="bullet">
  ///   <item>the premium (price) of the swaption</item>
  ///   <item>length of the option period (which usually ends two business days prior to the start date of the underlying swap),</item>
  ///   <item>the terms of the underlying swap, including:</item>
  ///   <item>notional amount (with amortization amounts, if any)</item>
  ///   <item>the fixed rate (which equals the strike of the swaption)</item>
  ///   <item>the frequency of observation for the floating leg of the swap (for example, 3 month Libor paid quarterly)</item>
  /// </list>
  /// <para><i>Source: Wikipedia</i></para>
  /// </remarks>
  /// <seealso href="http://personal.anderson.ucla.edu/francis.longstaff/4-00.pdf">The Relative Valuation of Caps and Swaptions: Theory and Empirical Evidence. Longstaff, Santa-Clara, Schwartz</seealso>
  /// <seealso href="http://www.fea.com/resources/pdf/swaptions.pdf">Alternative Valuation Methods for Swaptions: The Devil is in the Details. Blanco, Gray, Hazzard</seealso>
  /// <seealso href="http://www.fea.com/resources/pdf/swaptions.pdf">Basic Fixed Income Derivative Hedging. Financial-edu.com</seealso>
  /// <seealso href="http://wwz.unibas.ch/fileadmin/wwz/redaktion/finance/HS09/FOPT_2_090924.pdf">Martingales and Measures: Black's Model. Henn-Overbeck</seealso>
  /// <seealso href="http://pages.stern.nyu.edu/~dbackus/3176/adlec4.pdf">Black-Scholes and binomial valuation of swaptions (Advanced Fixed Income Analytics 4:5), Backus, Zin</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.SwaptionBlackPricer"/>
  [Serializable]
  [ReadOnly(true)]
  public class Swaption : Product, IOptionProduct
  {
    #region Constructors
    /// <summary>
    ///   Default Constructor
    /// </summary>
    protected Swaption() { }

    /// <summary>
    ///   Default Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Expiration date of the option</param>
    /// <param name="ccy">Currency</param>
    /// <param name="underlyingFixedLeg">Underlying Fixed Swap Leg </param>
    /// <param name="underlyingFloatLeg">Underlying Floating Swap Leg </param>
    /// <param name="notificationDays">Notification Days</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public Swaption(
      Dt effective,
      Dt maturity,
      Currency ccy,
      SwapLeg underlyingFixedLeg,
      SwapLeg underlyingFloatLeg,
      int notificationDays,
      PayerReceiver type,
      OptionStyle style,
      double strike)
      : base(effective, maturity, ccy)
    {
      Strike = strike;
      Style = style;
      Type = type;
      NotificationDays = notificationDays;
      NotificationCalendar = underlyingFixedLeg.Calendar;
      _swap = new Swap(underlyingFloatLeg, underlyingFixedLeg);
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
    /// <param name="errors">List to append errors to</param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // First check if any of the underlyings are null
      if (UnderlyingFixedLeg == null)
      {
        InvalidValue.AddError(errors, this, "UnderlyingFixedLeg cannot be null");
        return;
      }
      if (UnderlyingFloatLeg == null)
      {
        InvalidValue.AddError(errors, this, "UnderlyingFloatLeg cannot be null");
        return;
      }
      if (!UnderlyingFloatLeg.Floating)
      {
        InvalidValue.AddError(errors, this, "UnderlyingFloatLeg must set floating flag");
        return;
      }

      if (!UnderlyingFixedLeg.Maturity.IsEmpty() && !Maturity.IsEmpty() && Maturity > UnderlyingFixedLeg.Maturity)
        InvalidValue.AddError(errors, this, "The swaption expiration date can not be after the underlying swap maturity.");

      if (UnderlyingFloatLeg.Ccy != UnderlyingFixedLeg.Ccy)
        InvalidValue.AddError(errors, UnderlyingFloatLeg, "Ccy", String.Format("Invalid Swap Legs. The 2 legs must use same currency"));

      if (UnderlyingFloatLeg.ReferenceIndex == null)
        InvalidValue.AddError(errors, this, "Missing reference index on the floating leg.");

      if (NotificationDays < 0)
        InvalidValue.AddError(errors, this, "NotificationDays", "NotificationDays must be greater than or equal to 0");

      // Validate strike
      if (Strike < -1 || Strike > 1.0)
        InvalidValue.AddError(errors, this, "Strike", "Option Strike must be between -100% and 100%");

      // Validate American/European Swaptions
      if (Style == OptionStyle.None)
        InvalidValue.AddError(errors, this, "Style", "Option Style cannot be None");

      // Validate type
      if (Type == PayerReceiver.None)
        InvalidValue.AddError(errors, this, "Type", "Option Type cannot be None");

      return;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Underlying swap
    /// </summary>
    [Category("Underlying")]
    public Swap Swap
    {
      get { return _swap; } 
    }

    /// <summary>
    ///   Underlying (Fixed) Swap Leg
    /// </summary>
    [Category("Underlying")]
    public SwapLeg UnderlyingFixedLeg
    {
      get { return _swap.PayerLeg; }
      set { _swap.PayerLeg = value; }
    }

    /// <summary>
    ///   Underlying (Floating) Swap Leg
    /// </summary>
    [Category("Underlying")]
    public SwapLeg UnderlyingFloatLeg
    {
      get { return _swap.ReceiverLeg; }
      set { _swap.ReceiverLeg = value; }
    }

    /// <summary>
    ///   When the underlyng swap start if exercise.
    /// </summary>
    [Category("Underlying")]
    public SwapStartTiming SwapStartTiming
    {
      get { return _swap.OptionTiming; }
      set { _swap.OptionTiming = value; }
    }

    /// <summary>
    ///   Notification Days
    /// </summary>
    [Category("Option")]
    public int NotificationDays { get; set; }

    /// <summary>
    /// Gets the expiry date of the option.
    /// </summary>
    /// <value>The expiration.</value>
    [Category("Option")]
    public Dt Expiration
    {
      get
      {
        return _expiry.IsEmpty()
          ? Dt.AddDays(Maturity, -NotificationDays, NotificationCalendar)
          : _expiry;
      }
      set { _expiry = value; }
    }

    /// <summary>
    ///   Option type
    /// </summary>
    [Category("Option")]
    public OptionType OptionType
    {
      get { return (Type == PayerReceiver.Payer ? OptionType.Call : OptionType.Put); }
    }

    /// <summary>
    ///   Option Payer/Receiver
    /// </summary>
    [Category("Option")]
    public PayerReceiver Type { get; set; }

    /// <summary>
    ///   Option style
    /// </summary>
    [Category("Option")]
    public OptionStyle Style { get; set; }

    /// <summary>
    ///   Option strike price
    /// </summary>
    [Category("Option")]
    public double Strike { get; set; }

    /// <summary>
    ///   Financing spread (Actual/360)
    /// </summary>
    [Category("Base")]
    public double FinancingSpread { get; set; }

    ///<summary>
    /// The calendar that notification schedule is using
    ///</summary>
    [Category("Option")]
    public Calendar NotificationCalendar { get; set; }

    ///<summary>
    /// The right of option holder
    ///</summary>
    [Category("Option")]
    public OptionRight OptionRight { get; set; }

    /// <summary>
    ///  Settlement type
    /// </summary>
    [Category("Option")]
    public SettlementType SettlementType
    {
      get; set;
    }

    IProduct IOptionProduct.Underlying
    {
      get { return Swap; }
    }

    #endregion Properties

    #region Data

    private Swap _swap;
    private Dt _expiry;

    #endregion Data

  } // class Swaption

}
