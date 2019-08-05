using System;
using System.Collections.Generic;
using BaseEntity.Metadata;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  [Entity(EntityId = 4051,
    AuditPolicy = AuditPolicy.History,
    IsChildEntity = true,
    Description = "Entity for describing the a Data Measure Value",
    ChildKey = new[] { "Name", "DataMeasureConfig" }
    )]
  public class DataMeasureDefinition : AuditedObject
  {
    private ObjectRef _dataMeasureConfig;
    private IList<DataMeasureValue> _dataMeasureValues;
    /// <summary>
    /// ctor
    /// </summary>
    public DataMeasureDefinition()
    {
      FormatString = "{0}";
    }


    /// <summary>
    /// Name of the Data Measure
    /// </summary>
    [StringProperty(MaxLength = 64, AllowNullValue = false)]
    public string Name { get; set; }

    /// <summary>
    /// Description of the Data Measure
    /// </summary>
    [StringProperty(MaxLength = 512)]
    public string Description { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [ManyToOneProperty(AllowNullValue = false)]
    public DataMeasureConfig DataMeasureConfig
    {
      get { return (DataMeasureConfig)ObjectRef.Resolve(_dataMeasureConfig); }
      set { _dataMeasureConfig = ObjectRef.Create(value); }
    }

    /// <summary>
    /// Number of days a piece of data is valid from its observation date
    /// </summary>
    [NumericProperty(AllowNullValue = true)]
    public int? DaysValid { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringProperty(MaxLength = 32, AllowNullValue = false)]
    public string FormatString { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [EnumProperty(AllowNullValue = false)]
    public DataMeasureValueType MeasureValueType { get; set; }


 

    /// <summary>
    /// 
    /// </summary>
    [OneToManyProperty(Clazz = typeof(DataMeasureValue), // Child must inherit from PersistentObject
      CollectionType = "bag", // "list",
      //IndexColumn = "ObservationDate",
      IsInverse = true, // Indicates that this is a bidirectional association 
      Adder = "AddDataMeasureValue", // Helper method to add Child to list and set Parent property
      Remover = "RemoveDataMeasureValue", // Helper method to remove Child from list and unset Parent property
      Cascade = "all-delete-orphan"
      )]
    public IList<DataMeasureValue> DataMeasureValues
    {
      get { return _dataMeasureValues ?? (_dataMeasureValues = new List<DataMeasureValue>()); }
      set { _dataMeasureValues = value; }
    } 

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dataMeasureValue"></param>
    public void AddDataMeasureValue(DataMeasureValue dataMeasureValue)
    {
      dataMeasureValue.DataMeasureDefinition = this;
      DataMeasureValues.Add(dataMeasureValue);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dataMeasureValue"></param>
    public void RemoveDataMeasureValue(DataMeasureValue dataMeasureValue)
    {
      DataMeasureValues.Remove(dataMeasureValue);
      dataMeasureValue.DataMeasureDefinition = null;
    }

  }
}