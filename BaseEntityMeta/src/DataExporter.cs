// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using BaseEntity.Core.Logging;
using BaseEntity.Shared;
using log4net;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Class to export data from the database to an XML file.
  /// </summary>
  public class DataExporter
  {
    private static readonly ILog Logger = QLogManager.GetLogger(typeof(DataExporter));

    // ReSharper disable once NotAccessedField.Local
    private readonly IDataExporterRegistry _dataExporterRegistry;

    /// <summary>
    /// The WebMathTraining XML Schema namespace
    /// </summary>
    public const string WebMathTrainingSchemaNamespace = "http://WebMathTrainingsolutions.com/Schema";

    /// <summary>
    /// The WebMathTraining XML Sparse Schema namespace.
    /// Used for import of partial data.
    /// </summary>
    public const string WebMathTrainingSparseSchemaNamespace = "http://WebMathTrainingsolutions.com/Schema-Sparse";

    // Used to export key and element values
    //private delegate void ValueExporter(string name, object obj, XmlNode parentNode);

    // Used to export a property value
    //private delegate void PropertyExporter(PropertyMeta pm, BaseEntityObject ro, XmlNode propNode);

    //private readonly bool skipAuditInfo_;
    private readonly bool _schemaCompliant;
    private XmlNamespaceManager _nsmgr;
    private readonly string _WebMathTrainingNs;

    #region Constructors

    /// <summary>
    /// Construct default instance
    /// </summary>
    public DataExporter()
      : this(true, "%Y%m%d", false)
    {}

    /// <summary>
    ///
    /// </summary>
    /// <param name="skipAudit"></param>
    public DataExporter(bool skipAudit)
      : this(skipAudit, "%Y%m%d", false)
    {}

    /// <summary>
    ///
    /// </summary>
    /// <param name="skipAudit"></param>
    /// <param name="dtFormat"></param>
    public DataExporter(bool skipAudit, string dtFormat) : this(skipAudit, dtFormat, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataExporter"/> class.
    /// </summary>
    /// <param name="skipAudit">if set to <c>true</c> [skip audit].</param>
    /// <param name="dtFormat">The dt format.</param>
    /// <param name="schemaCompliant">if set to <c>true</c> [schema compliant].</param>
    public DataExporter(bool skipAudit, string dtFormat, bool schemaCompliant)
    {
      
      DtFormat = dtFormat;
      _schemaCompliant = schemaCompliant;

      if (_schemaCompliant)
      {
        _WebMathTrainingNs = WebMathTrainingSchemaNamespace;
      }

      _dataExporterRegistry = new DataExporterRegistry {SkipAuditInfo = skipAudit};
    }
    #endregion

    #region Properties

    /// <summary>
    ///
    /// </summary>
    public string DtFormat { get; }

    /// <summary>
    ///
    /// </summary>
    public string XmlSchemaCompliantDtFormat { get; } = "%Y-%m-%d";
    #endregion

    /// <summary>
    /// Convert a RiskObject to XML
    /// </summary>
    /// <param name="obj">object to convert</param>
    /// <returns>XMLDocument containing an XML representation of the object</returns>
    public XmlDocument ExportToMemory(PersistentObject obj)
    {
      XmlDocument doc = new XmlDocument();

      ArrayList lst = new ArrayList {obj};

      // Generate xml list of all the objects
      int errItemCount;
      ExportObjectList(lst, doc, out errItemCount);

      return doc;
    }

    /// <summary>
    /// Export a single object into an XML file
    /// </summary>
    /// <param name="fileName">file to write to. File will be overwritten if it exists</param>
    /// <param name="obj">RiskObject to export to the file</param>
    /// <returns>0=success;-1=error;-2=some exported and some in error</returns>
    public int Export(string fileName, BaseEntityObject obj)
    {
      ArrayList lst = new ArrayList {obj};

      return Export(fileName, lst);
    }

    /// <summary>
    /// Export database objects into an XML file
    /// </summary>
    /// <param name="fileName">file to write to. File will be overwritten if it exists</param>
    /// <param name="data">list of objects to write to the file</param>
    /// <returns>0=no errors;-1=all errors;-2=some errors</returns>
    public int Export(string fileName, IList data)
    {
      Logger.InfoFormat("Exporting {0} objects to {1}.", data.Count, fileName);

      XmlDocument doc = new XmlDocument();

      if (_schemaCompliant)
      {
        _nsmgr = new XmlNamespaceManager(doc.NameTable);
        _nsmgr.AddNamespace("q", _WebMathTrainingNs);
      }

      // Generate xml list of all the objects
      int errItemCount;
      if (ExportObjectList(data, doc, out errItemCount) == false)
        return -1;

      // Write to file
      using (var writer = new XmlTextWriter(fileName, Encoding.UTF8))
      {
        writer.Formatting = Formatting.Indented;
        doc.WriteTo(writer);
        writer.Flush();
      }

      if (errItemCount != 0)
        return -2;

      return 0;
    }

    /// <summary>
    /// Create a DOM document containing xml representations of all the objects in
    /// the list passed in.
    /// </summary>
    /// <param name="data">list of RiskObjects to serialize into the DOM</param>
    /// <param name="doc">Xml document to populate</param>
    /// <param name="errItemCount">number of items in error</param>
    /// <returns>true=success. at least some items exported;false=notsogood</returns>
    public bool ExportObjectList(IList data, XmlDocument doc, out int errItemCount)
    {
      XmlNode root = doc.CreateElement("WebMathTrainingdata", _WebMathTrainingNs);

      doc.AppendChild(root);

      // store the version
      XmlAttribute aVersion = doc.CreateAttribute("ver");
      aVersion.Value = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
      if (root.Attributes != null)
      {
        root.Attributes.Append(aVersion);

        // store the time
        XmlAttribute aTime = doc.CreateAttribute("time");
        aTime.Value = DateTime.UtcNow.ToString("o");
        root.Attributes.Append(aTime);

        if (!_schemaCompliant)
        {
          // store culture used during export
          XmlAttribute aCulture = doc.CreateAttribute("culture");
          aCulture.Value = System.Threading.Thread.CurrentThread.CurrentCulture.Name;
          root.Attributes.Append(aCulture);
        }
      }

      // Add each objects export xml to this document
      int iCount = 0;
      errItemCount = 0;
      int iMax = data.Count;

      foreach (object o in data)
      {
        iCount++;
        Logger.InfoFormat("Exporting {0} of {1}. Export is {2}% complete", iCount, iMax, iCount * 100 / iMax);

        try
        {
          // TODO: figure out where ExportObject belongs
          root.AppendChild(PropertyExporter.ExportObject((BaseEntityObject)o, doc));
        }
        catch (Exception ex)
        {
          Logger.ErrorFormat("Exception caught exporting object {0}", o.GetType().Name);
          Logger.Error(ex.Message);
          Logger.Debug(ex.ToString());
          errItemCount++;
        }
      }

      return (iCount > errItemCount);
    }

    #region Value Exporters

    #endregion

    #region XMLUtilities

    /// <summary>
    /// Given an XML document this will return a list of key/value pairs where the key is an xpath and the value
    /// is the value at that xpath.
    /// This returns a list for all the leaf nodes in the xml.
    /// </summary>
    /// <param name="doc">xml to parse</param>
    /// <returns>list of xpath/value pairs</returns>
    public List<DictionaryEntry> DumpAllXPaths(XmlNode doc)
    {
      List<DictionaryEntry> lst = new List<DictionaryEntry>();

      RecurseXml(lst, doc, "");

      return lst;
    }

    /// <summary>
    /// Recurse through an XML document and build a list of all XPaths and values for each leaf node
    /// </summary>
    /// <param name="lst">list to populate</param>
    /// <param name="n">Current xml node</param>
    /// <param name="xPath">current xml xpath</param>
    private static void RecurseXml(List<DictionaryEntry> lst, XmlNode n, string xPath)
    {
      if (n == null)
        return;

      // If all children have the same name then we need an index in the name
      bool needIndex = false;
      for (int x = 1; x < n.ChildNodes.Count; x++)
      {
        if (n.ChildNodes[x].Name == n.ChildNodes[x - 1].Name)
          needIndex = true;
      }

      int childCount = 0;

      foreach (XmlNode nChild in n.ChildNodes)
      {
        childCount++;

        string childName = nChild.Name;

        if (needIndex)
          childName += "[" + childCount + "]";

        string childXPath = String.IsNullOrEmpty(xPath) ? childName : xPath + "/" + childName;
        string childValue = nChild.ChildNodes.Count > 0 ? nChild.ChildNodes[0].InnerText : nChild.InnerText;

        if (nChild.ChildNodes.Count == 0 || nChild.ChildNodes.Count == 1 && nChild.ChildNodes[0].Name == "#text")
          lst.Add(new DictionaryEntry(childXPath, childValue));
        else
          RecurseXml(lst, nChild, childXPath); // Recurse
      }
    }

    #endregion
  }
}