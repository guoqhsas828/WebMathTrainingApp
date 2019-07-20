// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Xml;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// interface of data importer registry
  /// </summary>
  public interface IDataImporterRegistry
  {
    /// <summary>
    /// Get property importer
    /// </summary>
    /// <param name="propertyMetaType">proper type</param>
    /// <returns></returns>
    Func<IDataImporter, object, PropertyMeta, XmlNode, object> GetPropertyImporter(Type propertyMetaType);

    /// <summary>
    /// Register property importer
    /// </summary>
    /// <param name="propertyMetaType">property meta type</param>
    /// <param name="propertyImporter">property importer</param>
    void RegisterPropertyImporter(Type propertyMetaType, Func<IDataImporter, object, PropertyMeta, XmlNode, object> propertyImporter);

    /// <summary>
    /// Get value importer
    /// </summary>
    /// <param name="valueType">value type</param>
    /// <returns></returns>
    Func<Type, string, XmlNode, object> GetValueImporter(Type valueType);

    /// <summary>
    /// Register value importer
    /// </summary>
    /// <param name="valueType">value type</param>
    /// <param name="valueImporter">value importer</param>
    void RegisterValueImporter(Type valueType, Func<Type, string, XmlNode, object> valueImporter);
  }
}