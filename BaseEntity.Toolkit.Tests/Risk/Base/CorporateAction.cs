using System;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Representation of a Corporate Action for a Legal Entity
  /// </summary>
  [Serializable]
  [Entity(EntityId = 216, AuditPolicy = AuditPolicy.History, Key = new []{"OldTicker", "NewTicker"})]
  public class CorporateAction : AuditedObject, IComparable<CorporateAction>
  {
    #region Constructor

    /// <summary>
    ///   Default Constructor
    /// </summary>
    protected CorporateAction()
      : this(Dt.Empty, null, null, 1.0)
    {
    }

    /// <summary>
    ///   Constructor with minimum required information for building a valid Corporate Action
    /// </summary>
    /// <param name="implementationDate"></param>
    /// <param name="oldTicker"></param>
    /// <param name="newTicker"></param>
    /// <param name="percentDebtTransferred"></param>
    public CorporateAction(Dt implementationDate, string oldTicker, string newTicker, double percentDebtTransferred)
    {
      ImplementationDate = implementationDate;
      OldTicker = oldTicker;
      NewTicker = newTicker;
      PercentDebtTransferred = percentDebtTransferred;
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Date from which the Corporate Actions is Effective
    /// </summary>
    [DtProperty(AllowNullValue = true)]
    public Dt EffectiveDate { get; set; }

    /// <summary>
    ///   Date from which we start getting quotes against the New Ticker
    /// </summary>
    [DtProperty(AllowNullValue = false)]
    public Dt ImplementationDate { get; set; }

    /// <summary>
    ///   Description for the Corporate Action Event Type
    /// </summary>
    /// <example>Name Change, Merger, Demerger</example>
    [StringProperty(AllowNullValue = true, MaxLength = 64)]
    public string EventType { get; set; }

    /// <summary>
    ///   Old Reference Entity Full Name
    /// </summary>
    [StringProperty(AllowNullValue = true, MaxLength = 128)]
    public string OldRefEntityName { get; set; }

    /// <summary>
    ///   Old Reference Entity Short Name
    /// </summary>
    [StringProperty(AllowNullValue = true, MaxLength = 64)]
    public string OldShortName { get; set; }

    /// <summary>
    ///   Old Reference Entity RED Code
    /// </summary>
    [StringProperty(AllowNullValue = true, MaxLength = 6)]
    public string OldRed { get; set; }

    /// <summary>
    ///   Old Reference Entity CUSIP
    /// </summary>
    [StringProperty(AllowNullValue = true, MaxLength = 16)]
    public string OldCusip { get; set; }

    /// <summary>
    ///   Old Reference Entity Ticker
    /// </summary>
    [StringProperty(AllowNullValue = false, MaxLength = 20)]
    public string OldTicker { get; set; }

    /// <summary>
    ///   Old Reference Entity Parent Ticker
    /// </summary>
    [StringProperty(AllowNullValue = true, MaxLength = 20)]
    public string OldParentTicker { get; set; }

    /// <summary>
    ///   Old Reference Entity Parent Full Name
    /// </summary>
    [StringProperty(AllowNullValue = true, MaxLength = 128)]
    public string OldParentRefEntityName { get; set; }


    /// <summary>
    ///   New Reference Entity Full Name
    /// </summary>
    [StringProperty(AllowNullValue = true, MaxLength = 128)]
    public string NewRefEntityName { get; set; }

    /// <summary>
    ///   New Reference Entity Short Name
    /// </summary>
    [StringProperty(AllowNullValue = true, MaxLength = 64)]
    public string NewShortName { get; set; }

    /// <summary>
    ///   New Reference Entity Ticker
    /// </summary>
    [StringProperty(AllowNullValue = false, MaxLength = 20)]
    public string NewTicker { get; set; }

    /// <summary>
    ///   New Reference Entity RED Code
    /// </summary>
    [StringProperty(AllowNullValue = true, MaxLength = 6)]
    public string NewRed { get; set; }

    /// <summary>
    ///   New Reference Entity CUSIP
    /// </summary>
    [StringProperty(AllowNullValue = true, MaxLength = 16)]
    public string NewCusip { get; set; }

    /// <summary>
    ///   New Reference Entity Parent Ticker
    /// </summary>
    [StringProperty(AllowNullValue = true, MaxLength = 20)]
    public string NewParentTicker { get; set; }

    /// <summary>
    ///   New Reference Entity Parent Full Name
    /// </summary>
    [StringProperty(AllowNullValue = true, MaxLength = 128)]
    public string NewParentRefEntityName { get; set; }

    /// <summary>
    ///   Percentage Debt Transferred from Old Reference Entity to New Reference Entity
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     For Demergers, one or more or all of the new Reference Entities can inherit 
    ///     all or part of the debt from the Old Reference Entity. 
    ///   </para>
    ///   <para>
    ///     If no debt is transferred from the Old Ticker to the New Ticker, the
    ///     Corporate Actions is not applied to the positions against the old 
    ///     Reference Entity
    ///   </para>
    /// </remarks>
    [NumericProperty(AllowNullValue = false, Format = NumberFormat.Percentage)]
    public double PercentDebtTransferred { get; set; }

    #endregion

    #region Methods

    /// <summary>
    ///   Checks to see if the Corporate Action is valid and reports any errors.
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);

      if (ImplementationDate.IsEmpty())
        InvalidValue.AddError(errors, this, "ImplementationDate", "Value cannot be empty!");

      if (PercentDebtTransferred < 0.0 || PercentDebtTransferred > 1.0)
        InvalidValue.AddError(errors, this, "PercentDebtTransferred", "Value must be between 0% and 100%");

      if(String.Equals(OldTicker, NewTicker, StringComparison.OrdinalIgnoreCase))
        InvalidValue.AddError(errors, this, "NewTicker", "NewTicker cannot be same as OldTicker");
    }


    /// <summary>
    ///  Determines whether the other Corporate Action is same.
    /// </summary>
    /// <param name="other">The other Corporate Action to compare.</param>
    /// <param name="ignorePercentDebtTransferred">if set to <c>true</c> 
    /// will ignore the PercentDebtTransferred for comparison.</param>
    /// <returns>
    /// 	<c>true</c> if the other Corporate Action is same; otherwise, <c>false</c>.
    /// </returns>
    public bool IsSame(CorporateAction other, bool ignorePercentDebtTransferred)
    {
      if (this.EffectiveDate != other.EffectiveDate) return false;
      if (this.ImplementationDate != other.ImplementationDate) return false;
      if (this.EventType != other.EventType) return false;

      if (!String.Equals(this.OldRefEntityName, other.OldRefEntityName, StringComparison.OrdinalIgnoreCase)) return false;
      if (!String.Equals(this.OldShortName, other.OldShortName, StringComparison.OrdinalIgnoreCase)) return false;
      if (!String.Equals(this.OldRed, other.OldRed, StringComparison.OrdinalIgnoreCase)) return false;
      if (!String.Equals(this.OldCusip, other.OldCusip, StringComparison.OrdinalIgnoreCase)) return false;
      if (!String.Equals(this.OldTicker, other.OldTicker, StringComparison.OrdinalIgnoreCase)) return false;
      if (!String.Equals(this.OldParentTicker, other.OldParentTicker, StringComparison.OrdinalIgnoreCase)) return false;
      if (!String.Equals(this.OldParentRefEntityName, other.OldParentRefEntityName, StringComparison.OrdinalIgnoreCase)) return false;

      if (!String.Equals(this.NewRefEntityName, other.NewRefEntityName, StringComparison.OrdinalIgnoreCase)) return false;
      if (!String.Equals(this.NewShortName, other.NewShortName, StringComparison.OrdinalIgnoreCase)) return false;
      if (!String.Equals(this.NewRed, other.NewRed, StringComparison.OrdinalIgnoreCase)) return false;
      if (!String.Equals(this.NewCusip, other.NewCusip, StringComparison.OrdinalIgnoreCase)) return false;
      if (!String.Equals(this.NewTicker, other.NewTicker, StringComparison.OrdinalIgnoreCase)) return false;
      if (!String.Equals(this.NewParentTicker, other.NewParentTicker, StringComparison.OrdinalIgnoreCase)) return false;
      if (!String.Equals(this.NewParentRefEntityName, other.NewParentRefEntityName, StringComparison.OrdinalIgnoreCase)) return false;

      if (ignorePercentDebtTransferred)
        return true;
      return this.PercentDebtTransferred.ApproximatelyEqualsTo(other.PercentDebtTransferred);
    }

    #endregion

    #region IComparable<CorporateAction> Members

    /// <summary>
    ///   Comapares the ImplementationDate
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public int CompareTo(CorporateAction other)
    {
      return (Dt.Cmp(this.ImplementationDate, other.ImplementationDate));
    }

    #endregion
  }
}
