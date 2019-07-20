// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Reflection;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public class PropertyMetaCreatorFactory : IPropertyMetaCreatorFactory
  {
    private static readonly IDictionary<Type, IPropertyMetaCreator> Creators = new Dictionary<Type, IPropertyMetaCreator>();

    /// <summary>
    /// Registers the property meta creator.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="propertyMetaCreator">The property meta creator.</param>
    /// <exception cref="System.NotImplementedException"></exception>
    public void RegisterPropertyMetaCreator(Type type, IPropertyMetaCreator propertyMetaCreator)
    {
      Creators[type] = propertyMetaCreator;
    }

    /// <summary>
    /// Gets the property meta creator.
    /// </summary>
    /// <param name="propertyAttribute">The property attribute.</param>
    /// <returns></returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public IPropertyMetaCreator GetPropertyMetaCreator(PropertyAttribute propertyAttribute)
    {
      var type = propertyAttribute.GetType();
      IPropertyMetaCreator creator;
      if (Creators.TryGetValue(type, out creator))
      {
        return Creators[type];
      }
      return null;
    }
  }

  internal class BinaryBlobPropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      var genericType = typeof(BinaryBlobPropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
      return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
    }
  }

  internal class ArrayOfDoublesPropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      Type type;
      if (propInfo.PropertyType == typeof(double[]))
      {
        type = typeof(ArrayOfDoubles1DPropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
      } 
      else if (propInfo.PropertyType == typeof(double[,]))
      {
        type = typeof(ArrayOfDoubles2DPropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
      }
      else
      {
        throw new InvalidPropertyTypeException(classMeta, propInfo);
      }
      return (PropertyMeta)Activator.CreateInstance(type, classMeta, attr, propInfo);
    }
  }

  internal class BooleanPropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      if (propInfo.PropertyType == typeof(bool?))
      {
        throw new MetadataException("BooleanPropertyMeta is not supported on nullable properties");
      }
      var genericType = typeof(BooleanPropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
      return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
    }
  }

  internal class ComponentPropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      var genericType = typeof(ComponentPropertyMeta<,>).MakeGenericType(propInfo.DeclaringType, propInfo.PropertyType);
      return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
    }
  }

  internal class ComponentCollectionPropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      var propAttr = (ComponentCollectionPropertyAttribute)attr;

      var collectionType = CollectionPropertyMeta.GetCollectionType(propAttr, propInfo);

      Type genericType;
      switch (collectionType)
      {
        case "map":
          var typeArgs = propInfo.PropertyType.GetGenericArguments();
          genericType = typeof(MapComponentCollectionPropertyMeta<,,>).MakeGenericType(propInfo.DeclaringType, typeArgs[0], typeArgs[1]);
          break;
        case "bag":
        case "list":
          var propType = propInfo.PropertyType;
          Type itemType = propType.IsGenericType ? propType.GetGenericArguments()[0] : typeof(object);
          genericType = typeof(ListComponentCollectionPropertyMeta<,>).MakeGenericType(propInfo.DeclaringType, itemType);
          break;
        default:
          throw new MetadataException($"Invalid Collection Type[{collectionType}]");
      }

      return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
    }
  }

  internal class DateTimePropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      if (propInfo.PropertyType == typeof(DateTime))
      {
        var genericType = typeof(DateTimePropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
        return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
      }
      else
      {
        var genericType = typeof(NullableDateTimePropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
        return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
      }
    }
  }

  internal class ElementCollectionPropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      var propAttr = (ElementCollectionPropertyAttribute)attr;
      var elementType = ElementCollectionPropertyMeta.GetElementType(classMeta, propAttr, propInfo);
      var collectionType = CollectionPropertyMeta.GetCollectionType(propAttr, propInfo);

      Type genericType;
      switch (collectionType)
      {
        case "set":
          genericType = typeof(SetElementCollectionPropertyMeta<,>).MakeGenericType(propInfo.DeclaringType, elementType);
          break;
        case "bag":
        case "list":
          genericType = typeof(ListElementCollectionPropertyMeta<,>).MakeGenericType(propInfo.DeclaringType, elementType);
          break;
        case "map":
          var typeArgs = propInfo.PropertyType.GetGenericArguments();
          genericType = typeof(MapElementCollectionPropertyMeta<,,>).MakeGenericType(propInfo.DeclaringType, typeArgs[0], typeArgs[1]);
          break;
        default:
          throw new MetadataException($"Invalid CollectionType [{collectionType}]");
      }

      return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
    }
  }

  internal class EnumPropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      Type enumType;
      var propType = propInfo.PropertyType;
      if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>))
      {
        enumType = propType.GetGenericArguments()[0];
      }
      else
      {
        enumType = propType;
      }
      if (!enumType.IsEnum)
      {
        throw new InvalidPropertyTypeException(classMeta, propInfo);
      }
      if (enumType == propType)
      {
        var genericType = typeof(EnumPropertyMeta<,>).MakeGenericType(propInfo.DeclaringType, enumType);
        return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
      }
      else
      {
        var genericType = typeof(NullableEnumPropertyMeta<,>).MakeGenericType(propInfo.DeclaringType, enumType);
        return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
      }
    }
  }

  internal class GuidPropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      var genericType = typeof(GuidPropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
      return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
    }
  }

  internal class ManyToOnePropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta,
      PropertyAttribute attr,
      PropertyInfo propInfo)
    {
      var typeArgs = new[] {propInfo.DeclaringType, propInfo.PropertyType};
      var genericType = typeof(ManyToOnePropertyMeta<,>).MakeGenericType(typeArgs);
      return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
    }
  }

  internal class ManyToManyPropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      var propAttr = (ManyToManyPropertyAttribute)attr;
      var typeArgs = propInfo.PropertyType.GetGenericArguments();

      Type genericType;
      switch (propAttr.CollectionType)
      {
        case "map":
          genericType = typeof(MapManyToManyPropertyMeta<,,>).MakeGenericType(propInfo.DeclaringType, typeArgs[0], typeArgs[1]);
          break;
        case "bag":
          genericType = typeof(BagManyToManyPropertyMeta<,>).MakeGenericType(propInfo.DeclaringType, typeArgs[0]);
          break;
        case "list":
          genericType = typeof(ListManyToManyPropertyMeta<,>).MakeGenericType(propInfo.DeclaringType, typeArgs[0]);
          break;
        default:
          throw new MetadataException($"Invalid CollectionType [{propAttr.CollectionType}] for property [{propInfo.Name}] in entity [{classMeta.Name}]");
      }

      return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
    }
  }

  internal class NumericPropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      Type type;
      if (propInfo.PropertyType == typeof(double))
      {
        type = typeof(DoublePropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
      }
      else if (propInfo.PropertyType == typeof(double?))
      {
        type = typeof(NullableDoublePropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
      }
      else if (propInfo.PropertyType == typeof(int))
      {
        type = typeof(Int32PropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
      }
      else if (propInfo.PropertyType == typeof(int?))
      {
        type = typeof(NullableInt32PropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
      }
      else if (propInfo.PropertyType == typeof(long))
      {
        type = typeof(Int64PropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
      }
      else if (propInfo.PropertyType == typeof(long?))
      {
        type = typeof(NullableInt64PropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
      }
      else
      {
        throw new InvalidPropertyTypeException(classMeta, propInfo);
      }
      return (PropertyMeta)Activator.CreateInstance(type, classMeta, attr, propInfo);
    }
  }

  internal class ObjectIdPropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      var genericType = typeof(ObjectIdPropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
      return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
    }
  }

  internal class OneToManyPropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      var propAttr = (OneToManyPropertyAttribute)attr;
      var typeArgs = propInfo.PropertyType.GetGenericArguments();

      Type genericType;
      switch (propAttr.CollectionType)
      {
        case "map":
          genericType = typeof(MapOneToManyPropertyMeta<,,>).MakeGenericType(propInfo.DeclaringType, typeArgs[0], typeArgs[1]);
          break;
        case "bag":
          genericType = typeof(BagOneToManyPropertyMeta<,>).MakeGenericType(propInfo.DeclaringType, typeArgs[0]);
          break;
        case "list":
          genericType = typeof(ListOneToManyPropertyMeta<,>).MakeGenericType(propInfo.DeclaringType, typeArgs[0]);
          break;
        default:
          throw new Exception();
      }

      return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
    }
  }

  internal class OneToOnePropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      var typeArgs = new[] {propInfo.DeclaringType, propInfo.PropertyType};
      var genericType = typeof(OneToOnePropertyMeta<,>).MakeGenericType(typeArgs);
      return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
    }
  }

  internal class StringPropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      var genericType = typeof(StringPropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
      return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
    }
  }

  internal class VersionPropertyMetaCreator : IPropertyMetaCreator
  {
    public PropertyMeta Create(ClassMeta classMeta, PropertyAttribute attr, PropertyInfo propInfo)
    {
      var genericType = typeof(VersionPropertyMeta<>).MakeGenericType(propInfo.DeclaringType);
      return (PropertyMeta)Activator.CreateInstance(genericType, classMeta, attr, propInfo);
    }
  }
}