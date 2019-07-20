// 
// Copyright (c) WebMathTraining 2002-2012. All rights reserved.
// 

using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace BaseEntity.Configuration
{
  /// <summary>
  /// A configuration section for containing an arbitrary serialized object.
  /// </summary>
  public class ObjectConfigurationSection : ConfigurationSection
  {
    private object _data;
    private string _fileName;
    private string _typeName;
    private FileSystemWatcher _watcher;

    /// <summary>
    /// The name of an external file containing the serialized object.
    /// </summary>
    public string FileName
    {
      get { return _fileName; }
      set { _fileName = value; }
    }

    /// <summary>
    /// The name of the type that is serialized.
    /// </summary>
    public string TypeName
    {
      get { return _typeName; }
    }

    /// <summary>
    /// The contained object.
    /// </summary>
    public object Data
    {
      get { return _data; }
      set
      {
        _data = value;
        Type t = _data.GetType();
        _typeName = t.FullName + ", " + t.Assembly.FullName;
      }
    }

    #region Overrides

    /// <summary>
    /// Retrieves the contained object from the section.
    /// </summary>
    /// <returns>The contained data object.</returns>
    protected override object GetRuntimeObject()
    {
      SetWatcher();
      return _data;
    }

    /// <summary>
    /// Deserializes the configuration section in the configuration file.
    /// </summary>
    /// <param name="reader">The reader containing the XML for the section.</param>
    protected override void DeserializeSection(XmlReader reader)
    {
      if (!reader.Read() || (reader.NodeType != XmlNodeType.Element))
      {
        throw new ConfigurationErrorsException("Configuration reader expected to find an element",
                                               reader);
      }
      DeserializeElement(reader,
                         false);
    }

    /// <summary>
    /// Deserializes the configuration element in the configuration file.
    /// </summary>
    /// <param name="reader">The reader containing the XML for the section.</param>
    /// <param name="serializeCollectionKey">true to serialize only the collection key properties; otherwise, false. 
    /// Ignored in this implementation. </param>
    protected override void DeserializeElement(XmlReader reader, bool serializeCollectionKey)
    {
      reader.MoveToContent();
      // Check for invalid usage
      if (reader.AttributeCount > 1)
      {
        throw new ConfigurationErrorsException("Only a single type or fileName attribute is allowed.");
      }
      if (reader.AttributeCount == 0)
      {
        throw new ConfigurationErrorsException("A type or fileName attribute is required.");
      }
      // Determine if we need to get the section from the inline XML or from an external file.
      _fileName = reader.GetAttribute("fileName");
      if (_fileName == null)
      {
        DeserializeData(reader);
        reader.ReadEndElement();
      }
      else
      {
        if (!reader.IsEmptyElement)
        {
          throw new ConfigurationErrorsException("The section element must be empty when using the fileName attribute.");
        }
        using (var file = new FileStream(_fileName,
                                         FileMode.Open,
                                         FileAccess.Read))
        {
          XmlReader rdr = new XmlTextReader(file);
          rdr.MoveToContent();
          DeserializeData(rdr);
          rdr.Close();
        }
      }
    }

    /// <summary>
    /// Serializes the configuration section to an XML string representation.
    /// </summary>
    /// <param name="parentElement">The parent element of this element.</param>
    /// <param name="name">The name of the section.</param>
    /// <param name="saveMode">The mode to use for saving.</param>
    /// <returns>The string representation of the section.</returns>
    protected override string SerializeSection(ConfigurationElement parentElement, string name, ConfigurationSaveMode saveMode)
    {
      var sWriter = new StringWriter(CultureInfo.InvariantCulture);
      var xWriter = new XmlTextWriter(sWriter) {Formatting = Formatting.Indented, Indentation = 4, IndentChar = ' '};
      SerializeToXmlElement(xWriter,
                            name);
      xWriter.Flush();
      return sWriter.ToString();
    }

    /// <summary>
    /// Serializes the section into the configuration file.
    /// </summary>
    /// <param name="writer">The writer to use for serializing the class.</param>
    /// <param name="elementName">The name of the configuration section.</param>
    /// <returns>True if successful, false otherwise.</returns>
    protected override bool SerializeToXmlElement(XmlWriter writer, string elementName)
    {
      // writer could actually be null
      if (writer == null)
      {
        return false;
      }
      writer.WriteStartElement(elementName);
      bool success;
      if (string.IsNullOrEmpty(_fileName))
      {
        success = SerializeElement(writer,
                                   false);
      }
      else
      {
        writer.WriteAttributeString("fileName",
                                    _fileName);
        using (var file = new FileStream(_fileName,
                                         FileMode.Create,
                                         FileAccess.Write))
        {
          var settings = new XmlWriterSettings {Indent = true, IndentChars = ("\t"), OmitXmlDeclaration = false};
          XmlWriter wtr = XmlWriter.Create(file,
                                           settings);
          wtr.WriteStartElement(elementName);
          success = SerializeElement(wtr,
                                     false);
          wtr.WriteEndElement();
          wtr.Flush();
          wtr.Close();
        }
      }
      writer.WriteEndElement();
      return success;
    }

    /// <summary>
    /// Serialize the element to XML.
    /// </summary>
    /// <param name="writer">The XmlWriter to use for the serialization.</param>
    /// <param name="serializeCollectionKey">Flag whether to serialize the collection keys. Not used in this override.</param>
    /// <returns>True if the serialization was successful, false otherwise.</returns>
    protected override bool SerializeElement(XmlWriter writer, bool serializeCollectionKey)
    {
      // writer could actually be null
      if (writer == null)
      {
        return false;
      }
      writer.WriteAttributeString("type",
                                  _typeName);
      var serializer = new XmlSerializer(_data.GetType());
      serializer.Serialize(writer,
                           _data);
      return true;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Deserializes the data from the reader.
    /// </summary>
    /// <param name="reader">The XmlReader containing the serilized data.</param>
    private void DeserializeData(XmlReader reader)
    {
      _typeName = reader.GetAttribute("type");
      if (_typeName == null)
      {
        throw new ConfigurationErrorsException("No 'type' attribute specified");
      }
      var t = Type.GetType(_typeName);
      if (t == null)
      {
        throw new ConfigurationErrorsException(string.Format("Type '{0}' could not be loaded",
                                                             _typeName));
      }
      reader.Read();
      reader.MoveToContent();
      var serializer = new XmlSerializer(t);
      _data = serializer.Deserialize(reader);
    }

    /// <summary>
    /// Determines if a FileSystemWatcher needs to be set on the file
    /// to watch for external changes.
    /// </summary>
    private void SetWatcher()
    {
      if (SectionInformation.RestartOnExternalChanges && !string.IsNullOrEmpty(_fileName))
      {
        if (_watcher == null)
        {
          var configFile = new FileInfo(_fileName);
          if (configFile.DirectoryName != null)
          {
            _watcher = new FileSystemWatcher(configFile.DirectoryName) {Filter = configFile.Name, NotifyFilter = NotifyFilters.LastWrite};
            _watcher.Changed += OnConfigChanged;
            _watcher.EnableRaisingEvents = true;
          }
        }
      }
    }

    /// <summary>
    /// Handle a change event from the FileSystemWatcher.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnConfigChanged(object sender, FileSystemEventArgs e)
    {
      _watcher.EnableRaisingEvents = false;
      _watcher.Changed -= OnConfigChanged;
      ConfigurationManager.RefreshSection(SectionInformation.Name);
    }

    #endregion
  }
}