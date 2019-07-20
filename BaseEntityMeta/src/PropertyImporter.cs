// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using Iesi.Collections;
using BaseEntity.Shared;
#if NETSTANDARD2_0
using ISet = System.Collections.Generic.ISet<object>;
using HashedSet = System.Collections.Generic.HashSet<object>;
#endif

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public static class PropertyImporter
  {
    // TODO: Inject
    private static readonly IDataImporterRegistry _dataImporterRegistry = new DataImporterRegistry();

    /// <summary>
    /// Imports the boolean.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="pm">The pm.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    public static object ImportBoolean(IDataImporter dataImporter, object fromObj, PropertyMeta pm, XmlNode propNode)
    {
      return ImportBoolean(pm.PropertyType, propNode.InnerText, propNode);
    }

    /// <summary>
    /// Imports the boolean.
    /// </summary>
    /// <param name="valueType">Type of the value.</param>
    /// <param name="strValue">The string value.</param>
    /// <param name="node"></param>
    /// <returns></returns>
    public static object ImportBoolean(Type valueType, string strValue, XmlNode node)
    {      
      return !string.IsNullOrEmpty(node.NamespaceURI)
        ? (!string.IsNullOrEmpty(strValue.Trim()) && Convert.ToBoolean(strValue, CultureInfo.InvariantCulture))
        : (!string.IsNullOrEmpty(strValue.Trim()) && bool.Parse(strValue));
    }

    /// <summary>
    /// Imports the component.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="propMeta">The property meta.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    /// <exception cref="System.Exception"></exception>
    public static BaseEntityObject ImportComponent(IDataImporter dataImporter, object fromObj, PropertyMeta propMeta, XmlNode propNode)
    {
      var cpm = (ComponentPropertyMeta)propMeta;

      if (propNode.ChildNodes.Count != 1)
        throw new Exception($"Invalid XML: node [{propNode.InnerXml}]!");

      XmlNode compNode = propNode.FirstChild;
      return (BaseEntityObject)dataImporter.ImportComponent(cpm.ChildEntity, compNode);
    }

    /// <summary>
    /// Imports the component collection.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="propMeta">The property meta.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    /// <exception cref="MetadataException">
    /// </exception>
    /// <exception cref="System.Exception"></exception>
    /// <exception cref="System.NotSupportedException"></exception>
    public static object ImportComponentCollection(IDataImporter dataImporter, object fromObj, PropertyMeta propMeta, XmlNode propNode)
    {
      var cpm = (ComponentCollectionPropertyMeta)propMeta;

      switch (cpm.CollectionType)
      {
        case "bag":
        case "list":

          // Required to remove obsolete items from the collection 
          var obsoleteItems = (IList)cpm.CreateCollection();

          var list = (IList)propMeta.GetValue(fromObj) ?? (IList)cpm.CreateCollection();
          foreach (object item in list)
          {
            obsoleteItems.Add(item);
          }

          int idx = 0;
          foreach (XmlNode childNode in propNode.ChildNodes)
          {
            IBaseEntityObject childObj;
            var childClassMeta = ClassCache.Find(childNode.Name);
            if (childClassMeta == null)
            {
              throw new MetadataException($"Invalid compoment type [{childNode.Name}]");
            }
            if (childClassMeta.HasChildKey)
            {
              IList<object> childKeys = dataImporter.ImportChildKey(childClassMeta, childNode);
              childObj = FindComponentByChildKey(list, childClassMeta, childKeys);
            }
            else
            {
              // Update child object only if the object 
              // at the index is of the same type
              childObj = null;
              if (list.Count > idx)
              {
                var objAtIdx = (IBaseEntityObject)list[idx];
                if (objAtIdx.GetType() == childClassMeta.Type)
                  childObj = objAtIdx;
              }
            }
            if (childObj == null)
            {
              childObj = childClassMeta.CreateInstance();
              list.Add(childObj);
            }
            else
            {
              obsoleteItems.Remove(childObj);
            }
            dataImporter.ImportObjectProperties(childNode, childObj, childClassMeta);
            idx++;
          }

          foreach (object obsoleteItem in obsoleteItems)
          {
            list.Remove(obsoleteItem);
          }
          return list;

        case "map":

          Type indexType = (cpm.IndexType.IsEnum) ? typeof(Enum) : cpm.IndexType;
          var import = _dataImporterRegistry.GetValueImporter(indexType);
          if (import == null)
          {
            throw new Exception($"Unable to import values of type [{indexType.Name}]");
          }

          var obsoleteKeys = new HashSet<object>();
          var dict = (IDictionary)propMeta.GetValue(fromObj) ?? new Hashtable();

          foreach (object dictKey in dict.Keys)
          {
            obsoleteKeys.Add(dictKey);
          }

          foreach (XmlNode childNode in propNode.ChildNodes)
          {
            XmlNode keyNode = childNode.FirstChild;
            XmlNode valueNode = childNode.LastChild.FirstChild;
            object key = import(cpm.IndexType, keyNode.InnerText, keyNode);

            object value;
            ClassMeta childClassMeta;
            if (dict.Contains(key))
            {
              value = dict[key];
              childClassMeta = ClassCache.Find(value.GetType());
              obsoleteKeys.Remove(key);
            }
            else
            {
              childClassMeta = ClassCache.Find(valueNode.Name);
              if (childClassMeta == null)
              {
                throw new MetadataException($"Invalid compoment type [{valueNode.Name}]");
              }
              value = childClassMeta.CreateInstance();
              dict.Add(key, value);
            }

            dataImporter.ImportObjectProperties(valueNode, (IBaseEntityObject)value, childClassMeta);
          }

          foreach (object key in obsoleteKeys)
          {
            dict.Remove(key);
          }

          return dict;

        default:
          throw new NotSupportedException($"Cannot import ComponentCollection {cpm.CollectionType} types");
      }
    }

    /// <summary>
    ///   Finds an item in a component collection using the child keys.
    /// </summary>
    /// <param name="childObjects">Component collection to search</param>
    /// <param name="childClassMeta">ClassMeta for the component</param>
    /// <param name="key">Key property values to match</param>
    /// <returns>Used in ImportComponentCollection when the Component has a child key defined.</returns>
    private static IBaseEntityObject FindComponentByChildKey(ICollection childObjects, ClassMeta childClassMeta, IList<object> key)
    {
      if (childObjects == null || childObjects.Count == 0)
        return null;

      string hashKey = PersistentObject.FormChildKeyFromKeyValues(childClassMeta, key);

      return (from IBaseEntityObject co in childObjects
        let chikdKeys = childClassMeta.ChildKeyPropertyList.Select(keyProp => keyProp.GetValue(co)).ToList()
        let childKeyStr = PersistentObject.FormChildKeyFromKeyValues(childClassMeta, chikdKeys)
        where string.Equals(hashKey, childKeyStr, StringComparison.OrdinalIgnoreCase)
        select co).FirstOrDefault();
    }

    /// <summary>
    /// Imports the element collection.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="propMeta">The property meta.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    /// <exception cref="System.Exception">
    /// </exception>
    /// <exception cref="System.NotSupportedException"></exception>
    public static object ImportElementCollection(IDataImporter dataImporter, object fromObj, PropertyMeta propMeta, XmlNode propNode)
    {
      var cpm = (ElementCollectionPropertyMeta)propMeta;

      Type elementType = (cpm.ElementType.IsEnum) ? typeof(Enum) : cpm.ElementType;
      var importElement = _dataImporterRegistry.GetValueImporter(elementType);
      if (importElement == null)
      {
        throw new Exception($"Unable to import values of type [{elementType.Name}]");
      }

      switch (cpm.CollectionType)
      {
        case "list":

          // Do an ordered match
          var list = (IList)propMeta.GetValue(fromObj) ?? (IList)cpm.CreateCollection();
          int origItemCnt = list.Count;

          int idx = 0;
          foreach (XmlNode item in propNode.ChildNodes)
          {
            object valueFromXml = importElement(cpm.ElementType, item.InnerText, item);

            if (origItemCnt > idx)
            {
              object valueFromObj = list[idx];
              if (!Equals(valueFromXml, valueFromObj))
              {
                list[idx] = valueFromXml;
              }
            }
            else
            {
              list.Add(valueFromXml);
            }
            idx++;
          }

          // remove any extra elements in db not present in xml
          while (list.Count > idx)
          {
            list.RemoveAt(list.Count - 1);
          }

          return list;

        case "bag":

          // For bags order does not matter         
          var bag = (IList)propMeta.GetValue(fromObj);
          if (bag == null)
          {
            bag = (IList)cpm.CreateCollection();
          }

          var itemsToMatch = (IList)cpm.CreateCollection();
          foreach (object valueFromObj in bag)
          {
            itemsToMatch.Add(valueFromObj);
          }

          bool allItemsMatch = true;
          foreach (XmlNode item in propNode.ChildNodes)
          {
            object valueFromXml = importElement(cpm.ElementType, item.InnerText, item);
            object matchingDbItem = itemsToMatch.Cast<object>().FirstOrDefault(itemToMatch => Equals(valueFromXml, itemToMatch));
            if (matchingDbItem != null)
            {
              // matching item found
              itemsToMatch.Remove(matchingDbItem);
            }
            else
            {
              // no matching item found
              allItemsMatch = false;
              break;
            }
          }

          if (allItemsMatch && itemsToMatch.Count == 0)
            return bag;

          bag.Clear();
          foreach (XmlNode item in propNode.ChildNodes)
          {
            object valueFromXml = importElement(cpm.ElementType, item.InnerText, item);
            bag.Add(valueFromXml);
          }

          return bag;

        case "map":

          Type indexType = (cpm.IndexType.IsEnum) ? typeof(Enum) : cpm.IndexType;
          var importIndex = _dataImporterRegistry.GetValueImporter(indexType);
          if (importIndex == null)
          {
            throw new Exception($"Unable to import values of type [{indexType.Name}]");
          }

          var obsoleteKeys = new HashSet<object>();
          var dictionary = (IDictionary)propMeta.GetValue(fromObj) ?? new Hashtable();

          foreach (object dictKey in dictionary.Keys)
          {
            obsoleteKeys.Add(dictKey);
          }

          foreach (XmlNode item in propNode.ChildNodes)
          {
            XmlNode keyNode = item.FirstChild;
            XmlNode valueNode = item.LastChild;
            object key = importIndex(cpm.IndexType, keyNode.InnerText, keyNode);
            object valueFromXml = importElement(cpm.ElementType, valueNode.InnerText, valueNode);

            if (dictionary.Contains(key))
            {
              obsoleteKeys.Remove(key);
              object valueFromObj = dictionary[key];
              if (!Equals(valueFromXml, valueFromObj))
              {
                dictionary[key] = valueFromXml;
              }
            }
            else
            {
              dictionary.Add(key, valueFromXml);
            }
          }

          foreach (object key in obsoleteKeys)
          {
            dictionary.Remove(key);
          }

          return dictionary;

        case "set":

          var set = (ISet)propMeta.GetValue(fromObj) ?? (ISet)cpm.CreateCollection();

          var obsoleteSet = new HashedSet(set);

          foreach (XmlNode item in propNode.ChildNodes)
          {
            object valueFromXml = importElement(cpm.ElementType, item.InnerText, item);

            if (set.Contains(valueFromXml))
            {
              obsoleteSet.Remove(valueFromXml);
            }
            else
            {
              set.Add(valueFromXml);
            }
          }

#if NETSTANDARD2_0
          foreach (var o in obsoleteSet)
          {
            set.Remove(o);
          }
#else
          set.RemoveAll(obsoleteSet);
#endif
          return set;

        default:
          throw new NotSupportedException($"Cannot import ElementCollection {cpm.CollectionType} types");
      }
    }

    /// <summary>
    /// Imports the unique identifier.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="pm">The pm.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    public static object ImportGuid(IDataImporter dataImporter, object fromObj, PropertyMeta pm, XmlNode propNode)
    {
      return _dataImporterRegistry.GetValueImporter(pm.PropertyType)(pm.PropertyType, propNode.InnerText, propNode);
    }

    /// <summary>
    /// Imports the unique identifier.
    /// </summary>
    /// <param name="valueType">Type of the value.</param>
    /// <param name="strValue">The string value.</param>
    /// <param name="propNode"></param>
    /// <returns></returns>
    public static object ImportGuid(Type valueType, string strValue, XmlNode propNode)
    {
      return Guid.Parse(strValue);
    }

    /// <summary>
    /// Imports the date time.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="pm">The pm.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    public static object ImportDateTime(IDataImporter dataImporter, object fromObj, PropertyMeta pm, XmlNode propNode)
    {
      var dateTimePropertyMeta = (DateTimePropertyMeta)pm;
      var schemaCompliant = !string.IsNullOrEmpty(propNode.NamespaceURI);
      return ImportDateTime(pm.PropertyType, propNode.InnerText, dateTimePropertyMeta.IsTreatedAsDateOnly, schemaCompliant);
    }

    /// <summary>
    /// Imports the date time.
    /// </summary>
    /// <param name="valueType">Type of the value.</param>
    /// <param name="strValue">The string value.</param>
    /// <param name="node"></param>
    /// <returns></returns>
    public static object ImportDateTime(Type valueType, string strValue, XmlNode node)
    {
      DateTime val;
      if (!DateTime.TryParse(strValue, out val))
      {
        val = NumericUtils.ExcelDateToDateTime(double.Parse(strValue));
      }

      return val == DateTime.MinValue ? DateTimePropertyMeta.SqlMinDateUtc : val.ToUniversalTime();
    }

    /// <summary>
    /// Imports the date time.
    /// </summary>
    /// <param name="valueType">Type of the value.</param>
    /// <param name="strValue">The string value.</param>
    /// <param name="isTreatedAsDateOnly">if set to <c>true</c> [is treated as date only].</param>
    /// <param name="schemaCompliant">if set to <c>true</c> [schema compliant].</param>
    /// <returns></returns>
    public static object ImportDateTime(Type valueType, string strValue, bool isTreatedAsDateOnly, bool schemaCompliant)
    {
      DateTime val;

      if (isTreatedAsDateOnly)
      {
        if (string.IsNullOrWhiteSpace(strValue))
        {
          val = DateTime.MinValue;
        }
        else
        {
          string format = schemaCompliant ? "yyyy-MM-dd" : "yyyyMMdd";
          if (!DateTime.TryParseExact(strValue, format, null, DateTimeStyles.None, out val))
            val = NumericUtils.ExcelDateToDateTime(double.Parse(strValue));
        }
      }
      else
      {
        if (string.IsNullOrWhiteSpace(strValue))
        {
          val = DateTimePropertyMeta.SqlMinDateUtc;
        }
        else
        {
          if (!DateTime.TryParse(strValue, out val))
            val = NumericUtils.ExcelDateToDateTime(double.Parse(strValue)).ToUniversalTime();
        }
      }

      return val;
    }

    /// <summary>
    /// Imports the enum.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="pm">The pm.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    public static object ImportEnum(IDataImporter dataImporter, object fromObj, PropertyMeta pm, XmlNode propNode)
    {
      var epm = (EnumPropertyMeta)pm;
      return ImportEnum(epm.EnumType, propNode.InnerText, propNode);
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
    /// Imports the enum.
    /// </summary>
    /// <param name="enumType">Type of the enum.</param>
    /// <param name="strValue">The string value.</param>
    /// <param name="propNode"></param>
    /// <returns></returns>
    public static object ImportEnum(Type enumType, string strValue, XmlNode propNode)
    {
      if (IsFlagEnumOrNullableFlagEnum(enumType))
      {
        var schemaCompliant = !string.IsNullOrEmpty(propNode.NamespaceURI);

        if (schemaCompliant)
        {
          strValue = strValue.Trim();
          // Check if contains multiple values
          if (strValue.Contains(" "))
          {
            // Also this handles sets of multiple spaces as delimeters, just in case if was edited by a user with mistake
            strValue = string.Join(", ", strValue.Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries));
          }
        }
      }

      return Enum.Parse(enumType, strValue);
    }

    /// <summary>
    /// Imports the one to one.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="pm">The pm.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    /// <exception cref="System.Exception"></exception>
    public static object ImportOneToOne(IDataImporter dataImporter, object fromObj, PropertyMeta pm, XmlNode propNode)
    {
      if (propNode.ChildNodes.Count == 0)
      {
        return null;
      }

      if (propNode.ChildNodes.Count != 1)
        throw new Exception($"Expecting one child node, found [{propNode.ChildNodes.Count}] : node [{propNode.InnerXml}]!");

      XmlNode childObjNode = propNode.FirstChild;
      var childClassMeta = ClassCache.Find(childObjNode.Name);

      // We need to merge the child object properties into any existing child object,
      // otherwise we will orphan the existing child.  Alternatively, we could delete
      // any existing child and simply replace with the new object, but this seems
      // less optimal for several reasons.
      var childObj = (PersistentObject)pm.GetValue(fromObj);
      if (childObj == null)
      {
        childObj = (PersistentObject)childClassMeta.CreateInstance();
      }

      // Import (merge) into existing object
      dataImporter.ImportEntityProperties(childObjNode, childObj, childClassMeta);

      return childObj;
    }

    /// <summary>
    /// Imports the many to one.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="pm">The pm.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    /// <exception cref="System.Exception"></exception>
    /// <exception cref="MetadataException"></exception>
    public static object ImportManyToOne(IDataImporter dataImporter, object fromObj, PropertyMeta pm, XmlNode propNode)
    {
      var mop = (ManyToOnePropertyMeta)pm;

      if (propNode.ChildNodes.Count == 0)
      {
        return null;
      }

      if (propNode.ChildNodes.Count != 1)
        throw new Exception($"Invalid XML: node [{propNode.InnerXml}]!");

      XmlNode childObjNode = propNode.FirstChild;
      var childClassMeta = ClassCache.Find(childObjNode.Name);
      IList<object> key = dataImporter.ImportKey(childClassMeta, childObjNode);

      PersistentObject childObj;
      bool isOwned = !mop.Cascade.Equals("none");
      if (isOwned)
      {
        childObj = dataImporter.FindByKey(childClassMeta, key);
        if (childObj != null)
        {
          // If the child object is owned and present in the database merge the new properties with the existing object
          dataImporter.ImportEntityProperties(childObjNode, childObj, childClassMeta);
        }
        else
        {
          // If the child object does not exist in the database, import as a new entity
          childObj = dataImporter.ImportEntity(childClassMeta, childObjNode);
        }
      }
      else
      {
        childObj = dataImporter.FindByKey(childClassMeta, key);
        if (childObj == null)
        {
          // The object wasn't imported by this importer, and its key wasn't found in the database, so throw
          throw new MetadataException($"{PersistentObject.FormKey(childClassMeta, key)} not found");
        }
      }

      return childObj;
    }

    /// <summary>
    /// Imports the one to many.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="propMeta">The property meta.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    /// <exception cref="MetadataException">
    /// </exception>
    /// <exception cref="System.Exception">Invalid XML!</exception>
    /// <exception cref="System.NotSupportedException"></exception>
    public static object ImportOneToMany(IDataImporter dataImporter, object fromObj, PropertyMeta propMeta, XmlNode propNode)
    {
      var oneToManyPropMeta = (OneToManyPropertyMeta)propMeta;

      switch (oneToManyPropMeta.CollectionType)
      {
        case "bag":
        case "list":

          var prevList = (IList)oneToManyPropMeta.CreateCollection();
          var obsoleteList = (IList)oneToManyPropMeta.CreateCollection();
          var list = (IList)propMeta.GetValue(fromObj);
          if (list == null)
          {
            list = (IList)oneToManyPropMeta.CreateCollection();
          }
          else
          {
            foreach (object item in list)
            {
              prevList.Add(item);
              obsoleteList.Add(item);
            }
          }

          int idx = 0;
          foreach (XmlNode childObjNode in propNode.ChildNodes)
          {
            IList<object> key;
            var childClassMeta = ClassCache.Find(childObjNode.Name);
            PersistentObject childObj = null;

            if (oneToManyPropMeta.IsInverse)
            {
              if (!childClassMeta.HasKey)
              {
                key = dataImporter.ImportChildKey(childClassMeta, childObjNode);
                childObj = FindEntityByChildKey(list, childClassMeta, key);
              }
              else
              {
                key = dataImporter.ImportKey(childClassMeta, childObjNode);

                // If the parent is not in the session, then it must be transient,
                // in which case there is no reason to lookup the child.
                if (EntityContext.Current.Contains(fromObj))
                  childObj = dataImporter.FindByKey(childClassMeta, key);
              }
            }
            else
            {
              if (childClassMeta.HasKey)
              {
                key = dataImporter.ImportKey(childClassMeta, childObjNode);
                childObj = dataImporter.FindByKey(childClassMeta, key);
              }
              else
              {
                // Only true for if all-delete-orphan?
                key = new List<object> {idx};
                childObj = (prevList.Count > idx) ? (PersistentObject)prevList[idx] : null;
              }
            }

            if (!oneToManyPropMeta.Cascade.Equals("none"))
            {
              bool addChildObjToList = false;
              // This means we own the referenced object, so we will import its properties as well
              if (childObj == null)
              {
                // Import new object
                childObj = dataImporter.ImportEntity(childClassMeta, childObjNode);
                addChildObjToList = true;
              }
              else
              {
                if (list.Contains(childObj))
                {
                  // Remove the child object from the obsolete list
                  obsoleteList.Remove(childObj);
                }
                else
                {
                  addChildObjToList = true;
                }

                // Import (merge) into existing object
                dataImporter.ImportEntityProperties(childObjNode, childObj, childClassMeta);
              }

              if (addChildObjToList)
              {
                if (oneToManyPropMeta.Adder != null)
                  oneToManyPropMeta.Adder.Invoke(fromObj, new object[] {childObj});
                else
                  list.Add(childObj);
              }
            }
            else
            {
              if (childObj == null)
              {
                throw new MetadataException($"{PersistentObject.FormKey(childClassMeta, key)} not found");
              }
              if (list.Contains(childObj))
              {
                // Remove the child object from the obsolete list
                obsoleteList.Remove(childObj);
              }
            }

            idx++;
          }

          // remove any objects not present in the xml
          foreach (object removeObj in obsoleteList)
            list.Remove(removeObj);

          return list;

        case "map":

          Type indexType = (oneToManyPropMeta.IndexType.IsEnum) ? typeof(Enum) : oneToManyPropMeta.IndexType;
          var import = _dataImporterRegistry.GetValueImporter(indexType);
          if (import == null)
          {
            throw new Exception($"Unable to import values of type [{indexType.Name}]");
          }

          IDictionary prevDict = new Hashtable();
          IDictionary removeDict = new Hashtable();
          var dict = (IDictionary)propMeta.GetValue(fromObj);
          if (dict == null)
          {
            dict = new Hashtable();
          }
          else
          {
            foreach (object dictKey in dict.Keys)
            {
              prevDict.Add(dictKey, dict[dictKey]);
              removeDict.Add(dictKey, dict[dictKey]);
            }
          }

          XmlNamespaceManager nsmgr;
          bool schemaCompliantMode;
          DataImporter.ReadNamespaceInfo(propNode.OwnerDocument, out schemaCompliantMode, out nsmgr);

          foreach (XmlElement childObjNode in propNode.ChildNodes)
          {
            var childClassMeta = ClassCache.Find(childObjNode.Name);

            XmlNode indexNode;
            if (schemaCompliantMode)
              indexNode = childObjNode.SelectSingleNode("q:" + oneToManyPropMeta.IndexColumn, nsmgr);
            else
              indexNode = childObjNode.SelectSingleNode(oneToManyPropMeta.IndexColumn);

            object dictKey = import(oneToManyPropMeta.IndexType, indexNode.InnerText, indexNode);

            IList<object> childObjKey;
            PersistentObject childObj = null;
            if (oneToManyPropMeta.IsInverse)
            {
              if (!childClassMeta.HasKey)
              {
                childObjKey = dataImporter.ImportChildKey(childClassMeta, childObjNode);
                childObj = FindEntityByChildKey(dict, childClassMeta, childObjKey);
              }
              else
              {
                childObjKey = dataImporter.ImportKey(childClassMeta, childObjNode);

                // If the parent is not in the session, then it must be transient,
                // in which case there is no reason to lookup the child.
                if (EntityContext.Current.Contains(fromObj)) childObj = dataImporter.FindByKey(childClassMeta, childObjKey);
              }
            }
            else
            {
              // Only true for if all-delete-orphan?
              childObj = (prevDict.Contains(dictKey)) ? (PersistentObject)prevDict[dictKey] : null;
              childObjKey = null;
            }

            if (!oneToManyPropMeta.Cascade.Equals("none"))
            {
              // This means we own the referenced object, so we will import its properties as well

              if (childObj == null)
              {
                if (childClassMeta == null)
                  throw new Exception("Invalid XML!");

                // Import new object
                childObj = dataImporter.ImportEntity(childClassMeta, childObjNode);
              }
              else
              {
                removeDict.Remove(dictKey);
                // Import (merge) into existing object
                dataImporter.ImportEntityProperties(childObjNode, childObj, childClassMeta);
              }
            }
            else
            {
              if (childObj == null)
                throw new MetadataException(PersistentObject.FormKey(childClassMeta, childObjKey) + " not found");
            }

            dict[dictKey] = childObj;
          }

          // Remove any objects not present in the xml
          foreach (object dictKey in removeDict.Keys)
            dict.Remove(dictKey);

          return dict;

        default:
          throw new NotSupportedException(
            $"Cannot import property {oneToManyPropMeta.Entity.Name}.{oneToManyPropMeta.Name} - collection type [{oneToManyPropMeta.CollectionType}] not supported!");
      }
    }

    /// <summary>
    /// Imports the many to many.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="propMeta">The property meta.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    /// <exception cref="MetadataException"></exception>
    /// <exception cref="System.NotSupportedException"></exception>
    public static object ImportManyToMany(IDataImporter dataImporter, object fromObj, PropertyMeta propMeta, XmlNode propNode)
    {
      var mmp = (ManyToManyPropertyMeta)propMeta;

      IList<PropertyMeta> keyPropList;
      if (mmp.ReferencedEntity.HasChildKey)
      {
        keyPropList = mmp.ReferencedEntity.ChildKeyPropertyList;
      }
      else if (mmp.ReferencedEntity.HasKey)
      {
        keyPropList = mmp.ReferencedEntity.KeyPropertyList;
      }
      else
      {
        throw new MetadataException($"Entity {mmp.ReferencedEntity.Name} does not have a business key defined!");
      }

      if (mmp.CollectionType == "list" || mmp.CollectionType == "bag")
      {
        var list = (IList)propMeta.GetValue(fromObj);
        var prevList = list.Cast<PersistentObject>().ToList();
        var prevMap = prevList.ToDictionary(item => item.FormKey());
        list.Clear();

        foreach (XmlNode childObjNode in propNode.ChildNodes)
        {
          var cm = ClassCache.Find(childObjNode.Name);
 
          IList<object> key = cm.HasChildKey ? dataImporter.ImportChildKey(cm, childObjNode) : dataImporter.ImportKey(cm, childObjNode);
 
          PersistentObject childObj;
          bool isOwned = mmp.Cascade.Equals("all-delete-orphan");
          if (isOwned)
          {
            // Look for the entity in the original list in order to get its object id
            var keyStr = cm.HasChildKey ? PersistentObject.FormChildKey(cm, key) : PersistentObject.FormKey(cm, key);
            childObj = prevMap.TryGetValue(keyStr, out childObj) ? childObj : null;
            if (childObj != null)
            {
              // If the child object is owned and present in the database merge the new properties with the existing object
              dataImporter.ImportEntityProperties(childObjNode, childObj, cm);
            }
            else
            {
              // If the child object does not exist in the database, import as a new entity
              childObj = dataImporter.ImportEntity(cm, childObjNode);
            }
          }
          else
          {
            childObj = dataImporter.FindByKey(cm, key);
          }
          if (childObj == null)
          {
            throw new MetadataException($"{PersistentObject.FormKey(cm, key)} not found");
          }

          list.Add(childObj);
        }

        return list;
      }

      throw new NotSupportedException($"Cannot import property {mmp.Entity.Name}.{mmp.Name} - collection type [{mmp.CollectionType}] not supported!");
    }

    /// <summary>
    ///   Finds an entity in a collection by Child Key
    /// </summary>
    /// <param name="childObjects"></param>
    /// <param name="childClassMeta"></param>
    /// <param name="key"></param>
    /// <returns>Matching entity implementing PersistentObject or null.</returns>
    /// <remarks>Used in ImportOneToMany </remarks>
    private static PersistentObject FindEntityByChildKey(ICollection childObjects, ClassMeta childClassMeta, IList<object> key)
    {
      if (childObjects == null || childObjects.Count == 0)
        return null;

      var dict = childObjects as IDictionary;
      if (dict != null)
      {
        string childKeyStr = PersistentObject.FormChildKeyFromKeyValues(childClassMeta, key);
        return dict.Contains(childKeyStr) ? dict[childKeyStr] as PersistentObject : null;
      }

      string hashKey = PersistentObject.FormChildKey(childClassMeta, key);
      return childObjects.Cast<PersistentObject>().FirstOrDefault(childObj => hashKey == PersistentObject.FormChildKey(childObj));
    }

    /// <summary>
    /// Imports the numeric.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="pm">The pm.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    public static object ImportNumeric(IDataImporter dataImporter, object fromObj, PropertyMeta pm, XmlNode propNode)
    {
      return _dataImporterRegistry.GetValueImporter(pm.PropertyType)(pm.PropertyType, propNode.InnerText, propNode);
    }

    /// <summary>
    /// Imports the string.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="pm">The pm.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    public static object ImportString(IDataImporter dataImporter, object fromObj, PropertyMeta pm, XmlNode propNode)
    {
      return ImportString(pm.PropertyType, propNode.InnerText, propNode);
    }

    /// <summary>
    /// Imports the string.
    /// </summary>
    /// <param name="valueType">Type of the value.</param>
    /// <param name="strValue">The string value.</param>
    /// <param name="propNode"></param>
    /// <returns></returns>
    public static object ImportString(Type valueType, string strValue, XmlNode propNode)
    {
      return strValue;
    }

    /// <summary>
    /// Imports the binary.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="pm">The pm.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    public static object ImportBinary(IDataImporter dataImporter, object fromObj, PropertyMeta pm, XmlNode propNode)
    {
      var cdata = (XmlCDataSection)propNode.FirstChild;
      return (cdata == null) ? null : Convert.FromBase64String(cdata.InnerText);
    }

    /// <summary>
    /// Imports the array of doubles.
    /// </summary>
    /// <param name="dataImporter">The data importer.</param>
    /// <param name="fromObj">From object.</param>
    /// <param name="pm">The pm.</param>
    /// <param name="propNode">The property node.</param>
    /// <returns></returns>
    public static object ImportArrayOfDoubles(IDataImporter dataImporter, object fromObj, PropertyMeta pm, XmlNode propNode)
    {
      if (pm.PropertyType == typeof(double[]))
      {
        return ImportArrayOfDoubles(pm.PropertyType, propNode.InnerText, propNode);
      }
      if (pm.PropertyType == typeof(double[,]))
      {
        return ImportArrayOfDoubles2D(pm.PropertyType, propNode.InnerText, propNode);
      }
      throw new NotImplementedException($"Object Format of Type {fromObj.GetType()} is currently not supported by the ImportArrayOfDoubles method");
    }

    /// <summary>
    /// Imports the array of doubles.
    /// </summary>
    /// <param name="valueType">Type of the value.</param>
    /// <param name="strValue">The string value.</param>
    /// <param name="node"></param>
    /// <returns></returns>
    public static object ImportArrayOfDoubles(Type valueType, string strValue, XmlNode node)
    {
      var tokens = strValue.Split(',');
      var doubles = new double[tokens.Length];
      for (int i = 0; i < tokens.Length; i++)
      {
        doubles[i] = Convert.ToDouble(tokens[i], CultureInfo.InvariantCulture);
      }
      return doubles;
    }

    /// <summary>
    /// Imports the 2D array of doubles.
    /// </summary>
    /// <param name="valType"></param>
    /// <param name="strValue"></param>
    /// <param name="node"></param>
    /// <returns></returns>
    public static object ImportArrayOfDoubles2D(Type valType, string strValue, XmlNode node)
    {
      var rowArray = strValue.Split(';');
      if (rowArray.Length == 0 || rowArray[0].Split(',').Length == 0)
      {
        return null;
      }
      var values = new double[rowArray.Length, rowArray[0].Split(',').Length];
      for (var i = 0; i < rowArray.Length; i++)
      {
        var tokens = rowArray[i].Split(',');
        for (var j = 0; j < tokens.Length; j++)
        {
          values[i, j] = Convert.ToDouble(tokens[j], CultureInfo.InvariantCulture);
        }
      }
      return values;
    }

    /// <summary>
    /// Imports the double.
    /// </summary>
    /// <param name="valueType">Type of the value.</param>
    /// <param name="strValue">The string value.</param>
    /// <param name="node"></param>
    /// <returns></returns>
    public static object ImportDouble(Type valueType, string strValue, XmlNode node)
    {
      return !string.IsNullOrEmpty(node.NamespaceURI) 
        ? Convert.ToDouble(strValue, CultureInfo.InvariantCulture) 
        : double.Parse(strValue);
    }

    /// <summary>
    /// Imports the int32.
    /// </summary>
    /// <param name="valueType">Type of the value.</param>
    /// <param name="strValue">The string value.</param>
    /// <param name="node"></param>
    /// <returns></returns>
    public static object ImportInt32(Type valueType, string strValue, XmlNode node)
    {
      return int.Parse(strValue);
    }

    /// <summary>
    /// Imports the int64.
    /// </summary>
    /// <param name="valueType">Type of the value.</param>
    /// <param name="strValue">The string value.</param>
    /// <param name="propNode"></param>
    /// <returns></returns>
    public static object ImportInt64(Type valueType, string strValue, XmlNode propNode)
    {
      return long.Parse(strValue);
    }

    /// <summary>
    /// Imports the nullable double.
    /// </summary>
    /// <param name="valueType">Type of the value.</param>
    /// <param name="strValue">The string value.</param>
    /// <param name="propNode"></param>
    /// <returns></returns>
    /// <exception cref="System.FormatException">Error parsing ' + strValue + ' to Nullable Double.</exception>
    public static object ImportNullableDouble(Type valueType, string strValue, XmlNode propNode)
    {
      if ((strValue == null) || (strValue.Trim().Length == 0))
      {
        return new double?();
      }

      try
      {
        return !string.IsNullOrEmpty(propNode.NamespaceURI)
          ? Convert.ToDouble(strValue, CultureInfo.InvariantCulture)
          : double.Parse(strValue);
      }
      catch (Exception ex)
      {
        throw new FormatException("Error parsing '" + strValue + "' to Nullable<Double>.", ex);
      }
    }

    /// <summary>
    /// Imports the nullable int32.
    /// </summary>
    /// <param name="valueType">Type of the value.</param>
    /// <param name="strValue">The string value.</param>
    /// <param name="propNode"></param>
    /// <returns></returns>
    /// <exception cref="System.FormatException">Error parsing ' + strValue + ' to Nullable Int32.</exception>
    public static object ImportNullableInt32(Type valueType, string strValue, XmlNode propNode)
    {
      if ((strValue == null) || (strValue.Trim().Length == 0))
      {
        return new int?();
      }

      try
      {
        return int.Parse(strValue);
      }
      catch (Exception ex)
      {
        throw new FormatException("Error parsing '" + strValue + "' to Nullable<Int32>.", ex);
      }
    }

    /// <summary>
    /// Imports the nullable int64.
    /// </summary>
    /// <param name="valueType">Type of the value.</param>
    /// <param name="strValue">The string value.</param>
    /// <param name="propNode"></param>
    /// <returns></returns>
    /// <exception cref="System.FormatException">Error parsing ' + strValue + ' to Nullable Int64.</exception>
    public static object ImportNullableInt64(Type valueType, string strValue, XmlNode propNode)
    {
      if ((strValue == null) || (strValue.Trim().Length == 0))
      {
        return new long?();
      }

      try
      {
        return long.Parse(strValue);
      }
      catch (Exception ex)
      {
        throw new FormatException("Error parsing '" + strValue + "' to Nullable<Int64>.", ex);
      }
    }

    /// <summary>
    /// Imports the nullable date time.
    /// </summary>
    /// <param name="valueType">Type of the value.</param>
    /// <param name="strValue">The string value.</param>
    /// <param name="node"></param>
    /// <returns></returns>
    public static object ImportNullableDateTime(Type valueType, string strValue, XmlNode node)
    {
      return string.IsNullOrWhiteSpace(strValue) ? null : ImportDateTime(valueType, strValue, node);
    }
  }
}