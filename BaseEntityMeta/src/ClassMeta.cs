/*
 * ClassMeta.cs -
 *
 * Copyright (c) WebMathTraining 2002-2011. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// </summary>
  public class ClassMeta
  {
    #region Constructors

    /// <summary>
    /// </summary>
    /// <param name="entityId"></param>
    /// <param name="type"></param>
    protected ClassMeta(int entityId, Type type)
    {
      _name = type.Name;
      _displayName = type.Name;
      _type = type;

      _entityId = entityId;
      if (entityId > 0)
        _tableName = type.Name;
    }

    /// <summary>
    /// Create a new ClassMeta instance initialized using the given ClassMetaAttribute
    /// </summary>
    /// <remarks>
    /// <para>This assumes that any base types have already been added to the ClassCache.</para>
    /// <para>Initialization of the PropertyList is in the SecondPassInit() and so this
    /// constructor should not have any logic that depends on the PropertyList of this
    /// entity or its base component.</para>
    /// </remarks>
    internal ClassMeta(Type type, ComponentAttribute compAttr, InternalClassCache classCache)
    {
      _type = type;
      _name = compAttr.Name ?? type.Name;
      _displayName = compAttr.DisplayName ?? _name;
      _description = compAttr.Description;
      
      #if !V100_META
      if (type.IsSubclassOf(typeof(PersistentObject)))
      {
        throw new MetadataException(String.Format(
          "Invalid Component [{0}] : Must not inherit from PersistentObject", _name));
      }
      #endif
      // Check to see if derived from another component and if so perform additional validation and defaulting logic

      ClassMeta baseComponent = null;
      for (Type baseType = Type.BaseType;
           baseType != typeof (object) && baseComponent == null;
           baseType = baseType.BaseType)
      {
        var cm = classCache.Find(baseType);
        if (cm != null)
        {
          if (cm.IsEntity)
          {
            throw new MetadataException(String.Format(
              "Component [{0}] cannot derived from Entity [{1}]", Name, cm.Name));
          }

          baseComponent = cm;
        }
      }

      if (baseComponent != null)
      {
        if (compAttr.ChildKey != null)
        {
          throw new MetadataException(String.Format(
            "Component [{0}] must inherit ChildKey from [{1}]", Name, baseComponent.Name));
        }

        _childKeyPropertyNames = baseComponent.ChildKeyPropertyNames;
      }
      else
      {
        _childKeyPropertyNames = compAttr.ChildKey;
      }

      _childKeyPropertyList = new List<PropertyMeta>();
      if (_childKeyPropertyNames != null)
      {
        for (int i = 0; i < _childKeyPropertyNames.Length; i++)
          _childKeyPropertyList.Add(null);
      }

      _keyPropertyList = new List<PropertyMeta>();
      _cascadeList = new List<ICascade>();
    }

    /// <summary>
    /// Create a new ClassMeta instance initialized using the given ClassMetaAttribute
    /// </summary>
    /// <remarks>
    /// <para>This assumes that any base types have already been added to the ClassCache.</para>
    /// <para>Initialization of the PropertyList is in the SecondPassInit() and so this
    /// constructor should not have any logic that depends on the PropertyList of this
    /// entity or its BaseEntity.</para>
    /// </remarks>
    internal ClassMeta(Type type, EntityAttribute entityAttr, IEntityPolicy entityPolicy, InternalClassCache classCache)
    {
      _type = type;
      _entityId = entityAttr.EntityId;
      _name = entityAttr.Name ?? type.Name;
      _displayName = entityAttr.DisplayName ?? _name;
      _description = entityAttr.Description;

      if (!type.IsSubclassOf(typeof(PersistentObject)))
      {
        throw new MetadataException(String.Format(
          "Invalid Entity [{0}] : Must inherit from PersistentObject", _name));
      }

      // Check that the EntityId is within the allowed range (the EntityId fits within the left-most
      // 16 bits of the ObjectId, however since the ObjectId is a long, the left-most bit is used fof
      // the sign, and that means we only have 15 bits available to use).

      if (EntityId < 0 || EntityId > short.MaxValue)
      {
        throw new MetadataException(String.Format(
          "Invalid EntityId [{0}] for Entity [{1}] : Must be >= 0 and <= {2}", EntityId, Name, short.MaxValue));
      }

      // Check to see if derived from another Entity and if so perform additional validation and defaulting logic

      ClassMeta baseEntity = null;
      for (Type baseType = Type.BaseType;
           baseType != typeof(object) && baseEntity == null;
           baseType = baseType.BaseType)
      {
        var cm = classCache.Find(baseType);
        if (cm != null)
        {
          if (cm.IsComponent)
          {
            throw new MetadataException(String.Format(
              "Entity [{0}] cannot derive from non-entity [{1}]", Name, cm.Name));
          }

          baseEntity = cm;
        }
      }

      _propertyMapping = entityAttr.PropertyMapping == PropertyMappingStrategy.None ? PropertyMappingStrategy.RelationalOnly : entityAttr.PropertyMapping;

      if (baseEntity != null)
      {
        //*******************************************************************************************************
        // This is a derived entity
        //*******************************************************************************************************
        if (baseEntity.SubclassMapping == SubclassMappingStrategy.None)
        {
          throw new MetadataException(String.Format(
            "BaseEntity [{0}] must specify SubclassMappingStrategy", baseEntity.Name));
        }

        if (entityAttr.SubclassMapping != SubclassMappingStrategy.None)
        {
          throw new MetadataException(String.Format(
            "Entity [{0}] must inherit SubclassMappingStrategy from [{1}]", Name, baseEntity.Name));
        }
        if (entityAttr.AuditPolicy != AuditPolicy.None)
        {
          throw new MetadataException(String.Format(
            "Entity [{0}] must inherit AuditPolicy from [{1}]", Name, baseEntity.Name));
        }
        if (entityAttr.Key != null)
        {
          throw new MetadataException(String.Format(
            "Entity [{0}] must inherit Key from [{1}]", Name, baseEntity.Name));
        }
        if (entityAttr.ChildKey != null)
        {
          throw new MetadataException(String.Format(
            "Entity [{0}] must inherit ChildKey from [{1}]", Name, baseEntity.Name));
        }

        _baseEntity = baseEntity;
        AuditPolicy = baseEntity.AuditPolicy;
        _subclassMapping = baseEntity.SubclassMapping;
        _isChildEntity = baseEntity.IsChildEntity;

        if ((_propertyMapping == PropertyMappingStrategy.ExtendedOnly) &&
            (_baseEntity.SubclassMapping == SubclassMappingStrategy.TablePerSubclass))
        {
          throw new MetadataException(String.Format(
            "Invalid PropertyMapping [ExtendedOnly] for [{0}] : not compatible with SubclassMapping [TablePerSubclass]",
            Name));
        }

        if ((_subclassMapping == SubclassMappingStrategy.TablePerClassHierarchy) ||
            (_propertyMapping == PropertyMappingStrategy.ExtendedOnly))
        {
          if (entityAttr.TableName != null)
          {
            throw new MetadataException(String.Format(
              "Entity [{0}] must inherit TableName from [{1}]", Name, baseEntity.Name));
          }

          _tableName = baseEntity.TableName;
        }
        else
        {
          _tableName = entityAttr.TableName ?? Name;
        }

        _keyPropertyNames = baseEntity.KeyPropertyNames;
        _childKeyPropertyNames = baseEntity.ChildKeyPropertyNames;
      }
      else
      {
        //*******************************************************************************************************
        // This is either a base entity or a standalone entity
        //*******************************************************************************************************

        if (entityAttr.Key != null && entityAttr.ChildKey != null)
        {
          throw new MetadataException(String.Format(
            "Entity [{0}] has both Key and ChildKey properties defined", Name));
        }

        if (entityAttr.AuditPolicy != AuditPolicy.None && !_type.IsSubclassOf(typeof (AuditedObject)))
        {
          throw new MetadataException(String.Format(
            "Entity [{0}] must have AuditPolicy=None since it does not derive from AuditedObject", Name));
        }

        if (_propertyMapping == PropertyMappingStrategy.ExtendedOnly)
        {
          throw new MetadataException(String.Format(
            "Invalid PropertyMappingStrategy [{0}] for BaseEntity [{1}]", _propertyMapping, Name));
        }

#if DISABLED
  // Need to fix some db unit tests that violate this condition
        if (entityAttr.ChildKey != null && !entityAttr.IsChildEntity)
        {
          throw new MetadataException(String.Format(
            "Invalid Entity [{0}] : ChildKey is specified by has IsChildEntity = false", Name));
        }
#endif

        if (entityAttr.SubclassMapping == SubclassMappingStrategy.None)
        {
          if (_entityId == 0)
          {
            throw new MetadataException(String.Format(
              "Invalid EntityId [{0}] for Entity [{1}]", EntityId, Name));
          }
        }
        else
        {
          if (_entityId != 0)
          {
            throw new MetadataException(String.Format(
              "Invalid EntityId [{0}] for BaseEntity [{1}]", EntityId, Name));
          }

          _subclassMapping = entityAttr.SubclassMapping;
        }

        AuditPolicy = entityAttr.AuditPolicy;
        _isChildEntity = entityAttr.IsChildEntity;
        _tableName = entityAttr.TableName ?? _name;

        _keyPropertyNames = entityAttr.Key;
        _childKeyPropertyNames = entityAttr.Key ?? entityAttr.ChildKey;
      }

      _keyPropertyList = new List<PropertyMeta>();
      _childKeyPropertyList = new List<PropertyMeta>();

      if (_keyPropertyNames != null)
      {
        for (int i = 0; i < _keyPropertyNames.Length; i++)
          _keyPropertyList.Add(null);
      }
      if (_childKeyPropertyNames != null)
      {
        for (int i = 0; i < _childKeyPropertyNames.Length; i++)
          _childKeyPropertyList.Add(null);
      }

      _cascadeList = new List<ICascade>();

      if (_entityId != 0)
      {
        _minObjectId = ((long)_entityId) << 48;
        _maxObjectId = (((long)_entityId + 1) << 48) - 1;
      }

      if (IsBaseEntity)
      {
        if (!Type.IsAbstract)
          throw new MetadataException("Invalid BaseEntity [" + Name + "] : must be abstract");
      }
      else
      {
        if (Type.IsAbstract)
          throw new MetadataException("Invalid Entity [" + Name + "] : cannot be abstract");
      }

      // If this entity has a built-in EntityPolicy then use it
      _entityPolicy = entityPolicy;

      // Allow entity to opt out of full historization
      OldStyleValidFrom = entityAttr.OldStyleValidFrom;
    }

    #endregion

    #region Properties

    /// <summary>
    /// EntityId
    /// </summary>
    /// <remarks>
    /// All concrete database entities must have a unique EntityId.  Non-database (transient)
    /// classes and abstract base classes have EntityId=0.
    /// </remarks>
    public int EntityId
    {
      get { return _entityId; }
    }

    /// <summary>
    ///
    /// </summary>
    public bool IsComponent
    {
      get { return !IsEntity; }
    }

    /// <summary>
    ///
    /// </summary>
    public bool IsEntity
    {
      get { return TableName != null; }
    }

    /// <summary>
    /// 
    /// </summary>
    public ClassMeta BaseEntity
    {
      get { return _baseEntity; }
    }

    /// <summary>
    /// Return true if this entity either has or supports derived entities.
    /// </summary>
    /// <remarks>
    /// Every BaseEntity must specify a SubclassMappingStrategy, and for
    /// other entities the SubclassMappingStrategy must be set to None.
    /// </remarks>
    public bool IsBaseEntity
    {
      get { return IsEntity && (SubclassMapping != SubclassMappingStrategy.None) && EntityId == 0; }
    }

    /// <summary>
    /// Return true if this entity has a base entity
    /// </summary>
    public bool IsDerivedEntity
    {
      get { return IsEntity && (SubclassMapping != SubclassMappingStrategy.None) && BaseEntity != null; }
    }

    /// <summary>
    /// Return true if this entity does not have any base or derived entities
    /// </summary>
    /// <remarks>
    /// All the classes that participate in a class hierarchy must specify a SubclassMappingStrategy,
    /// so if this is an entity and it does not specify a SubclassMappingStrategy, then it must be
    /// a standalone entity.
    /// </remarks>
    public bool IsStandaloneEntity
    {
      get { return (IsEntity && (SubclassMapping == SubclassMappingStrategy.None)); }
    }

    /// <summary>
    /// Return true if this is managed by one or more Aggregate Root entities
    /// </summary>
    public bool IsChildEntity
    {
      get { return _isChildEntity; }
    }
    
    /// <summary>
    /// Return true if this is an aggregate root
    /// </summary>
    public bool IsRootEntity
    {
      get { return IsEntity && !IsChildEntity; }
    }
    
    /// <summary>
    /// Return true if the underlying type is an abstract class
    /// </summary>
    public bool IsAbstract
    {
      get { return _type.IsAbstract; }
    }

    /// <summary>
    /// .NET class type
    /// </summary>
    public Type Type
    {
      get { return _type; }
      set { _type = value; }
    }

    /// <summary>
    /// Name of the table used to store instances of this class (by default, same as <see cref="ClassMeta.Name">Name</see>.
    /// </summary>
    public string TableName
    {
      get { return _tableName; }
    }

    /// <summary>
    /// Removes the back-quote (`) characters from the TableName if
    /// found (these are used for table names that conflict with SQL
    /// reserved words).
    /// </summary>
    public string UnquotedTableName
    {
      get { return (TableName == null) ? null : TableName.Trim('`'); }
    }

    /// <summary>
    /// Class name (must be unique)
    /// </summary>
    /// <remarks>
    /// Currently, this is always the unqualified name of the .NET class.
    /// </remarks>
    public string Name
    {
      get { return _name; }
    }

    /// <summary>
    /// The FullName of the underlying <see cref="Type"/>
    /// </summary>
    public string FullName
    {
      get { return _type.FullName; }
    }

    /// <summary>
    /// Display name for this class
    /// </summary>
    public string DisplayName
    {
      get { return _displayName; }
    }

    /// <summary>
    /// Description
    /// </summary>
    public string Description
    {
      get { return _description; }
    }

    /// <summary>
    /// 
    /// </summary>
    public IEntityPolicy EntityPolicy
    {
      get { return _entityPolicy; }
    }

    /// <summary>
    /// 
    /// </summary>
    public PropertyMappingStrategy PropertyMapping
    {
      get { return _propertyMapping; }
      set { _propertyMapping = value; }
    }
    
    /// <summary>
    /// Used to specify the strategy for mapping subclasses to database tables.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public SubclassMappingStrategy SubclassMapping
    {
      get { return _subclassMapping; }
      set { _subclassMapping = value; }
    }

    /// <summary>
    ///  All properties for this entity, including inherited ones
    /// </summary>
    public IList<PropertyMeta> PropertyList
    {
      get { return _propertyList; }
    }

    /// <summary>
    /// Map of all properties for this <see cref="ClassMeta">ClassMeta</see> keyed by Name
    /// </summary>
    public IDictionary<string, PropertyMeta> PropertyMap
    {
      get { return _propertyMap; }
    }

    /// <summary>
    /// Map of all persistent properties for this <see cref="ClassMeta">ClassMeta</see> keyed by Name
    /// </summary>
    public IDictionary<string, PropertyMeta> PersistentPropertyMap
    {
      get { return _persistentPropertyMap; }
    }

    /// <summary>
    /// Map of all ExtendedData properties for this <see cref="ClassMeta">ClassMeta</see> keyed by Name
    /// </summary>
    public IDictionary<string, PropertyMeta> ExtendedPropertyMap
    {
      get { return _extendedPropertyMap; }
    }

    /// <summary>
    /// </summary>
    public List<PropertyMeta> PrimaryKeyPropertyList
    {
      get { return _primaryKeyPropertyList; }
    }

    /// <summary>
    /// </summary>
    public string[] KeyPropertyNames
    {
      get { return _keyPropertyNames; }
    }

    /// <summary>
    /// </summary>
    public string[] ChildKeyPropertyNames
    {
      get { return _childKeyPropertyNames; }
    }

    /// <summary>
    ///  Return list of properties that form natural key
    /// </summary>
    public IList<PropertyMeta> KeyPropertyList
    {
      get { return _keyPropertyList; }
    }

    /// <summary>
    /// true if entity has one or more key properties
    /// </summary>
    public bool HasKey
    {
      get { return _keyPropertyList.Count != 0; }
    }

    /// <summary>
    /// true if entity has one or more child key properties
    /// </summary>
    public bool HasChildKey
    {
      get { return _childKeyPropertyList.Count != 0; }
    }

    /// <summary>
    /// List of property that are used as key when this entity is used in collection property.
    /// When businessky keys is specified, child key should not be specified but all business key will be used as child key. 
    /// </summary>
    public IList<PropertyMeta> ChildKeyPropertyList
    {
      get { return _childKeyPropertyList; }
    }

    /// <summary>
    /// 
    /// </summary>
    public IList<ICascade> CascadeList
    {
      get { return _cascadeList; }
    }
    
    /// <summary>
    ///
    /// </summary>
    public AuditPolicy AuditPolicy { get; set; }

    /// <summary>
    /// Indicates that history is used only for audit purposes and can be removed once archived.
    /// </summary>
    public bool OldStyleValidFrom { get; set; }

    /// <summary>
    /// Minimum valid ObjectId (undefined for components or abstract base entities)
    /// </summary>
    public long MinObjectId
    {
      get { return _minObjectId; }
    }

    /// <summary>
    /// Maximum valid ObjectId (undefined for components or abstract base entities)
    /// </summary>
    public long MaxObjectId
    {
      get { return _maxObjectId; }
    }
    
    #endregion

    #region Methods

    /// <summary>
    /// Create default instance of the underlying class type
    /// </summary>
    /// <returns></returns>
    public IBaseEntityObject CreateInstance()
    {
      return (IBaseEntityObject)_fastActivator.Invoke();
    }

    /// <summary>
    /// Initialize PropertyList and do additional validation checks
    /// </summary>
    internal void SecondPassInit(IClassCache classCache)
    {
      CreateFastActivator(Type);

      // Child entities inherit OldStyleValidFrom setting from base
      if (BaseEntity != null)
      {
        OldStyleValidFrom = BaseEntity.OldStyleValidFrom;
      }
      
      // We want the properties sorted base class first, however
      // GetProperties() will order them the other way around, so
      // we need this bit of code.

      var typeList = new List<Type>();
      var typeHash = new Dictionary<string, List<PropertyInfo>>();

      Type thisType = Type;
      while (thisType != typeof (object))
      {
        if (thisType == null)
        {
          throw new InvalidOperationException("oops");
        }
        typeList.Insert(0, thisType);
        typeHash[thisType.Name] = new List<PropertyInfo>();
        thisType = thisType.BaseType;
      }

      foreach (PropertyInfo propInfo in Type.GetProperties())
      {
        Type myType = propInfo.DeclaringType;
        List<PropertyInfo> propList = typeHash[myType.Name];
        propList.Add(propInfo);
      }

      // Now we can add the attributed properties

      foreach (Type myType in typeList)
      {
        IList<PropertyInfo> propList = typeHash[myType.Name];
        foreach (PropertyInfo propInfo in propList)
        {
          object[] propAttrs = propInfo.GetCustomAttributes(
            typeof (PropertyAttribute), false);

          foreach (PropertyAttribute propMetaAttr in propAttrs)
          {
            var creator = (new PropertyMetaCreatorFactory()).GetPropertyMetaCreator(propMetaAttr);
            if (creator == null)
            {
              throw new MetadataException(string.Format("No property meta creator registered for property meta type: {0}", propMetaAttr.GetType()));
            }
            AddProperty(creator.Create(this, propMetaAttr, propInfo));
          }
        }
      }

      for (int i = 0; i < PropertyList.Count; i++)
      {
        var pm = PropertyList[i];

        pm.Index = i;

        if (pm.Persistent)
        {
          _persistentPropertyMap[pm.Name] = pm;

          bool isOwner = IsOwner(pm.PropertyInfo);
          if (isOwner)
          {
            // Override ExtendedData based on owner setting
            if (PropertyMapping == PropertyMappingStrategy.ExtendedOnly)
              pm.ExtendedData = true;
            else if (PropertyMapping == PropertyMappingStrategy.RelationalOnly)
              pm.ExtendedData = false;

            if (pm.ExtendedData)
              _extendedPropertyMap[pm.Name] = pm;
          }
        }

        var cascade = pm as ICascade;
        if (cascade != null)
        {
          _cascadeList.Add(cascade);
        }

        pm.SecondPassInit(classCache);
      }
    }

    /// <summary>
    /// Perform any PropertyMeta initialization steps that requires ClassMeta to be fully initialized
    /// </summary>
    internal void ThirdPassInit()
    {
      if (HasKey)
      {
        for (int i = 0; i < _keyPropertyList.Count; i++)
        {
          var pm = _keyPropertyList[i];
          if (pm == null)
          {
            throw new MetadataException(String.Format(
              "No matching property found for Key component [{0}]", _keyPropertyNames[i]));
          }
          if (pm.Name == "ValidFrom")
          {
            OldStyleValidFrom = true;
          }
        }
      }
      else if (HasChildKey)
      {
#if TBD
        if (IsRootEntity)
        {
          throw new MetadataException(String.Format(
            "RootEntity [{0}] cannot have ChildKey defined", Name));
        }
#endif
        for (int i = 0; i < _childKeyPropertyList.Count; i++)
        {
          var pm = _childKeyPropertyList[i];
          if (pm == null)
          {
            throw new MetadataException(String.Format(
              "No matching property found for ChildKey [{0}] on entity [{1}]", _childKeyPropertyNames[i], Type.Name));
          }
        }
      }

      foreach (PropertyMeta pm in PropertyList)
      {
        pm.ThirdPassInit();
      }

      foreach (ICascade cascade in CascadeList)
      {
        var pm = (PropertyMeta) cascade;
        if (!IsOwner(pm))
        {
          continue;
        }

        if (cascade.Cascade == "all-delete-orphan")
        {
          if (IsChildEntity && !cascade.ReferencedEntity.IsChildEntity)
          {
            throw new MetadataException(String.Format(
              "Invalid property [{0}.{1}] : ChildEntity cannot own RootEntity [{2}]",
              Name, cascade.Name, cascade.ReferencedEntity.Name));
          }
        }
      }
    }

    /// <summary>
    ///  Add property
    /// </summary>
    internal void AddProperty(PropertyMeta propMeta)
    {
      propMeta.Entity = this;
      _propertyMap[propMeta.Name] = propMeta;
      _propertyList.Add(propMeta);

      if (propMeta.IsPrimaryKey)
      {
        if (_primaryKeyPropertyList == null)
          _primaryKeyPropertyList = new List<PropertyMeta>();

        _primaryKeyPropertyList.Add(propMeta);
        propMeta.IsNullable = false;
      }

      if (propMeta.IsKey)
      {
        if (_keyPropertyNames != null || _childKeyPropertyNames != null)
        {
#if !V100_META
          //In v10.0, there are serval entity have dumplicated specification on the same key property
          throw new MetadataException(String.Format(
            "Key is specified both on ClassMeta [{0}] and on PropertyMeta [{1}]",
            Name, propMeta.Name));
#endif
        }
        else
        {
          _keyPropertyList.Add(propMeta);
          _childKeyPropertyList.Add(propMeta);
          propMeta.IsNullable = propMeta.AllowNullableKey && propMeta.IsNullable;
        }
      }
      else
      {
        // Check if defined as key property at the entity level
        if (_keyPropertyNames != null)
        {
          int idx;
          for (idx = 0; idx < _keyPropertyNames.Length; idx++)
          {
            if (_keyPropertyNames[idx] == propMeta.Name)
              break;
          }
          if (idx < _keyPropertyNames.Length)
          {
            _keyPropertyList[idx] = propMeta;
            _childKeyPropertyList[idx] = propMeta;
            propMeta.IsKey = true;
            propMeta.IsNullable = propMeta.AllowNullableKey && propMeta.IsNullable;
          }
        }
        else if (_childKeyPropertyNames != null)
        {
          int idx;
          for (idx = 0; idx < _childKeyPropertyNames.Length; idx++)
          {
            if (_childKeyPropertyNames[idx] == propMeta.Name)
              break;
          }
          if (idx < _childKeyPropertyNames.Length)
          {
            _childKeyPropertyList[idx] = propMeta;
            propMeta.IsNullable = false;
          }
        }
      }
    }

    /// <summary>
    /// Get the property with the specified name.
    /// </summary>
    public PropertyMeta GetProperty(string name)
    {
      PropertyMeta propMeta;
      bool result = _propertyMap.TryGetValue(name, out propMeta);
      return (result) ? propMeta : null;
    }

    /// <summary>
    /// True if this class is the same as the specified class, or derives from it
    /// </summary>
    /// <param name="classMeta"></param>
    /// <returns></returns>
    public bool IsA(ClassMeta classMeta)
    {
      return IsA(classMeta.Type);
    }
    
    /// <summary>
    ///
    /// </summary>
    public bool IsA(Type baseType)
    {
      return Type == baseType || Type.IsSubclassOf(baseType);
    }

    /// <summary>
    ///  Return true if this entity "owns" this property.
    /// </summary>
    /// <param name="pm"></param>
    public bool IsOwner(PropertyMeta pm)
    {
      return IsOwner(pm.PropertyInfo);
    }

    /// <summary>
    ///  Return true if this entity "owns" this property.
    /// </summary>
    /// <param name="propInfo"></param>
    public bool IsOwner(PropertyInfo propInfo)
    {
      // Check if this property belongs to this entity, or to a base entity
      // (this is needed because the entity hierarchy does not match exactly
      // with the class hierarchy)

      Type declaringType = propInfo.DeclaringType;
      if (declaringType == Type)
      {
        // This property is declared by the Type for this entity
        return true;
      }
      else if (BaseEntity == null)
      {
        // There is no base entity, so this property must belong to this entity
        return true;
      }
      else if ( !BaseEntity.IsA(declaringType) )
      {
        // There is a base entity, but is further up the inheritance hierarchy than the declaring type
        return true;
      }

      return false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return "ClassMeta:" + _type.Name;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public string GetBusinessKey(object obj)
    {
      return string.Join(".", KeyPropertyList.Select(pm => string.Format("{0}", pm.GetValue(obj))).ToArray());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public ClassMeta GetUltimateBaseEntity()
    {
      return IsBaseEntity && !IsDerivedEntity ? this : BaseEntity?.GetUltimateBaseEntity();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public static bool IsSame(object objA, object objB)
    {
      return IsSame(objA, objB, false);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <param name="ignoreSystemProps"></param>
    /// <returns></returns>
    public static bool IsSame(object objA, object objB, bool ignoreSystemProps)
    {
      if (objA == null)
      {
        return objB == null;
      }
      if (objB == null)
      {
        return false;
      }

      var cmA = ClassCache.Find(objA);
      var cmB = ClassCache.Find(objB);

      if (cmA != cmB)
      {
        return false;
      }

      foreach (var pm in cmB.PropertyList.Where(pm => pm.Persistent))
      {
        if (ignoreSystemProps)
        {
          if (pm.IsSystemProperty)
          {
            if (cmA.OldStyleValidFrom)
            {
              // If it is OldStyleValidFrom and there is a difference in ValidFrom property, 
              // it should not be considered as same.
              if (pm.Name != "ValidFrom")
                continue;
            }
            else
            {
              continue;
            }
          }
        }

        if (!pm.IsSame(objA, objB))
          return false;
      }

      return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="objA"></param>
    /// <param name="objB"></param>
    /// <returns></returns>
    public static ISnapshotDelta CreateDelta(BaseEntityObject objA, BaseEntityObject objB)
    {
      if (objA == null)
      {
        return objB == null ? null : new ObjectDelta(ItemAction.Added, objB);
      }

      if (objB == null)
      {
        return new ObjectDelta(ItemAction.Removed, objA);
      }

      var cmA = ClassCache.Find(objA);
      var cmB = ClassCache.Find(objB);

      if (cmA != cmB)
      {
        return new KeyedCollectionDelta<BaseEntityObject>(
          new[]
          {
            new ObjectDelta(ItemAction.Removed, objA),
            new ObjectDelta(ItemAction.Added, objB)
          });
      }

      ObjectKey key;
      var poA = objA as PersistentObject;
      if (poA != null)
      {
        var poB = (PersistentObject)objB;
        var idA = poA.ObjectId;
        if (idA == 0)
        {
          throw new InvalidOperationException("Cannot create ObjectDelta for transient (unsaved) entity");
        }
        var idB = poB.ObjectId;
        if (idB == 0)
        {
          throw new InvalidOperationException("Cannot create ObjectDelta for transient (unsaved) entity");
        }
        if (idA == idB)
        {
          key = new EntityKey(idA);
        }
        else
        {
          return new KeyedCollectionDelta<BaseEntityObject>(
            new[]
            {
              new ObjectDelta(ItemAction.Removed, objA),
              new ObjectDelta(ItemAction.Added, objB)
            });
        }
      }
      else
      {
        var oldKey = new ComponentKey(objA);
        var newKey = new ComponentKey(objB);
        if (oldKey.Equals(newKey))
        {
          key = oldKey;
        }
        else
        {
          return new KeyedCollectionDelta<BaseEntityObject>(
            new[]
            {
              new ObjectDelta(ItemAction.Removed, objA),
              new ObjectDelta(ItemAction.Added, objB)
            });
        }
      }

      var objectDelta = new ObjectDelta(cmA, key, objA, objB);
      return objectDelta.PropertyDeltas.Any() ? objectDelta : null;
    }

    /// <summary>
    /// Generate Python code fragment that can be used to read entity history
    /// </summary>
    public string GenerateHistoryReaderCode()
    {
      var sb = new StringBuilder();

      foreach (var pm in PropertyList)
      {
        var code = pm.GenerateHistoryReaderFunction();
        if (!string.IsNullOrEmpty(code))
        {
          sb.Append(code);
          sb.AppendLine();
        }
      }

      sb.AppendLine(string.Format("def Read{0}(reader):", Name));

      if (IsEntity)
      {
        sb.AppendLine("  entityId = reader.ReadInt32()");
        sb.AppendLine("  data = EntityData(entityId)");
      }
      else
      {
        sb.AppendLine("  typeName = reader.ReadString()");
        sb.AppendLine("  data = ComponentData(typeName)");

        if (IsAbstract)
        {
          bool firstTime = true;
          foreach (var cm in ClassCache.FindAll().Where(cm => !cm.IsAbstract && typeof(Type).IsAssignableFrom(cm.Type)))
          {
            sb.AppendLine(string.Format("  {0} typeName=\"{1}\": Read{1}(reader)", firstTime ? "if" : "elif", cm.Name));
            firstTime = false;
          }
        }
      }

      sb.AppendLine();

      sb.AppendLine("  while True:");
      sb.AppendLine("    propIndex = reader.ReadInt32()");
      sb.AppendLine("    if propIndex == -1: break");
      
      foreach (var pm in PropertyList)
      {
        sb.AppendLine(string.Format("    elif propIndex == {0}: data[\"{1}\"] = {2}",
          pm.Index,
          pm.Name,
          pm.GenerateHistoryReaderInvocation()));
      }

      sb.AppendLine("  return data");

      return sb.ToString();
    }

    #endregion

    #region FastActivator
    
    private delegate object FastActivator();

    private void CreateFastActivator(Type type)
    {
      const BindingFlags bindingFlags =
        BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

      var paramTypes = new Type[] {};
      var dynamicMethod = new DynamicMethod("___FastActivator" + type.Name, type, null, type);
      var ilGenerator = dynamicMethod.GetILGenerator();
      var constructor = type.GetConstructor(bindingFlags, null, paramTypes, null);
      if (constructor == null)
      {
        throw new MetadataException("No default constructor for [" + type.Name + "]");
      }
      ilGenerator.Emit(OpCodes.Newobj, constructor);
      ilGenerator.Emit(OpCodes.Ret);

      _fastActivator = dynamicMethod.CreateDelegate(
        typeof (FastActivator)) as FastActivator;
    }

    #endregion

    #region Data

    private Type _type;
    private readonly int _entityId;
    private readonly ClassMeta _baseEntity;
    private readonly string _tableName;
    private readonly string _name;
    private readonly string _displayName;
    private readonly string _description;
    private readonly bool _isChildEntity;
    private PropertyMappingStrategy _propertyMapping;
    private SubclassMappingStrategy _subclassMapping;
    private FastActivator _fastActivator;
    private readonly IList<PropertyMeta> _propertyList = new List<PropertyMeta>();
    private readonly IEntityPolicy _entityPolicy;

    private readonly IDictionary<string, PropertyMeta> _propertyMap = new Dictionary<string, PropertyMeta>();
    private readonly IDictionary<string, PropertyMeta> _persistentPropertyMap = new Dictionary<string, PropertyMeta>();
    private readonly IDictionary<string, PropertyMeta> _extendedPropertyMap = new Dictionary<string, PropertyMeta>();

    // Primary key properties
    private List<PropertyMeta> _primaryKeyPropertyList;

    // Alternate key properties
    private readonly string[] _keyPropertyNames;
    private readonly IList<PropertyMeta> _keyPropertyList;

    // ChildKey properties
    private readonly string[] _childKeyPropertyNames;
    private readonly IList<PropertyMeta> _childKeyPropertyList;

    // List of properties that reference other entities
    private readonly List<ICascade> _cascadeList;

    // Identifies the range of ObjectId values for entities
    private readonly long _minObjectId;
    private readonly long _maxObjectId;

    #endregion
  }
}
