/*
 * ExchangeBondOption.cs
 *
 *
 */

using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using System.Collections;

namespace BaseEntity.Toolkit.Products
{
  ///
  /// <summary>
  ///   Option to Exchange bonds
  /// </summary>
  ///
  /// <remarks>
  ///   This is the option to exchange one bond for any of a set of
  ///   alternate bonds at agreed strike prices.
  /// </remarks>
  ///
  /// <preliminary>
  ///   This class is preliminary and not supported for customer use. This may be
  ///   removed or moved to a separate product at a future date.
  /// </preliminary>
  ///
  [Serializable]
  [ReadOnly(true)]
  public class ExchangeBondOption : Product
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected
    ExchangeBondOption()
    { }
    
    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="exchangeBonds">List of bonds to exchange to</param>
    /// <param name="strikes">List of strike prices for each exchange bond</param>
    /// <param name="exchange">Bond to exchange for</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    ///
    public
    ExchangeBondOption(Dt effective, Dt maturity, Currency ccy, Bond[] exchangeBonds, double[] strikes,
                       Bond exchange, Dt expiration, OptionType type, OptionStyle style)
      : base(effective, maturity, ccy)
    {
      // Use properties for validation
      if (exchangeBonds.Length != strikes.Length)
        throw new ArgumentException("Number of exchange bonds does not match number of strikes");
      ExchangeBonds = exchangeBonds;
      Strikes = strikes;
      Exchange = exchange;
      Expiration = expiration;
      Type = type;
      Style = style;
    }
    
    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      ExchangeBondOption obj = (ExchangeBondOption)base.Clone();

      obj.exchangeBonds_ = new Bond[exchangeBonds_.Length];
      for (int i = 0; i < exchangeBonds_.Length; i++)
        obj.exchangeBonds_[i] = (Bond)exchangeBonds_[i].Clone();
      obj.exchange_ = (Bond)exchange_.Clone();

      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    ///
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      for (int i = 0; i < exchangeBonds_.Length; i++)
        exchangeBonds_[i].Validate(errors);
      exchange_.Validate(errors);
      return;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Exchange bonds
    /// </summary>
    [Category("Underlying")]
    public Bond[] ExchangeBonds
    {
      get { return exchangeBonds_; }
      set
      {
        for (int i = 0; i < value.Length; i++)
          value[i].Validate();
        exchangeBonds_ = value;
      }
    }
    
    /// <summary>
    ///   Option strike prices
    /// </summary>
    [Category("Option")]
    public double[] Strikes
    {
      get { return strikes_; }
      set
      {
        for (int i = 0; i < value.Length; i++)
          if (value[i] < 0.0)
            throw new ArgumentException(String.Format("Invalid strike price. Must be >0, not {0}", value[i]));
        strikes_ = value;
      }
    }
    
    /// <summary>
    ///   Bond to exchange for
    /// </summary>
    [Category("Underlying")]
    public Bond Exchange
    {
      get { return exchange_; }
      set
      {
        if (value == null)
          throw new ArgumentException("Invalid exchange bond. Must not be null");
        value.Validate();
        exchange_ = value;
      }
    }
    
    /// <summary>
    ///   Expiration date of option.
    /// </summary>
    [Category("Option")]
    public Dt Expiration
    {
      get { return expiration_; }
      set
      {
        if (!value.IsValid())
          throw new ArgumentException(String.Format("Invalid expiration date. Must be valid date, not {0}", value));
        expiration_ = value;
      }
    }
    
    /// <summary>
    ///   Option type
    /// </summary>
    [Category("Option")]
    public OptionType Type
    {
      get { return type_; }
      set
      {
        if (value == OptionType.None)
          throw new ArgumentException("Invalid option type. Must not be None");
        type_ = value;
      }
    }
    
    /// <summary>
    ///   Option style
    /// </summary>
    [Category("Option")]
    public OptionStyle Style
    {
      get { return style_; }
      set
      {
        if (value == OptionStyle.None)
          throw new ArgumentException("Invalid option style. Must not be None");
        style_ = value;
      }
    }

    #endregion Properties

    #region Data

    private Bond[] exchangeBonds_;  // Bonds to exchange to
    private double[] strikes_;      // Strike prices to exchange at
    private Bond exchange_;         // Underlying to exchange for
    private Dt expiration_;         // Expiration date
    private OptionType type_;       // Option type
    private OptionStyle style_;     // Option style

    #endregion Data

  } // class ExchangeBondOption

}
