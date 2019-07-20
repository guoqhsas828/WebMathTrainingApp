// 
// Copyright (c) WebMathTraining 2002-2015. All rights reserved.
// 

using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using log4net;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public abstract class EntityDiffGram
  {
    private static readonly ILog Log = LogManager.GetLogger(typeof(EntityDiffGram));

    /// <summary>
    /// Represents the AuditHistory XML used to represent no diff (either because no prior version or no change between two versions)
    /// </summary>
    private const string EmptyXml = "<AuditHistory xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://WebMathTrainingsolutions.com/Audit\"></AuditHistory>";

    private static readonly Stream SchemaStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WebMathTraining.Metadata.WebMathTraining.Audit.xsd");
    private string _xml;
    private static XmlSchema _schema;
    private XmlDocument _doc;

    /// <summary>
    /// 
    /// </summary>
    public long RootObjectId { get; set; }

    /// <summary>
    /// Optional comment used to summarize the changes made between PrevTid and NextTid
    /// </summary>
    public string Comment { get; set; }

    /// <summary>
    /// XML representation of the diff
    /// </summary>
    public string Xml
    {
      get { return _xml ?? (_xml = EmptyXml); }
      set { _xml = value; }
    }

    /// <summary>
    /// Return true if the XML representation of the diff contains no content.
    /// </summary>
    public bool IsEmpty => XmlDocument?.DocumentElement?.HasChildNodes == false;

    /// <summary>
    /// Gets or sets the validation error.
    /// </summary>
    /// <value>The validation error.</value>
    public Exception ValidationError { get; set; }

    internal XmlDocument XmlDocument
    {
      get
      {
        if (_doc == null)
        {
          _doc = new XmlDocument();
          if (Xml != null)
            _doc.LoadXml(Xml);
        }
        return _doc;
      }
    }

    /// <summary>
    /// Validates the XML according to its schema.
    /// </summary>
    public void Validate()
    {
      if (_schema == null)
      {
        _schema = XmlSchema.Read(SchemaStream, (s, e) => Log.Error("Failed to read schema resource", e.Exception));
      }
      XmlDocument.Schemas.Add(_schema);
      XmlDocument.Validate((s, e) =>
      {
        Log.Error("Audit XML is invalid", e.Exception);
        ValidationError = e.Exception;
      });
    }
  }
}