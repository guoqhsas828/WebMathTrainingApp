/*
 * MasterAgreement.cs
 *
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Linq;
using BaseEntity.Core.Logging;
using BaseEntity.Metadata;
using BaseEntity.Risk.Base;
using BaseEntity.Shared;
using log4net;

namespace BaseEntity.Risk
{
  /// <summary>
  /// defines a Master Agreement for a single Counterparty.
  /// </summary>
  [Serializable]
  [Entity(EntityId = 124, Description = "Master Agreement for counterparty risk calculations", AuditPolicy = AuditPolicy.History, PropertyMapping = PropertyMappingStrategy.Hybrid)]
  public class MasterAgreement: AuditedObject
  {
    #region Data
    //logger
    private static ILog Log = QLogManager.GetLogger(typeof(MasterAgreement));

    //data
    private ObjectRef counterparty_;
    private ObjectRef bookingEntity_;

    private ObjectRef collateralAgreement_;
   
    private IList<string> productTypes_;
  
    private ObjectRef nettingAgreement_; 
    private ObjectRef masterAgreementType_;
    
    private string name_; 
    #endregion


    #region Properties

    /// <summary>
    ///  unique name
    /// </summary>
    [StringProperty(MaxLength = 64, IsKey = true)]
    public string Name
    {
      get { return name_; }
      set { name_ = value; }
    }

    ///<summary>
    /// The Counterparty to this agreement
    ///</summary>
    [ManyToOneProperty(AllowNullValue = false)]
    public LegalEntity Counterparty
    {
      get { return (LegalEntity)ObjectRef.Resolve(counterparty_); }
      set { counterparty_ = ObjectRef.Create(value); }
    }

    ///<summary>
    /// The Booking Entity for this agreement
    ///</summary>
    [ManyToOneProperty(AllowNullValue = false)]
    public LegalEntity BookingEntity
    {
      get { return (LegalEntity)ObjectRef.Resolve(bookingEntity_); }
      set { bookingEntity_ = ObjectRef.Create(value); }
    }

    /// <summary>
    /// The Collateral Agreement for this agreement
    /// </summary>
    [ManyToOneProperty(AllowNullValue = true)]
    public CollateralAgreement CollateralAgreement
    {
      get { return (CollateralAgreement)ObjectRef.Resolve(collateralAgreement_); }
      set { collateralAgreement_ = ObjectRef.Create(value); }
    }

    /// <summary>
    ///   Filter criteria. Use this MasterAgreement for trades whose product.GetType().Name is in this list
    /// </summary>
    [ElementCollectionProperty(CollectionType = "list", ElementColumn = "ProductType", ElementType = typeof(string), ElementMaxLength = 40)]
    [Browsable(false)]
    public IList<string> ProductTypes
    {
      get
      {
        if (productTypes_ == null)
          productTypes_ = new List<string>();
        return productTypes_;
      }
      set { productTypes_ = value; }
    }


    ///<summary>
    /// Reference to parent Netting Agreement
    ///</summary>
    [ManyToOneProperty(AllowNullValue = true)]
    [Browsable(false)]
    public NettingAgreement NettingAgreement
    {
      get { return (NettingAgreement)ObjectRef.Resolve(nettingAgreement_); }
      set { nettingAgreement_ = ObjectRef.Create(value); }
    }

    ///<summary>
    /// agreement type
    ///</summary>
    [ManyToOneProperty]
    public MasterAgreementType MasterAgreementType
    {
      get { return (MasterAgreementType)ObjectRef.Resolve(masterAgreementType_); }
      set { masterAgreementType_ = ObjectRef.Create(value); }
    }

    #endregion

    /// <summary>
    /// 
    /// </summary>
    public long CollateralAgreementId => collateralAgreement_ == null || collateralAgreement_.IsNull ? 0 : collateralAgreement_.Id;

    #region Methods

    /// <summary>
    /// Returns counterparty name followed by comma delimited list of product types
    /// </summary>
    public override string ToString()
    {
      var sb = new StringBuilder(Name);
      
      if(ProductTypes.Count > 0)
      {
        sb.Append(" [");
        foreach (string productType in ProductTypes)
        {
          sb.AppendFormat("{0},", productType); 
        }
        sb.Replace(',',']',sb.Length-1,1);
      }

      return sb.ToString();
    }


    /// <summary>
    /// Clone
    /// </summary>
    public override object Clone()
    {
      MasterAgreement other = (MasterAgreement)base.Clone();
      other.NettingAgreement = NettingAgreement;
      other.MasterAgreementType = MasterAgreementType;
      other.Counterparty = Counterparty;
      other.CollateralAgreement = CollateralAgreement;

      other.ProductTypes = new List<string>(); 
      foreach (string productType in ProductTypes)
      {
        other.ProductTypes.Add(productType);
      }

      return other; 
    }

    /// <summary>
    /// Validate the Collateral Agreement.
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

      if (CollateralAgreement != null) CollateralAgreement.Validate();
    }

    #endregion
	}

 
}
