using System;
using BaseEntity.Metadata;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  [Entity(EntityId = 1008, PropertyMapping = PropertyMappingStrategy.Hybrid, DisplayName="Generic", Description="Generic Product")]
  public class GenericProduct : Product
  {

    #region Persistent Properties

    /// <summary>
    /// 
    /// </summary>
    [ManyToOneProperty(AllowNullValue = false)]
    public GenericSubType ProductSubType
    {
      get { return (GenericSubType)ObjectRef.Resolve(_productSubType); }
      set { _productSubType = ObjectRef.Create(value); }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    /// <returns></returns>
    public override Dt CalcSettle(Dt asOf)
    {
      return Dt.AddDays(asOf, DaysToSettle, Calendar.None);
    }

    #endregion

    #region Data

    private ObjectRef _productSubType;

    #endregion
  }
}