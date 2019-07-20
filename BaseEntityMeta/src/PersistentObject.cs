// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///   Abstract base class for all WebMathTraining persistent entities.
  /// </summary>
  [DataContract]
  [Serializable]
  [DisplayName("Entity")]
  public abstract class PersistentObject : BaseEntityObject
  {
    #region Constructors

    /// <summary>
    ///   Construct default instance
    /// </summary>
    protected PersistentObject()
    {}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objectId"></param>
    protected PersistentObject(long objectId)
    {
      _objectId = objectId;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="other"></param>
    protected PersistentObject(PersistentObject other)
    {
      _objectId = other._objectId;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    /// <remarks>
    /// By default validation is metadata-driven.  Entities that enforce
    /// additional constraints can override this method.  Methods that do
    /// override must first call Validate() on the base class.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      var cm = ClassCache.Find(GetType());
      if (cm != null)
      {
        foreach (PropertyMeta propertyMeta in cm.PropertyList)
          propertyMeta.Validate(this, errors);
      }
    }

    /// <summary>
    /// Clone
    /// </summary>
    /// <remarks>
    /// The <see cref="ObjectId">ObjectId</see> of the cloned instance = 0.
    /// </remarks>
    public override object Clone()
    {
      PersistentObject po = (PersistentObject)base.Clone();

      // DONT CLONE THE OBJECT ID
      po._objectId = 0;

      return po;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="adaptor"></param>
    /// <returns></returns>
    public PersistentObject Copy(IEntityContextAdaptor adaptor)
    {
      if (adaptor == null)
      {
        throw new ArgumentNullException("adaptor");
      }

      var sb = new StringBuilder();
      using (var writer = new XmlEntityWriter(sb))
      {
        writer.WriteEntityGraph(this);
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
    public virtual PersistentObject CopyAsNew(IEditableEntityContext context)
    {
      if (context == null)
      {
        throw new ArgumentNullException("context");
      }

      var walker = new OwnedOrRelatedObjectWalker(true);

      walker.Walk(this);

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
    /// If there is a defined childkey (unique key within a parent object) then that is returned
    /// If there is a defined businesskey (unique key within the database) then that is returned
    /// else return "n/a"
    /// </summary>
    /// <returns></returns>
    public string FormKey()
    {
      var cm = ClassCache.Find(this);

      if (cm != null && cm.HasChildKey)
        return InternalFormKey(cm,
          cm.ChildKeyPropertyList,
          GetKeyValues(this, cm.ChildKeyPropertyList));

      if (cm != null && cm.HasKey)
        return InternalFormKey(cm,
          cm.KeyPropertyList,
          GetKeyValues(this, cm.KeyPropertyList));

      return "n/a";
    }

    private static List<object> GetKeyValues(PersistentObject po, IEnumerable<PropertyMeta> keyPropList)
    {
      return keyPropList.Select(keyProp => keyProp.GetValue(po)).ToList();
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

    ///<summary>
    /// Derives the values for dependent properties based on other persistent properties.
    ///</summary>
    public virtual void DeriveValues()
    {}

    /// <summary>
    /// Request Update Lock
    /// </summary>
    /// <returns>will throw exception if can not lock the object</returns>
    public void RequestUpdate()
    {
      string errorMsg;
      if (!TryRequestUpdate(out errorMsg))
        throw new SecurityException(errorMsg);
    }

    /// <summary>
    /// 
    /// </summary>
    public bool TryRequestUpdate(out string errorMsg)
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

      return mutableEntityContext.TryRequestUpdate(this, out errorMsg);
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Object id
    /// </summary>
    /// <remarks>
    ///   For internal use only.
    /// </remarks>
    [DataMember]
    [ObjectIdProperty(IsPrimaryKey = true)]
    public long ObjectId
    {
      get { return _objectId; }
      set { _objectId = value; }
    }

    /// <summary>
    /// Returns true if the <see cref="PersistentObject"/> does not have an ObjectId.
    /// </summary>
    [NotMapped]
    public bool IsAnonymous
    {
      get { return _objectId == 0; }
    }

    /// <summary>
    /// 
    /// </summary>
    [NotMapped]
    public bool IsTransient
    {
      get { return _objectId < 0; }
    }

    /// <summary>
    /// Returns true if the <see cref="PersistentObject"/> does not have a globally unique ObjectId.
    /// </summary>
    [NotMapped]
    public bool IsUnsaved
    {
      get { return _objectId <= 0; }
    }

    #endregion

    #region Data

    [NotMapped]
    private Int64 _objectId { get { return Id; } set { Id = value; } }

    #endregion
  }
}