using System;
using System.Collections.Generic;
using System.Xml;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class DataImporterRegistry : IDataImporterRegistry
  {
    private static readonly IDictionary<Type, Func<IDataImporter, object, PropertyMeta, XmlNode, object>> _propertyImporters =
      new Dictionary<Type, Func<IDataImporter, object, PropertyMeta, XmlNode, object>>();

    private static readonly IDictionary<Type, Func<Type, string, XmlNode, object>> _valueImporters = new Dictionary<Type, Func<Type, string, XmlNode, object>>();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="propertyMetaType"></param>
    /// <returns></returns>
    public Func<IDataImporter, object, PropertyMeta, XmlNode, object> GetPropertyImporter(Type propertyMetaType)
    {
      var nonGenericType = propertyMetaType.BaseType;
      if (nonGenericType == null)
      {
        throw new MetadataException("Type [" + propertyMetaType + "] does not have a BaseType");
      }
      Func<IDataImporter, object, PropertyMeta, XmlNode, object> importer;
      if (_propertyImporters.TryGetValue(nonGenericType, out importer))
      {
        return importer;
      }
      return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="propertyMetaType"></param>
    /// <param name="propertyImporter"></param>
    public void RegisterPropertyImporter(Type propertyMetaType, Func<IDataImporter, object, PropertyMeta, XmlNode, object> propertyImporter)
    {
      if (!typeof(PropertyMeta).IsAssignableFrom(propertyMetaType))
      {
        throw new MetadataException(string.Format("Bad property meta type: {0}. Property importers can only be registered for PropertyMeta-derived types.", propertyMetaType));
      }
      _propertyImporters[propertyMetaType] = propertyImporter;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="valueType"></param>
    /// <returns></returns>
    public Func<Type, string, XmlNode, object> GetValueImporter(Type valueType)
    {
      Func<Type, string, XmlNode, object> importer;
      if (_valueImporters.TryGetValue(valueType, out importer))
      {
        return importer;
      }
      return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="valueType"></param>
    /// <param name="valueImporter"></param>
    public void RegisterValueImporter(Type valueType, Func<Type, string, XmlNode, object> valueImporter)
    {
      _valueImporters[valueType] = valueImporter;
    }
  }
}
