// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Xml;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Class of DataExporterRegister
  /// </summary>
  public class DataExporterRegistry : IDataExporterRegistry
  {
    private static readonly IDictionary<Type, Action<PropertyMeta, IBaseEntityObject, XmlNode>> PropertyExporters =
      new Dictionary<Type, Action<PropertyMeta, IBaseEntityObject, XmlNode>>();

    private static readonly IDictionary<Type, Action<string, object, XmlNode>> ValueExporters = new Dictionary<Type, Action<string, object, XmlNode>>();

    /// <summary>
    /// GetPropertyExporter
    /// </summary>
    /// <param name="propertyMetaType">property meta type</param>
    /// <returns></returns>
    public Action<PropertyMeta, IBaseEntityObject, XmlNode> GetPropertyExporter(Type propertyMetaType)
    {
      var nonGenericType = propertyMetaType.BaseType;
      if (nonGenericType == null)
      {
        throw new MetadataException("Type [" + propertyMetaType + "] does not have a BaseType");
      }
      Action<PropertyMeta, IBaseEntityObject, XmlNode> exporter;
      if (PropertyExporters.TryGetValue(nonGenericType, out exporter))
      {
        return exporter;
      }
      return null;
    }

    /// <summary>
    /// Register Property exporter
    /// </summary>
    /// <param name="propertyMetaType">property meta type</param>
    /// <param name="propertyExporter">property exporter</param>
    public void RegisterPropertyExporter(Type propertyMetaType, Action<PropertyMeta, IBaseEntityObject, XmlNode> propertyExporter)
    {
      if (!typeof(PropertyMeta).IsAssignableFrom(propertyMetaType))
      {
        throw new MetadataException(string.Format("Bad property meta type: {0}. Property exporters can only be registered for PropertyMeta-derived types.",
          propertyMetaType));
      }
      PropertyExporters[propertyMetaType] = propertyExporter;
    }

    /// <summary>
    /// Get value exporter
    /// </summary>
    /// <param name="valueType">value type</param>
    /// <returns></returns>
    public Action<string, object, XmlNode> GetValueExporter(Type valueType)
    {
      Action<string, object, XmlNode> exporter;
      if (ValueExporters.TryGetValue(valueType, out exporter))
      {
        return exporter;
      }
      return null;
    }

    /// <summary>
    /// Register value exporter
    /// </summary>
    /// <param name="valueType">value type</param>
    /// <param name="valueExporter">value exporter</param>
    public void RegisterValueExporter(Type valueType, Action<string, object, XmlNode> valueExporter)
    {
      ValueExporters[valueType] = valueExporter;
    }

    private static bool skipAuditInfo_;

    /// <summary>
    /// Bool type. SkipAuditInfo
    /// </summary>
    public bool SkipAuditInfo  // read-write instance property
    {
      get
      {
        return skipAuditInfo_;
      }
      set
      {
        skipAuditInfo_ = value;
      }
    }

  }
}