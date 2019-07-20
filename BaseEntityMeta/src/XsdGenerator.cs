// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// A generator of XML Schema for the WebMathTraining object model.
  /// Not thread-safe.
  /// </summary>
  public class XsdGenerator
  {
    private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(XsdGenerator));

    private static int _keyId;
    private static readonly IXmlDocProvider XmlDocProvider = new XmlDocProvider();
    private static bool _annotate;
    private static XmlSchemaChoice _rootChoice;

    /// <summary>
    /// The WebMathTraining XML Schema namespace
    /// </summary>
    public const string WebMathTrainingSchemaNamespace = "http://WebMathTrainingsolutions.com/Schema";

    /// <summary>
    /// The WebMathTraining XML Sparse Schema namespace.
    /// Used for import of partial data.
    /// </summary>
    public const string WebMathTrainingSparseSchemaNamespace = "http://WebMathTrainingsolutions.com/Schema-Sparse";

    private static readonly Dictionary<Type, Action<XmlSchema, XmlSchemaElement, int, bool>> XmlSchemaSimpleTypeGenerators =
      new Dictionary<Type, Action<XmlSchema, XmlSchemaElement, int, bool>>();

    /// <summary>
    /// The XML Schema Namespace
    /// </summary>
    public const string XmlSchemaNs = "http://www.w3.org/2001/XMLSchema";

    /// <summary>
    /// Initializes a new instance of the <see cref="XsdGenerator"/> class.
    /// </summary>
    public XsdGenerator()
      : this(ClassCache.FindAll())
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="XsdGenerator"/> class.
    /// </summary>
    /// <param name="entityList">The entity list.</param>
    public XsdGenerator(IEnumerable<ClassMeta> entityList)
    {
      foreach (var cm in entityList)
      {
        _classMetaList.Add(cm);
        _classMetaCacheByName.Add(cm.Name, cm);
      }

      XmlSchemaSimpleTypeGenerators[typeof(string)] = SetStringXmlSchemaType;
      XmlSchemaSimpleTypeGenerators[typeof(DateTime)] = SetDateTimeXmlSchemaType;
      XmlSchemaSimpleTypeGenerators[typeof(bool)] = SetBooleanXmlSchemaType;
      XmlSchemaSimpleTypeGenerators[typeof(double)] = SetDoubleXmlSchemaType;
      XmlSchemaSimpleTypeGenerators[typeof(int)] = SetIntXmlSchemaType;
      XmlSchemaSimpleTypeGenerators[typeof(long)] = SetLongXmlSchemaType;
    }

    /// <summary>
    /// Registers an XML schema generator for a simple type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="generator">The generator.</param>
    public static void RegisterXmlSchemaSimpleTypeGenerator(Type type, Action<XmlSchema, XmlSchemaElement, int, bool> generator)
    {
      XmlSchemaSimpleTypeGenerators[type] = generator;
    }

    /// <summary>
    /// Generates schema for everything.
    /// </summary>
    /// <returns></returns>
    public XmlSchema Generate()
    {
      return Generate(_classMetaList.Select(c => c.Name));
    }

    /// <summary>
    /// Generates schema for the specified entity name.
    /// </summary>
    /// <param name="entityName">Name of the entity.</param>
    /// <returns>
    /// XmlDocument
    /// </returns>
    public XmlSchema Generate(string entityName)
    {
      return Generate(new List<string>(new[] { entityName }));
    }

    /// <summary>
    /// Generates schema for the specified entity names.
    /// </summary>
    /// <param name="entityNames">The entity names.</param>
    /// <param name="annotate">if set to <c>true</c> [annotate].</param>
    /// <param name="isSparseMode"></param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentException">Invalid ClassMeta [ + entityName + ]</exception>
    public XmlSchema Generate(IEnumerable<string> entityNames, bool annotate = true, bool isSparseMode = false)
    {
      _annotate = annotate;
      _keyId = 0;

      /*
        <xs:schema xmlns:q="_qns_" 
                   elementFormDefault="qualified" 
                   targetNamespace="_qns_" 
                   xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:element name="WebMathTrainingdata">
            <xs:complexType>
              <xs:choice minOccurs="1" maxOccurs="unbounded">
                ...
              </xs:choice>
              <xs:attribute name="ver" type="xs:string" />
              <xs:attribute name="time" type="xs:dateTime" />
            </xs:complexType>
          </xs:element>
        </xs:schema>
      */
      var xmlSchema = new XmlSchema
      {
        TargetNamespace = WebMathTrainingSchemaNamespace
      };
      xmlSchema.Namespaces.Add("q", 
        isSparseMode 
        ? WebMathTrainingSparseSchemaNamespace
        : WebMathTrainingSchemaNamespace);
      xmlSchema.ElementFormDefault = XmlSchemaForm.Qualified;

      var WebMathTrainingdata = new XmlSchemaElement();
      xmlSchema.Items.Add(WebMathTrainingdata);

      WebMathTrainingdata.Name = "WebMathTrainingdata";

      var annotation = new XmlSchemaAnnotation();
      if (_annotate)
      {
        var ver = $"Version {Assembly.GetExecutingAssembly().GetName().Version}";
        Annotate(WebMathTrainingdata, annotation, new XmlDocument().CreateTextNode(ver.Trim()));
      }

      var WebMathTrainingDataComplexType = new XmlSchemaComplexType();
      WebMathTrainingdata.SchemaType = WebMathTrainingDataComplexType;

      var verAttribute = new XmlSchemaAttribute
      {
        Name = "ver",
        SchemaTypeName = new XmlQualifiedName("string", XmlSchemaNs)
      };
      WebMathTrainingDataComplexType.Attributes.Add(verAttribute);

      var timeAttribute = new XmlSchemaAttribute
      {
        Name = "time",
        SchemaTypeName = new XmlQualifiedName("dateTime", XmlSchemaNs)
      };
      WebMathTrainingDataComplexType.Attributes.Add(timeAttribute);

      _rootChoice = new XmlSchemaChoice();
      WebMathTrainingDataComplexType.Particle = _rootChoice;
      _rootChoice.MinOccurs = 1;
      _rootChoice.MaxOccursString = "unbounded";

      foreach (string entityName in entityNames.OrderBy(_ => _))
      {
        ClassMeta cm;
        if (!_classMetaCacheByName.TryGetValue(entityName, out cm))
        {
          throw new ArgumentException("Invalid ClassMeta [" + entityName + "]");
        }

        if (_annotate)
        {
          AnnotateAppInfo(annotation, cm);
        }

        MapClass(xmlSchema, cm, isSparseMode);
      }

      return xmlSchema;
    }

    /// <summary>
    /// Maps a class.
    /// </summary>
    /// <param name="xmlSchema">The XML schema.</param>
    /// <param name="cm">The cm.</param>
    /// <param name="includeDerived">if set to <c>true</c> [include derived].</param>
    public static void MapClass(XmlSchema xmlSchema, ClassMeta cm, bool includeDerived = true)
    {
      if (xmlSchema.Items.OfType<XmlSchemaComplexType>().Any(c => c.Name == cm.Name))
      {
        return;
      }

      if (cm.IsEntity)
      {
        if (cm.IsBaseEntity)
        {
          MapBaseClass(xmlSchema, cm, includeDerived);
        }
        else if (cm.IsStandaloneEntity)
        {
          MapStandaloneClass(xmlSchema, cm);
        }
        else if (cm.IsDerivedEntity)
        {
          MapDerivedClass(xmlSchema, cm);
        }
      }
      else
      {
        MapStandaloneClass(xmlSchema, cm);
      }
    }

    private static void AddRootChoice(ClassMeta entity)
    {
      if (!entity.IsAbstract && !entity.IsComponent && !entity.IsChildEntity)
      {
        var classElement = new XmlSchemaElement
        {
          Name = entity.Name,
          SchemaTypeName = new XmlQualifiedName(entity.Name, WebMathTrainingSchemaNamespace),
          MinOccurs = 0,
          MaxOccursString = "unbounded"
        };
        _rootChoice.Items.Add(classElement);
      }
    }

    private static void MapBaseClass(XmlSchema xmlSchema, ClassMeta entity, bool includeDerived = true)
    {
      MapStandaloneClass(xmlSchema, entity);
      if (includeDerived)
      {
        foreach (var derivedEntity in ClassCache.FindAll().Where(d => d.BaseEntity == entity))
        {
          MapClass(xmlSchema, derivedEntity);
        }
      }
    }

    private static void MapStandaloneClass(XmlSchema xmlSchema, ClassMeta entity)
    {
      /*
        <xs:complexType name="_class_">
          <xs:sequence>
            _properties_
          </xs:sequence>
        </xs:complexType>
      */
      var entityComplexType = new XmlSchemaComplexType
      {
        Name = entity.Name
      };

      if (_annotate)
      {
        Annotate(entityComplexType, entity.Type);
      }

      var sequence = new XmlSchemaSequence();
      entityComplexType.Particle = sequence;

      MapProperties(xmlSchema, sequence, entity);

      xmlSchema.Items.Add(entityComplexType);

      AddRootChoice(entity);
    }

    private static void MapDerivedClass(XmlSchema xmlSchema, ClassMeta entity)
    {
      /*
        <xs:complexType name="_derived_">
          <xs:complexContent mixed="false">
            <xs:extension base="q:_base_">
              <xs:sequence>
                _properties_
              </xs:sequence>
            </xs:extension>
          </xs:complexContent>
        </xs:complexType>
      */
      var entityComplexType = new XmlSchemaComplexType
      {
        Name = entity.Name
      };

      if (_annotate)
      {
        Annotate(entityComplexType, entity.Type);
      }

      var content = new XmlSchemaComplexContent();
      entityComplexType.ContentModel = content;

      var extension = new XmlSchemaComplexContentExtension();
      content.Content = extension;
      extension.BaseTypeName = new XmlQualifiedName(entity.BaseEntity.Name, WebMathTrainingSchemaNamespace);

      var sequence = new XmlSchemaSequence();
      extension.Particle = sequence;

      MapProperties(xmlSchema, sequence, entity);

      xmlSchema.Items.Add(entityComplexType);

      MapClass(xmlSchema, entity.BaseEntity, false);

      AddRootChoice(entity);
    }

    private static void MapProperties(XmlSchema xmlSchema, XmlSchemaGroupBase group, ClassMeta entity)
    {
      var properties = entity.PropertyList;

      // polymorphic relations whose ownership is resolved at runtime must come last,
      // since they're exposed in schema on the derived type
      var ownershipResolverMetas = properties.OfType<ManyToOnePropertyMeta>().Where(mopm => mopm.OwnershipResolver != null).ToList();
      var orderedProperties = properties.Except(ownershipResolverMetas).Concat(ownershipResolverMetas);
      properties = orderedProperties.ToList();

      foreach (var propMeta in properties)
      {
        if (propMeta.IsPrimaryKey)
        {
          continue;
        }

        if (!propMeta.Persistent)
        {
          Log.DebugFormat("Skipping transient property {0}.{1}", entity.Name, propMeta.Name);
          continue;
        }

        // dynamically-resolved ownerships cannot be handled by the base class mapping
        bool dynamicOwnership = false;
        var dmopm = propMeta as ManyToOnePropertyMeta;
        if (dmopm?.OwnershipResolver != null)
        {
          dynamicOwnership = true;
        }

        if (!dynamicOwnership && !entity.IsOwner(propMeta) || dynamicOwnership && entity.IsAbstract)
        {
          // base element will deal with it
          continue;
        }

        if (propMeta.Name == "LastUpdated" || propMeta.Name == "UpdatedBy")
        {
          continue;
        }

        if (propMeta.Name == "ValidFrom")
        {
          if (entity.OldStyleValidFrom)
          {
            if (entity.KeyPropertyNames != null && entity.KeyPropertyNames.Contains("ValidFrom") ||
                entity.ChildKeyPropertyNames != null && entity.ChildKeyPropertyNames.Contains("ValidFrom"))
            {
              MapScalarProperty(xmlSchema, group, (ScalarPropertyMeta)propMeta);
            }
          }
          continue;
        }

        var vpm = propMeta as VersionPropertyMeta;
        if (vpm != null)
        {
          continue;
        }

        var ompm = propMeta as OneToManyPropertyMeta;
        if (ompm != null)
        {
          MapCollectionProperty(xmlSchema, group, ompm);
          continue;
        }

        var mmpm = propMeta as ManyToManyPropertyMeta;
        if (mmpm != null)
        {
          if (mmpm.CollectionType == "list" || mmpm.CollectionType == "bag")
          {
            MapCollectionProperty(xmlSchema, group, mmpm);
          }
          else
          {
            throw new NotSupportedException(
              $"CollectionType {mmpm.CollectionType} is not supported for many-to-many properties");
          }
          continue;
        }

        var cpm = propMeta as ComponentPropertyMeta;
        if (cpm != null)
        {
          MapComponentProperty(xmlSchema, group, cpm);
          continue;
        }

        var ccpm = propMeta as ComponentCollectionPropertyMeta;
        if (ccpm != null)
        {
          MapCollectionProperty(xmlSchema, group, ccpm);
          continue;
        }

        var ecpm = propMeta as ElementCollectionPropertyMeta;
        if (ecpm != null)
        {
          MapCollectionProperty(xmlSchema, group, ecpm);
          continue;
        }

        var scalarPropMeta = propMeta as ScalarPropertyMeta;
        if (scalarPropMeta != null)
        {
          MapScalarProperty(xmlSchema, group, scalarPropMeta);
          continue;
        }

        throw new NotSupportedException($"Property {propMeta.Name} is not supported.");

      }
    }

    /// <summary>
    /// Determines whether is sparse schema mode.
    /// </summary>
    public static bool IsSparseSchemaMode(XmlSchema xmlSchema)
    {
      if (xmlSchema.Namespaces.ToArray()
          .Any(n => string.Compare(n.Namespace, WebMathTrainingSparseSchemaNamespace, StringComparison.Ordinal) == 0))
        return true;

      return false;
    }


    private static void MapComponentProperty(XmlSchema xmlSchema, XmlSchemaGroupBase group, ComponentPropertyMeta propMeta)
    {
      /*
        <xs:element minOccurs="0" maxOccurs="1" name="_property_">
          ...
        </xs:element>
      */

      var propertyElement = new XmlSchemaElement
      {
        Name = propMeta.Name,
        MinOccurs = IsSparseSchemaMode(xmlSchema) && !propMeta.IsKey || propMeta.IsNullable ? 0 : 1,
        MaxOccurs = 1
      };

      if (_annotate)
      {
        Annotate(propertyElement, propMeta.PropertyInfo);
      }

      var include = MapComponentPropertyMeta(xmlSchema, propertyElement, propMeta);
      if (include)
      {
        group.Items.Add(propertyElement);
      }
    }

    private static bool MapComponentPropertyMeta(XmlSchema xmlSchema,
                                                 XmlSchemaElement element,
                                                 ComponentPropertyMeta propMeta)
    {
      /*
        <xs:complexType>
          <xs:sequence>
            <xs:element minOccurs="0" maxOccurs="1" name="_class_" type="q:_class_" />
          </xs:sequence>
        </xs:complexType>
      */
      var refClassMeta = ClassCache.Find(propMeta.PropertyType);
      var complexType = new XmlSchemaComplexType();
      element.SchemaType = complexType;
      var sequence = new XmlSchemaSequence();
      complexType.Particle = sequence;
      var refElement = new XmlSchemaElement();
      sequence.Items.Add(refElement);
      refElement.MinOccurs = IsSparseSchemaMode(xmlSchema) && !propMeta.IsKey || propMeta.IsNullable ? 0 : 1;
      refElement.MaxOccurs = 1;
      refElement.Name = refClassMeta.Name;
      refElement.SchemaTypeName = new XmlQualifiedName(refClassMeta.Name, WebMathTrainingSchemaNamespace);

      // We don't map component class hierarchy exactly
      MapClass(xmlSchema, refClassMeta);
      return true;
    }

    private static void MapCollectionProperty(XmlSchema xmlSchema, XmlSchemaGroupBase group,
      CollectionPropertyMeta propMeta)
    {
      /*
        <xs:element minOccurs="0" maxOccurs="1" name="_property_">
          _children_
        </xs:element>
      */
      var propertyElement = new XmlSchemaElement
      {
        Name = propMeta.Name,
        MinOccurs = 0,
        MaxOccurs = 1
      };

      if (_annotate)
      {
        Annotate(propertyElement, propMeta.PropertyInfo);
      }

      MapCollectionPropertyMeta(xmlSchema, propertyElement, propMeta);

      group.Items.Add(propertyElement);
    }

    private static void MapScalarProperty(XmlSchema xmlSchema, XmlSchemaGroupBase group, ScalarPropertyMeta propMeta)
    {
      /*
        <xs:element minOccurs="1" maxOccurs="1" name="_property_" ...>
          ...
        </xs:element>
      */
      var propertyElement = new XmlSchemaElement
      {
        Name = propMeta.Name,
        MinOccurs = IsSparseSchemaMode(xmlSchema) && !propMeta.IsKey || propMeta.IsNullable ? 0 : 1,
        MaxOccurs = 1
      };

      if (_annotate)
      {
        Annotate(propertyElement, propMeta.PropertyInfo);
      }

      var include = MapScalarPropertyMeta(xmlSchema, propertyElement, propMeta);
      if (include)
      {
        group.Items.Add(propertyElement);
      }
    }

    /// <summary>
    /// Sets the type of an element to an XML schema simpleType.
    /// </summary>
    /// <param name="xmlSchema">The XML schema.</param>
    /// <param name="element">The element.</param>
    /// <param name="type">The type.</param>
    /// <param name="maxLength">The maximum length.</param>
    /// <param name="isKey"></param>
    /// <exception cref="System.NotSupportedException"></exception>
    public static void SetXmlSchemaSimpleType(XmlSchema xmlSchema, XmlSchemaElement element, Type type, int maxLength = 0, bool isKey = false)
    {
      if (type.IsEnum)
      {
        SetEnumXmlSchemaType(xmlSchema, element, type);
      }
      else
      {
        Action<XmlSchema, XmlSchemaElement, int, bool> generator;
        if (!XmlSchemaSimpleTypeGenerators.TryGetValue(type, out generator))
        {
          throw new NotSupportedException($"No registered XML Schema simple type generator for type: {type}");
        }
        generator.Invoke(xmlSchema, element, maxLength, isKey);
      }
    }

    private void SetLongXmlSchemaType(XmlSchema xmlSchema, XmlSchemaElement element, int maxLength, bool isKey)
    {
      /*
          <xs:element ... type="xs:long" />
        */
      element.SchemaTypeName = new XmlQualifiedName("long", XmlSchemaNs);
    }

    private void SetIntXmlSchemaType(XmlSchema xmlSchema, XmlSchemaElement element, int maxLength, bool isKey)
    {
      /*
        <xs:element ... type="xs:int" />
      */
      element.SchemaTypeName = new XmlQualifiedName("int", XmlSchemaNs);
    }

    private void SetDoubleXmlSchemaType(XmlSchema xmlSchema, XmlSchemaElement element, int maxLength, bool isKey)
    {
      /*
        <xs:element ... type="xs:double" />
      */
      element.SchemaTypeName = new XmlQualifiedName("double", XmlSchemaNs);
    }

    private void SetBooleanXmlSchemaType(XmlSchema xmlSchema, XmlSchemaElement element, int maxLength, bool isKey)
    {
      /*
        <xs:element ... type="xs:boolean" />
      */
      element.SchemaTypeName = new XmlQualifiedName("boolean", XmlSchemaNs);
    }

    private void SetDateTimeXmlSchemaType(XmlSchema xmlSchema, XmlSchemaElement element, int maxLength, bool isKey)
    {
      /*
        <xs:element ... type="xs:dateTime" />
      */
      element.SchemaTypeName = new XmlQualifiedName("dateTime", XmlSchemaNs);
    }

    private void SetStringXmlSchemaType(XmlSchema xmlSchema, XmlSchemaElement element, int maxLength, bool isKey)
    {
      if (maxLength > 0)
      {
        /*
          <xs:element ...>
             <xs:simpleType>
              <xs:restriction base="xs:string">
                <xs:maxLength value="n" />
              </xs:restriction>
            </xs:simpleType>
          </xs:element>
        */
        var simpleType = new XmlSchemaSimpleType();
        element.SchemaType = simpleType;
        var restriction = new XmlSchemaSimpleTypeRestriction();
        simpleType.Content = restriction;
        restriction.BaseTypeName = new XmlQualifiedName("string", XmlSchemaNs);
        var lengthFacet = new XmlSchemaMaxLengthFacet();
        restriction.Facets.Add(lengthFacet);
        lengthFacet.Value = maxLength.ToString(CultureInfo.InvariantCulture);

        if (isKey)
        {
          var minLengthFacet = new XmlSchemaMinLengthFacet();
          restriction.Facets.Add(minLengthFacet);
          minLengthFacet.Value = 1.ToString(CultureInfo.InvariantCulture);
        }
      }
      else
      {
        /*
          <xs:element ... type="xs:string" />
        */
        element.SchemaTypeName = new XmlQualifiedName("string", XmlSchemaNs);
      }
    }

    private static string EnumName(Type type)
    {
      return $"e{type.Name}";
    }

    /// <summary>
    /// Logic copied from NHibernate
    /// </summary>
    private static bool IsFlagEnumOrNullableFlagEnum(Type type)
    {
      if (type == null)
      {
        return false;
      }
      Type typeofEnum = type;
      if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
      {
        typeofEnum = type.GetGenericArguments()[0];
      }
      return typeofEnum.IsEnum && typeofEnum.GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0;
    }

    /// <summary>
    /// Sets the type of the enum XML schema.
    /// </summary>
    /// <param name="xmlSchema">The XML schema.</param>
    /// <param name="element">The element.</param>
    /// <param name="type">The type.</param>
    public static void SetEnumXmlSchemaType(XmlSchema xmlSchema, XmlSchemaElement element, Type type)
    {
      if (IsFlagEnumOrNullableFlagEnum(type))
      {
        var enumSimpleType = new XmlSchemaSimpleType();
        element.SchemaType = enumSimpleType;
        var enumSimpleTypeList = new XmlSchemaSimpleTypeList();
        enumSimpleType.Content = enumSimpleTypeList;
        enumSimpleTypeList.ItemTypeName = new XmlQualifiedName(EnumName(type), WebMathTrainingSchemaNamespace);
      }
      else
      {
        element.SchemaTypeName = new XmlQualifiedName(EnumName(type), WebMathTrainingSchemaNamespace);
      }

      MapEnum(xmlSchema, type);
    }

    private static void MapEnum(XmlSchema xmlSchema, Type type)
    {
      if (!type.IsEnum)
      {
        throw new ArgumentException(@"Type must be an Enum", nameof(type));
      }

      if (xmlSchema.Items.OfType<XmlSchemaSimpleType>().Any(c => c.Name == EnumName(type)))
      {
        return;
      }

      var enumSimpleType = new XmlSchemaSimpleType();

      if (_annotate)
      {
        Annotate(enumSimpleType, type);
      }

      var restriction = new XmlSchemaSimpleTypeRestriction();
      enumSimpleType.Content = restriction;
      enumSimpleType.Name = EnumName(type);
      restriction.BaseTypeName = new XmlQualifiedName("string", XmlSchemaNs);
      foreach (var enumValue in Enum.GetNames(type))
      {
        var enumeration = new XmlSchemaEnumerationFacet();

        if (_annotate)
        {
          Annotate(enumeration, type.GetField(enumValue));
        }

        restriction.Facets.Add(enumeration);
        enumeration.Value = enumValue;
      }
      xmlSchema.Items.Add(enumSimpleType);
    }

    private static void MapCollectionPropertyMeta(XmlSchema xmlSchema,
                                                  XmlSchemaElement element,
                                                  CollectionPropertyMeta propMeta)
    {
      var ompm = propMeta as OneToManyPropertyMeta;
      if (ompm != null)
      {
        /*
          <xs:element minOccurs="0" maxOccurs="1" name="_property_">
            <xs:complexType>
              <xs:sequence>
                <xs:element minOccurs="0" maxOccurs="unbounded" name="_childclass_">
                  <xs:complexType>
                    <xs:sequence>
                      ...
                    </xs:sequence>
                  </xs:complexType>
                </xs:element>
              </xs:sequence>
            </xs:complexType>
          </xs:element>
        */
        bool isOwned = !ompm.Cascade.Equals("none");
        if (isOwned)
        {
          var collectionComplexType = new XmlSchemaComplexType();
          element.SchemaType = collectionComplexType;
          var sequence = new XmlSchemaSequence();
          collectionComplexType.Particle = sequence;
          var choice = new XmlSchemaChoice();
          sequence.Items.Add(choice);
          choice.MinOccurs = 0;
          choice.MaxOccursString = "unbounded";

          var knownItemClassMetas = ClassCache.FindAll().Where(cm => cm.IsA(ompm.Clazz) && !cm.IsAbstract);
          foreach (var itemClassMeta in knownItemClassMetas)
          {
            var itemElement = new XmlSchemaElement();
            choice.Items.Add(itemElement);
            itemElement.Name = itemClassMeta.Name;
            itemElement.SchemaTypeName = new XmlQualifiedName(itemClassMeta.Name, WebMathTrainingSchemaNamespace);

            MapClass(xmlSchema, itemClassMeta);
          }
        }
        else
        {
          var refClassMeta = ClassCache.Find(ompm.Clazz);
          var collectionComplexType = new XmlSchemaComplexType();
          element.SchemaType = collectionComplexType;
          var sequence = new XmlSchemaSequence();
          collectionComplexType.Particle = sequence;
          var refElement = new XmlSchemaElement();
          sequence.Items.Add(refElement);
          refElement.Name = refClassMeta.Name;
          refElement.MinOccurs = 0;
          refElement.MaxOccursString = "unbounded";

          SetKeyXmlSchemaType(xmlSchema, refElement, refClassMeta);
        }

        return;
      }

      var ccpm = propMeta as ComponentCollectionPropertyMeta;
      if (ccpm != null)
      {
        if (ccpm.CollectionType == "map")
        {
          /*
           <xs:complexType>
            <xs:sequence>
              <xs:element minOccurs="0" maxOccurs="unbounded" name="Item">
                <xs:complexType>
                  <xs:sequence>
                    <xs:element minOccurs="1" maxOccurs="1" name="Key" type="xs:string" />
                    <xs:element minOccurs="1" maxOccurs="1" name="Value">
                      <xs:complexType>
                        <xs:sequence>
                          <xs:choice minOccurs="0" maxOccurs="1">
                            <xs:element name="TypeName" type="q:TypeName" />
                             ...
                          </xs:choice>
                        </xs:sequence>
                      </xs:complexType>
                    </xs:element>
                  </xs:sequence>
                </xs:complexType>
              </xs:element>
            </xs:sequence>
          </xs:complexType>
           */

          var collectionComplexType = new XmlSchemaComplexType();
          element.SchemaType = collectionComplexType;
          var sequence = new XmlSchemaSequence();
          collectionComplexType.Particle = sequence;
          var itemElement = new XmlSchemaElement();
          sequence.Items.Add(itemElement);
          itemElement.Name = "Item";
          itemElement.MinOccurs = 0;
          itemElement.MaxOccursString = "unbounded";
          var itemComplexType = new XmlSchemaComplexType();
          itemElement.SchemaType = itemComplexType;
          var itemSequence = new XmlSchemaSequence();
          itemComplexType.Particle = itemSequence;
          var keyElement = new XmlSchemaElement();
          itemSequence.Items.Add(keyElement);
          keyElement.Name = "Key";
          keyElement.MinOccurs = 1;
          keyElement.MaxOccurs = 1;

          SetXmlSchemaSimpleType(xmlSchema, keyElement, ccpm.IndexType, ccpm.IndexMaxLength);

          var valueElement = new XmlSchemaElement();
          itemSequence.Items.Add(valueElement);
          valueElement.Name = "Value";
          valueElement.MinOccurs = 1;
          valueElement.MaxOccurs = 1;
          var valueComplexType = new XmlSchemaComplexType();
          valueElement.SchemaType = valueComplexType;
          var valueSequence = new XmlSchemaSequence();
          valueComplexType.Particle = valueSequence;

          var choice = new XmlSchemaChoice();
          valueSequence.Items.Add(choice);
          choice.MinOccurs = 0;
          choice.MaxOccurs = 1;

          var knownItemClassMetas = ClassCache.FindAll().Where(cm => cm.IsA(ccpm.Clazz) && !cm.IsAbstract);
          foreach (var itemClassMeta in knownItemClassMetas)
          {
            var componentElement = new XmlSchemaElement();
            choice.Items.Add(componentElement);
            componentElement.Name = itemClassMeta.Name;
            componentElement.SchemaTypeName = new XmlQualifiedName(itemClassMeta.Name, WebMathTrainingSchemaNamespace);

            MapClass(xmlSchema, itemClassMeta);
          }

          var unique = new XmlSchemaUnique();
          element.Constraints.Add(unique);
          unique.Name = $"key_{_keyId++}";
          var keyPath = new XmlSchemaXPath();
          unique.Selector = keyPath;
          keyPath.XPath = "q:Item";
          keyPath.Namespaces.Add("q", WebMathTrainingSchemaNamespace);
          var fieldPath = new XmlSchemaXPath();
          unique.Fields.Add(fieldPath);
          fieldPath.XPath = "q:Key";
          fieldPath.Namespaces.Add("q", WebMathTrainingSchemaNamespace);
        }
        else
        {
          /*
            <xs:element minOccurs="0" maxOccurs="1" name="_property_">
              <xs:complexType>
                <xs:sequence>
                  <xs:choice minOccurs="0" maxOccurs="unbounded">
                    <xs:element name="_childclass_" type="q:_childclass_" />
                  </xs:choice>
                </xs:sequence>
              </xs:complexType>
            </xs:element>
          */
          var collectionComplexType = new XmlSchemaComplexType();
          element.SchemaType = collectionComplexType;
          var sequence = new XmlSchemaSequence();
          collectionComplexType.Particle = sequence;
          var choice = new XmlSchemaChoice();
          sequence.Items.Add(choice);
          choice.MinOccurs = 0;
          choice.MaxOccursString = "unbounded";

          var knownItemClassMetas = ClassCache.FindAll().Where(cm => cm.IsA(ccpm.Clazz) && !cm.IsAbstract);
          foreach (var itemClassMeta in knownItemClassMetas)
          {
            var itemElement = new XmlSchemaElement();
            choice.Items.Add(itemElement);
            itemElement.Name = itemClassMeta.Name;
            itemElement.SchemaTypeName = new XmlQualifiedName(itemClassMeta.Name, WebMathTrainingSchemaNamespace);

            MapClass(xmlSchema, itemClassMeta);
          }
        }
        return;
      }

      var mmpm = propMeta as ManyToManyPropertyMeta;
      if (mmpm != null)
      {
        /*
          <xs:element minOccurs="0" maxOccurs="1" name="_property_">
            <xs:complexType>
              <xs:sequence>
                <xs:element minOccurs="0" maxOccurs="unbounded" name="_childclass_">
                  <xs:complexType>
                    <xs:sequence>
                      ...
                    </xs:sequence>
                  </xs:complexType>
                </xs:element>
              </xs:sequence>
            </xs:complexType>
          </xs:element>
        */
        bool isOwned = !mmpm.Cascade.Equals("none");
        if (isOwned)
        {
          var collectionComplexType = new XmlSchemaComplexType();
          element.SchemaType = collectionComplexType;
          var sequence = new XmlSchemaSequence();
          collectionComplexType.Particle = sequence;
          var choice = new XmlSchemaChoice();
          sequence.Items.Add(choice);
          choice.MinOccurs = 0;
          choice.MaxOccursString = "unbounded";

          var knownItemClassMetas = ClassCache.FindAll().Where(cm => cm.IsA(mmpm.Clazz) && !cm.IsAbstract);
          foreach (var itemClassMeta in knownItemClassMetas)
          {
            var itemElement = new XmlSchemaElement();
            choice.Items.Add(itemElement);
            itemElement.Name = itemClassMeta.Name;
            itemElement.SchemaTypeName = new XmlQualifiedName(itemClassMeta.Name, WebMathTrainingSchemaNamespace);

            MapClass(xmlSchema, itemClassMeta);
          }
        }
        else
        {
          var refClassMeta = ClassCache.Find(mmpm.Clazz);
          var collectionComplexType = new XmlSchemaComplexType();
          element.SchemaType = collectionComplexType;
          var sequence = new XmlSchemaSequence();
          collectionComplexType.Particle = sequence;
          var refElement = new XmlSchemaElement();
          sequence.Items.Add(refElement);
          refElement.Name = refClassMeta.Name;
          refElement.MinOccurs = 0;
          refElement.MaxOccursString = "unbounded";

          SetKeyXmlSchemaType(xmlSchema, refElement, refClassMeta);
        }
        return;
      }

      var ecpm = propMeta as ElementCollectionPropertyMeta;
      if (ecpm != null)
      {
        if (ecpm.CollectionType == "map")
        {
          /*
            <xs:element minOccurs="0" maxOccurs="1" name="_property_">
              <xs:complexType>
                <xs:sequence>
                  <xs:element minOccurs="0" maxOccurs="unbounded" name="Item">
                    <xs:complexType>
                      <xs:sequence>
                        <xs:element minOccurs="1" maxOccurs="1" name="Key" ...>
                          ...
                        </xs:element>
                        <xs:element minOccurs="1" maxOccurs="1" name="Value" ...>
                          ...
                        </xs:element>
                      </xs:sequence>
                    </xs:complexType>
                  </xs:element>
                </xs:sequence>
              </xs:complexType>
            </xs:element>
            <xs:unique name="key_n">
              <xs:selector xpath="q:Item" />
              <xs:field xpath="q:Key" />
            </xs:unique>
          */
          var collectionComplexType = new XmlSchemaComplexType();
          element.SchemaType = collectionComplexType;
          var sequence = new XmlSchemaSequence();
          collectionComplexType.Particle = sequence;
          var itemElement = new XmlSchemaElement();
          sequence.Items.Add(itemElement);
          itemElement.Name = "Item";
          itemElement.MinOccurs = 0;
          itemElement.MaxOccursString = "unbounded";
          var itemComplexType = new XmlSchemaComplexType();
          itemElement.SchemaType = itemComplexType;
          var itemSequence = new XmlSchemaSequence();
          itemComplexType.Particle = itemSequence;
          var keyElement = new XmlSchemaElement();
          itemSequence.Items.Add(keyElement);
          keyElement.Name = "Key";
          keyElement.MinOccurs = 1;
          keyElement.MaxOccurs = 1;

          SetXmlSchemaSimpleType(xmlSchema, keyElement, ecpm.IndexType, ecpm.IndexMaxLength);

          var valueElement = new XmlSchemaElement();
          itemSequence.Items.Add(valueElement);
          valueElement.Name = "Value";
          valueElement.MinOccurs = 1;
          valueElement.MaxOccurs = 1;

          SetXmlSchemaSimpleType(xmlSchema, valueElement, ecpm.ElementType, ecpm.ElementMaxLength);

          var unique = new XmlSchemaUnique();
          element.Constraints.Add(unique);
          unique.Name = $"key_{_keyId++}";
          var keyPath = new XmlSchemaXPath();
          unique.Selector = keyPath;
          keyPath.XPath = "q:Item";
          keyPath.Namespaces.Add("q", WebMathTrainingSchemaNamespace);
          var fieldPath = new XmlSchemaXPath();
          unique.Fields.Add(fieldPath);
          fieldPath.XPath = "q:Key";
          fieldPath.Namespaces.Add("q", WebMathTrainingSchemaNamespace);
        }
        else
        {
          /*
            <xs:element minOccurs="0" maxOccurs="1" name="_property_">
              <xs:complexType>
                <xs:sequence>
                  <xs:element minOccurs="0" maxOccurs="unbounded" name="Item" ...>
                    ...
                  </xs:element>
                </xs:sequence>
              </xs:complexType>
            </xs:element>
          */
          var collectionComplexType = new XmlSchemaComplexType();
          element.SchemaType = collectionComplexType;
          var sequence = new XmlSchemaSequence();
          collectionComplexType.Particle = sequence;
          var itemElement = new XmlSchemaElement();
          sequence.Items.Add(itemElement);
          itemElement.Name = "Item";
          itemElement.MinOccurs = 0;
          itemElement.MaxOccursString = "unbounded";

          SetXmlSchemaSimpleType(xmlSchema, itemElement, ecpm.ElementType, ecpm.ElementMaxLength);

          if (ecpm.CollectionType == "set")
          {
            var unique = new XmlSchemaUnique();
            element.Constraints.Add(unique);
            unique.Name = $"key_{_keyId++}";
            var keyPath = new XmlSchemaXPath();
            unique.Selector = keyPath;
            keyPath.XPath = "q:Item";
            keyPath.Namespaces.Add("q", WebMathTrainingSchemaNamespace);
            var fieldPath = new XmlSchemaXPath();
            unique.Fields.Add(fieldPath);
            fieldPath.XPath = ".";
          }
        }
        return;
      }

      throw new NotSupportedException($"Can't map CollectionPropertyMeta type {propMeta.GetType()}");
    }

    private static bool SetKeyXmlSchemaType(XmlSchema xmlSchema, XmlSchemaElement element, ClassMeta refClassMeta)
    {
      /*
        <xs:complexType>
          <xs:sequence>
            _keyproperties_
          </xs:sequence>
        </xs:complexType>
      */
      var keyPropList = refClassMeta.KeyPropertyList;
      if (keyPropList.Count == 0)
      {
        keyPropList = refClassMeta.PrimaryKeyPropertyList;
      }
      if (!keyPropList.Any(k => k is ObjectIdPropertyMeta))
      {
        var keyFieldsComplexType = new XmlSchemaComplexType();
        element.SchemaType = keyFieldsComplexType;
        var keyFieldsSequence = new XmlSchemaSequence();
        keyFieldsComplexType.Particle = keyFieldsSequence;
        foreach (var keyProperty in keyPropList.Cast<ScalarPropertyMeta>())
        {
          MapScalarProperty(xmlSchema, keyFieldsSequence, keyProperty);
        }
        return true;
      }
      return false;
    }

    private static bool MapScalarPropertyMeta(XmlSchema xmlSchema,
                                              XmlSchemaElement element,
                                              ScalarPropertyMeta propMeta)
    {
      return propMeta.MapXmlSchema(xmlSchema, element);
    }

    /// <summary>
    /// Sets the type of the referenced entity key XML schema.
    /// </summary>
    /// <param name="xmlSchema">The XML schema.</param>
    /// <param name="element">The element.</param>
    /// <param name="refClassMeta">The reference class meta.</param>
    /// <param name="ownershipResolver">The ownership resolver.</param>
    /// <param name="ownerType">Type of the owner.</param>
    /// <returns></returns>
    public static bool SetReferencedEntityKeyXmlSchemaType(XmlSchema xmlSchema, XmlSchemaElement element,
      ClassMeta refClassMeta, IOwnershipResolver ownershipResolver, Type ownerType)
    {
      var keyComplexType = new XmlSchemaComplexType();
      element.SchemaType = keyComplexType;

      if (refClassMeta.IsBaseEntity && ownershipResolver != null)
      {
        // OwnershipResolver complicates this. If there is one, there's only one type choice, and it'll
        // be either the full object schema or the key, depending on ownership.
        var concreteSequence = new XmlSchemaSequence();
        keyComplexType.Particle = concreteSequence;
        var itemElement = new XmlSchemaElement();
        concreteSequence.Items.Add(itemElement);
        itemElement.MinOccurs = IsSparseSchemaMode(xmlSchema) ? 0 : 1;
        itemElement.MaxOccurs = 1;

        /*
          <xs:complexType>
            <xs:choice minOccurs="0" maxOccurs="unbounded">
              <xs:element name="_derivedclass_" type="q:_derivedclass_" />
              ...
            </xs:choice>
          </xs:complexType>
        */
        var ownedType = ownershipResolver.GetOwnedConcreteType(ownerType);
        var itemClassMeta = ClassCache.Find(ownedType);
        itemElement.Name = itemClassMeta.Name;
        return SetKeyXmlSchemaType(xmlSchema, itemElement, itemClassMeta);
      }

      /*
        <xs:complexType>
          <xs:sequence>
       
            <xs:element name="_refclass_">
              ...key...
            <element />
            ...
          </xs:sequence>
        </xs:complexType>
      */
      var keySequence = new XmlSchemaSequence();
      keyComplexType.Particle = keySequence;
      var refElement = new XmlSchemaElement();
      keySequence.Items.Add(refElement);
      refElement.Name = refClassMeta.Name;
      refElement.MinOccurs = 0;
      refElement.MaxOccurs = 1;

      return SetKeyXmlSchemaType(xmlSchema, refElement, refClassMeta);
    }

    /// <summary>
    /// Sets the type of the referenced entity XML schema.
    /// </summary>
    /// <param name="xmlSchema">The XML schema.</param>
    /// <param name="element">The element.</param>
    /// <param name="refClassMeta">The reference class meta.</param>
    /// <param name="ownershipResolver">The ownership resolver.</param>
    /// <param name="ownerType">Type of the owner.</param>
    public static void SetReferencedEntityXmlSchemaType(XmlSchema xmlSchema, XmlSchemaElement element,
      ClassMeta refClassMeta, IOwnershipResolver ownershipResolver, Type ownerType)
    {
      var complexType = new XmlSchemaComplexType();
      element.SchemaType = complexType;

      if (refClassMeta.IsBaseEntity)
      {
        // OwnershipResolver complicates this. If there is one, there's only one type choice, and it'll
        // be either the full object schema or the key, depending on ownership.
        if (ownershipResolver != null)
        {
          var concreteSequence = new XmlSchemaSequence();
          complexType.Particle = concreteSequence;
          var itemElement = new XmlSchemaElement();
          concreteSequence.Items.Add(itemElement);
          itemElement.MinOccurs = IsSparseSchemaMode(xmlSchema) ? 0 : 1;
          itemElement.MaxOccurs = 1;

          var ownedType = ownershipResolver.GetOwnedConcreteType(ownerType);
          var itemClassMeta = ClassCache.Find(ownedType);
          itemElement.Name = itemClassMeta.Name;
          itemElement.SchemaTypeName = new XmlQualifiedName(itemClassMeta.Name, WebMathTrainingSchemaNamespace);

          MapClass(xmlSchema, itemClassMeta);
          return;
        }

        /*
        <xs:complexType>
          <xs:choice minOccurs="0" maxOccurs="unbounded">
            <xs:element name="_derivedclass_" type="q:_derivedclass_" />
            ...
          </xs:choice>
        </xs:complexType>
        */
        var choice = new XmlSchemaChoice();
        complexType.Particle = choice;
        choice.MinOccurs = IsSparseSchemaMode(xmlSchema) ? 0 : 1;
        choice.MaxOccurs = 1;
        var knownItemClassMetas = ClassCache.FindAll().Where(cm => cm.IsA(refClassMeta) && !cm.IsAbstract);
        foreach (var itemClassMeta in knownItemClassMetas)
        {
          var itemElement = new XmlSchemaElement();
          choice.Items.Add(itemElement);

          itemElement.Name = itemClassMeta.Name;
          itemElement.SchemaTypeName = new XmlQualifiedName(itemClassMeta.Name, WebMathTrainingSchemaNamespace);

          MapClass(xmlSchema, itemClassMeta);
        }
        return;
      }
      /*
      <xs:complexType>
        <xs:sequence>
          <xs:element name="_refclass_" type="q:_refclass_" />
        </xs:sequence>
      </xs:complexType>
      */
      var sequence = new XmlSchemaSequence();
      complexType.Particle = sequence;
      var refElement = new XmlSchemaElement();
      sequence.Items.Add(refElement);
      refElement.Name = refClassMeta.Name;
      refElement.MinOccurs = 0;
      refElement.MaxOccurs = 1;
      refElement.SchemaTypeName = new XmlQualifiedName(refClassMeta.Name, WebMathTrainingSchemaNamespace);
    }

    private static void Annotate(XmlSchemaAnnotated annotated, MemberInfo memberInfo)
    {
      var annotation = new XmlSchemaAnnotation();
      Annotate(annotated, annotation, XmlDocProvider.GetSummary(memberInfo));
    }

    private static void Annotate(XmlSchemaAnnotated annotated, XmlSchemaAnnotation annotation, XmlText annotationText)
    {
      annotated.Annotation = annotation;
      var documentation = new XmlSchemaDocumentation();
      annotation.Items.Add(documentation);
      documentation.Markup = new XmlNode[] { annotationText };
    }

    private void AnnotateAppInfo(XmlSchemaAnnotation annotation, ClassMeta cm)
    {
      if (cm.Type?.Assembly.Location != null)
      {
        var uri = new Uri(cm.Type.Assembly.Location);
        var appInfo = annotation.Items.OfType<XmlSchemaAppInfo>().FirstOrDefault(a => a.Source.Equals(uri.ToString()));
        if (appInfo == null)
        {
          appInfo = new XmlSchemaAppInfo();
          annotation.Items.Add(appInfo);
          appInfo.Source = uri.ToString();
        }
      }
    }

    private readonly IList<ClassMeta> _classMetaList = new List<ClassMeta>();
    private readonly IDictionary<string, ClassMeta> _classMetaCacheByName = new Dictionary<string, ClassMeta>();
  }
}