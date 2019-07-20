/*
 * HbmGenerator.cs
 *
 * Copyright (c) WebMathTraining Inc 2008. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Xml;
using NHibernate.Type;
using BaseEntity.Database.Types;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Database.Workflow;

namespace BaseEntity.Database.Engine
{
  /// <exclude />
  public class HbmGenerator
  {
    private static readonly Dictionary<Type, Type> MappedTypes = new Dictionary<Type, Type>();
    private static readonly Dictionary<Type, Type> MappedNullableTypes = new Dictionary<Type, Type>();

    static HbmGenerator()
    {
      MappedTypes[typeof(DateTime)] = typeof(UtcDateTimeType);
      MappedTypes[typeof(Guid)] = typeof(GuidType);
      MappedNullableTypes[typeof(DateTime)] = typeof(UtcDateTimeType);
      MappedNullableTypes[typeof(Guid)] = typeof(GuidType);
    }

    /// <summary>
    /// Registers the type of the mapped.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="mappedType">Type of the mapped.</param>
    public static void RegisterMappedType(Type type, Type mappedType)
    {
      MappedTypes[type] = mappedType;
    }

    /// <summary>
    /// Registers the type of the mapped nullable.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="mappedNullableType">Type of the mapped nullable.</param>
    public static void RegisterMappedNullableType(Type type, Type mappedNullableType)
    {
      MappedNullableTypes[type] = mappedNullableType;
    }

    private const string NHibernateUri = "urn:nhibernate-mapping-2.2";

    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger( typeof(HbmGenerator) );

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public XmlDocument Generate()
    {
      return Generate(ClassCache.FindAll().Select(c => c.Name));
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="entityName"></param>
    /// <returns>XmlDocument</returns>
    public XmlDocument Generate(string entityName)
    {
      return Generate(new List<string>(new[] {entityName}));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entityNames"></param>
    /// <returns></returns>
    public XmlDocument Generate(IEnumerable<string> entityNames)
    {
      var xmlDoc = new XmlDocument();
      var mappingElement = (XmlElement)xmlDoc.AppendChild(
        xmlDoc.CreateElement("hibernate-mapping", NHibernateUri));

      foreach (string entityName in entityNames)
      {
        var cm = ClassCache.Find(entityName);
        if (cm == null)
        {
          throw new ArgumentException("Invalid ClassMeta [" + entityName + "]");
        }

        if (cm.IsBaseEntity && !cm.IsDerivedEntity)
        {
          // Generate mapping for base entity and any derived entities
          if (cm.SubclassMapping == SubclassMappingStrategy.TablePerClassHierarchy)
          {
            MapTablePerClassHierarchy(mappingElement, cm);
          }
          else if (cm.SubclassMapping == SubclassMappingStrategy.TablePerSubclass)
          {
            MapTablePerSubclass(mappingElement, cm);
          }
          else if (cm.SubclassMapping == SubclassMappingStrategy.Hybrid)
          {
            MapTablePerSubclassWithDiscriminator(mappingElement, cm);
          }
          else
          {
            throw new MetadataException(String.Format(
              "Invalid SubclassMappingStrategy [{0}]", cm.SubclassMapping));
          }
        }
        else if (cm.IsStandaloneEntity)
        {
          MapStandaloneClass(mappingElement, cm);
        }
      }

      GenerateCommitLogMapping(mappingElement);
      GenerateAuditLogMapping(mappingElement);
      GenerateAuditHistoryMapping(mappingElement);
      GenerateWorkflowInstanceMapping(mappingElement);

      return xmlDoc;
    }

    private static void GenerateWorkflowInstanceMapping(XmlElement mappingElement)
    {
      var xmlDoc = mappingElement.OwnerDocument;
      if (xmlDoc != null)
      {
        var classElement = xmlDoc.CreateElement("class", NHibernateUri);
        mappingElement.AppendChild(classElement);
        classElement.SetAttribute("name", typeof(Instance).GetAssemblyQualifiedShortName());
        classElement.SetAttribute("table", "[System.Activities.DurableInstancing].[Instances]");
        classElement.SetAttribute("lazy", "false");
        classElement.SetAttribute("mutable", "false");

        var idElement = xmlDoc.CreateElement("id", NHibernateUri);
        idElement.SetAttribute("name", "InstanceId");
        idElement.SetAttribute("type", "Guid");
        classElement.AppendChild(idElement);

        var creationTimeElement = xmlDoc.CreateElement("property", NHibernateUri);
        creationTimeElement.SetAttribute("name", "CreationTime");
        creationTimeElement.SetAttribute("type", typeof(UtcDateTimeType).GetAssemblyQualifiedShortName());
        creationTimeElement.SetAttribute("not-null", "false");
        classElement.AppendChild(creationTimeElement);

        var lastUpdatedTimeElement = xmlDoc.CreateElement("property", NHibernateUri);
        lastUpdatedTimeElement.SetAttribute("name", "LastUpdatedTime");
        lastUpdatedTimeElement.SetAttribute("type", typeof(UtcDateTimeType).GetAssemblyQualifiedShortName());
        lastUpdatedTimeElement.SetAttribute("not-null", "false");
        classElement.AppendChild(lastUpdatedTimeElement);

        var activeBookmarksElement = xmlDoc.CreateElement("property", NHibernateUri);
        activeBookmarksElement.SetAttribute("name", "ActiveBookmarks");
        activeBookmarksElement.SetAttribute("type", "String");
        activeBookmarksElement.SetAttribute("not-null", "false");
        classElement.AppendChild(activeBookmarksElement);

        var suspensionExceptionNameElement = xmlDoc.CreateElement("property", NHibernateUri);
        suspensionExceptionNameElement.SetAttribute("name", "SuspensionExceptionName");
        suspensionExceptionNameElement.SetAttribute("type", "String");
        suspensionExceptionNameElement.SetAttribute("not-null", "false");
        classElement.AppendChild(suspensionExceptionNameElement);

        var suspensionReasonElement = xmlDoc.CreateElement("property", NHibernateUri);
        suspensionReasonElement.SetAttribute("name", "SuspensionReason");
        suspensionReasonElement.SetAttribute("type", "String");
        suspensionReasonElement.SetAttribute("not-null", "false");
        classElement.AppendChild(suspensionReasonElement);

        var executionStatusElement = xmlDoc.CreateElement("property", NHibernateUri);
        executionStatusElement.SetAttribute("name", "ExecutionStatus");
        executionStatusElement.SetAttribute("type", "String");
        executionStatusElement.SetAttribute("not-null", "false");
        classElement.AppendChild(executionStatusElement);

        var isSuspendedElement = xmlDoc.CreateElement("property", NHibernateUri);
        isSuspendedElement.SetAttribute("name", "IsSuspended");
        isSuspendedElement.SetAttribute("not-null", "true");
        classElement.AppendChild(isSuspendedElement);
      }
    }

    private static void GenerateCommitLogMapping(XmlElement mappingElement)
    {
      var xmlDoc = mappingElement.OwnerDocument;
      if (xmlDoc != null)
      {
        var classElement = xmlDoc.CreateElement("class", NHibernateUri);
        mappingElement.AppendChild(classElement);
        classElement.SetAttribute("name", typeof(CommitLog).GetAssemblyQualifiedShortName());
        classElement.SetAttribute("table", "CommitLog");
        classElement.SetAttribute("lazy", "false");
        classElement.SetAttribute("mutable", "false");

        var idElement = (XmlElement)classElement.AppendChild(
          xmlDoc.CreateElement("id", NHibernateUri));
        idElement.SetAttribute("name", "Tid");
        idElement.SetAttribute("column", "Tid");
        idElement.SetAttribute("type", typeof(int).GetAssemblyQualifiedShortName());
        idElement.SetAttribute("unsaved-value", "0");
        var generatorElement = (XmlElement)idElement.AppendChild(
          xmlDoc.CreateElement("generator", NHibernateUri));
        generatorElement.SetAttribute("class", "identity");
        classElement.AppendChild(idElement);

        var lastUpdatedElement = xmlDoc.CreateElement("property", NHibernateUri);
        lastUpdatedElement.SetAttribute("name", "LastUpdated");
        lastUpdatedElement.SetAttribute("type", typeof(DateTime).GetAssemblyQualifiedShortName());
        lastUpdatedElement.SetAttribute("not-null", "true");
        classElement.AppendChild(lastUpdatedElement);

        var updatedByElement = xmlDoc.CreateElement("property", NHibernateUri);
        updatedByElement.SetAttribute("name", "UpdatedBy");
        updatedByElement.SetAttribute("type", typeof(long).GetAssemblyQualifiedShortName());
        updatedByElement.SetAttribute("not-null", "true");
        classElement.AppendChild(updatedByElement);

        var commentElement = xmlDoc.CreateElement("property", NHibernateUri);
        commentElement.SetAttribute("name", "Comment");
        commentElement.SetAttribute("type", "String(140)");
        commentElement.SetAttribute("not-null", "false");
        classElement.AppendChild(commentElement);

        var transactionIdElement = xmlDoc.CreateElement("property", NHibernateUri);
        transactionIdElement.SetAttribute("name", "TransactionId");
        transactionIdElement.SetAttribute("type", "Guid");
        transactionIdElement.SetAttribute("not-null", "false");
        classElement.AppendChild(transactionIdElement);
      }
    }

    private static void GenerateAuditLogMapping(XmlElement mappingElement)
    {
      var xmlDoc = mappingElement.OwnerDocument;
      if (xmlDoc != null)
      {
        var classElement = xmlDoc.CreateElement("class", NHibernateUri);
        mappingElement.AppendChild(classElement);
        classElement.SetAttribute("name", typeof(AuditLog).GetAssemblyQualifiedShortName());
        classElement.SetAttribute("table", "AuditLog");
        classElement.SetAttribute("lazy", "false");
        classElement.SetAttribute("mutable", "false");

        var idElement = xmlDoc.CreateElement("composite-id", NHibernateUri);
        var tidElement = xmlDoc.CreateElement("key-property", NHibernateUri);
        tidElement.SetAttribute("name", "Tid");
        tidElement.SetAttribute("type", typeof(int).GetAssemblyQualifiedShortName());
        idElement.AppendChild(tidElement);

        var objectIdElement = xmlDoc.CreateElement("key-property", NHibernateUri);
        objectIdElement.SetAttribute("name", "ObjectId");
        objectIdElement.SetAttribute("type", typeof(long).GetAssemblyQualifiedShortName());
        idElement.AppendChild(objectIdElement);
        classElement.AppendChild(idElement);

        var rootObjectIdElement = xmlDoc.CreateElement("property", NHibernateUri);
        rootObjectIdElement.SetAttribute("name", "RootObjectId");
        rootObjectIdElement.SetAttribute("type", typeof(long).GetAssemblyQualifiedShortName());
        rootObjectIdElement.SetAttribute("not-null", "true");
        classElement.AppendChild(rootObjectIdElement);

        var parentObjectIdElement = xmlDoc.CreateElement("property", NHibernateUri);
        parentObjectIdElement.SetAttribute("name", "ParentObjectId");
        parentObjectIdElement.SetAttribute("type", typeof(long).GetAssemblyQualifiedShortName());
        parentObjectIdElement.SetAttribute("not-null", "true");
        classElement.AppendChild(parentObjectIdElement);

        var entityIdElement = xmlDoc.CreateElement("property", NHibernateUri);
        entityIdElement.SetAttribute("name", "EntityId");
        entityIdElement.SetAttribute("type", typeof(int).GetAssemblyQualifiedShortName());
        entityIdElement.SetAttribute("not-null", "true");
        classElement.AppendChild(entityIdElement);

        var validFromElement = xmlDoc.CreateElement("property", NHibernateUri);
        validFromElement.SetAttribute("name", "ValidFrom");
        validFromElement.SetAttribute("type", typeof(DateTime).GetAssemblyQualifiedShortName());
        validFromElement.SetAttribute("not-null", "true");
        classElement.AppendChild(validFromElement);

        var actionElement = xmlDoc.CreateElement("property", NHibernateUri);
        actionElement.SetAttribute("name", "Action");
        actionElement.SetAttribute("type", typeof(int).GetAssemblyQualifiedShortName());
        actionElement.SetAttribute("not-null", "true");
        classElement.AppendChild(actionElement);

        var objectDeltaElement = xmlDoc.CreateElement("property", NHibernateUri);
        objectDeltaElement.SetAttribute("name", "ObjectDelta");
        objectDeltaElement.SetAttribute("type", "BinaryBlob");
        objectDeltaElement.SetAttribute("length", "2147483647");
        objectDeltaElement.SetAttribute("not-null", "false");
        classElement.AppendChild(objectDeltaElement);

        var isArchivedElement = xmlDoc.CreateElement("property", NHibernateUri);
        isArchivedElement.SetAttribute("name", "IsArchived");
        isArchivedElement.SetAttribute("type", typeof(bool).GetAssemblyQualifiedShortName());
        isArchivedElement.SetAttribute("not-null", "true");
        classElement.AppendChild(isArchivedElement);
      }
    }

    private static void GenerateAuditHistoryMapping(XmlElement mappingElement)
    {
      var xmlDoc = mappingElement.OwnerDocument;
      if (xmlDoc != null)
      {
        var classElement = xmlDoc.CreateElement("class", NHibernateUri);
        mappingElement.AppendChild(classElement);
        classElement.SetAttribute("name", typeof(AuditHistory).GetAssemblyQualifiedShortName());
        classElement.SetAttribute("table", "AuditHistory");
        classElement.SetAttribute("lazy", "false");
        classElement.SetAttribute("mutable", "false");

        var idElement = xmlDoc.CreateElement("composite-id", NHibernateUri);
        var tidElement = xmlDoc.CreateElement("key-property", NHibernateUri);
        tidElement.SetAttribute("name", "Tid");
        tidElement.SetAttribute("type", typeof(int).GetAssemblyQualifiedShortName());
        idElement.AppendChild(tidElement);

        var rootObjectIdElement = xmlDoc.CreateElement("key-property", NHibernateUri);
        rootObjectIdElement.SetAttribute("name", "RootObjectId");
        rootObjectIdElement.SetAttribute("type", typeof(long).GetAssemblyQualifiedShortName());
        idElement.AppendChild(rootObjectIdElement);
        classElement.AppendChild(idElement);

        var validFromElement = xmlDoc.CreateElement("property", NHibernateUri);
        validFromElement.SetAttribute("name", "ValidFrom");
        validFromElement.SetAttribute("type", typeof(DateTime).GetAssemblyQualifiedShortName());
        validFromElement.SetAttribute("not-null", "true");
        classElement.AppendChild(validFromElement);

        var objectDeltaElement = xmlDoc.CreateElement("property", NHibernateUri);
        objectDeltaElement.SetAttribute("name", "ObjectDelta");
        objectDeltaElement.SetAttribute("type", "BinaryBlob");
        objectDeltaElement.SetAttribute("length", "2147483647");
        objectDeltaElement.SetAttribute("not-null", "false");
        classElement.AppendChild(objectDeltaElement);
      }
    }

    /// <exclude />
    /// <summary>
    /// Return the type name for the entity that maps to this type
    /// </summary>
    /// <remarks>
    ///  This is required to support customer data extendibility.
    /// </remarks>
    /// <param name="type"></param>
    /// <returns></returns>
    public static string EntityTypeName(Type type)
    {
      // Lookup the entity for this type.  In the case of customer
      // data extendibility, both the WebMathTraining (base) type and the
      // customer (derived) type will map to the same entity.
      var entity = ClassCache.Find(type);

      // Return the type associated with this entity.  In the
      // case of customer data extendibility, this would be the
      // type of the customer (derived) class.
      return entity.Type.GetAssemblyQualifiedShortName();
    }

    /// <summary>
    /// Form column name from unqualified part and (optional) prefix
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="column"></param>
    /// <returns></returns>
    public static string ColumnName(string prefix, string column)
    {
      return (prefix == null) ? column : prefix + column;
    }

    private void MapStandaloneClass(XmlElement mappingElement, ClassMeta entity)
    {
      XmlDocument xmlDoc = mappingElement.OwnerDocument;

      XmlElement classElement = (XmlElement)mappingElement.AppendChild(
        xmlDoc.CreateElement("class", NHibernateUri));
      classElement.SetAttribute("name", entity.Type.GetAssemblyQualifiedShortName());
      classElement.SetAttribute("table", entity.TableName);
      classElement.SetAttribute("lazy", "false");
      MapPrimaryKey(classElement, entity);
      MapProperties(classElement, entity);

      if (entity.PropertyMapping == PropertyMappingStrategy.Hybrid)
        AddExtendedDataProperty(classElement, entity);
    }

    private void MapTablePerClassHierarchy(XmlElement mappingElement, ClassMeta baseEntity)
    {
      XmlDocument xmlDoc = mappingElement.OwnerDocument;

      XmlElement baseClassElement = (XmlElement)mappingElement.AppendChild(
        xmlDoc.CreateElement("class", NHibernateUri));
      baseClassElement.SetAttribute("name", baseEntity.Type.GetAssemblyQualifiedShortName());
      baseClassElement.SetAttribute("table", baseEntity.TableName);
      baseClassElement.SetAttribute("discriminator-value", "0");
      baseClassElement.SetAttribute("lazy", "false");
      MapPrimaryKey(baseClassElement, baseEntity);

      XmlElement discriminatorElement = (XmlElement)baseClassElement.AppendChild(
        xmlDoc.CreateElement("discriminator", NHibernateUri));
      discriminatorElement.SetAttribute("column", "EntityId");
      discriminatorElement.SetAttribute("type", "Int32");

      MapProperties(baseClassElement, baseEntity);

      var elem = AddExtendedDataProperty(baseClassElement, baseEntity);
      bool baseEntityNeedsExtendedData = (baseEntity.PropertyMapping == PropertyMappingStrategy.Hybrid);
      baseEntityNeedsExtendedData |= ClassCache.FindAll().Where(cm => cm.BaseEntity == baseEntity).Aggregate(baseEntityNeedsExtendedData, (current, childEntity) => current | MapTablePerClassHierarchySubClass(baseClassElement, childEntity));
      if (!baseEntityNeedsExtendedData)
      {
        baseClassElement.RemoveChild(elem);
      }
    }

    private bool MapTablePerClassHierarchySubClass(XmlElement baseClassElement, ClassMeta childEntity)
    {
      var childClassElements = new List<XmlElement>();

      XmlDocument xmlDoc = baseClassElement.OwnerDocument;
      XmlElement childClassElement = xmlDoc.CreateElement("subclass", NHibernateUri);
      childClassElement.SetAttribute("name", childEntity.Type.GetAssemblyQualifiedShortName());
      childClassElement.SetAttribute("discriminator-value", childEntity.EntityId.ToString(CultureInfo.InvariantCulture));
      childClassElement.SetAttribute("lazy", "false");
      MapProperties(childClassElement, childEntity);
      childClassElements.Add(childClassElement);

      // If any derived entities supports ExtendedData we need an ExtendedData column
      bool baseEntityNeedsExtendedData = (childEntity.PropertyMapping != PropertyMappingStrategy.RelationalOnly);

      if (childEntity.IsBaseEntity)
      {
        baseEntityNeedsExtendedData = ClassCache.FindAll().Where(cm => cm.BaseEntity == childEntity).Aggregate(baseEntityNeedsExtendedData, (current, cm) => current | MapTablePerClassHierarchySubClass(childClassElement, cm));
      }

      foreach (var elem in childClassElements)
      {
        baseClassElement.AppendChild(elem);
      }

      return baseEntityNeedsExtendedData;
    }

    private void MapTablePerSubclass(XmlElement mappingElement, ClassMeta baseEntity)
    {
      XmlDocument xmlDoc = mappingElement.OwnerDocument;

      var baseClassElement = (XmlElement) mappingElement.AppendChild(
        xmlDoc.CreateElement("class", NHibernateUri));
      baseClassElement.SetAttribute("name", baseEntity.Type.GetAssemblyQualifiedShortName());
      baseClassElement.SetAttribute("table", baseEntity.TableName);
      baseClassElement.SetAttribute("lazy", "false");
      MapPrimaryKey(baseClassElement, baseEntity);
      MapProperties(baseClassElement, baseEntity);

      if (baseEntity.PropertyMapping == PropertyMappingStrategy.Hybrid)
        AddExtendedDataProperty(baseClassElement, baseEntity);

      foreach (ClassMeta childEntity in ClassCache.FindAll())
      {
        if (childEntity.BaseEntity == baseEntity)
        {
          var childClassElement = (XmlElement) baseClassElement.AppendChild(
            xmlDoc.CreateElement("joined-subclass", NHibernateUri));
          childClassElement.SetAttribute("name", childEntity.Type.GetAssemblyQualifiedShortName());
          childClassElement.SetAttribute("table", childEntity.TableName);
          childClassElement.SetAttribute("lazy", "false");
          // For now, assume primary key is ObjectId
          var idElement = (XmlElement) childClassElement.AppendChild(
            xmlDoc.CreateElement("key", NHibernateUri));
          idElement.SetAttribute("column", "ObjectId");
          MapProperties(childClassElement, childEntity);

          if (childEntity.PropertyMapping == PropertyMappingStrategy.Hybrid)
            AddExtendedDataProperty(childClassElement, childEntity);
        }
      }
    }

    /// <summary>
    /// Hybrid strategy that uses join table only if subclass has relational properties.  Unlike
    /// plain-vanilla TablePerSubclass mappings, in this case we need a discriminator column since
    /// we cannot rely on having a join table in all cases to determine the derived type.
    /// </summary>
    /// <remarks>
    /// The discriminator can also benefit performance in some cases, and so mapping strategy
    /// is preferred over just TablePerSubclass.
    /// </remarks>
    /// <param name="mappingElement"></param>
    /// <param name="baseEntity"></param>
    private void MapTablePerSubclassWithDiscriminator(XmlElement mappingElement, ClassMeta baseEntity)
    {
      XmlDocument xmlDoc = mappingElement.OwnerDocument;

      var baseClassElement = (XmlElement)mappingElement.AppendChild(
        xmlDoc.CreateElement("class", NHibernateUri));
      baseClassElement.SetAttribute("name", baseEntity.Type.GetAssemblyQualifiedShortName());
      baseClassElement.SetAttribute("table", baseEntity.TableName);
      baseClassElement.SetAttribute("discriminator-value", "0");
      baseClassElement.SetAttribute("lazy", "false");
      MapPrimaryKey(baseClassElement, baseEntity);

      var discriminatorElement = (XmlElement)baseClassElement.AppendChild(
        xmlDoc.CreateElement("discriminator", NHibernateUri));
      discriminatorElement.SetAttribute("column", "EntityId");
      discriminatorElement.SetAttribute("type", "Int32");

      MapProperties(baseClassElement, baseEntity);

      var elem = AddExtendedDataProperty(baseClassElement, baseEntity);
      bool baseEntityNeedsExtendedData = (baseEntity.PropertyMapping == PropertyMappingStrategy.Hybrid);
      baseEntityNeedsExtendedData |= ClassCache.FindAll().Where(cm => cm.BaseEntity == baseEntity).Aggregate(baseEntityNeedsExtendedData, (current, childEntity) => current | MapTablePerSubclassWithDiscriminatorSubclass(baseClassElement, childEntity));
      if (!baseEntityNeedsExtendedData)
      {
        baseClassElement.RemoveChild(elem);
      }
    }

    private bool MapTablePerSubclassWithDiscriminatorSubclass(XmlElement baseClassElement, ClassMeta childEntity)
    {
      var childClassElements = new List<XmlElement>();

      var xmlDoc = baseClassElement.OwnerDocument;
      XmlElement childClassElement = xmlDoc.CreateElement("subclass", NHibernateUri);
      childClassElement.SetAttribute("name", childEntity.Type.GetAssemblyQualifiedShortName());
      childClassElement.SetAttribute("discriminator-value", childEntity.EntityId.ToString(CultureInfo.InvariantCulture));
      childClassElement.SetAttribute("lazy", "false");
      childClassElements.Add(childClassElement);

      bool baseEntityNeedsExtendedData;
      if (childEntity.PropertyMapping == PropertyMappingStrategy.ExtendedOnly)
      {
        // No join table needed in this case
        MapProperties(childClassElement, childEntity);
        baseEntityNeedsExtendedData = true;
      }
      else
      {
        baseEntityNeedsExtendedData = false;
        childClassElement.SetAttribute("lazy", "false");
        var joinElement = (XmlElement)childClassElement.AppendChild(
          xmlDoc.CreateElement("join", NHibernateUri));
        joinElement.SetAttribute("table", childEntity.TableName);
        // For now, assume primary key is ObjectId
        var idElement = (XmlElement)joinElement.AppendChild(
          xmlDoc.CreateElement("key", NHibernateUri));
        idElement.SetAttribute("column", "ObjectId");
        MapProperties(joinElement, childEntity);
        if (childEntity.PropertyMapping == PropertyMappingStrategy.Hybrid)
          AddExtendedDataProperty(joinElement, childEntity);
      }

      if (childEntity.IsBaseEntity)
      {
        baseEntityNeedsExtendedData = ClassCache.FindAll().Where(cm => cm.BaseEntity == childEntity).Aggregate(baseEntityNeedsExtendedData, (current, cm) => current | MapTablePerSubclassWithDiscriminatorSubclass(childClassElement, cm));
      }

      foreach (var elem in childClassElements)
      {
        baseClassElement.AppendChild(elem);
      }

      return baseEntityNeedsExtendedData;
    }

    private void MapPrimaryKey(XmlElement classElement, ClassMeta entity)
    {
      XmlDocument xmlDoc = classElement.OwnerDocument;

      if (entity.IsDerivedEntity)
      {
        // Primary key defined on base entity
        return;
      }

      List<PropertyMeta> keyPropList = entity.PrimaryKeyPropertyList;
      if (keyPropList == null)
        return;

      if (keyPropList.Count == 1)
      {
        PropertyMeta keyProp = keyPropList[0];

        if (keyProp is ObjectIdPropertyMeta)
        {
          // If it derives from PersistentObject, then the primary key is ObjectId
          var idElement = (XmlElement) classElement.AppendChild(
            xmlDoc.CreateElement("id", NHibernateUri));
          idElement.SetAttribute("name", keyProp.Name);
          idElement.SetAttribute("type", typeof (ObjectIdType).GetAssemblyQualifiedShortName());
          idElement.SetAttribute("unsaved-value", "0");

          var generatorElement = (XmlElement) idElement.AppendChild(
            xmlDoc.CreateElement("generator", NHibernateUri));
          generatorElement.SetAttribute("class", typeof(ObjectIdGenerator).GetAssemblyQualifiedShortName());

          XmlElement paramElement;

          paramElement = (XmlElement)generatorElement.AppendChild(
            xmlDoc.CreateElement("param", NHibernateUri));
          paramElement.AppendChild( xmlDoc.CreateTextNode(entity.Name + "_id"));
          paramElement.SetAttribute("name", "table");

          string idString = entity.EntityId.ToString();
          paramElement = (XmlElement)generatorElement.AppendChild(
            xmlDoc.CreateElement("param", NHibernateUri));
          paramElement.AppendChild( xmlDoc.CreateTextNode(idString));
          paramElement.SetAttribute("name", "entity");
        }
        else
        {
          // For now, assume id is assigned (ideally we would support other generators such
          // as identity and sequence, however this would involve much more work and is not
          // needed at the moment.

          XmlElement idElement = (XmlElement)classElement.AppendChild(
            xmlDoc.CreateElement("id", NHibernateUri));

          MapScalarProperty(idElement, keyProp, null, false);

          XmlElement generatorElement = (XmlElement)idElement.AppendChild(
            xmlDoc.CreateElement("generator", NHibernateUri));
          generatorElement.SetAttribute("class", "assigned");
        }
      }
      else
      {
        // Compound key

        XmlElement idElement = (XmlElement)classElement.AppendChild(xmlDoc.CreateElement("composite-id", NHibernateUri));

        foreach (PropertyMeta keyProp in keyPropList)
        {
          var mopm = keyProp as ManyToOnePropertyMeta;
          if (mopm != null)
          {
            classElement.AppendChild(xmlDoc.CreateElement("key-many-to-one", NHibernateUri));
          }
          else
          {
            idElement.AppendChild(xmlDoc.CreateElement("key-property", NHibernateUri));
          }

          MapScalarProperty(idElement, keyProp, null, false);
        }
      }
    }

    private void MapProperties(XmlElement classElement, ClassMeta entity)
    {
      MapProperties(classElement, entity, null);
    }

    private void MapProperties(XmlElement classElement, ClassMeta entity, string prefix)
    {
      var xmlDoc = classElement.OwnerDocument;

      var mustAllowNull = (entity.BaseEntity != null &&
                           entity.BaseEntity.SubclassMapping == SubclassMappingStrategy.TablePerClassHierarchy);

      foreach (var propMeta in entity.PropertyList)
      {
        if (propMeta.IsPrimaryKey)
        {
          continue;
        }

        if (propMeta.ExtendedData)
          continue;

        // Check if this property belongs to this entity, or to a base entity
        // (this is needed because the entity hierarchy does not match exactly
        // with the class hierarchy)

        if (!entity.IsOwner(propMeta))
        {
          continue;
        }

        if (entity.PrimaryKeyPropertyList != null)
        {
          // Skip over primary key properties (already mapped them)
          foreach (var keyPropMeta in entity.PrimaryKeyPropertyList)
          {
            if (propMeta == keyPropMeta)
            {
              continue;
            }
          }
        }

        var vpm = propMeta as VersionPropertyMeta;
        if (vpm != null)
        {
          var versionElement = (XmlElement) classElement.AppendChild(xmlDoc.CreateElement("version", NHibernateUri));
          versionElement.SetAttribute("name", propMeta.Name);
          versionElement.SetAttribute("unsaved-value", "0");
          continue;
        }

        var ompm = propMeta as OneToManyPropertyMeta;
        if (ompm != null)
        {
          if (ompm.UseJoinTable)
          {
            var collElement =
            (XmlElement)classElement.AppendChild(xmlDoc.CreateElement(ompm.CollectionType, NHibernateUri));
            collElement.SetAttribute("name", ompm.Name);
            collElement.SetAttribute("table", ompm.JoinTableName ?? entity.Name + ompm.Name);
            collElement.SetAttribute("cascade", ompm.Cascade);

            var keyElement = (XmlElement)collElement.AppendChild(
              xmlDoc.CreateElement("key", NHibernateUri));
            keyElement.SetAttribute("column", ompm.KeyColumns[0]);

            if (ompm.CollectionType == "list")
            {
              var indexElement = (XmlElement)collElement.AppendChild(xmlDoc.CreateElement("index", NHibernateUri));
              indexElement.SetAttribute("column", ompm.IndexColumn);
            }
            else if (ompm.CollectionType != "bag")
            {
              throw new NotSupportedException($"CollectionType {ompm.CollectionType} is not supported for one-to-many properties using join table.");
            }

            var oneToManyElement = (XmlElement)collElement.AppendChild(xmlDoc.CreateElement("many-to-many", NHibernateUri));
            oneToManyElement.SetAttribute("class", EntityTypeName(ompm.Clazz));
          }
          else
          {
            var collElement =
              (XmlElement)classElement.AppendChild(xmlDoc.CreateElement(ompm.CollectionType, NHibernateUri));
            collElement.SetAttribute("name", ompm.Name);
            collElement.SetAttribute("inverse", ompm.IsInverse ? "true" : "false");
            collElement.SetAttribute("cascade", ompm.Cascade);

            var keyElement = (XmlElement)collElement.AppendChild(
              xmlDoc.CreateElement("key", NHibernateUri));
            keyElement.SetAttribute("column", ompm.KeyColumns[0]);

            if (ompm.CollectionType == "list")
            {
              var indexElement = (XmlElement)collElement.AppendChild(xmlDoc.CreateElement("index", NHibernateUri));
              indexElement.SetAttribute("column", ompm.IndexColumn);
            }
            else if (ompm.CollectionType == "map")
            {
              var indexElement = (XmlElement)collElement.AppendChild(xmlDoc.CreateElement("index", NHibernateUri));

              indexElement.SetAttribute("column", ompm.IndexColumn);

              string typeStr = null;
              if (ompm.IndexType == typeof(string))
              {
                typeStr = String.Format("String({0})", ompm.IndexMaxLength);
              }
              if (typeStr == null)
              {
                Type mappedIndexType;
                if (ompm.IsNullable)
                {
                  MappedNullableTypes.TryGetValue(ompm.IndexType, out mappedIndexType);
                }
                else
                {
                  MappedTypes.TryGetValue(ompm.IndexType, out mappedIndexType);
                }
                if (mappedIndexType != null)
                {
                  typeStr = mappedIndexType.GetAssemblyQualifiedShortName();
                }
              }
              if (typeStr == null)
              {
                typeStr = CustomPropertyType(ompm.IndexType) ?? ompm.IndexType.GetAssemblyQualifiedShortName();
              }

              indexElement.SetAttribute("type", typeStr);
            }

            var oneToManyElement =
              (XmlElement)collElement.AppendChild(xmlDoc.CreateElement("one-to-many", NHibernateUri));
            oneToManyElement.SetAttribute("class", EntityTypeName(ompm.Clazz));
          }
          continue;
        }

        var mmpm = propMeta as ManyToManyPropertyMeta;
        if (mmpm != null)
        {
          var collElement =
            (XmlElement) classElement.AppendChild(xmlDoc.CreateElement(mmpm.CollectionType, NHibernateUri));
          collElement.SetAttribute("name", mmpm.Name);
          collElement.SetAttribute("table", mmpm.TableName ?? entity.Name + mmpm.Name);
          collElement.SetAttribute("cascade", mmpm.Cascade);

          var keyElement = (XmlElement) collElement.AppendChild(
            xmlDoc.CreateElement("key", NHibernateUri));
          keyElement.SetAttribute("column", mmpm.KeyColumns[0]);

          if (mmpm.CollectionType == "list")
          {
            var indexElement = (XmlElement) collElement.AppendChild(xmlDoc.CreateElement("index", NHibernateUri));
            indexElement.SetAttribute("column", mmpm.IndexColumn);
          }
          else if (mmpm.CollectionType != "bag")
          {
            throw new NotSupportedException(
              String.Format("CollectionType {0} is not supported for many-to-many properties", mmpm.CollectionType));
          }

          var manyToManyElement =
            (XmlElement) collElement.AppendChild(xmlDoc.CreateElement("many-to-many", NHibernateUri));
          manyToManyElement.SetAttribute("class", EntityTypeName(mmpm.Clazz));
          continue;
        }

        var cpm = propMeta as ComponentPropertyMeta;
        if (cpm != null)
        {
          var compElement = (XmlElement) classElement.AppendChild(xmlDoc.CreateElement("component", NHibernateUri));
          compElement.SetAttribute("name", cpm.Name);
          compElement.SetAttribute("class", cpm.PropertyType.GetAssemblyQualifiedShortName());
          MapProperties(compElement, cpm.ChildEntity, cpm.Prefix + "_");
          continue;
        }

        var ccpm = propMeta as ComponentCollectionPropertyMeta;
        if (ccpm != null)
        {
          XmlElement collElement =
            (XmlElement) classElement.AppendChild(xmlDoc.CreateElement(ccpm.CollectionType, NHibernateUri));
          collElement.SetAttribute("name", ccpm.Name);
          collElement.SetAttribute("table", ccpm.TableName);
          collElement.SetAttribute("lazy", "true");

          var keyElement = (XmlElement) collElement.AppendChild(
            xmlDoc.CreateElement("key", NHibernateUri));
          keyElement.SetAttribute("column", ccpm.KeyColumns[0]);

          switch (ccpm.CollectionType)
          {
            case "bag":
            case "list":
            case "map":
              if (ccpm.CollectionType != "bag")
              {
                // Set attributes of index column
                var indexElement = (XmlElement) collElement.AppendChild(xmlDoc.CreateElement("index", NHibernateUri));
                indexElement.SetAttribute("column", ccpm.IndexColumn);
                if (ccpm.CollectionType == "map")
                {
                  // Need to explicitly specify the type of the index column
                  string typeStr = null;
                  if (ccpm.IndexType == typeof (string))
                  {
                    typeStr = String.Format("String({0})", ccpm.IndexMaxLength);
                  }
                  if (typeStr == null)
                  {
                    Type mappedIndexType;
                    if (ccpm.IsNullable)
                    {
                      MappedNullableTypes.TryGetValue(ccpm.IndexType, out mappedIndexType);
                    }
                    else
                    {
                      MappedTypes.TryGetValue(ccpm.IndexType, out mappedIndexType);
                    }
                    if (mappedIndexType != null)
                    {
                      typeStr = mappedIndexType.GetAssemblyQualifiedShortName();
                    }
                  }
                  if (typeStr == null)
                  {
                    typeStr = CustomPropertyType(ccpm.IndexType) ?? ccpm.IndexType.GetAssemblyQualifiedShortName();
                  }

                  indexElement.SetAttribute("type", typeStr);
                }
              }

              var childEntity = ClassCache.Find(ccpm.Clazz);
              var compElement =
                (XmlElement) collElement.AppendChild(xmlDoc.CreateElement("composite-element", NHibernateUri));
              compElement.SetAttribute("class", childEntity.Type.GetAssemblyQualifiedShortName());
              MapProperties(compElement, childEntity);
              break;

            default:
              throw new NotSupportedException(
                String.Format("CollectionType {0} is not supported for component collection properties",
                              ccpm.CollectionType));
          }
          continue;
        }

        var ecpm = propMeta as ElementCollectionPropertyMeta;
        if (ecpm != null)
        {
          if (propMeta.Persistent)
          {
            var collElement =
              (XmlElement) classElement.AppendChild(xmlDoc.CreateElement(ecpm.CollectionType, NHibernateUri));
            collElement.SetAttribute("name", ecpm.Name);
            collElement.SetAttribute("table", ecpm.TableName ?? entity.Name + propMeta.Name);
            collElement.SetAttribute("lazy", "true");

            var keyElement = (XmlElement) collElement.AppendChild(
              xmlDoc.CreateElement("key", NHibernateUri));
            keyElement.SetAttribute("column", ecpm.KeyColumns[0]);

            string typeStr = null;
            if (ecpm.CollectionType == "list")
            {
              var indexElement = (XmlElement) collElement.AppendChild(xmlDoc.CreateElement("index", NHibernateUri));
              indexElement.SetAttribute("column", ecpm.IndexColumn);
            }
            else if (ecpm.CollectionType == "map")
            {
              var indexElement = (XmlElement) collElement.AppendChild(xmlDoc.CreateElement("index", NHibernateUri));
              indexElement.SetAttribute("column", ecpm.IndexColumn);

              if (ecpm.IndexType == typeof (string))
              {
                typeStr = String.Format("String({0})", ecpm.IndexMaxLength);
              }
              if (typeStr == null)
              {
                Type mappedIndexType;
                if (ecpm.IsNullable)
                {
                  MappedNullableTypes.TryGetValue(ecpm.IndexType, out mappedIndexType);
                }
                else
                {
                  MappedTypes.TryGetValue(ecpm.IndexType, out mappedIndexType);
                }
                if (mappedIndexType != null)
                {
                  typeStr = mappedIndexType.GetAssemblyQualifiedShortName();
                }
              }
              if (typeStr == null)
              {
                typeStr = CustomPropertyType(ecpm.IndexType) ?? ecpm.IndexType.GetAssemblyQualifiedShortName();
              }

              indexElement.SetAttribute("type", typeStr);
            }

            var elementElement = (XmlElement) collElement.AppendChild(xmlDoc.CreateElement("element", NHibernateUri));
            elementElement.SetAttribute("column", ecpm.ElementColumn);

            typeStr = null;
            if (ecpm.ElementType == typeof (string))
            {
              typeStr = String.Format("String({0})", ecpm.ElementMaxLength);
            }
            if (typeStr == null)
            {
              Type mappedElementType;
              if (ecpm.IsNullable)
              {
                MappedNullableTypes.TryGetValue(ecpm.ElementType, out mappedElementType);
              }
              else
              {
                MappedTypes.TryGetValue(ecpm.ElementType, out mappedElementType);
              }
              if (mappedElementType != null)
              {
                typeStr = mappedElementType.GetAssemblyQualifiedShortName();
              }
            }
            if (typeStr == null)
            {
              typeStr = CustomPropertyType(ecpm.ElementType) ?? ecpm.ElementType.GetAssemblyQualifiedShortName();
            }

            elementElement.SetAttribute("type", typeStr);
          }
          else
          {
            Logger.DebugFormat("Skipping transient property {0}.{1}", entity.Name, propMeta.Name);
          }
          continue;
        }

        var mopm = propMeta as ManyToOnePropertyMeta;
        if (mopm != null)
        {
          var manyToOneElement = (XmlElement)classElement.AppendChild(xmlDoc.CreateElement("many-to-one", NHibernateUri));
          MapScalarProperty(manyToOneElement, propMeta, prefix, mustAllowNull);
          continue;
        }

        var oopm = propMeta as OneToOnePropertyMeta;
        if (oopm != null)
        {
          var manyToOneElement = (XmlElement) classElement.AppendChild(xmlDoc.CreateElement("many-to-one", NHibernateUri));
          MapScalarProperty(manyToOneElement, propMeta, prefix, mustAllowNull);
          continue;
        }

        if (propMeta.Persistent)
        {
          var propertyElement = (XmlElement) classElement.AppendChild(xmlDoc.CreateElement("property", NHibernateUri));
          MapScalarProperty(propertyElement, propMeta, prefix, mustAllowNull);
        }
        else
        {
          Logger.DebugFormat("Skipping transient property {0}.{1}", entity.Name, propMeta.Name);
        }
      }
    }

    /// <summary>
    /// Map a scalar property (that is, one that maps to a column in a table).
    /// </summary>
    /// <param name="ownerElement">Element these attributes apply to (could be property, id, or composite-it)</param>
    /// <param name="propMeta"></param>
    /// <param name="prefix"></param>
    /// <param name="mustAllowNull"></param>
    private void MapScalarProperty(XmlElement ownerElement, PropertyMeta propMeta, string prefix, bool mustAllowNull)
    {
      ownerElement.SetAttribute("name", propMeta.Name);

      MapScalarPropertyMeta(ownerElement, propMeta, prefix, mustAllowNull);

      string columnName = ColumnName(prefix, propMeta.Column);
      if (columnName != propMeta.Name)
      {
        ownerElement.SetAttribute("column", columnName);
      }

      if (!propMeta.IsNullable && !mustAllowNull)
      {
        ownerElement.SetAttribute("not-null", "true");
      }

      if (propMeta.IsUnique)
      {
        // We do not add the unique keyword for key columns, as this
        // would cause the schema generator to add a unique constraint,
        // and we handle unique constraints for key columns ourselves.
        if (!propMeta.IsKey)
        {
          ownerElement.SetAttribute("unique", "true");
        }
      }
    }

    private void MapScalarPropertyMeta(XmlElement ownerElement, PropertyMeta propMeta, string prefix, bool mustAllowNull)
    {
      propMeta.MapScalar(ownerElement, prefix, mustAllowNull);
    }

    private XmlElement AddExtendedDataProperty(XmlElement classElement, ClassMeta entity)
    {
      Logger.DebugFormat("Adding ExtendedData property to {0}", entity.Name);

      XmlDocument xmlDoc = classElement.OwnerDocument;
      var propElement = (XmlElement) classElement.AppendChild(
        xmlDoc.CreateElement("property", NHibernateUri));

      propElement.SetAttribute("name", entity.Name + "ExtendedData");
      propElement.SetAttribute("column", ColumnName(null, "ExtendedData"));
      propElement.SetAttribute("type", typeof(ExtendedDataType).GetAssemblyQualifiedShortName());
      propElement.SetAttribute("access", typeof(ExtendedDataPropertyAccessor).AssemblyQualifiedName);

      return propElement;
    }

    private static string CustomPropertyType(Type propertyType)
    {
      if (propertyType == typeof(double?)) return typeof(double?).GetAssemblyQualifiedShortName();
      else if (propertyType == typeof(int?)) return typeof(int?).GetAssemblyQualifiedShortName();
      else if (propertyType == typeof(long?)) return typeof(long?).GetAssemblyQualifiedShortName();
      return null;
    }

    private static XmlDocument GetOwnerDocument(XmlElement element)
    {
      return element.OwnerDocument;
    }
  }
}