using BaseEntity.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Interface of data importer
  /// </summary>
  public interface IDataImporter
  {
    /// <summary>
    /// Import Component
    /// </summary>
    /// <param name="cm">class meta</param>
    /// <param name="n">xml node</param>
    /// <returns></returns>
    IBaseEntityObject ImportComponent(ClassMeta cm, XmlNode n);

    /// <summary>
    /// Import object properties
    /// </summary>
    /// <param name="n">xmlnode</param>
    /// <param name="obj">object</param>
    /// <param name="cm">class meta</param>
    void ImportObjectProperties(XmlNode n, IBaseEntityObject obj, ClassMeta cm);

    /// <summary>
    /// Import Child key
    /// </summary>
    /// <param name="cm">class meta</param>
    /// <param name="objNode">xml node</param>
    /// <returns></returns>
    IList<object> ImportChildKey(ClassMeta cm, XmlNode objNode);

    /// <summary>
    /// ImportEntityProperties
    /// </summary>
    /// <param name="n">xml node</param>
    /// <param name="obj">object</param>
    /// <param name="cm">class meta</param>
    void ImportEntityProperties(XmlNode n, PersistentObject obj, ClassMeta cm);

    /// <summary>
    /// Import key
    /// </summary>
    /// <param name="cm">class meta</param>
    /// <param name="objNode">object node</param>
    /// <returns></returns>
    IList<object> ImportKey(ClassMeta cm, XmlNode objNode);

    /// <summary>
    /// FindByKey
    /// </summary>
    /// <param name="cm">Class meta</param>
    /// <param name="key">Key</param>
    /// <returns></returns>
    PersistentObject FindByKey(ClassMeta cm, IList<object> key);

    /// <summary>
    /// Import entity
    /// </summary>
    /// <param name="cm">class meta</param>
    /// <param name="n">xml node</param>
    /// <returns></returns>
    PersistentObject ImportEntity(ClassMeta cm, XmlNode n);
  }
}
