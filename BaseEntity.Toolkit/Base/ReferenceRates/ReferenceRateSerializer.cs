//
//   2017. All rights reserved.
//
using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Xml;
using BaseEntity.Toolkit.Base.Serialization;

namespace BaseEntity.Toolkit.Base.ReferenceRates
{
  /// <summary>
  /// Custom serializer for <see cref="IReferenceRate"/>
  /// </summary>
  /// <remarks>
  ///   <para>Given <see cref="IReferenceRate"/>s are Immutable and cached, we don't need to serialise
  ///   the full object. We instead save just the key. When we de-serialise we use the key to look up
  ///   the matching pre-defined <see cref="IReferenceRate"/>.</para>
  /// 
  ///   <para>Saves and loads just the key for <see cref="IReferenceRate">Reference Rates</see>
  ///   that are in the <see cref="ReferenceRateCache">Reference Rate Cache</see>.</para>
  /// </remarks>
  internal class ReferenceRateSerializer : ISimpleXmlSerializer
  {
    #region  Methods

    /// <summary>
    /// Register ReferenceRate custom serializer
    /// </summary>
    internal static void Register()
    {
      CustomSerializers.Register(new ReferenceRateSerializer());
    }

    #endregion Methods

    #region ISimpleXmlSerializer members

    /// <summary>
    /// Determines whether this serializer can handle the specified type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><c>true</c> if the specified type is <see cref="IReferenceRate"/>
    ///  or it implements the interface <see cref="IReferenceRate"/>.</returns>
    public bool CanHandle(Type type)
    {
      Debug.Assert(type != null);
      if (type == typeof(IReferenceRate))
        return true;

      var interfaceName = typeof(IReferenceRate).FullName;
      Debug.Assert(interfaceName != null);
      return type.GetInterface(interfaceName, false) != null;
    }

    /// <summary>
    /// Deserializes the object value from the XML stream
    /// </summary>
    /// <remarks>
    ///   <para>If <see cref="IReferenceRate"/> key is specified,
    ///   looks up <see cref="ReferenceRateCache"/>, otherwise deserialises new class.</para>
    /// </remarks>
    /// <param name="reader">The XML reader</param>
    /// <param name="settings">The serialization settings</param>
    /// <param name="type">The target type</param>
    /// <returns>System.Object.</returns>
    public object ReadValue(XmlReader reader, SimpleXmlSerializer settings, Type type)
    {
      if (reader.IsEmptyElement)
      {
        var name = reader.GetAttribute("name");
        reader.Skip();
        return string.IsNullOrEmpty(name)
          ? null // just return null for empty element
          : ReferenceRate.GetValue<IReferenceRate>(name);
      }
      return ReadObjectGraph(reader, settings, type);
    }

    /// <summary>
    /// Serializes the object data to the XML writer.
    /// </summary>
    /// <remarks>
    ///   <para>If <see cref="IReferenceRate"/> is in <see cref="ReferenceRateCache"/>
    ///   just saves key, otherwise saves full class.</para>
    /// </remarks>
    /// <param name="writer">The XML writer</param>
    /// <param name="settings">The serialization settings</param>
    /// <param name="data">The data to serialize</param>
    public void WriteValue(XmlWriter writer, SimpleXmlSerializer settings, object data)
    {
      Debug.Assert(data != null);
      if (!DisableNameLookup)
      {
        var rr = data as IReferenceRate;
        if (rr == null)
          throw new ArgumentException("Internal error: WriteValue type {typeof(data)}, not IReferenceRate");
        if( ReferenceRate.CacheContains(rr) )
        {
          // write an empty element with the name attribute
          writer.WriteAttributeString("name", rr.Key);
          return;
        }
      }
      WriteObjectGraph(writer, settings, data);
    }

    #endregion

    #region Static data members

    [ThreadStatic]
    internal static bool DisableNameLookup; // Disable cache lookup to enable saving full xml if needed

    #endregion

    #region Helpers

    private static object ReadObjectGraph(XmlReader reader, SimpleXmlSerializer settings, Type type)
    {
      var fieldsInfo = settings.GetSerializationInfo(type);
      var obj = FormatterServices.GetUninitializedObject(type);
      return fieldsInfo.ReadValue(reader, settings, obj);
    }

    private static void WriteObjectGraph(XmlWriter writer, SimpleXmlSerializer settings, object data)
    {
      var fieldsInfo = settings.GetSerializationInfo(data.GetType());
      fieldsInfo.WriteValue(writer, settings, data);
    }

    #endregion
  }
}
