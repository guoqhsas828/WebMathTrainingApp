// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using Iesi.Collections;
using BaseEntity.Shared;
#if NETSTANDARD2_0
using ISet = System.Collections.Generic.ISet<object>;
#endif

namespace BaseEntity.Metadata
{
  /// <summary>
  /// A set of static methods for exporting XML representations of properties of WebMathTraining objects
  /// </summary>
  public static class PropertyExporter
  {
    private static readonly IDataExporterRegistry DataExporterRegistry = new DataExporterRegistry();

    /// <summary>
    /// Export string property to XML
    /// </summary>
    /// <param name="pm">Meta description of property</param>
    /// <param name="obj">Object who this property belongs to</param>
    /// <param name="parentXmlNode">XML node to serialize this object within</param>
    public static void ExportStringPropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      ExportString(pm.Name, pm.GetValue(obj), parentXmlNode);
    }

    /// <summary>
    /// Exports the enum property meta.
    /// </summary>
    /// <param name="pm">The pm.</param>
    /// <param name="obj">The object.</param>
    /// <param name="parentXmlNode">The parent XML node.</param>
    public static void ExportEnumPropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      ExportEnum(pm.Name, pm.GetValue(obj), parentXmlNode);
    }
    /// <summary>
    /// Export Component property to XML
    /// </summary>
    /// <param name="pm">Meta description of property</param>
    /// <param name="obj">Object who this property belongs to</param>
    /// <param name="parentXmlNode">XML node to serialize this object within</param>
    public static void ExportComponentPropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      var val = (BaseEntityObject)pm.GetValue(obj);

      if (val != null)
      {
        if (parentXmlNode.OwnerDocument == null)
        {
          throw new XmlException("An owner document is required");
        }
        XmlNode n = parentXmlNode.OwnerDocument.CreateElement(pm.Name, parentXmlNode.NamespaceURI);
        n.AppendChild(ExportObject(val, parentXmlNode.OwnerDocument));
        parentXmlNode.AppendChild(n);
      }
    }

    /// <summary>
    /// Export Component Collection property to XML
    /// </summary>
    /// <param name="pm">Meta description of property</param>
    /// <param name="obj">Object who this property belongs to</param>
    /// <param name="parentXmlNode">XML node to serialize this object within</param>
    public static void ExportComponentCollectionPropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      XmlDocument xmlDoc = parentXmlNode.OwnerDocument;
      if (xmlDoc == null)
      {
        throw new XmlException("An owner document is required");
      }
      var cpm = (ComponentCollectionPropertyMeta)pm;
      var val = (ICollection)pm.GetValue(obj);
      if (val != null)
      {
        XmlNode node = xmlDoc.CreateElement(pm.Name, parentXmlNode.NamespaceURI);

        var dict = val as IDictionary;

        if (val is IList)
        {
          foreach (BaseEntityObject item in val)
          {
            node.AppendChild(ExportObject(item, xmlDoc));
          }
        }
        else if (dict != null)
        {
          foreach (object key in dict.Keys)
          {
            Type indexType = (cpm.IndexType.IsEnum) ? typeof(Enum) : cpm.IndexType;
            var export = DataExporterRegistry.GetValueExporter(indexType);
            if (export == null)
            {
              throw new Exception($"Unable to export values of type [{indexType.Name}]");
            }

            XmlElement itemElement = xmlDoc.CreateElement("Item", parentXmlNode.NamespaceURI);

            export("Key", key, itemElement);

            XmlElement valueElement = xmlDoc.CreateElement("Value", parentXmlNode.NamespaceURI);
            valueElement.AppendChild(ExportObject((BaseEntityObject)dict[key], xmlDoc));
            itemElement.AppendChild(valueElement);

            node.AppendChild(itemElement);
          }
        }
        else
        {
          throw new ArgumentException($"Invalid ComponentCollection property type: {val.GetType()}");
        }

        parentXmlNode.AppendChild(node);
      }
    }

    /// <summary>
    /// Export ElementCollection property to XML
    /// </summary>
    /// <param name="pm">Meta description of property</param>
    /// <param name="obj">Object who this property belongs to</param>
    /// <param name="parentXmlNode">XML node to serialize this object within</param>
    public static void ExportElementCollectionPropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      XmlDocument xmlDoc = parentXmlNode.OwnerDocument;
      if (xmlDoc == null)
      {
        throw new XmlException("An owner document is required");
      }
      var cpm = (ElementCollectionPropertyMeta)pm;
      var val = (ICollection)pm.GetValue(obj);
      if (val != null)
      {
        XmlNode node = xmlDoc.CreateElement(pm.Name, parentXmlNode.NamespaceURI);

        Type elementType = (cpm.ElementType.IsEnum) ? typeof(Enum) : cpm.ElementType;
        var exportElement = DataExporterRegistry.GetValueExporter(elementType);
        if (exportElement == null)
        {
          throw new Exception($"Unable to export values of type [{elementType.Name}]");
        }

        var dict = val as IDictionary;

        if (val is IList || val is ISet)
        {
          foreach (object item in val)
          {
            exportElement("Item", item, node);
          }
        }
        else if (dict != null)
        {
          Type indexType = (cpm.IndexType.IsEnum) ? typeof(Enum) : cpm.IndexType;
          var exportIndex = DataExporterRegistry.GetValueExporter(indexType);
          if (exportIndex == null)
          {
            throw new Exception($"Unable to export values of type [{indexType.Name}]");
          }

          // Always export sorted dictionaries
          var sorted = new SortedList();
          foreach (DictionaryEntry de in dict)
          {
            sorted.Add(de.Key, de.Value);
          }

          foreach (DictionaryEntry de in sorted)
          {
            XmlElement itemElement = xmlDoc.CreateElement("Item", parentXmlNode.NamespaceURI);
            exportIndex("Key", de.Key, itemElement);
            exportElement("Value", de.Value, itemElement);
            node.AppendChild(itemElement);
          }
        }
        else
        {
          throw new ArgumentException($"Invalid ElementCollection property type: {val.GetType()}");
        }

        parentXmlNode.AppendChild(node);
      }
    }

    /// <summary>
    /// Export DateTime property to XML
    /// </summary>
    /// <param name="pm">Meta description of property</param>
    /// <param name="obj">Object who this property belongs to</param>
    /// <param name="parentXmlNode">XML node to serialize this object within</param>
    public static void ExportDateTimePropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      var dateTimePropertyMeta = (DateTimePropertyMeta)pm;
      if (dateTimePropertyMeta.IsTreatedAsDateOnly)
      {
        ExportDateTimeAsDate(pm.Name, pm.GetValue(obj), parentXmlNode);
      }
      else
      {
        ExportDateTime(pm.Name, pm.GetValue(obj), parentXmlNode);
      }
    }

    /// <summary>
    /// Export Guid property to XML
    /// </summary>
    /// <param name="pm">Meta description of property</param>
    /// <param name="obj">Object who this property belongs to</param>
    /// <param name="parentXmlNode">XML node to serialize this object within</param>
    public static void ExportGuidPropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      ExportGuid(pm.Name, pm.GetValue(obj), parentXmlNode);
    }

    /// <summary>
    /// Export OneToOne property to XML
    /// </summary>
    /// <param name="pm">Meta description of property</param>
    /// <param name="obj">Object who this property belongs to</param>
    /// <param name="parentXmlNode">XML node to serialize this object within</param>
    public static void ExportOneToOnePropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      var val = (BaseEntityObject)pm.GetValue(obj);
      if (val != null)
      {
        if (parentXmlNode.OwnerDocument == null)
        {
          throw new XmlException("An owner document is required");
        }
        XmlNode n = parentXmlNode.OwnerDocument.CreateElement(pm.Name, parentXmlNode.NamespaceURI);
        n.AppendChild(ExportObject(val, parentXmlNode.OwnerDocument));
        parentXmlNode.AppendChild(n);
      }
    }

    /// <summary>
    /// Export ManyToOne property to XML
    /// </summary>
    /// <param name="pm">Meta description of property</param>
    /// <param name="obj">Object who this property belongs to</param>
    /// <param name="parentXmlNode">XML node to serialize this object within</param>
    public static void ExportManyToOnePropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      var mop = pm as ManyToOnePropertyMeta;

      var childObj = (BaseEntityObject)pm.GetValue(obj);
      if (mop != null && childObj != null)
      {
        if (parentXmlNode.OwnerDocument == null)
        {
          throw new XmlException("An owner document is required");
        }
        XmlNode n = parentXmlNode.OwnerDocument.CreateElement(pm.Name, parentXmlNode.NamespaceURI);

        bool isOwned = !mop.Cascade.Equals("none");
        if (isOwned)
        {
          n.AppendChild(ExportObject(childObj, parentXmlNode.OwnerDocument));
        }
        else
        {
          var schemaCompliantMode = !string.IsNullOrEmpty(parentXmlNode.NamespaceURI);
          var pmtype = ClassCache.Find(pm.PropertyType);
          
          var exportKey = ExportKey(childObj, 
            parentXmlNode.OwnerDocument,
            // In schema-compliant mode for cases with non-owned properties of some base class (e.g. LeadTrade)
            // it will pass name of the base class (e.g. Trade) instead of concrete implementation (e.g. SwapTrade)
            schemaCompliantMode && mop.OwnershipResolver == null && pmtype.IsBaseEntity ? pmtype.Name : null);

          if (exportKey != null)
          {
            n.AppendChild(exportKey);
          }
        }

        if (n.ChildNodes.Count > 0)
        {
          parentXmlNode.AppendChild(n);
        }
      }
    }

    /// <summary>
    /// </summary>
    /// <param name="pm">Meta description of property</param>
    /// <param name="obj">Object who this property belongs to</param>
    /// <param name="parentXmlNode">XML node to serialize this object within</param>
    public static void ExportOneToManyPropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      XmlDocument xmlDoc = parentXmlNode.OwnerDocument;
      var val = (ICollection)pm.GetValue(obj);
      if (val != null)
      {
        if (parentXmlNode.OwnerDocument == null)
        {
          throw new XmlException("An owner document is required");
        }
        XmlNode node = parentXmlNode.OwnerDocument.CreateElement(pm.Name, parentXmlNode.NamespaceURI);
        var oneToManyPropMeta = (OneToManyPropertyMeta)pm;
        var dict = val as IDictionary;
        if (val is IList)
        {
          foreach (BaseEntityObject item in val)
          {
            if (!oneToManyPropMeta.Cascade.Equals("none"))
            {
              node.AppendChild(ExportObject(item, xmlDoc));
            }
            else
            {
              var exportKey = ExportKey(item, xmlDoc);
              if (exportKey != null)
              {
                node.AppendChild(exportKey);
              }
            }
          }
        }
        else if (dict != null)
        {
          foreach (object key in dict.Keys)
          {
            Type indexType = (oneToManyPropMeta.IndexType.IsEnum) ? typeof(Enum) : oneToManyPropMeta.IndexType;

            var export = DataExporterRegistry.GetValueExporter(indexType);
            if (export == null)
            {
              throw new Exception($"Unable to export values of type [{indexType.Name}]");
            }

            var value = (BaseEntityObject)dict[key];
            if (!oneToManyPropMeta.Cascade.Equals("none"))
            {
              node.AppendChild(ExportObject(value, xmlDoc));
            }
            else
            {
              var exportKey = ExportKey(value, xmlDoc);
              if (exportKey != null)
              {
                node.AppendChild(exportKey);
              }
            }
          }
        }
        else
        {
          throw new ArgumentException($"Invalid ComponentCollection property type: {val.GetType()}");
        }

        if (node.ChildNodes.Count > 0)
        {
          parentXmlNode.AppendChild(node);
        }
      }
    }

    /// <summary>
    /// </summary>
    /// <param name="pm">Meta description of property</param>
    /// <param name="obj">Object who this property belongs to</param>
    /// <param name="parentXmlNode">XML node to serialize this object within</param>
    public static void ExportManyToManyPropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      var mmp = (ManyToManyPropertyMeta)pm;
      XmlDocument xmlDoc = parentXmlNode.OwnerDocument;
      var val = (ICollection)pm.GetValue(obj);
      if (val != null)
      {
        if (parentXmlNode.OwnerDocument == null)
        {
          throw new XmlException("An owner document is required");
        }
        XmlNode node = parentXmlNode.OwnerDocument.CreateElement(pm.Name, parentXmlNode.NamespaceURI);
        if (val is IList)
        {
          foreach (BaseEntityObject item in val)
          {
            bool isOwned = !mmp.Cascade.Equals("none");
            if (isOwned)
            {
              node.AppendChild(ExportObject(item, parentXmlNode.OwnerDocument));
            }
            else
            {
              var exportKey = ExportKey(item, xmlDoc);
              if (exportKey != null)
                node.AppendChild(exportKey);
            }
          }
        }
        else
        {
          throw new ArgumentException($"Unsupported ManyToMany property type: {val.GetType()}");
        }

        if (node.ChildNodes.Count > 0)
        {
          parentXmlNode.AppendChild(node);
        }
      }
    }

    /// <summary>
    /// Export Numeric property to XML
    /// </summary>
    /// <param name="pm">Meta description of property</param>
    /// <param name="obj">Object who this property belongs to</param>
    /// <param name="parentXmlNode">XML node to serialize this object within</param>
    public static void ExportNumericPropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      if (pm.PropertyType == typeof(double) || pm.PropertyType == typeof(double?))
      {
        ExportDouble(pm.Name, pm.GetValue(obj), parentXmlNode);
      }
      else
      {
        ExportString(pm.Name, pm.GetValue(obj), parentXmlNode);
      }
    }

    /// <summary>
    /// Export boolean property to XML
    /// </summary>
    /// <param name="pm">Meta description of property</param>
    /// <param name="obj">Object who this property belongs to</param>
    /// <param name="parentXmlNode">XML node to serialize this object within</param>
    public static void ExportBooleanPropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      ExportBoolean(pm.Name, pm.GetValue(obj), parentXmlNode);
    }

    /// <summary>
    /// Exports a boolean.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="obj">The object.</param>
    /// <param name="parentXmlNode">The parent XML node.</param>
    public static void ExportBoolean(string name, object obj, XmlNode parentXmlNode)
    {
      if (obj != null)
      {
        if (parentXmlNode.OwnerDocument == null)
        {
          throw new XmlException("An owner document is required");
        }
        XmlNode n = parentXmlNode.OwnerDocument.CreateElement(name, parentXmlNode.NamespaceURI);
        if (!string.IsNullOrEmpty(parentXmlNode.NamespaceURI))
        {
          n.InnerText = (bool)obj ? "true" : "false";
        }
        else
        {
          n.InnerText = obj.ToString();
        }
        parentXmlNode.AppendChild(n);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pm"></param>
    /// <param name="obj"></param>
    /// <param name="parentXmlNode"></param>
    /// <returns></returns>
    public static void ExportBinaryBlobPropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      ExportBinary(pm.Name, pm.GetValue(obj), parentXmlNode);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pm"></param>
    /// <param name="obj"></param>
    /// <param name="parentXmlNode"></param>
    /// <returns></returns>
    public static void ExportArrayOfDoublesPropertyMeta(PropertyMeta pm, object obj, XmlNode parentXmlNode)
    {
      if (pm.PropertyType == typeof(double[]))
      {
        ExportArrayOfDoubles(pm.Name, pm.GetValue(obj), parentXmlNode);
      }
      else if (pm.PropertyType == typeof(double[,]))
      {
        ExportArrayOfDoubles2D(pm.Name, pm.GetValue(obj), parentXmlNode);
      }
      else
      {
        throw new NotImplementedException(
          $"Object Format of Type {obj.GetType()} is currently not supported by the ExportArrayOfDoublesPropertyMeta method");
      }
    }

    /// <summary>
    /// Exports the array of doubles.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="obj">The object.</param>
    /// <param name="parentXmlNode">The parent XML node.</param>
    public static void ExportArrayOfDoubles(string name, object obj, XmlNode parentXmlNode)
    {
      var value = (double[])obj;
      XmlDocument xmlDoc = parentXmlNode.OwnerDocument;

      if (value != null && value.Length != 0 && xmlDoc != null)
      {
        XmlNode xmlNode = xmlDoc.CreateElement(name, parentXmlNode.NamespaceURI);
        var tokens = ArrayUtil.Convert(value,
                                       d =>
                                         !string.IsNullOrEmpty(parentXmlNode.NamespaceURI) ? d.ToString("G17", CultureInfo.InvariantCulture) : d.ToString("G17"));
        var commadelimited = string.Join(",", tokens.ToArray());
        xmlNode.InnerText = commadelimited;
        parentXmlNode.AppendChild(xmlNode);
      }
    }

    /// <summary>
    /// Exports the array of doubles.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="obj">The object.</param>
    /// <param name="parentXmlNode">The parent XML node.</param>
    public static void ExportArrayOfDoubles2D(string name, object obj, XmlNode parentXmlNode)
    {
      var value = (double[,])obj;
      XmlDocument xmlDoc = parentXmlNode.OwnerDocument;

      if (value != null && value.Length != 0 && xmlDoc != null)
      {
        XmlNode xmlNode = xmlDoc.CreateElement(name, parentXmlNode.NamespaceURI);
        var sb = new StringBuilder();
        for (int i = 0; i < value.GetLength(0); i++)
        {
          if (i > 0) sb.Append(';');
          for (int j = 0; j < value.GetLength(1); j++)
          {
            if (j > 0) sb.Append(',');
            sb.Append(value[i, j].ToString("G17", CultureInfo.InvariantCulture));
          }
        }
        xmlNode.InnerText = sb.ToString();
        parentXmlNode.AppendChild(xmlNode);
      }
    }

    /// <summary>
    /// Exports the binary.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="obj">The object.</param>
    /// <param name="parentXmlNode">The parent XML node.</param>
    public static void ExportBinary(string name, object obj, XmlNode parentXmlNode)
    {
      var value = (byte[])obj;
      XmlDocument xmlDoc = parentXmlNode.OwnerDocument;

      if (value != null && value.Length != 0 && xmlDoc != null)
      {
        XmlNode xmlNode = xmlDoc.CreateElement(name, parentXmlNode.NamespaceURI);
        xmlNode.AppendChild(xmlDoc.CreateCDataSection(Convert.ToBase64String(value)));
        parentXmlNode.AppendChild(xmlNode);
      }
    }

    /// <summary>
    /// Exports the double.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="obj">The object.</param>
    /// <param name="parentXmlNode">The parent XML node.</param>
    public static void ExportDouble(string name, object obj, XmlNode parentXmlNode)
    {
      // This can happen for double? types
      if (obj == null)
      {
        return;
      }

      var value = (double)obj;
      XmlDocument xmlDoc = parentXmlNode.OwnerDocument;

      if (xmlDoc != null)
      {
        // Rather than having to pass down the "schema-compliant" flag and the WebMathTraining 
        // schema namespace from ExportUtil/DataExporter, instead rely on a couple of assumptions:
        //  1. that if the parent node has a namespace, we're in schema-compliant mode
        //  2. that the namespace (or lack thereof) on created elements is the same as the parent node
        XmlNode n = xmlDoc.CreateElement(name, parentXmlNode.NamespaceURI);
        n.InnerText = !string.IsNullOrEmpty(parentXmlNode.NamespaceURI) ? value.ToString("G17", CultureInfo.InvariantCulture) : value.ToString("G17");
        parentXmlNode.AppendChild(n);
      }
    }

    /// <summary>
    /// Export just the business key(s)
    /// </summary>
    /// <param name="ro">Object to serialize</param>
    /// <param name="doc">XML document to populate</param>
    /// <param name="elementName"> In schema-compliant mode for cases with non-owned properties of some base class (e.g. LeadTrade)
    /// it will be passed name of the base class (e.g. Trade) instead of concrete implementation (e.g. SwapTrade)
    /// </param>
    /// <returns>Node representing the object</returns>
    private static XmlNode ExportKey(BaseEntityObject ro, XmlDocument doc, string elementName = null)
    {
      var cm = ClassCache.Find(ro);
      if (doc.DocumentElement == null)
      {
        throw new XmlException("A document element is required");
      }
      XmlNode nRet = doc.CreateElement(elementName ?? cm.Name, doc.DocumentElement.NamespaceURI);
      var keyPropList = cm.KeyPropertyList;
      if (keyPropList.Count == 0)
      {
        keyPropList = cm.PrimaryKeyPropertyList;
      }

      if (keyPropList.Any(k => k is ObjectIdPropertyMeta))
      {
        // can't export ObjectIds
        return null;
      }

      foreach (PropertyMeta keyProp in keyPropList)
      {
        var export = DataExporterRegistry.GetPropertyExporter(keyProp.GetType());
        if (export == null)
        {
          throw new Exception($"Invalid key property type: {keyProp.GetType().Name}");
        }
        export(keyProp, ro, nRet);
      }

      return nRet;
    }

    /// <summary>
    /// Export a single object into XML. This method provides some indirection to allow
    /// custom object exporting later.
    /// </summary>
    /// <param name="o">Object ot serialize</param>
    /// <param name="doc">xml document to populate</param>
    /// <returns>Node representing the object</returns>
    public static XmlNode ExportObject(BaseEntityObject o, XmlDocument doc)
    {
      var cm = ClassCache.Find(o);

      if (doc.DocumentElement == null)
      {
        throw new XmlException("A document element is required");
      }
      XmlNode nObject = doc.CreateElement(cm.Name, doc.DocumentElement.NamespaceURI);

      if (nObject == null)
      {
        throw new Exception($"Cannot find object {o.GetType().Name} in the class cache");
      }

      var properties = cm.PropertyList;

      // Rather than having to pass down the "schema-compliant" flag and the WebMathTraining 
      // schema namespace from ExportUtil/DataExporter, instead rely on a couple of assumptions:
      //  1. that if the parent node has a namespace, we're in schema-compliant mode
      //  2. that the namespace (or lack thereof) on created elements is the same as the parent node
      if (!string.IsNullOrEmpty(nObject.NamespaceURI))
      {
        // polymorphic relations whose ownership is resolved at runtime must come last,
        // since they're exposed in schema on the derived type
        var ownershipResolverMetas = properties.OfType<ManyToOnePropertyMeta>().Where(mopm => mopm.OwnershipResolver != null).ToList();
        var orderedProperties = properties.Except(ownershipResolverMetas).Concat(ownershipResolverMetas);
        properties = orderedProperties.ToList();
      }

      foreach (PropertyMeta pm in properties)
      {
        if (!pm.Persistent)
        {
          continue;
        }

        if (pm.Name == "ObjectId")
          continue;

        if (DataExporterRegistry.SkipAuditInfo)
        {
          if (pm.PropertyInfo.DeclaringType == typeof(VersionedObject) || pm.PropertyInfo.DeclaringType == typeof(AuditedObject))
          {
            if (cm.OldStyleValidFrom && pm.Name == "ValidFrom" && (cm.KeyPropertyNames != null && cm.KeyPropertyNames.Contains("ValidFrom")))
            {
              // If this is a standard old-style ValidFrom entity then we treat ValidFrom just like any other property
            }
            else
            {
              // If this is a new-style ValidFrom entity or a special-case old-style one (e.g. Quote) then ignore ValidFrom
              continue;
            }
          }
        }

        var export = DataExporterRegistry.GetPropertyExporter(pm.GetType());
        if (export == null)
        {
          throw new Exception($"Unknown property type {pm.GetType().Name}");
        }
        export(pm, o, nObject);
      }
      return nObject;
    }

    /// <summary>
    /// Exports the string.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="obj">The object.</param>
    /// <param name="parentXmlNode">The parent XML node.</param>
    public static void ExportString(string name, object obj, XmlNode parentXmlNode)
    {
      if (obj != null)
      {
        if (parentXmlNode.OwnerDocument == null)
        {
          throw new XmlException("An owner document is required");
        }
        XmlNode n = parentXmlNode.OwnerDocument.CreateElement(name, parentXmlNode.NamespaceURI);
        n.InnerText = obj.ToString();
        parentXmlNode.AppendChild(n);
      }
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
    /// Exports the enum.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="obj">The object.</param>
    /// <param name="parentXmlNode">The parent XML node.</param>
    public static void ExportEnum(string name, object obj, XmlNode parentXmlNode)
    {
      if (obj != null)
      {
        if (parentXmlNode.OwnerDocument == null)
        {
          throw new XmlException("An owner document is required");
        }
        XmlNode n = parentXmlNode.OwnerDocument.CreateElement(name, parentXmlNode.NamespaceURI);
        if (!string.IsNullOrEmpty(parentXmlNode.NamespaceURI))
        {
          Enum val = (Enum)obj;
          Type type = val.GetType();
          if (IsFlagEnumOrNullableFlagEnum(type))
          {
            n.InnerText = obj.ToString().Replace(",", "");
          }
          else if (Enum.IsDefined(type, val))
          {
            n.InnerText = obj.ToString();
          }
        }
        else
        {
          n.InnerText = obj.ToString();
        }
        parentXmlNode.AppendChild(n);
      }
    }

    /// <summary>
    /// Exports the date time.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="obj">The object.</param>
    /// <param name="parentXmlNode">The parent XML node.</param>
    public static void ExportDateTime(string name, object obj, XmlNode parentXmlNode)
    {
      // This can happen for DateTime? types
      if (obj == null)
      {
        return;
      }

      var dateTime = (DateTime)obj;

      if (parentXmlNode.OwnerDocument == null)
      {
        throw new XmlException("An owner document is required");
      }
      XmlNode n = parentXmlNode.OwnerDocument.CreateElement(name, parentXmlNode.NamespaceURI);
      if (dateTime != DateTime.MinValue)
      {
        n.InnerText = dateTime.ToString("o");
      }
      parentXmlNode.AppendChild(n);
    }

    /// <summary>
    /// Exports the date time as a date.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="obj">The object.</param>
    /// <param name="parentXmlNode">The parent XML node.</param>
    public static void ExportDateTimeAsDate(string name, object obj, XmlNode parentXmlNode)
    {
      // This can happen for DateTime? types
      if (obj == null)
      {
        return;
      }

      var dateTime = (DateTime)obj;

      if (parentXmlNode.OwnerDocument == null)
      {
        throw new XmlException("An owner document is required");
      }
      XmlNode n = parentXmlNode.OwnerDocument.CreateElement(name, parentXmlNode.NamespaceURI);
      if (dateTime != DateTime.MinValue)
      {
        n.InnerText = string.IsNullOrEmpty(parentXmlNode.NamespaceURI) ? dateTime.ToString("yyyyMMdd") : dateTime.ToString("yyyy-MM-dd");
      }
      parentXmlNode.AppendChild(n);
    }

    /// <summary>
    /// Exports the unique identifier.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="obj">The object.</param>
    /// <param name="parentXmlNode">The parent XML node.</param>
    public static void ExportGuid(string name, object obj, XmlNode parentXmlNode)
    {
      if (obj == null)
      {
        return;
      }

      var guid = (Guid)obj;

      if (parentXmlNode.OwnerDocument == null)
      {
        throw new XmlException("An owner document is required");
      }
      XmlNode n = parentXmlNode.OwnerDocument.CreateElement(name, parentXmlNode.NamespaceURI);
      if (guid != Guid.Empty)
      {
        n.InnerText = guid.ToString();
      }
      parentXmlNode.AppendChild(n);
    }
  }
}