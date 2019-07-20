// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using BaseEntity.Configuration;
using BaseEntity.Shared;

namespace BaseEntity.Metadata
{
  /// <summary>
  /// Class to import data into the database
  /// </summary>
  public class DataImporter : IDataImporter
  {
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(DataImporter));

    private static readonly IDataImporterRegistry DataImporterRegistry = new DataImporterRegistry();

    // Used to import key and element values
    //private delegate object ValueImporter(Type type, string strValue);

    // Used to import a property value
    //private delegate object PropertyImporter(object obj, PropertyMeta pm, XmlNode propNode);

    #region Data

    private readonly Dictionary<string, PersistentObject> _entityCache = new Dictionary<string, PersistentObject>();
    private bool _schemaCompliantMode;
    private XmlNamespaceManager _nsmgr;

    #endregion

    /// <summary>
    /// If true, any property not found in import file will be set to its default value, 
    /// otherwise for existing entities these properties will retain their existing value.
    /// </summary>
    /// <remarks>
    /// The default is false.
    /// </remarks>
    public bool SetMissingPropertyValues { get; set; }

    /// <summary>
    /// Cache of already imported objects by business key
    /// </summary>
    public Dictionary<string, PersistentObject> EntityCache => _entityCache;

    #region Throw

    /// <summary>
    /// Import a list of XML objects into the database from a file.
    /// </summary>
    public IEnumerable<PersistentObject> Import(String fileName)
    {
      return Import(fileName, ImportMode.InsertOrUpdate);
    }

    /// <summary>
    /// Import a list of XML objects into the database from a file.
    /// </summary>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="importMode">Import Mode</param>
    /// <returns></returns>
    public IEnumerable<PersistentObject> Import(String fileName, ImportMode importMode)
    {
      if (!File.Exists(fileName))
      {
        throw new FileNotFoundException($"File [{fileName}] does not exist", fileName);
      }

      var document = new XmlDocument();
      document.Load(fileName);

      bool schemaSparseMode;
      _schemaCompliantMode = ReadNamespaceInfo(document, out schemaSparseMode, out _nsmgr);
      if (_schemaCompliantMode)
      {
        if (_nsmgr == null)
        {
          Logger.ErrorFormat("Unknown schema {0}.", document.DocumentElement?.NamespaceURI);
          return null;
        }

        Logger.InfoFormat(!schemaSparseMode ? "Processing xml with regular schema." : "Processing xml with sparse schema.");
      }

      // Verify file format
      var rootElement = (XmlElement)(_schemaCompliantMode 
        ? document.SelectSingleNode("//q:WebMathTrainingdata", _nsmgr) 
        : document.SelectSingleNode("//WebMathTrainingdata"));

      if (rootElement == null)
      {
        throw new ArgumentException("Import file must contain <WebMathTrainingdata>", nameof(fileName));
      }

      CheckVersion(rootElement);
      CheckCulture(rootElement);

      return ImportObjectList(rootElement, importMode);
    }

    private IEnumerable<PersistentObject> ImportObjectList(XmlElement element, ImportMode importMode)
    {
      const bool forceNew = false;

      var list = new List<PersistentObject>();

      // forceNew (or, better, ForceInsert) should be another ImportMode
      if (forceNew && importMode == ImportMode.Update)
      {
        throw new NotSupportedException("importMode of Update with forceNew of true is contradictory");
      }

      _entityCache.Clear();

      Int32 importedObjects = 0;
      Int32 objectsToImport = element.ChildNodes.Count;

      foreach (XmlNode node in element.ChildNodes)
      {
        var @object = ImportObject((XmlElement)node, importMode, forceNew);
        importedObjects++;
        Logger.DebugFormat("Read {0}. {1} of {2}. {3}% complete.", @object.GetType().Name, importedObjects, objectsToImport,
                           100 * importedObjects / objectsToImport);
        list.Add(@object);
      }

      return list;
    }

