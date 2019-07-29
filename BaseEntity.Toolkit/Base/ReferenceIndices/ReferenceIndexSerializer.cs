//
//   2017. All rights reserved.
//
using System;
using System.Runtime.Serialization;
using System.Xml;
using BaseEntity.Toolkit.Base.Serialization;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Base.ReferenceIndices
{
  /// <summary>
  /// Custom serializer for <see cref="ReferenceIndex"/>
  /// </summary>
  /// <remarks>
  /// </remarks>
  internal class ReferenceIndexSerializer : ISimpleXmlSerializer
  {
    #region  Methods

    /// <summary>
    /// Register ReferenceRate custom serializer
    /// </summary>
    internal static void Register()
    {
      CustomSerializers.Register(new ReferenceIndexSerializer());
    }

    #endregion Methods

    #region Backward Compatible Class 

    /// <summary>
    /// Old Reference Index
    /// </summary>
    /// <remarks>
    /// <para>This class contains the data to represent a published index</para>
    /// </remarks>
    [Serializable]
    private abstract class ReferenceIndexOrig
    {
      public ReferenceIndexOrig(ReferenceIndex obj)
      {
        Name = obj.Name;
        IndexName = obj.IndexName;
        IndexTenor = obj.IndexTenor;
        Currency = obj.Currency;
        Calendar = obj.Calendar;
        Roll = obj.Roll;
        DayCount = obj.DayCount;
        SettlementDays = obj.SettlementDays;
        HistoricalObservations = obj.HistoricalObservations;
        PublicationFrequency = obj.PublicationFrequency;
        ResetDateRule = obj.ResetDateRule;
      }

      public string Name { get; set; }
      public string IndexName { get; set; }
      public Tenor IndexTenor { get; set; }
      public Currency Currency { get; set; }
      public Calendar Calendar { get; set; }
      public BDConvention Roll { get; set; }
      public DayCount DayCount { get; set; }
      public int SettlementDays { get; set; }
      public RateResets HistoricalObservations { get; set; }
      public Frequency PublicationFrequency { get; set; }
      public CycleRule ResetDateRule { get; set; }
      public abstract ReferenceIndex NewObject();
    }

    /// <summary>
    /// Interest rate index
    /// </summary>
    [Serializable]
    private class InterestRateIndexOrig : ReferenceIndexOrig
    {
      public InterestRateIndexOrig(InterestRateIndex obj)
        : base(obj)
      { }
      public override ReferenceIndex NewObject()
      {
        return new InterestRateIndex(IndexName, IndexTenor.ToFrequency(), Currency, DayCount, Calendar, SettlementDays)
        {
          HistoricalObservations = HistoricalObservations
        };
      }
    }

    /// <summary>
    /// Swap rate index, i.e constant maturity swap index
    /// </summary>
    [Serializable]
    private class SwapRateIndexOrig : ReferenceIndexOrig
    {
      public SwapRateIndexOrig(SwapRateIndex obj)
        : base(obj)
      {
        ForwardRateIndex = new InterestRateIndexOrig(obj.ForwardRateIndex);
        IndexFrequency = obj.IndexFrequency;
      }
      public InterestRateIndexOrig ForwardRateIndex { get; set; }
      public Frequency IndexFrequency { get; set; }
      public override ReferenceIndex NewObject()
      {
        return new SwapRateIndex(IndexName, IndexTenor, IndexFrequency, Currency , DayCount,
        Calendar, Roll, SettlementDays, ForwardRateIndex.NewObject() as InterestRateIndex)
        {
          HistoricalObservations = HistoricalObservations
        };
      }
    }

    /// <summary>
    /// Inflation index
    /// </summary>
    [Serializable]
    private class InflationIndexOrig : ReferenceIndexOrig
    {
      public InflationIndexOrig(InflationIndex obj)
        : base(obj)
      {
        PublicationLag = obj.PublicationLag;
      }
      public Tenor PublicationLag { get; set; }
      public override ReferenceIndex NewObject()
      {
        return new InflationIndex(IndexName, IndexTenor, Currency, DayCount, Calendar, Roll, PublicationFrequency, PublicationLag)
        {
          HistoricalObservations = HistoricalObservations
        };
      }
    }

    /// <summary>
    /// Forward yield index
    /// </summary>
    [Serializable]
    private class ForwardYieldIndexOrig : ReferenceIndexOrig
    {
      public ForwardYieldIndexOrig(ForwardYieldIndex obj)
        : base(obj)
      {
        IndexFrequency = obj.IndexFrequency;
      }
      public Frequency IndexFrequency { get; set; }
      public override ReferenceIndex NewObject()
      {
        return new ForwardYieldIndex(IndexName, IndexTenor, IndexFrequency, Currency, DayCount, Calendar, Roll, SettlementDays)
        {
          HistoricalObservations = HistoricalObservations
        };
      }
    }

    /// <summary>
    /// Commodity Price Index
    /// </summary>
    [Serializable]
    private class CommodityPriceIndexOrig : ReferenceIndexOrig
    {
      public CommodityPriceIndexOrig(CommodityPriceIndex obj)
        : base(obj)
      { }
      public QuantityFrequency QuantityFrequency { get; set; }
      public override ReferenceIndex NewObject()
      {
        return new CommodityPriceIndex(IndexName, Currency, DayCount, Calendar, Roll, SettlementDays, PublicationFrequency)
        {
          HistoricalObservations = HistoricalObservations
        };
      }
    }

    /// <summary>
    /// Equity Price Index
    /// </summary>
    [Serializable]
    private class EquityPriceIndexOrig : ReferenceIndexOrig
    {
      public EquityPriceIndexOrig(EquityPriceIndex obj)
        : base(obj)
      { }
      public QuantityFrequency QuantityFrequency { get; set; }
      public override ReferenceIndex NewObject()
      {
        return new  EquityPriceIndex(IndexName, Currency, DayCount, Calendar, Roll, SettlementDays)
        {
          HistoricalObservations = HistoricalObservations
        };
      }
    }

    /// <summary>
    /// FX Price Index
    /// </summary>
    [Serializable]
    private class FxRateIndexOrig : ReferenceIndexOrig
    {
      public FxRateIndexOrig(FxRateIndex obj)
        : base(obj)
      {
        ForeignCurrency = obj.ForeignCurrency;
        QuoteCalendar = obj.QuoteCalendar;
        DaysToSpot = obj.DaysToSpot;
      }
      public Currency ForeignCurrency { get; private set; }
      public Calendar QuoteCalendar { get; set; }
      public int DaysToSpot { get; set; }
      public override ReferenceIndex NewObject()
      {
        return new FxRateIndex(IndexName, ForeignCurrency, Currency, Calendar, QuoteCalendar, DaysToSpot)
        {
          HistoricalObservations = HistoricalObservations
        };
      }
    }

    #endregion Backward Compatible Class 

    #region ISimpleXmlSerializer members

    /// <summary>
    /// Determines whether this serializer can handle the specified type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><c>true</c> if the specified type is <see cref="ReferenceIndex"/></returns>
    public bool CanHandle(Type type)
    {
      return type.IsSubclassOf(typeof(ReferenceIndex)) || type == typeof(ReferenceIndex);
    }

    /// <summary>
    /// Deserializes the object value from the XML stream
    /// </summary>
    /// <param name="reader">The XML reader</param>
    /// <param name="settings">The serialization settings</param>
    /// <param name="type">The target type</param>
    /// <returns>System.Object.</returns>
    public object ReadValue(XmlReader reader, SimpleXmlSerializer settings, Type type)
    {
      if (reader.IsEmptyElement)
      {
        reader.Skip();
        return null;
      }
      Type oldType;
      if (type == typeof(InterestRateIndex))
        oldType = typeof(InterestRateIndexOrig);
      else if (type == typeof(SwapRateIndex))
        oldType = typeof(SwapRateIndexOrig);
      else if (type == typeof(InflationIndex))
        oldType = typeof(InflationIndexOrig);
      else if (type == typeof(ForwardYieldIndex))
        oldType = typeof(ForwardYieldIndexOrig);
      else if (type == typeof(CommodityPriceIndex))
        oldType = typeof(CommodityPriceIndexOrig);
      else if (type == typeof(EquityPriceIndex))
        oldType = typeof(EquityPriceIndexOrig);
      else if (type == typeof(FxRateIndex))
        oldType = typeof(FxRateIndexOrig);
      else throw new Exception($"Internal error - unsupported ReferenceIndex type {type} in serialisation");
      var oldObj = ReadObjectGraph(reader, settings, oldType) as ReferenceIndexOrig;
      if( oldObj == null )
         throw new Exception($"Internal error - unable to parse ReferenceIndex_1 from type {type} in serialisation");
      return oldObj.NewObject();
    }

    /// <summary>
    /// Serializes the object data to the XML writer.
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <param name="writer">The XML writer</param>
    /// <param name="settings">The serialization settings</param>
    /// <param name="data">The data to serialize</param>
    public void WriteValue(XmlWriter writer, SimpleXmlSerializer settings, object data)
    {
      ReferenceIndexOrig oldObject;
      if (data.GetType() == typeof(InterestRateIndex))
        oldObject = new InterestRateIndexOrig(data as InterestRateIndex);
      else if (data.GetType() == typeof(SwapRateIndex))
        oldObject = new SwapRateIndexOrig(data as SwapRateIndex);
      else if (data.GetType() == typeof(InflationIndex))
        oldObject = new InflationIndexOrig(data as InflationIndex);
      else if (data.GetType() == typeof(ForwardYieldIndex))
        oldObject = new ForwardYieldIndexOrig(data as ForwardYieldIndex);
      else if (data.GetType() == typeof(CommodityPriceIndex))
        oldObject = new CommodityPriceIndexOrig(data as CommodityPriceIndex);
      else if (data.GetType() == typeof(EquityPriceIndex))
        oldObject = new EquityPriceIndexOrig(data as EquityPriceIndex);
      else if (data.GetType() == typeof(FxRateIndex))
        oldObject = new FxRateIndexOrig(data as FxRateIndex);
      else throw new Exception($"Internal error - unsupported ReferenceIndex type {data.GetType()} in serialisation");
      WriteObjectGraph(writer, settings, oldObject);
    }

    #endregion

    #region Helpers

    private static object ReadObjectGraph(XmlReader reader, SimpleXmlSerializer settings, Type type)
    {
      var fieldsInfo = settings.GetSerializationInfo(type);
      var obj = FormatterServices.GetUninitializedObject(type);
      return fieldsInfo.ReadValue(reader, settings, obj);
    }

    private static void WriteObjectGraph(XmlWriter writer, SimpleXmlSerializer settings, object data)
    {
      var fieldsInfo = settings.GetSerializationInfo(data.GetType());
      fieldsInfo.WriteValue(writer, settings, data);
    }

    #endregion
  }
}
