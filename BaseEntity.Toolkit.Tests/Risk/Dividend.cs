using System;
using System.Collections;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Curve Tenor which is a component of the curves
  /// </summary>
  [Component(ChildKey = new[] {"AnnounceDate"})]
  [Serializable]
  public class Dividend : BaseEntityObject
  {
    #region Constructors

    /// <summary>
    ///   Default constructor
    /// </summary>
    ///
    protected Dividend()
    {}

    #endregion

    #region Methods

    /// <summary>
    ///   Validate
    /// </summary>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if (!paymentDate_.IsValid())
        InvalidValue.AddError(errors, this, "PaymentDate", "Invalid payment date");

      if (!exDivDate_.IsValid())
        InvalidValue.AddError(errors, this, "ExDivDate", "Invalid ExDiv date");

      if (paymentDate_.IsValid() && exDivDate_.IsValid() && paymentDate_ <= exDivDate_)
        InvalidValue.AddError(errors, this, "PaymentDate", "Payment date must be after ExDiv date");

      if (amount_ <= 0.0)
        InvalidValue.AddError(errors, this, "Amount", String.Format("Divident payment {0} must be +ve", amount_));

      return;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Tenor for this curve tenor
    /// </summary>
    [DtProperty]
    public Dt AnnounceDate
    {
      get { return announceDate_; }
      set { announceDate_ = value; }
    }

    /// <summary>
    ///   Tenor for this curve tenor
    /// </summary>
    [DtProperty]
    public Dt ExDivDate
    {
      get { return exDivDate_; }
      set { exDivDate_ = value; }
    }

    /// <summary>
    ///   Reference for this curve tenor
    /// </summary>
    [DtProperty]
    public Dt RecordDate
    {
      get { return recordDate_; }
      set { recordDate_ = value; }
    }

    /// <summary>
    ///   Reference for this curve tenor
    /// </summary>
    [DtProperty]
    public Dt PaymentDate
    {
      get { return paymentDate_; }
      set { paymentDate_ = value; }
    }

    /// <summary>
    ///   Currency for this credit curve
    /// </summary>
    [EnumProperty]
    public Currency Ccy
    {
      get { return ccy_; }
      set { ccy_ = value; }
    }

    /// <summary>
    ///   Market quote value
    /// </summary>
    [NumericProperty(FormatString = "#,###.#####", AllowNullValue = false)]
    public double Amount
    {
      get { return amount_; }
      set { amount_ = value; }
    }

    #endregion Properties

    #region Data

    private Dt announceDate_;
    private Dt exDivDate_;
    private Dt recordDate_;
    private Dt paymentDate_;
    private Currency ccy_;
    private double amount_;

    #endregion Data
  }

  // class Dividend
}