    private PersistentObject ImportObject(XmlElement element, ImportMode importMode, Boolean forceNew)
    {
      var context = (IEditableEntityContext)EntityContext.Current;

      var classMeta = ClassCache.Find(element.Name);
      if (classMeta == null)
      {
        throw new MetadataException($"Invalid object type [{element.Name}]");
      }

      if (Logger.IsVerboseEnabled())
      {
        Logger.Verbose($"Importing a {classMeta.Name}");
      }

      PersistentObject @object = null;

      IList<Object> key = ImportKey(classMeta, element);
      if (!forceNew)
      {
        @object = FindByKey(classMeta, key);
      }

      if (@object == null)
      {
        // Import new object only if the import mode is either 'Insert' or 'InsertOrUpdate'
        if (importMode == ImportMode.Update)
        {
          Logger.WarnFormat("New object found but import mode is Update. Skipping {0} [{1}]", classMeta.Name, key);
        }
        else
        {
          if (Logger.IsVerboseEnabled())
          {
            Logger.Verbose($"Inserting entity: {string.Join("|", key)}");
          }

          @object = ImportEntity(classMeta, element);
          context.Save(@object);
        }
      }
      else
      {
        // Update existing object only if the import mode is either 'Update' or 'InsertOrUpdate'
        if (importMode == ImportMode.Insert)
        {
          Logger.WarnFormat("Existing object found but import mode is Insert. Skipping {0} [{1}]", classMeta.Name, key);
        }
        else
        {
          if (Logger.IsVerboseEnabled())
          {
            Logger.Verbose($"Updating entity: {string.Join("|", key)}");
          }

          @object.RequestUpdate();
          ImportObjectProperties(element, @object, classMeta);
        }
      }

      if (@object != null)
      {
        if (Logger.IsVerboseEnabled())
        {
          Logger.Verbose($"Validating {@object}");
        }

        @object.Validate();
      }

      return @object;
    }

    /// <summary>
    /// Import new entity instance from an XML representation
    /// </summary>
    /// <param name="cm">class meta definition of object to create</param>
    /// <param name="n">XML node representing the object</param>
    /// <returns>new object created based on XML</returns>
    public PersistentObject ImportEntity(ClassMeta cm, XmlNode n)
    {
      var obj = (PersistentObject)cm.CreateInstance();

      ImportEntityProperties(n, obj, cm);

      return obj;
    }

    /// <summary>
    /// Parse the object meta data and populate properties from XML
    /// </summary>
    /// <param name="n">XML node representing the object</param>
    /// <param name="obj">object to populate</param>
    /// <param name="cm">Meta description of the object</param>
    /// <returns></returns>
    public void ImportEntityProperties(XmlNode n, PersistentObject obj, ClassMeta cm)
    {
      if (cm.HasKey)
      {
        IList<object> keyValues = ImportKey(cm, n);
        var keyMeta = cm; 
        while (keyMeta.IsDerivedEntity)
          keyMeta = keyMeta.BaseEntity;
        string key = PersistentObjectUtil.FormKey(keyMeta, keyValues);
        _entityCache[key] = obj;
      }

      ImportObjectProperties(n, obj, cm);
    }

    /// <summary>
    /// Import child key values
    /// </summary>
    public IList<object> ImportChildKey(ClassMeta cm, XmlNode objNode)
    {
      var keyPropList = cm.ChildKeyPropertyList;
      if (keyPropList == null)
      {
        throw new MetadataException("Entity [" + cm.Name + "] does not have a dependent key defined.");
      }

      return ImportKey(objNode, keyPropList);
    }


