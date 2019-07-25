//
// Copyright (c)    2017. All rights reserved.
//
using System;
using System.Xml;

namespace BaseEntity.Toolkit.Base.Serialization
{
  /// <summary>
  /// Interface ISimpleXmlSerializer
  /// </summary>
  public interface ISimpleXmlSerializer
  {
    /// <summary>
    /// Determines whether this serializer can handle the specified type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><c>true</c> if this instance can handle the specified type; otherwise, <c>false</c>.</returns>
    bool CanHandle(Type type);

    /// <summary>
    /// Reads the value.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="type">The type.</param>
    /// <returns>System.Object.</returns>
    object ReadValue(XmlReader reader, SimpleXmlSerializer settings, Type type);

    /// <summary>
    /// Serializes the object data to the XML writer.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="data">The data.</param>
    void WriteValue(XmlWriter writer, SimpleXmlSerializer settings, object data);
  }
}
