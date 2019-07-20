/*
 * CollectionPropertyMeta.cs -
 *
 * Copyright (c) WebMathTraining 2002-2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Iesi.Collections;
#if NETSTANDARD2_0
using ISet = System.Collections.Generic.ISet<object>;
#endif

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Abstract base class for element, component, one-to-many, and many-to-many collection types
  /// </summary>
  /// <remarks></remarks>
  public abstract class CollectionPropertyMeta : PropertyMeta
  {
		#region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionPropertyMeta"/> class.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="propAttr">The prop meta attr.</param>
    /// <param name="propInfo">The prop info.</param>
    /// <remarks></remarks>
    protected CollectionPropertyMeta(ClassMeta entity, CollectionPropertyAttribute propAttr, PropertyInfo propInfo)
      : base(entity, propAttr, propInfo)
    {
      _keyColumns = propAttr.KeyColumns ?? new[] {entity.Name + "Id"};

      Type propType = propInfo.PropertyType;

      _collectionType = GetCollectionType(propAttr, propInfo);
      if (_collectionType == "set")
      {
        if (propType.Name != typeof(ISet<>).Name)
        {
          throw new MetadataException(String.Format(
            "Property [{0}] in entity [{1}] has CollectionType 'set' but PropertyType [{2}] is not of type ISet!",
              propInfo.Name, entity.Name, propInfo.PropertyType.Name));
        }
      }
      else if (_collectionType == "list")
		  {
        if (propType.Name != typeof(IList).Name && 
            propType.Name != typeof(IList<>).Name)
        {
          throw new MetadataException(String.Format(
            "Property [{0}] in entity [{1}] has CollectionType 'list' but PropertyType [{2}] does not implement IList!",
              propInfo.Name, entity.Name, propInfo.PropertyType.Name));
        }
		  }
      else if (_collectionType == "bag")
      {
        if (propType.Name != typeof (IList).Name &&
            propType.Name != typeof (IList<>).Name)
        {
          throw new MetadataException(String.Format(
            "Property [{0}] in entity [{1}] has CollectionType 'bag' but PropertyType [{2}] does not implement IList!",
            propInfo.Name, entity.Name, propInfo.PropertyType.Name));
        }
      }
      else if (_collectionType == "map")
      {
        if (propType.Name != typeof(IDictionary).Name &&
            propType.Name != typeof(IDictionary<,>).Name)
        {
          throw new MetadataException(String.Format(
            "Property [{0}] in entity [{1}] has CollectionType 'map' but PropertyType [{2}] does not implement IDictionary!",
            propInfo.Name, entity.Name, propInfo.PropertyType.Name));
        }
      }

      if (_collectionType == "bag")
      {
        if (propAttr.IndexColumn != null)
        {
          throw new MetadataException(String.Format(
            "Invalid property [{0}.{1}] : IndexColumn does not apply to 'bag' collections",
            entity.Name, propInfo.Name));
        }
        if (propAttr.IndexType != null)
        {
          throw new MetadataException(String.Format(
            "Invalid property [{0}.{1}] : IndexType does not apply to 'bag' collections",
            entity.Name, propInfo.Name));
        }
        if (propAttr.IndexMaxLength != 0)
        {
          throw new MetadataException(String.Format(
            "Invalid property [{0}.{1}] : IndexMaxLength does not apply to 'bag' collections",
            entity.Name, propInfo.Name));
        }
      }

      _indexType = GetIndexType(entity, propAttr, propInfo);

      if (_indexType != null)
      {
        _indexColumn = propAttr.IndexColumn ?? ((_collectionType == "list") ? "Idx" : "Key");
        _indexMaxLength = propAttr.IndexMaxLength;
      }
		}

    #endregion

		#region Properties

    /// <summary>
    /// Gets the type of the collection.
    /// </summary>
    /// <remarks></remarks>
    public string CollectionType
    {
      get { return _collectionType; }
    }

    /// <summary>
    /// Specifies the column or columns used to join from the collection table to the collection owner.
    /// </summary>
    /// <remarks></remarks>
    public string[] KeyColumns
    {
      get { return _keyColumns; }
    }

    /// <summary>
    /// Specifies the column in the collection table that uniquely identifies elements within the collection.
    /// </summary>
    /// <remarks>For "list" collections the index column specifies the index of the item in the list.  For "map" collections
    /// it specifies the unique key for the item in the dictionary.  It is not used for non-indexed collections such
    /// as "bag" and "set".</remarks>
    public string IndexColumn
    {
      get { return _indexColumn; }
    }

    /// <summary>
    /// Specifies the type of the index for this collection
    /// </summary>
    /// <remarks>For "list" collections, this is always Int32.  For "map" collections, it is either the .NET Type or a string
    /// that represents the NHibernate type.  The latter is used for string properties in order to specify the length
    /// of the column.</remarks>
		public Type IndexType
		{
			get { return _indexType; }
		}

    /// <summary>
    /// Max length of the index value (applies only to variable length types such as string)
    /// </summary>
    /// <remarks></remarks>
		public int IndexMaxLength
		{
			get { return _indexMaxLength; }
		}

    /// <summary>
    /// Returns true if the underlying property type implement IList
    /// </summary>
    /// <remarks></remarks>
    protected bool IsList
    {
      get
      {
        return (PropertyType.Name == typeof (IList).Name ||
                PropertyType.Name == typeof (IList<>).Name);
      }
    }

    /// <summary>
    /// Return true if the underlying property type implements IDictionary
    /// </summary>
    /// <remarks></remarks>
    protected bool IsMap
    {
      get
      {
        return (PropertyType.Name == typeof (IDictionary).Name ||
                PropertyType.Name == typeof (IDictionary<,>).Name);
      }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is set.
    /// </summary>
    /// <remarks></remarks>
    protected bool IsSet
    {
      get { return (PropertyType.Name == typeof (ISet<>).Name); }
    }


    /// <summary>
    /// Returns true if collection items are identified by index (position) in the collection
    /// </summary>
    /// <remarks></remarks>
    protected virtual bool IsIndexed
    {
      get { return (CollectionType == "list"); }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Derive the default CollectionType value from PropertyType
    /// </summary>
    /// <param name="propAttr"></param>
    /// <param name="propInfo">The prop info.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static string GetCollectionType(CollectionPropertyAttribute propAttr, PropertyInfo propInfo)
    {
      if (propAttr.CollectionType != null) return propAttr.CollectionType;

      Type propType = propInfo.PropertyType;
      if (propType.Name == typeof(IDictionary).Name ||
          propType.Name == typeof(IDictionary<,>).Name)
      {
        return "map";
      }

      if (propType.Name == typeof(IList).Name ||
          propType.Name == typeof(IList<>).Name)
      {
        return "list";
      }

      if (propType.Name == typeof(ISet<>).Name)
      {
        return "set";
      }

      if (propInfo.DeclaringType == null)
      {
        throw new MetadataException(string.Format(
          "Property [{0}] does not have a DeclaringType", propInfo));
      }

      throw new MetadataException(String.Format(
        "Property [{0}.{1}] has invalid PropertyType [{2}]",
        propInfo.DeclaringType.Name, propInfo.Name, propType.Name));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="propAttr"></param>
    /// <param name="propInfo"></param>
    /// <returns></returns>
    public static Type GetIndexType(ClassMeta entity, CollectionPropertyAttribute propAttr, PropertyInfo propInfo)
    {
      var propType = propInfo.PropertyType;

      var collectionType = GetCollectionType(propAttr, propInfo);

      if (collectionType == "list")
      {
        return typeof(Int32);
      }
      
      if (collectionType == "map")
      {
        if (propType.IsGenericType)
        {
          // Derive IndexType from PropertyType
          Type indexType = propType.GetGenericArguments()[0];
          if ((propAttr.IndexType != null) && (indexType != propAttr.IndexType))
          {
            throw new MetadataException(String.Format(
              "IndexType [{0}] does not match IDictionary key type [{1}] for property [{2}] in entity [{3}]",
              propAttr.IndexType, indexType, propInfo.Name, entity.Name));
          }

          return indexType;
        }

        if (propAttr.IndexType != null)
        {
          return propAttr.IndexType;
        }

        throw new MetadataException(String.Format(
          "Must specify IndexType for non-generic property [{0}.{1}]", entity.Name, propInfo.Name));
      }

      return null;
    }

    /// <summary>
    /// Create an empty collection instance appropriate for this property
    /// </summary>
    /// <returns></returns>
    /// <remarks></remarks>
    public abstract ICollection CreateCollection();

    /// <summary>
    /// Determines whether the specified collection is null or empty.
    /// </summary>
    /// <param name="coll">The coll.</param>
    /// <returns><c>true</c> if [is null or empty] [the specified coll]; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    protected static bool IsNullOrEmpty(ICollection coll)
    {
      return coll == null || coll.Count == 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string GenerateHistoryReaderInvocation()
    {
      return string.Format("Read{0}{1}(reader)", Entity.Name, Name);
    }

    #endregion

    #region Data

    private readonly string _collectionType;
    private readonly string[] _keyColumns;
		private readonly string _indexColumn;
		private readonly Type _indexType;
		private readonly int _indexMaxLength;

    #endregion
  }
}