    /// <summary>
    /// Imports the object properties.
    /// </summary>
    /// <param name="n">The n.</param>
    /// <param name="obj">The object.</param>
    /// <param name="cm">The cm.</param>
    /// <exception cref="System.Exception"></exception>
    public void ImportObjectProperties(XmlNode n, IBaseEntityObject obj, ClassMeta cm)
    {
      var notFound = new HashSet<int>();

      foreach (PropertyMeta pm in cm.PropertyList)
      {
        if (!pm.Persistent)
          continue;

        if (pm.PropertyInfo.DeclaringType == typeof(PersistentObject) ||
            pm.PropertyInfo.DeclaringType == typeof(VersionedObject))
        {
          continue;
        }

        if (pm.PropertyInfo.DeclaringType == typeof(AuditedObject))
        {
          if (cm.OldStyleValidFrom && pm.Name == "ValidFrom" && cm.KeyPropertyNames != null && cm.KeyPropertyNames.Contains("ValidFrom"))
          {
            // If this is a standard old-style ValidFrom entity then we treat ValidFrom just like any other property
          }
          else
          {
            // If this is a new-style ValidFrom entity or a special-case old-style one (e.g. Quote) then ignore ValidFrom
            continue;
          }
        }

        XmlNode nPropXml = _schemaCompliantMode ? n.SelectSingleNode("q:" + pm.Name, _nsmgr) : n.SelectSingleNode(pm.Name);
        if (nPropXml == null)
        {
          notFound.Add(pm.Index);
          continue;
        }

        if (Logger.IsVerboseEnabled())
        {
          Logger.Verbose($"Importing property {pm.Name}");
        }

        Type type = pm.GetType();
        var import = DataImporterRegistry.GetPropertyImporter(type);
        if (import != null)
        {
          object propVal = import(this, obj, pm, nPropXml);
          pm.SetValue(obj, propVal);
        }
      }

      if (SetMissingPropertyValues)
      {
        // Any property that was not found is set to its default value
        foreach (var pm in cm.PersistentPropertyMap.Values.Where(pm => notFound.Contains(pm.Index) && !pm.HasDefaultValue((BaseEntityObject)obj)))
        {
          var cascade = pm as ICascade;
          if (cascade?.InverseCascade == null || !cascade.InverseCascade.IsInverse)
            pm.SetDefaultValue((BaseEntityObject)obj);
        }
      }
    }

