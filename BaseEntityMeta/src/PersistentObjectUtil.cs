using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace BaseEntity.Metadata
{
  public static class PersistentObjectUtil
  {

    /// <summary>
    /// 
    /// </summary>
    public static bool TryRequestUpdate(this PersistentObject po, out string errorMsg)
    {
      var entityContext = EntityContext.Current;
      if (entityContext == null)
      {
        throw new MetadataException("No current EntityContext!");
      }

      var mutableEntityContext = entityContext as IEditableEntityContext;
      if (mutableEntityContext == null)
      {
        throw new InvalidOperationException("Context type [" + entityContext.GetType().Name + "] does not support updates");
      }

      return mutableEntityContext.TryRequestUpdate(po, out errorMsg);
    }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    /// <remarks>
    /// By default validation is metadata-driven.  Entities that enforce
    /// additional constraints can override this method.  Methods that do
    /// override must first call Validate() on the base class.
    /// </remarks>
    public static void DoValidate(this PersistentObject po, ArrayList errors)
    {
      var cm = ClassCache.Find(po.GetType());
      if (cm != null)
      {
        foreach (PropertyMeta propertyMeta in cm.PropertyList)
          propertyMeta.Validate(po, errors);
      }
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="adaptor"></param>
    /// <returns></returns>
    public static PersistentObject Copy(this PersistentObject po, IEntityContextAdaptor adaptor)
    {
      if (adaptor == null)
      {
        throw new ArgumentNullException("adaptor");
      }

      var sb = new StringBuilder();
      using (var writer = new XmlEntityWriter(sb))
      {
        writer.WriteEntityGraph(po);
      }

      var list = new List<PersistentObject>();

      using (var reader = new XmlEntityReader(sb.ToString(), adaptor))
      {
        while (!reader.EOF)
          list.Add(reader.ReadEntity());
      }

      return list[0];
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static PersistentObject DoCopyAsNew(this PersistentObject po0, IEditableEntityContext context)
    {
      if (context == null)
      {
        throw new ArgumentNullException("context");
      }

      var walker = new OwnedOrRelatedObjectWalker(true);

      walker.Walk(po0);

      var ids = walker.OwnedObjects.Where(po => po.ObjectId != 0).Select(po => po.ObjectId).ToList();

      var sb = new StringBuilder();
      using (var writer = new CloningEntityWriter(context, sb, ids))
      {
        foreach (var entity in walker.OwnedObjects)
          writer.WriteEntity(entity);
      }

      var clones = new List<PersistentObject>();

      var adaptor = new EntityContextEditorAdaptor(context);

      var strValue = sb.ToString();
      using (var reader = new XmlEntityReader(strValue, adaptor))
      {
        while (!reader.EOF)
          clones.Add(reader.ReadEntity());
      }

      return clones[0];
    }

    /// <summary>
    ///   Will build a key string from the Child Key values
    /// </summary>
    /// <param name="cm"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static string FormChildKeyFromKeyValues(ClassMeta cm, IList<object> key)
    {
      var keyPropList = cm.ChildKeyPropertyList;
      if (keyPropList == null || keyPropList.Count == 0)
      {
        throw new ArgumentException(String.Format(
          "Entity [{0}] does not have a well-defined child key!", cm.Name));
      }
      return BuildKeyString(cm, keyPropList, key);
    }


    /// <summary>
    ///   Build's a string value given a list of propertymeta's and values 
    /// </summary>
    /// <param name="cm"></param>
    /// <param name="keyPropList"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    private static string BuildKeyString(ClassMeta cm, IList<PropertyMeta> keyPropList, IList<object> key)
    {
      var sb = new StringBuilder();
      for (int i = 0; i < keyPropList.Count; i++)
      {
        // DataExporter exports an empty tag in case the business key Dt property is MinValue.
        if (key[i] == null)
        {
          var pm = keyPropList[i];
          var propertyMetaType = pm.GetType();
          var nonGenericType = propertyMetaType.BaseType;
          if (nonGenericType == null)
          {
            throw new MetadataException("Type [" + propertyMetaType + "] does not have a BaseType");
          }
          var keyPropertyMetaName = nonGenericType.Name;
          switch (keyPropertyMetaName)
          {
            case "DtPropertyMeta":
            case "DateTimePropertyMeta":
            case "ManyToOnePropertyMeta":
              break;

            default:
              throw new MetadataException(string.Format(
                "Error generating hashcode for object of type {0} (key property {1} has null value)!", cm.Name,
                keyPropList[i].Name));
          }
        }

        // Don't append the '|' for the first value 
        if (i != 0)
          sb.Append('|');

        PropertyMeta keyProp = keyPropList[i];
        sb.Append(keyProp.BuildKeyString(key[i]));
      }
      return sb.ToString();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cm"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static string FormKey(ClassMeta cm, IList<object> key)
    {
      var keyPropList = cm.KeyPropertyList;
      if (keyPropList.Count == 0)
      {
        throw new ArgumentException(String.Format(
          "Entity [{0}] does not have a unique business key!", cm.Name));
      }

      return InternalFormKey(cm, keyPropList, key);
    }

    /// <summary>
    /// If there is a defined childkey (unique key within a parent object) then that is returned
    /// If there is a defined businesskey (unique key within the database) then that is returned
    /// else return "n/a"
    /// </summary>
    /// <returns></returns>
    public static string FormKey(this PersistentObject po)
    {
      var cm = ClassCache.Find(po);

      if (cm != null && cm.HasChildKey)
        return InternalFormKey(cm,
          cm.ChildKeyPropertyList,
          GetKeyValues(po, cm.ChildKeyPropertyList));

      if (cm != null && cm.HasKey)
        return InternalFormKey(cm,
          cm.KeyPropertyList,
          GetKeyValues(po, cm.KeyPropertyList));

      return "n/a";
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="po"></param>
    /// <returns>string</returns>
    public static string FormChildKey(PersistentObject po)
    {
      var cm = ClassCache.Find(po);
      var keyPropList = cm.ChildKeyPropertyList;
      if (keyPropList == null)
      {
        throw new ArgumentException(cm.Name + " does not have a child key defined!");
      }

      return InternalFormKey(cm,
        keyPropList,
        GetKeyValues(po, keyPropList));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cm"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static string FormChildKey(ClassMeta cm, IList<object> key)
    {
      var keyPropList = cm.ChildKeyPropertyList;
      if (keyPropList == null || keyPropList.Count == 0)
      {
        throw new ArgumentException(String.Format(
          "Entity [{0}] does not have a well-defined child key!", cm.Name));
      }

      return InternalFormKey(cm, keyPropList, key);
    }

    ///  <summary>
    /// 
    ///  </summary>
    ///  <param name="po"></param>
    /// <param name="formBaseClassKey">if po is part of a class hierarchy, form the key for base class</param>
    /// <returns>string</returns>
    public static string FormKey(PersistentObject po, bool formBaseClassKey = false)
    {
      var cm = ClassCache.Find(po);
      while (cm.IsDerivedEntity && formBaseClassKey)
        cm = cm.BaseEntity;

      var keyPropList = cm.KeyPropertyList;
      if (keyPropList.Count == 0)
      {
        return cm.Name + "[" + po.ObjectId + "]";
      }

      return InternalFormKey(cm,
        keyPropList,
        GetKeyValues(po, keyPropList));
    }

    /// <summary>
    /// 
    /// </summary>
    private static string InternalFormKey(ClassMeta cm, IList<PropertyMeta> keyPropList, IList<object> key)
    {
      var sb = new StringBuilder(cm.Name);
      sb.Append("|");
      sb.Append(BuildKeyString(cm, keyPropList, key));
      return sb.ToString();
    }

    private static List<object> GetKeyValues(PersistentObject po, IEnumerable<PropertyMeta> keyPropList)
    {
      return keyPropList.Select(keyProp => keyProp.GetValue(po)).ToList();
    }
  }
}
