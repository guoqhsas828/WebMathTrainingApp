using System;
using BaseEntity.Metadata;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{

  /// <summary>
  /// 
  /// </summary>
  [Serializable]
  [Entity(EntityId = 4052,
    AuditPolicy = AuditPolicy.History,
    Description = "Entity for storing a Data Measure Value",
    IsChildEntity = true,
    ChildKey = new[] { "TargetObjectId", "DataMeasureDefinition", "ObservedDate" }
    //Key = new[] { "TargetObjectId", "DataMeasureDefinition", "ObservedDate" } // 
    )]
  public class DataMeasureValue : AuditedObject
  {
 

    #region Data

    private ObjectRef _dataMeasureDefinition;
    //private ObjectRef _dataMeasureConfig;
    //private ObjectRef _targetObject;

    #endregion
    /// <summary>
    /// The object id of the persistant object.
    /// </summary>
    [NumericProperty(AllowNullValue = false)]
    public long TargetObjectId { get; set; }

    /// <summary>
    /// Ref to MeasureDefinition
    /// </summary>
    [ManyToOneProperty(AllowNullValue = false)]
    public DataMeasureDefinition DataMeasureDefinition
    {
      get { return (DataMeasureDefinition)ObjectRef.Resolve(_dataMeasureDefinition); }
      set { _dataMeasureDefinition = ObjectRef.Create(value); }
    }

    /// <summary>
    /// Observed Date of the value (should be datetime?)
    /// </summary>
    [DtProperty(AllowNullValue = false)]
    public Dt ObservedDate { get; set; }



    /// <summary>
    /// 
    /// </summary>
    [NumericProperty(AllowNullValue = true)]
    public double? NumberValue { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [StringProperty(AllowNullValue = true, MaxLength = 128)]
    public string StringValue { get; set; }

    /// <summary>
    ///  
    /// </summary>
    [DtProperty(AllowNullValue = true)]
    public Dt DtValue { get; set; }


    /// <summary>
    /// Gets the value based off the MeasureValueType
    /// </summary>
    public object Value
    {
      get
      {
        if (DataMeasureDefinition == null)
          return null;

        switch (DataMeasureDefinition.MeasureValueType)
        {
          case DataMeasureValueType.Number:
            return NumberValue;

          case DataMeasureValueType.String:
            return StringValue;

          case DataMeasureValueType.Dt:
            return DtValue;


          default:
            return null;
        }


      }
      //set
      //{
      //  if (DataMeasureDefinition == null)
      //    throw new RiskException("Unable to Set DataMeasureValue before setting the DataMeasureDefinition");

      //  switch (DataMeasureDefinition.MeasureValueType)
      //  {
      //    case DataMeasureValueType.Number:
      //      NumberValue = (double?)value;
      //      break;

      //    case DataMeasureValueType.String:
      //      StringValue = (string)value;
      //      break;

      //    case DataMeasureValueType.Dt:
      //      DtValue = (Dt)value;
      //      break;

      //  }
      //}
    }



    /// <summary>
    /// Gets the value based off the MeasureValueType
    /// </summary>
    public string GetValueAsString
    {
      get
      {
        if (DataMeasureDefinition == null)
          return string.Empty;

        switch (DataMeasureDefinition.MeasureValueType)
        {
          case DataMeasureValueType.Number:
            if (NumberValue == null)
              return string.Empty;

            return string.Format(DataMeasureDefinition.FormatString, NumberValue);

          case DataMeasureValueType.String:
            if (StringValue == null)
              return string.Empty;

            return string.Format(DataMeasureDefinition.FormatString, StringValue);

          case DataMeasureValueType.Dt:
            if (DtValue.IsEmpty())
              return string.Empty;

            return string.Format(DataMeasureDefinition.FormatString, DtValue);


          default:
            return string.Empty;
        }

      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="number"></param>
    public void SetNumberValue(double? number)
    {
      if (DataMeasureDefinition == null)
        throw new RiskException("Unable to Set DataMeasureValue before setting the DataMeasureDefinition");

      switch (DataMeasureDefinition.MeasureValueType)
      {
        case DataMeasureValueType.Number:
          NumberValue = number;
          break;

        case DataMeasureValueType.String:
          //StringValue = (string)value;
          break;

        case DataMeasureValueType.Dt:
          //DtValue = (Dt)value;
          break;

      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="str"></param>
    public void SetStringValue(string str)
    {
      if (DataMeasureDefinition == null)
        throw new RiskException("Unable to Set DataMeasureValue before setting the DataMeasureDefinition");

      switch (DataMeasureDefinition.MeasureValueType)
      {
        case DataMeasureValueType.Number:
          //NumberValue = number;
          break;

        case DataMeasureValueType.String:
          StringValue = str;
          break;

        case DataMeasureValueType.Dt:
          //DtValue = (Dt)value;
          break;

      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="date"></param>
    public void SetDtValue(Dt date)
    {
      if (DataMeasureDefinition == null)
        throw new RiskException("Unable to Set DataMeasureValue before setting the DataMeasureDefinition");

      switch (DataMeasureDefinition.MeasureValueType)
      {
        case DataMeasureValueType.Number:
          //NumberValue = number;
          break;

        case DataMeasureValueType.String:
          //StringValue = (string)value;
          break;

        case DataMeasureValueType.Dt:
          DtValue = date;
          break;

      }
    }

 

  }
}
