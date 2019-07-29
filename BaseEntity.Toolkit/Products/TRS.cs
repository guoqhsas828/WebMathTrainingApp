/*
 * TRS.cs
 *
 */

using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using System.Collections;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  ///   Type of total return (ie price, yield, etc.)
  /// </summary>
  public enum TRSType
  {
    /// <summary>Full Price</summary>
    Price,
    /// <summary>Flat price</summary>
    Flat,
    /// <summary>Yield</summary>
    Yield
  }

  /// <summary>
  ///   Total Return Swap
  /// </summary>
  /// <remarks>
  ///   <para>A total return swap is a swap where one party agrees to pay the other party the return of a
  ///   defined underlying asset.</para>
  ///   <para>Typically one party (the TR Payer) pays to the other party (the TR Receiver) the total return
  ///   of a specified asset (the underlying reference obligation). The "total return" consists of
  ///   the sum of interest, fees, and any other payments of the underlying reference obligation.</para>
  ///   <para>The credit risk of the underlying reference entity is transfered from the TR Payer to the
  ///   TR Receiver.</para>
  ///   <para>The total return swap can be on many different underlyings but most commonly is on an
  ///   underlying bond, loan, equity index or stock.</para>
  ///   <para>At maturity, the total return swap pays:</para>
  ///   <formula>
  ///     ( \mathrm{factor} * min(\mathrm{Maximum}, Quote_T) - ( \mathrm{targetFactor} * \mathrm{targetQuote} ) ) * \mathrm{Notional}
  ///   </formula>
  ///   <para>Where Quote can be full price, flat price, or yield.</para>
  /// </remarks>
  [Serializable]
  [ReadOnly(true)]
  public class TRS : Product
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected
    TRS()
    { }
    
    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="underlying">Underlying bond</param>
    /// <param name="type">Type of total return (full price, flat price, yield, etc)</param>
    /// <param name="determination">Determination date</param>
    /// <param name="target">Target price or yield</param>
    ///
    public
    TRS(Dt effective, Dt maturity, Currency ccy, IProduct underlying,
         TRSType type, Dt determination, double target)
      : base(effective, maturity, ccy)
    {
      // Use properties for validation
      Underlying = underlying;
      Type = type;
      Determination = determination;
      Factor = 1.0;
      Maximum = 0.0;
      TargetFactor = 1.0;
      Target = target;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="underlying">Underlying bond</param>
    /// <param name="type">Type of total return (full price, flat price, yield, etc)</param>
    /// <param name="determination">Determination date</param>
    /// <param name="factor">Price or yield multiplier</param>
    /// <param name="maximum">Maximum price or yield</param>
    /// <param name="targetFactor">Target factor</param>
    /// <param name="target">Target price or yield</param>
    ///
    public
    TRS(Dt effective, Dt maturity, Currency ccy, IProduct underlying,
         TRSType type, Dt determination, double factor, double maximum,
         double targetFactor, double target)
      : base(effective, maturity, ccy)
    {
      // Use properties for validation
      Underlying = underlying;
      Type = type;
      Determination = determination;
      Factor = factor;
      Maximum = maximum;
      TargetFactor = targetFactor;
      Target = target;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      TRS obj = (TRS)base.Clone();

      obj.underlying_ = (IProduct)underlying_.Clone();

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

      // Validate underlying
      if (underlying_ == null)
        InvalidValue.AddError(errors, this, "Underlying", "Must specify underlying for TRS");
      else if (!(underlying_ is Bond))
        InvalidValue.AddError(errors, this, "Underlying", String.Format("Invalid underlying. Currenty only support TRS on Bonds"));
      else
        underlying_.Validate(errors);

      // Determination date
      if (!determination_.IsValid())
        InvalidValue.AddError(errors, this, "Underlying", "Invalid Determination Date");
      if (determination_ < Effective)
        InvalidValue.AddError(errors, this, "Determination", String.Format("Determination date {0} must be after effective {1}", determination_, Effective));
      if (determination_ > Maturity)
        InvalidValue.AddError(errors, this, "Determination", String.Format("Determination date {0} must be on or before maturity {1}", determination_, Maturity));

      if( maximum_ <= 0.0 )
        InvalidValue.AddError(errors, this, "Maximum", String.Format("Invalid maximum price or yield {0}. Must be +ve", maximum_));
      if( target_ < 0.0 )
        InvalidValue.AddError(errors, this, "Maximum", String.Format("Invalid target price or yield {0}. Must be +Ve", target_));

      return;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Underlying Bond
    /// </summary>
    [Category("Underlying")]
    public IProduct Underlying
    {
      get { return underlying_; }
      set { underlying_ = value; }
    }

    /// <summary>
    ///   TRS Type (full price/flat price/yield/etc)
    /// </summary>
    [Category("Base")]
    public TRSType Type
    {
      get { return type_; }
      set { type_ = value; }
    }

    /// <summary>
    ///   Determination date (quoted date of final price or yield)
    /// </summary>
    [Category("Base")]
    public Dt Determination
    {
      get { return determination_; }
      set { determination_ = value; }
    }

    /// <summary>
    ///   Multiplier factor for final price or yield
    /// </summary>
    [Category("Base")]
    public double Factor
    {
      get { return factor_; }
      set { factor_ = value; }
    }

    /// <summary>
    ///   Maximum final price or yield
    /// </summary>
    [Category("Base")]
    public double Maximum
    {
      get { return maximum_; }
      set { maximum_ = value; }
    }

    /// <summary>
    ///   Multiplier factor for target price or yield
    /// </summary>
    [Category("Base")]
    public double TargetFactor
    {
      get { return targetFactor_; }
      set { targetFactor_ = value; }
    }

    /// <summary>
    ///   Target price or yield
    /// </summary>
    [Category("Base")]
    public double Target
    {
      get { return target_; }
      set { target_ = value; }
    }

    #endregion Properties

    #region Data

    private IProduct underlying_;
    private TRSType type_;
    private Dt determination_;
    private double factor_;
    private double maximum_;
    private double targetFactor_;
    private double target_;

    #endregion Data

  } // class TRS

}
