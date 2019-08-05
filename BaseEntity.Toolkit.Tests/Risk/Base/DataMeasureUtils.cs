using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Database;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  /// <summary>
  /// 
  /// </summary>
  public static class DataMeasureUtils
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="persistentObject"></param>
    /// <returns></returns>
    public static List<DataMeasureConfig> GetMeasureConfigs(this PersistentObject persistentObject)
    {
      var allTypes = persistentObject.GetInheritanceHierarchy().Select(t => t.ToString()).ToArray();


      return Session.Linq<DataMeasureConfig>().Where(c => allTypes.Contains(c.TargetType)).ToList();

    }


    private static IEnumerable<Type> GetInheritanceHierarchy(this PersistentObject persistentObject)
    {
      for (var current = persistentObject.GetType(); current != null && current != typeof(BaseEntityObject); current = current.BaseType)
      {
        yield return current;
      }

    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="config"></param>
    /// <param name="persistentObject"></param>
    /// <returns></returns>
    public static bool ValidateObjectType(this DataMeasureConfig config, PersistentObject persistentObject)
    {
      var allTypes = persistentObject.GetInheritanceHierarchy().Select(t => t.ToString()).ToArray();


      return allTypes.Any(s => s == config.TargetType);



    }

    ///// <summary>
    ///// 
    ///// </summary>
    ///// <param name="persistentObject"></param>
    ///// <param name="configName"></param>
    ///// <param name="measureName"></param>
    ///// <param name="value"></param>
    ///// <param name="validFrom"></param>
    //public static void AddMeasureValue(this PersistentObject persistentObject, string configName, string measureName, object value, DateTime validFrom)
    //{
    //  throw new NotImplementedException();
    //}

    ///// <summary>
    ///// 
    ///// </summary>
    ///// <param name="persistentObject"></param>
    ///// <param name="config"></param>
    ///// <param name="measureDefinition"></param>
    ///// <param name="value"></param>
    ///// <param name="observedDate"></param>
    //public static void AddMeasureValue(this PersistentObject persistentObject, DataMeasureConfig config, DataMeasureDefinition measureDefinition, object value, DateTime observedDate)
    //{



    //  var dmv = GetByObservedDate(persistentObject, config, measureDefinition, observedDate);
    //  if (dmv != null)
    //  {
    //    throw new RiskException(string.Format("A value already exists for {0} [{1}] [{2}] [{3}]", config.TargetType, persistentObject.ObjectId,
    //      measureDefinition.Name, observedDate.ToShortDateString()));
    //  }



    //  throw new NotImplementedException();


    //}


    /// <summary>
    /// Will add or update a DataMeasureValue
    /// </summary>
    /// <param name="persistentObject"></param>
    /// <param name="config"></param>
    /// <param name="measureDefinition"></param>
    /// <param name="value"></param>
    /// <param name="observedDate"></param>
    public static DataMeasureValue SetMeasureValue(this PersistentObject persistentObject, DataMeasureConfig config, DataMeasureDefinition measureDefinition, object value, Dt observedDate)
    {
      if (config == null)
        throw new ArgumentNullException(nameof(config));

      if (measureDefinition == null)
        throw new ArgumentNullException(nameof(measureDefinition));

      if (!config.ValidateObjectType(persistentObject))
        throw new RiskException(string.Format("PersistentObject[{0}] is not valid for DataMeasureConfig[{1}][{2}]", persistentObject.GetType(), config.Name, config.TargetType));

      if (persistentObject.IsNewObject())
        throw new RiskException("Unable to used an Un-Saved PersistentObject object, as it wont have a proper object id");

      config.RequestUpdate();

      var dmv = GetByObservedDate(persistentObject, config, measureDefinition, observedDate);
      if (dmv == null)
      {
        dmv = new DataMeasureValue()
        {
          TargetObjectId = persistentObject.ObjectId,
          DataMeasureDefinition = measureDefinition,
          ObservedDate = observedDate,
        };

        measureDefinition.DataMeasureValues.Add(dmv);
      }


      switch (measureDefinition.MeasureValueType)
      {
        case DataMeasureValueType.Number:
          if (value == null)
            dmv.SetNumberValue(null);
          else
            dmv.SetNumberValue((double)value);
          break;

        case DataMeasureValueType.String:
          dmv.SetStringValue((string)value);
          break;

        case DataMeasureValueType.Dt:
          dmv.SetDtValue((Dt)value);
          break;

      }



      //Session.CommitTransaction();


      return dmv;
    }


    /// <summary>
    /// Will look for a value at a given ObservedDate date 
    /// Will return null if there is no value record at that date
    /// </summary>
    /// <param name="persistentObject"></param>
    /// <param name="config"></param>
    /// <param name="measureDefinition"></param>
    /// <param name="observedDate"></param>
    public static DataMeasureValue GetByObservedDate(this PersistentObject persistentObject, DataMeasureConfig config, DataMeasureDefinition measureDefinition, Dt observedDate)
    {



      var dmv = Session.Linq<DataMeasureValue>()
                                              .FirstOrDefault(mv => mv.DataMeasureDefinition == measureDefinition
                                                           && mv.TargetObjectId == persistentObject.ObjectId
                                                           && mv.ObservedDate == observedDate);
      return dmv;


    }


    /// <summary>
    /// Will look a value that is valid for the provided date.
    /// validity is based off  the closest value by observed date, that also does not exceed the defined DaysValid.
    /// </summary>
    /// <param name="persistentObject"></param>
    /// <param name="config"></param>
    /// <param name="measureDefinition"></param>
    /// <param name="date"></param>
    /// <returns></returns>
    public static DataMeasureValue GetByDate(this PersistentObject persistentObject, DataMeasureConfig config, DataMeasureDefinition measureDefinition, DateTime date)
    {
      var maxDate = new Dt(date);
      var minDate = ((measureDefinition.DaysValid.HasValue) ? new Dt(date.AddDays((measureDefinition.DaysValid.Value * -1))) : Dt.Empty);

      var v = Session.Linq<DataMeasureValue>().Where(mv => mv.DataMeasureDefinition == measureDefinition
                                                           && mv.TargetObjectId == persistentObject.ObjectId
                                                           && mv.ObservedDate <= maxDate
                                                           && mv.ObservedDate >= minDate)
                                              .OrderByDescending(mv => mv.ObservedDate)
                                              .FirstOrDefault();

      return v;
    }







    /// <summary>
    /// Get all the valid values for each defined measure in the config for the object.
    /// </summary>
    /// <param name="persistentObject"></param>
    /// <param name="config"></param>
    /// <param name="asOf"></param>
    /// <returns></returns>
    public static IDictionary<string, DataMeasureValue> GetAllByDate(this PersistentObject persistentObject, DataMeasureConfig config, Dt asOf)
    {
      var result = new Dictionary<string, DataMeasureValue>();


      //  var map = new Dictionary<string, IEnumerable<DataMeasureValue>>();

      foreach (var measure in config.DataMeasureDefinitions)
      {
        var maxDate = asOf;
        var minDate = (measure.DaysValid.HasValue) ? Dt.Add(asOf, (measure.DaysValid.Value * -1)) : Dt.Empty;

        var q = Session.Linq<DataMeasureValue>().Where(mv => mv.DataMeasureDefinition == measure
                                                             && mv.TargetObjectId == persistentObject.ObjectId
                                                             && mv.ObservedDate <= maxDate
                                                             && mv.ObservedDate >= minDate)
                                                            .OrderByDescending(mv => mv.ObservedDate)
                                                            .FirstOrDefault();
        //.Take(1)
        //.ToFuture(); // not supported
        //map.Add(measure.Name, q);

        result.Add(measure.Name, q);
      }


      return result;
    }



    private static bool IsValidValue(DataMeasureValue dmv, Dt asOf)
    {
      if (asOf.IsEmpty())
        return false;

      var minDate = asOf;
      var maxDate = (dmv.DataMeasureDefinition.DaysValid.HasValue) ? Dt.Add(asOf, (dmv.DataMeasureDefinition.DaysValid.Value)) : Dt.MaxValue;

      return dmv.ObservedDate <= minDate && dmv.ObservedDate <= maxDate;
    }

    private static Type GetDataColumnType(DataMeasureDefinition def)
    {

      switch (def.MeasureValueType)
      {
        case DataMeasureValueType.Number:
          return typeof(double);
        case DataMeasureValueType.Dt:
          return typeof(Dt);
        case DataMeasureValueType.String:
          return typeof(string);

      }

      throw new NotImplementedException(string.Format("Unknown DataMeasureValueType to Type conversion for [{0}]", def.MeasureValueType));
    }



    /// <summary>
    /// 
    /// </summary>
    /// <param name="persistentObject"></param>
    /// <param name="config"></param>
    /// <param name="startDt"></param>
    /// <param name="endDt"></param>
    /// <returns></returns>
    public static DataTable GetAllByDateRange(this PersistentObject persistentObject, DataMeasureConfig config, Dt startDt, Dt endDt)
    {
      var tbl = new DataTable();
      tbl.Columns.Add(new DataColumn() { ColumnName = "Date", DataType = typeof(Dt) });

      var map = new Dictionary<DataMeasureDefinition, IEnumerable<DataMeasureValue>>();
      var dates = new SortedSet<Dt>();

      foreach (var measure in config.DataMeasureDefinitions)
      {

        tbl.Columns.Add(new DataColumn() { ColumnName = measure.Name, DataType = GetDataColumnType(measure) });
 
        var maxDate = endDt;
        var minDate = (measure.DaysValid.HasValue) ? Dt.Add(startDt, (measure.DaysValid.Value * -1)) : Dt.Empty;

        var query = Session.Linq<DataMeasureValue>().Where(mv => mv.DataMeasureDefinition == measure
                                                             && mv.TargetObjectId == persistentObject.ObjectId
                                                             && mv.ObservedDate <= maxDate
                                                             && mv.ObservedDate >= minDate)
                                                            .OrderByDescending(mv => mv.ObservedDate)
                                                            .ToList();
        //.Take(1)
        //.ToFuture(); // not supported
        //map.Add(measure.Name, q);

        foreach (var dmv in query)
        {
          if (!dates.Contains(dmv.ObservedDate)) dates.Add(dmv.ObservedDate);
        }

        map.Add(measure, query);
      }


      foreach (var date in dates)
      {
        var row = tbl.NewRow();
        row["Date"] = date;
        foreach (var kvp in map)
        {
          var val = kvp.Value.FirstOrDefault(v => IsValidValue(v, date));
          if (val != null && val.Value != null)
            row[kvp.Key.Name] = val.Value;
        }

        tbl.Rows.Add(row);
      }



      return tbl;
    }

  }


}
