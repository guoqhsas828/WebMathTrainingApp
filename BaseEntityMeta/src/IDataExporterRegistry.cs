using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Interface of data exporter registry
  /// </summary>
  public interface IDataExporterRegistry
  {
    /// <summary>
    /// GetPropertyExporter
    /// </summary>
    /// <param name="propertyMetaType">property meta type</param>
    /// <returns></returns>
    Action<PropertyMeta, IBaseEntityObject, XmlNode> GetPropertyExporter(Type propertyMetaType);

    /// <summary>
    /// Register property exporter
    /// </summary>
    /// <param name="propertyMetaType">property meta type</param>
    /// <param name="propertyExporter">property exporter</param>
    void RegisterPropertyExporter(Type propertyMetaType, Action<PropertyMeta, IBaseEntityObject, XmlNode> propertyExporter);

    /// <summary>
    /// GetValueExporter
    /// </summary>
    /// <param name="valueType">value type</param>
    /// <returns></returns>
    Action<string, object, XmlNode> GetValueExporter(Type valueType);

    /// <summary>
    /// RegisterValueExporter
    /// </summary>
    /// <param name="valueType">value type</param>
    /// <param name="valueExporter">value exporter</param>
    void RegisterValueExporter(Type valueType, Action<string, object, XmlNode> valueExporter);

    /// <summary>
    /// Bool type. SkipAuditInfo.
    /// </summary>
    bool SkipAuditInfo
    {
      get; set;
    }
  }
}
