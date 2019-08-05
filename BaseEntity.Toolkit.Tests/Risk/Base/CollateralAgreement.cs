using System;
using BaseEntity.Metadata;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Models.Simulations;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  [Entity(Name = "CollateralAgreement",
    TableName = "CollateralAgreement",
    SubclassMapping = SubclassMappingStrategy.TablePerSubclass,
    AuditPolicy = AuditPolicy.History)]
  public abstract class CollateralAgreement : AuditedObject
  {

    #region Constructors

    /// <summary>
    /// Default Constructor
    /// </summary>
    protected CollateralAgreement()
    {
      _currency = Currency.USD;
    }

    #endregion

    #region Persistent Properties

    /// <summary>
    /// 
    /// </summary>
    [EnumProperty]
    public Currency Currency
    {
      get { return _currency; }
      set { _currency = value; }
    }

    /// <summary>
    /// 
    /// </summary>
    [StringProperty(MaxLength = 64, IsKey = true)]
    public string Name { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ma"></param>
    /// <param name="asOf"></param>
    /// <param name="marketEnv"></param>
    /// <returns></returns>
    public abstract ICollateralMap FindCollateralMap(MasterAgreement ma, Dt asOf, MarketEnvironment marketEnv);

    /// <summary>
    /// Method allows implementations to restrict types of products that are supported
    /// </summary>
    /// <param name="productType">simple Type.Name</param>
    public virtual bool IsProductTypeSupported(string productType)
    {
      return true;
    }

    #endregion

    #region Data

    private Currency _currency;

    #endregion
  }
}