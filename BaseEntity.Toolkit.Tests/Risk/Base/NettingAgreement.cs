/*
 * NettingAgreement.cs
 *
 */

using System;
using System.Collections;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  /// <summary>
  /// Associates a group of MasterAgreements that share a common Counterparty into a Netting Set
  /// </summary>
  [Serializable]
  [Entity(EntityId = 125, AuditPolicy = AuditPolicy.History)]
  public class NettingAgreement : AuditedObject
  {
    #region Data
    private ObjectRef _counterparty;
    private ObjectRef _bookingEntity; 
    private string _name; 
    #endregion

    #region Constructors
    /// <summary>
    /// Default Constructor
    /// </summary>
    protected NettingAgreement()
    {
    }
    #endregion

    #region Persistent Properties

    /// <summary>
    ///  unique name
    /// </summary>
    [StringProperty(MaxLength = 64, IsKey = true)]
    public string Name
    {
      get { return _name; }
      set { _name = value; }
    }

    ///<summary>
    /// The root entity of counterparties to all MasterAgreements in NettingSet
    ///</summary>
    [ManyToOneProperty(AllowNullValue = false)]
    public LegalEntity Counterparty
    {
      get { return (LegalEntity)ObjectRef.Resolve(_counterparty); }
      set { _counterparty = ObjectRef.Create(value); }
    }

    ///<summary>
    /// The root booking entity of all MasterAgreements in NettingSet
    ///</summary>
    [ManyToOneProperty(AllowNullValue = false)]
    public LegalEntity BookingEntity
    {
      get { return (LegalEntity)ObjectRef.Resolve(_bookingEntity); }
      set { _bookingEntity = ObjectRef.Create(value); }
    }

    #endregion

    #region Transient Properties
    /////<summary>
    ///// Sum of thresholds for each master agreement with haircut applied
    /////</summary>
    //public double EffectiveReceivableThreshold
    //{
    //  get
    //  {
    //    double total = 0.0;
    //    foreach (var masterAgreement in MasterAgreements)
    //    {
    //      total += masterAgreement.ReceivableThreshold * (1-masterAgreement.Haircut); 
    //    }
    //    return total; 
    //  }
    //}

    /////<summary>
    ///// Sum of thresholds for each master agreement with haircut applied
    /////</summary>
    //public double EffectivePayableThreshold
    //{
    //  get
    //  {
    //    double total = 0.0;
    //    foreach (var masterAgreement in MasterAgreements)
    //    {
    //      total += masterAgreement.PayableThreshold * (1 - masterAgreement.Haircut);
    //    }
    //    return total;
    //  }
    //}

    #endregion

    #region Methods

    /// <summary>
		/// Validate the Netting Agreement.
		/// </summary>
		/// 
		/// <param name="errors"></param>
		/// 
		public override void Validate(ArrayList errors)
		{
			// Validate base
			base.Validate(errors);
      
      if (Counterparty != null && !Counterparty.HasRole(LegalEntityRoles.Party))
        InvalidValue.AddError(errors, this, "Counterparty", "Counterparty must have LegalEntityRole 'Party'.");
      if (BookingEntity != null && !BookingEntity.HasRole(LegalEntityRoles.BookingEntity))
        InvalidValue.AddError(errors, this, "BookingEntity", "BookingEntity must have LegalEntityRole 'BookingEntity'.");
		}
		#endregion
	}

 
}
