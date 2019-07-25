using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;
using Utility = BaseEntity.Toolkit.Base.Serialization.SimpleXmlSerializationUtility;

namespace BaseEntity.Toolkit.Base.Serialization
{
  /// <summary>
  /// An XML serializer to customize the name-type pairs of the serialized fields.
  /// </summary>
  /// <seealso cref="ISimpleXmlSerializer" />
  public abstract class CustomFieldMapSerializer : ISimpleXmlSerializer
  {
    /// <summary>
    /// The name-type map of the fields
    /// </summary>
    private readonly Dictionary<string, Type> _fieldTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomFieldMapSerializer"/> class.
    /// </summary>
    /// <param name="fieldTypes">The fields.</param>
    protected CustomFieldMapSerializer(
      IEnumerable<KeyValuePair<string, Type>> fieldTypes)
    {
      var map = _fieldTypes = new Dictionary<string, Type>();
      foreach (var pair in fieldTypes)
      {
        map.Add(pair.Key, pair.Value);
      }
    }

    /// <summary>
    /// Construct the target object from the deserialized field values.
    /// </summary>
    /// <param name="fieldValues">The field values.</param>
    /// <returns>System.Object.</returns>
    protected abstract object Construct(IReadOnlyDictionary<string, object> fieldValues);

    /// <summary>
    /// Gets the field values to serialize from the specified object.
    /// </summary>
    /// <param name="data">The object to serialize</param>
    /// <returns>IReadOnlyDictionary&lt;System.String, System.Object&gt;.</returns>
    protected abstract IReadOnlyDictionary<string, object> GetFieldValues(object data);

    /// <summary>
    /// Determines whether this serializer can handle the specified type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><c>true</c> if this instance can handle the specified type; otherwise, <c>false</c>.</returns>
    public abstract bool CanHandle(Type type);

    /// <summary>
    /// Reads the value.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="type">The type.</param>
    /// <returns>System.Object.</returns>
    /// <exception cref="System.Runtime.Serialization.SerializationException"></exception>
    object ISimpleXmlSerializer.ReadValue(
      XmlReader reader, SimpleXmlSerializer settings, Type type)
    {
      var values = new Dictionary<string, object>();
      var fields = _fieldTypes;
      if (fields.Count == 0)
      {
        reader.Skip();
      }
      else
      {
        reader.ReadStartElement();
        while (reader.IsStartElement())
        {
          var name = reader.LocalName;
          Type fieldType;
          if (!fields.TryGetValue(name, out fieldType))
          {
            throw new SerializationException(
              $"{name}: unknown field in type {type}");
          }
          var v = SimpleXmlSerializationUtility.ReadValue(reader, settings, fieldType);
          values.Add(name, v);
        }
        reader.ReadEndElement();
      }
      return Construct(values);
    }

    /// <summary>
    /// Serializes the object data to the XML writer.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="data">The data.</param>
    void ISimpleXmlSerializer.WriteValue(
      XmlWriter writer, SimpleXmlSerializer settings, object data)
    {
      var values = GetFieldValues(data);
      if (values == null) return;

      var fields = _fieldTypes;
      foreach (var pair in fields)
      {
        var key = pair.Key;
        object obj;
        if (!values.TryGetValue(key, out obj)) continue;

        SimpleXmlSerializationUtility.WriteItem(writer, settings, key, pair.Value, true, obj);
      }
    }
  }
}
