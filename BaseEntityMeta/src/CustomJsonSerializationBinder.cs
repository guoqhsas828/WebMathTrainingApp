using System;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BaseEntity.Configuration;
using System.Collections.Generic;

namespace BaseEntity.Metadata
{
  // Inspiration
  // https://stackoverflow.com/questions/8039910/how-do-i-omit-the-assembly-name-from-the-type-name-while-serializing-and-deseria
  public class CustomJsonSerializationBinder : DefaultSerializationBinder
  {
    private readonly string _namespaceToTypes;

    public CustomJsonSerializationBinder(string namespaceToTypes)
    {
      _namespaceToTypes = namespaceToTypes;
    }

    public override void BindToName(
        Type serializedType, out string assemblyName, out string typeName)
    {
      assemblyName = null;
      typeName = serializedType.FullName.Replace(_namespaceToTypes, string.Empty).Trim('.');
    }

    public override Type BindToType(string assemblyName, string typeName)
    {
      var typeNameWithNamespace = $"{_namespaceToTypes}.{typeName}";
      return Type.GetType(typeNameWithNamespace);
    }
  }

  public class PersistentObjectConverter : JsonCreationConverter<PersistentObject>
  {
    protected override PersistentObject Create(Type objectType, JObject jObject)
    {
      //if (FieldExists("PluginType", jObject))
      //{
      //  return new PluginAssembly()
      //  {
      //    ObjectId = FieldExists("ObjectId", jObject) ?  FieldValue<long>(jObject, "ObjectId") : 0,
      //    Description = FieldValue<string>(jObject, "Description"),
      //    Enabled = FieldValue<bool>(jObject, "Enabled"),
      //    FileName = FieldValue<string>(jObject, "FileName"),
      //    Name=FieldValue<string>(jObject, "Name"),
      //    PluginType = FieldValue<PluginType>(jObject, "PluginType")
      //  };
      //}
      //else
      {
        throw new NotImplementedException();
      }
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
      throw new NotImplementedException();
    }

    private bool FieldExists(string fieldName, JObject jObject)
    {
      return jObject[fieldName] != null;
    }

    private static T FieldValue<T>(JObject jObject, string fieldName)
    {
      return jObject[fieldName].Value<T>();
    }
  }

  public abstract class JsonCreationConverter<T> : JsonConverter
  {
    /// <summary>
    /// Create an instance of objectType, based properties in the JSON object
    /// </summary>
    /// <param name="objectType">type of object expected</param>
    /// <param name="jObject">
    /// contents of JSON object that will be deserialized
    /// </param>
    /// <returns></returns>
    protected abstract T Create(Type objectType, JObject jObject);

    private static Type GetElementType(Type objectType)
    {
      if (objectType.IsGenericType)
      {
        Type typeDef = objectType.GetGenericTypeDefinition();

        Type[] types = objectType.GetGenericArguments();
        var itemType = (typeDef == typeof(IDictionary<,>)) ? types[1] : types[0];
        return GetElementType(itemType);
      }
      else
        return objectType;
    }

    public override bool CanConvert(Type objectType)
    {
      Type itemType = GetElementType(objectType);

      return typeof(T).IsAssignableFrom(itemType);
    }

    public override bool CanWrite
    {
      get { return false; }
    }

    public override object ReadJson(JsonReader reader,
                                    Type objectType,
                                     object existingValue,
                                     JsonSerializer serializer)
    {
      // Load JObject from stream
      JObject jObject = JObject.Load(reader);

      // Create target object based on JObject
      T target = Create(objectType, jObject);

      // Populate the object properties
      serializer.Populate(jObject.CreateReader(), target);

      return target;
    }
  }
}
