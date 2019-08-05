using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  [Serializable]
  [Entity(EntityId = 310, Description = "Listed option on a stock", DisplayName = "Stock Option (Listed)", Category = "Equities")]
  [StandardProduct]
  public class StockOption : Product, ISingleNameProduct
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    public StockOption()
    {
      DaysToSettle = 0;
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

      // Valid maturity
      if (Maturity.IsEmpty())
        InvalidValue.AddError(errors, this, "Maturity date must be set");

      if (!Issue.IsEmpty())
      {
        // Valid issue and effective implies...
        if (!Effective.IsEmpty())
        {
          // Effective date after issue date
          if (Dt.Cmp(Effective, Issue) < 0)
            InvalidValue.AddError(errors, this, String.Format("Effective {0} must be on or after issue {1}", Effective, Issue));
          // Effective date on or before maturity
          if (Dt.Cmp(Effective, Maturity) > 0)
            InvalidValue.AddError(errors, this, String.Format("Effective {0} must be on or before maturity {1}", Effective, Maturity));
        }

        // Issue date before maturity date
        if (Dt.Cmp(Issue, Maturity) >= 0)
          InvalidValue.AddError(errors, this, String.Format("Issue date {0} must be before maturity date {1}", Effective, Maturity));
      }

      if (Type == OptionType.None)
        InvalidValue.AddError(errors, this, "Option Type cannot be None");

      if (Style == OptionStyle.None)
        InvalidValue.AddError(errors, this, "Option Style cannot be None");

      if (Style == OptionStyle.Bermudan)
        InvalidValue.AddError(errors, this, String.Format("Option Style [{0}] not supported", Style));

      if (Strike < 0.0)
        InvalidValue.AddError(errors, this, String.Format("Invalid strike {0}. Must be +Ve", Strike));

      if (Underlying == null)
        InvalidValue.AddError(errors, this, "Underlying can not be null");
      else
      {
        if (Ccy != Underlying.Ccy)
        {
          InvalidValue.AddError(errors, this, "Ccy", "Option ccy needs to be the same as underlying ccy");
        }
        Underlying.Validate(errors);
      }

      return;
    }

    /// <summary>
    ///   Determine if a negative amount means its a Buy
    /// </summary>
    /// <remarks>
    ///   <para>For most products a positive notional is a buy. Credit products like CDS follow the
    ///   reverse convention so that a positive notional means long credit.</para>
    /// </remarks>
    /// <returns>true - negative amt is buy, false - positive amt is buy</returns>
    public override bool BuyIsNegative()
    {
      return false;
    }

    /// <summary>
    ///   Calculate natural settle date for this product if traded on specified asof date
    /// </summary>
    /// <param name="asOf"></param>
    /// <returns>Settlement date</returns>
    public override Dt CalcSettle(Dt asOf)
    {
      return Dt.AddDays(asOf, DaysToSettle, Calendar);
    }

    /// <summary>
    ///   Determines whether the Product is active on the specified settlement date. 
    /// </summary>
    /// <remarks>
    ///   <para>For cash products this is if the product has matured.
    ///   For options, this is if the option has expired.</para>
    ///   <para>When pricing for settlement on the maturity or expiration
    ///   date, the trade is no longer active. Risk assumes pricing is
    ///   at the end of the business day.</para>
    /// </remarks>
    /// <param name="settle">Settlement date</param>
    /// <returns>true if the product is still active, false otherwise</returns>
    public override bool IsActive(Dt settle)
    {
      bool isActive = base.IsActive(settle);

      if (isActive)
      {
        if (this.Expiration.IsValid() && Dt.Cmp(this.Expiration, settle) <= 0)
          return false;
      }

      return isActive;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Name of issuer of product
    /// </summary>
    [ManyToOneProperty(Column = "IssuerId")]
    public LegalEntity Issuer
    {
      get { return (LegalEntity)ObjectRef.Resolve(issuer_); }
      set { issuer_ = ObjectRef.Create(value); }
    }

    /// <summary>
    ///   Underlying stock
    /// </summary>
    [ManyToOneProperty(AllowNullValue = false)]
    public Stock Underlying
    {
      get { return (Stock)ObjectRef.Resolve(underlying_); }
      set { underlying_ = ObjectRef.Create(value); }
    }

    /// <summary>
    /// 
    /// </summary>
    public long UnderlyingId
    {
      get { return underlying_ == null || underlying_.IsNull ? 0 : underlying_.Id; }
    }

    /// <summary>
    ///   Expiration date of option.
    /// </summary>
    [DtProperty]
    public Dt Expiration
    {
      get { return expiration_; }
      set
      {
        //if( !value.IsValid() )
        //	throw new System.ArgumentOutOfRangeException(String.Format("Invalid expiration date {0}. Must be valid date", value));
        expiration_ = value;
        Maturity = value;
      }
    }

    /// <summary>
    ///   Option type
    /// </summary>
    [EnumProperty]
    public OptionType Type { get; set; }

    /// <summary>
    ///   Option style
    /// </summary>
    [EnumProperty]
    public OptionStyle Style { get; set; }

    /// <summary>
    ///   Option strike price
    /// </summary>
    [NumericProperty(Format = NumberFormat.Currency, RelatedProperty = "Ccy", AllowNullValue = false)]
    public double Strike { get; set; }

    /// <summary>
    ///   Cusip
    /// </summary>
    [StringProperty(MaxLength = 16)]
    public string Cusip { get; set; }

    /// <summary>
    ///   Calendar (used to determine valid settlement days)
    /// </summary>
    [CalendarProperty]
    public Calendar Calendar { get; set; }

    #endregion Properties

    #region Non-persistent properties

    /// <summary>
    ///   Number of underlying shares per option contract covers
    /// </summary>
    [NumericProperty(Persistent = false)]
    public double ContractSize
    {
      get { return Notional; }
      set { Notional = value; }
    }

    #endregion

    #region ICreditProduct Members

    /// <summary>
    ///   List of objects that owns one or more reference credits
    /// </summary>
    IEnumerable<IReferenceCreditsOwner> ICreditProduct.ReferenceCreditsOwners
    {
      get { yield return Underlying; }
    }

    #endregion

    #region ISingleNameProduct Members

    /// <summary>
    ///   Reference credit
    /// </summary>
    IReferenceCredit ISingleNameProduct.ReferenceCredit
    {
      get { return Underlying; }
    }

    #endregion

    #region Data

    private ObjectRef underlying_;
    private Dt expiration_;
    private ObjectRef issuer_;

    #endregion Data

    /// <summary>
    ///   Financial Instrument Global Identifier (Formerly BBGID)
    /// </summary>
    [StringProperty(MaxLength = 32)]
    public string FIGI { get; set; }
  }
}
