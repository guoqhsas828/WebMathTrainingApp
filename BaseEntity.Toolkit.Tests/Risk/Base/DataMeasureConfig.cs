using System;
using System.Collections.Generic;
using BaseEntity.Metadata;

namespace BaseEntity.Risk
{
  /// <summary>
  /// A Measure Config is unique to the target entity type
  /// </summary>
  [Serializable]
  [Entity(EntityId = 4050,
  AuditPolicy = AuditPolicy.History,
  Description = "Entity for describing the a",
  Key = new[] { "Name", "TargetType" }
  )]
  public class DataMeasureConfig : AuditedObject
  {
    /// <summary>
    /// Name
    /// </summary>
    [StringProperty(MaxLength = 64,   AllowNullValue = false)]
    public string Name { get; set; }

    /// <summary>
    /// Target type, like BaseEntity.Risk.Product.CashCDO
    /// </summary>
    [StringProperty(MaxLength = 256,   AllowNullValue = false)]
    public string TargetType { get; set; }

    /// <summary>
    /// Description of the Data Measure 
    /// </summary>
    [StringProperty(MaxLength = 512, AllowNullValue = true)]
    public string Description { get; set; }

    /// <summary>
    /// Indicates if this data from this config should be loaded into datamart
    /// this might not be the case for all data configured.
    /// </summary>
    public bool IncludeInDataMart { get { return true; } }


    /// <summary>
    /// Measure Definitions
    /// </summary>
    [OneToManyProperty(Clazz = typeof(DataMeasureDefinition), // Child must inherit from PersistentObject
      CollectionType = "bag",  
      IsInverse = true, // Indicates that this is a bidirectional association
      Adder = "AddDataMeasureDefinition", // Helper method to add Child to list and set Parent property
      Remover = "RemoveDataMeasureDefinition", // Helper method to remove Child from list and unset Parent property
      Cascade = "all-delete-orphan")]
    public IList<DataMeasureDefinition> DataMeasureDefinitions
    {
      get { return _dataMeasureDefinitions ?? (_dataMeasureDefinitions = new List<DataMeasureDefinition>()); }
      set { _dataMeasureDefinitions = value; }
    }

    private IList<DataMeasureDefinition> _dataMeasureDefinitions;


    /// <summary>
    /// Remove Data Measure Definition
    /// </summary>
    /// <param name="dataMeasureDefinition"></param>
    public void AddDataMeasureDefinition(DataMeasureDefinition dataMeasureDefinition)
    {
      dataMeasureDefinition.DataMeasureConfig = this;
      DataMeasureDefinitions.Add(dataMeasureDefinition);
    }


    /// <summary>
    /// Remove Data Measure Definition
    /// </summary>
    /// <param name="dataMeasureDefinition"></param>
    public void RemoveDataMeasureDefinition(DataMeasureDefinition dataMeasureDefinition)
    {
      DataMeasureDefinitions.Remove(dataMeasureDefinition);
      dataMeasureDefinition.DataMeasureConfig = null;
    }

    

  }
}