    /// <summary>
    /// Finds the <see cref="PersistentObject"/> by key.
    /// </summary>
    /// <param name="cm">The cm.</param>
    /// <param name="keyList">The key.</param>
    /// <returns></returns>
    public PersistentObject FindByKey(ClassMeta cm, IList<object> keyList)
    {
      while (cm.IsDerivedEntity)
        cm = cm.BaseEntity;
      string hashKey = PersistentObjectUtil.FormKey(cm, keyList);
      if (_entityCache.ContainsKey(hashKey))
        return _entityCache[hashKey];

      var context = (IQueryableEntityContext)EntityContext.Current;
      var po = context.FindByKey(cm, keyList);
      if (po == null)
      {
        return null;
      }

      _entityCache[PersistentObjectUtil.FormKey(po, true)] = po;

      return po;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rootNode"></param>
    public static void CheckCulture(XmlNode rootNode)
    {
      if (rootNode == null)
      {
        throw new ArgumentNullException(nameof(rootNode));
      }

      // Check for culture attribute of import file
      if (rootNode.Attributes?["culture"] != null)
      {
        // check if current culture matchs that of import file.
        if (!rootNode.Attributes["culture"].Value.Equals(Thread.CurrentThread.CurrentCulture.Name, StringComparison.OrdinalIgnoreCase))
        {
          // File Culture and Current Culture did not match 
          // Override Culture
          Logger.WarnFormat("Current culture [{0}] does not match import file culture [{1}]. Overriding current culture with file culture",
                            Thread.CurrentThread.CurrentCulture.Name, rootNode.Attributes["culture"].Value);
          var fileCulture = new CultureInfo(rootNode.Attributes["culture"].Value);
          Thread.CurrentThread.CurrentCulture = fileCulture;
        }
        else
        {
          Logger.DebugFormat("Current culture [{0}] matches import file culture", Thread.CurrentThread.CurrentCulture.Name);
        }
      }
      else
      {
        // No culture attribute found. must be older export 
        Logger.Warn("Warning : Culture of import file is unknown");
      }
    }

    private static void CheckVersion(XmlNode rootNode)
    {
      string versionName = Assembly.GetExecutingAssembly().GetName().Version.ToString();
      if (rootNode.Attributes?["ver"] != null && rootNode.Attributes["ver"].Value != versionName)
      {
        Logger.WarnFormat($"Warning file version does not match {versionName}");
      }
    }

    #endregion

    #region Don't throw

    /// <summary>
    /// Import a list of XML objects into the database from a file.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="errItemCount"></param>
    public IList Import(string fileName, out int errItemCount)
    {
      return Import(fileName, ImportMode.InsertOrUpdate, out errItemCount);
    }

    /// <summary>
    /// Import a list of XML objects into the database from a file.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="importMode">Import Mode</param>
    /// <param name="errItemCount"></param>
    public IList Import(string fileName, ImportMode importMode, out int errItemCount)
    {
      errItemCount = 0;

      if (!File.Exists(fileName))
      {
        Logger.ErrorFormat("File {0} cannot be found.", fileName);

        return new ArrayList();
      }

      try
      {
        var doc = new XmlDocument();

        doc.Load(fileName);

        return ImportObjectList(doc, importMode, out errItemCount);
      }
      catch (Exception ex)
      {
        Logger.ErrorFormat("Invalid XML in file {0}", fileName);
        Logger.Debug(ex.ToString());
        return new ArrayList();
      }
    }

    /// <summary>
    /// Import a list of xml objects into memory from an DOM Document
    /// </summary>
    /// <param name="doc">xml list of RiskObjects</param>
    /// <param name="errItemCount">number of items in error</param>
    /// <returns></returns>
    public IList ImportObjectList(XmlDocument doc, out int errItemCount)
    {
      return ImportObjectList(doc, out errItemCount, false);
    }

    /// <summary>
    /// Import a list of xml objects into memory from an DOM Document
    /// </summary>
    /// <param name="doc">xml list of RiskObjects</param>
    /// <param name="importMode">Import Mode</param>
    /// <param name="errItemCount">number of items in error</param>
    /// <returns></returns>
    public IList ImportObjectList(XmlDocument doc, ImportMode importMode, out int errItemCount)
    {
      return ImportObjectList(doc, importMode, out errItemCount, false);
    }

    /// <summary>
    /// Import a list of xml objects into memory from a DOM document
    /// </summary>
    /// <param name="doc"></param>
    /// <param name="errItemCount"></param>
    /// <param name="noUpdate">true-assume objects are new instances so dont search for existing object to update</param>
    /// <returns></returns>
    public IList ImportObjectList(XmlDocument doc, out int errItemCount, bool noUpdate)
    {
      return ImportObjectList(doc, ImportMode.InsertOrUpdate, out errItemCount, noUpdate);
    }

    /// <summary>
    /// Import a list of xml objects into memory from a DOM document
    /// </summary>
    /// <param name="doc"></param>
    /// <param name="importMode">Import Mode</param>
    /// <param name="errItemCount"></param>
    /// <param name="forceNew">true-assume objects are new instances so dont search for existing object to update</param>
    /// <param name="clearEntityCache">clear cache of entities already resolved by this importer</param>
    /// <returns></returns>
    public IList ImportObjectList(XmlDocument doc, ImportMode importMode, out int errItemCount, bool forceNew, bool clearEntityCache = true)
    {
      string errorMsg;
      return ImportObjectList(doc, importMode, out errItemCount, out errorMsg, forceNew, clearEntityCache);
    }

    /// <summary>
      /// Import a list of xml objects into memory from a DOM document
      /// </summary>
      /// <param name="doc"></param>
      /// <param name="importMode">Import Mode</param>
      /// <param name="errItemCount"></param>
      /// <param name="errorMsg"></param>
      /// <param name="forceNew">true-assume objects are new instances so dont search for existing object to update</param>
      /// <param name="clearEntityCache">clear cache of entities already resolved by this importer</param>
      /// <returns></returns>
      public IList ImportObjectList(XmlDocument doc, ImportMode importMode, out int errItemCount, out string errorMsg, bool forceNew, bool clearEntityCache = true)
    {
      var context = (IEditableEntityContext)EntityContext.Current;
      bool schemaSparseMode;
      _schemaCompliantMode = ReadNamespaceInfo(doc, out schemaSparseMode, out _nsmgr);
      if (_schemaCompliantMode)
      {
        if (_nsmgr == null)
        {
          errorMsg = "Unknown schema.";
          Logger.ErrorFormat(errorMsg);
          errItemCount = 1;
          return null;
        }

        Logger.InfoFormat(!schemaSparseMode ? "Processing xml with regular schema." : "Processing xml with sparse schema.");
      }

      // Verify file format
      XmlNode nRoot = _schemaCompliantMode 
        ? doc.SelectSingleNode("//q:WebMathTrainingdata", _nsmgr) 
        : doc.SelectSingleNode("//WebMathTrainingdata");

      if (nRoot == null)
      {
        errorMsg = "Invalid file format. WebMathTrainingdata node not found";
        Logger.Error(errorMsg);
        errItemCount = -1;
        return null;
      }

      string versionName = Assembly.GetExecutingAssembly().GetName().Version.ToString();

      if (nRoot.Attributes?["ver"] != null && nRoot.Attributes["ver"].Value != versionName)
      {
        Logger.WarnFormat($"Warning file version does not match {versionName}");
      }

      CheckCulture(nRoot);

      if (clearEntityCache)
      {
        _entityCache.Clear();
      }

      var lst = new ArrayList();

      int iCount = 0;
      errItemCount = 0;
      errorMsg = null;
      int iMax = nRoot.ChildNodes.Count;

      foreach (XmlNode objNode in nRoot.ChildNodes)
      {
        PersistentObject obj = null;

        try
        {
          ClassMeta classMeta;

          try
          {
            classMeta = ClassCache.Find(objNode.Name);
          }
          catch (Exception)
          {
            throw new Exception($"Invalid object type [{objNode.Name}]");
          }

          if (classMeta == null)
            throw new Exception($"Invalid object type [{objNode.Name}]");

          if (Logger.IsVerboseEnabled())
          {
            Logger.Verbose($"Importing a {classMeta.Name}");
          }

          IList<object> key = ImportKey(classMeta, objNode);

          if (!forceNew)
            obj = FindByKey(classMeta, key);

          if (obj == null)
          {
            // Import new object only if the import mode is 
            // either 'Insert' or 'InsertOrUpdate'
            if (importMode == ImportMode.Update)
              continue;

            if (Logger.IsVerboseEnabled())
            {
              Logger.Verbose($"Inserting entity: {string.Join("|", key)}");
            }

            obj = ImportEntity(classMeta, objNode);
            context.Save(obj);
          }
          else
          {
            // Update existing object only if the import mode is 
            // either 'Update' or 'InsertOrUpdate'
            if (importMode == ImportMode.Insert)
              continue;

            if (Logger.IsVerboseEnabled())
            {
              Logger.Verbose($"Updating entity: {string.Join("|", key)}");
            }

            // Update existing object
            obj.RequestUpdate();
            ImportObjectProperties(objNode, obj, classMeta);
          }

          if (Logger.IsVerboseEnabled())
          {
            Logger.Verbose($"Validating {obj}");
          }

          InvalidValue[] errors = obj.TryValidate();
          if (errors != null)
          {
            var keyString = new StringBuilder();

            if (key != null)
            {
              string delim = "";
              foreach (object o in key)
              {
                keyString.Append(o + delim);
                delim = "/";
              }
            }

            var sb = new StringBuilder();
            foreach (InvalidValue error in errors)
            {
              var msg = string.Format("ERROR: [{0}] {1}", keyString, error);
              Logger.ErrorFormat(msg);
              sb.AppendLine(msg);
            }

            errorMsg = sb.ToString();
            errItemCount++;
          }
          else
          {
            // derive any dependent properties
            obj.DeriveValues();

            iCount++;
            lst.Add(obj);
            Logger.DebugFormat("Read {0}. {1} of {2}. {3}% complete.", obj.GetType().Name, iCount, iMax, 100 * iCount / iMax);
          }
        }
        catch (Exception ex)
        {
          errorMsg = Logger.IsDebugEnabled ? ex.ToString() : ex.Message;

          if (Logger.IsDebugEnabled)
            Logger.DebugFormat("Error importing [{0}] : {1}", objNode.OuterXml, errorMsg);
          else
            Logger.ErrorFormat("Error importing: {0}", errorMsg);

          errItemCount++;
        }
      }

      return lst;
    }

    /// <summary>
    /// Import component from an XML representation
    /// </summary>
    /// <param name="cm">class meta definition of object to create</param>
    /// <param name="n">XML node representing the object</param>
    /// <returns>new object created based on XML</returns>
    public IBaseEntityObject ImportComponent(ClassMeta cm, XmlNode n)
    {
      IBaseEntityObject obj = cm.CreateInstance();

      ImportObjectProperties(n, obj, cm);

      return obj;
    }

    /// <summary>
    /// Import key values
    /// </summary>
    public IList<object> ImportKey(ClassMeta cm, XmlNode objNode)
    {
      var keyPropList = cm.KeyPropertyList;
      if (keyPropList.Count == 0)
      {
        throw new MetadataException("Entity [" + cm.Name + "] does not have a well-defined business key.");
      }

      return ImportKey(objNode, keyPropList);
    }

    private IList<object> ImportKey(XmlNode objNode, IEnumerable<PropertyMeta> keyPropList)
    {
      var key = new List<object>();
      foreach (PropertyMeta keyProp in keyPropList)
      {
        XmlNode keyNode = _schemaCompliantMode 
          ? objNode.SelectSingleNode("q:" + keyProp.Name, _nsmgr) 
          : objNode.SelectSingleNode(keyProp.Name);

        if (keyNode == null)
        {
          //throw new MetadataException("Invalid XML. Missing Key Node [" + keyProp.Name + "].");
          key.Add(null);
          continue;
        }

        Type type = keyProp.GetType();
        var import = DataImporterRegistry.GetPropertyImporter(type);
        if (import == null)
        {
          throw new MetadataException("Unable to import values of type [" + type + "]");
        }
        key.Add(import(this, null, keyProp, keyNode));
      }

      return key;
    }

    /// <summary>
    /// 
    /// </summary>
    public void ClearEntityCache()
    {
      _entityCache.Clear();
    }

    #endregion

    /// <summary>
    /// Reads the namespace and determines schema mode.
    /// </summary>
    /// <returns> Is schema compliant. </returns>
    public static bool ReadNamespaceInfo(XmlDocument doc, out bool schemaSparseMode, out XmlNamespaceManager nsmgr)
    {
      if (!string.IsNullOrEmpty(doc?.DocumentElement?.NamespaceURI))
      {
        var nameSpace = doc.DocumentElement.NamespaceURI;

        if (string.CompareOrdinal(DataExporter.WebMathTrainingSchemaNamespace, nameSpace) == 0)
        {
          schemaSparseMode = false;
          nsmgr = new XmlNamespaceManager(doc.NameTable);
          nsmgr.AddNamespace("q", DataExporter.WebMathTrainingSchemaNamespace);

          Logger.InfoFormat("Processing xml with regular schema: {0}.", nameSpace);
        }
        else if (string.CompareOrdinal(DataExporter.WebMathTrainingSparseSchemaNamespace, nameSpace) == 0)
        {
          schemaSparseMode = true;
          nsmgr = new XmlNamespaceManager(doc.NameTable);
          nsmgr.AddNamespace("q", DataExporter.WebMathTrainingSparseSchemaNamespace);

          Logger.InfoFormat("Processing xml with sparse schema: {0}.", nameSpace);
        }
        else
        {
          Logger.ErrorFormat("Unknown schema {0}.", nameSpace);
          nsmgr = null;
          schemaSparseMode = false;
        }
        return true;
      }
      schemaSparseMode = false;
      nsmgr = null;
      return false;
    }

    #region Property Importers

    #endregion

    #region Value Importers

    #endregion

    private IEditableEntityContext CreateEntityContext(DateTime asOf, bool setValidFrom)
    {
      return (IEditableEntityContext)EntityContextFactory.Create(asOf, ReadWriteMode.ReadWrite, setValidFrom);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="importMode"></param>
    /// <param name="setValidFrom"></param>
    /// <param name="asOf"></param>
    /// <returns></returns>
    public int Import(string fileName, ImportMode importMode, bool setValidFrom, DateTime asOf)
    {
      using (new EntityContextBinder(asOf, setValidFrom))
      {
        var context = (IEditableEntityContext)EntityContext.Current;

        try
        {
          int errItemCount;

          var stopwatch = new Stopwatch();
          stopwatch.Start();

          IList list = Import(fileName, importMode, out errItemCount);

          if (errItemCount > 0)
          {
            context.RollbackTransaction();
            Logger.Error($"{errItemCount} invalid objects in import file!");
          }
          else
          {
            context.CommitTransaction();
            stopwatch.Stop();

            Logger.InfoFormat("Import completed. {0} objects imported. Elapsed Time: {1:c}", list?.Count ?? 0, stopwatch.Elapsed);
          }

          return errItemCount;
        }
        catch (Exception ex)
        {
          Logger.Error(ex.Message);
          Logger.Debug(ex.ToString());
          return -1;
        }
      }
    }

  }
}