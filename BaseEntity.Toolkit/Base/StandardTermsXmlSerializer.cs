//
//   2015-2017. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.Serialization;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  Standard XML serializer for <see cref="StandardTermsCache{T}"/>
  /// </summary>
  public static class StandardTermsXmlSerializer
  {
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(StandardTermsXmlSerializer));

    /// <summary>
    /// Load terms from xml file
    /// </summary>
    /// <remarks>
    ///   <para>Initialises terms from file. If load fails, leaves cache clear and returns false.</para>
    /// </remarks>
    /// <param name="cache">Reference Rate Cache</param>
    /// <param name="xmlFilePath">Name of file to load from</param>
    /// <returns>True if load successful, false if not</returns>
    public static bool LoadFromFile<T>(StandardTermsCache<T> cache, string xmlFilePath) where T : IStandardTerms
    {
      // Clear existing cache contents
      cache.Clear();
      // If empty filename or "default" - use defaults
      if (string.IsNullOrEmpty(xmlFilePath) || string.Compare(xmlFilePath, "built-in", System.StringComparison.OrdinalIgnoreCase) == 0)
        return false;
      // Load
      var list = LoadFromFile<T>(xmlFilePath);
      if (list.Length <= 0)
        return false;
      foreach (var t in list)
      {
        try
        {
          cache.Add(t);
        }
        catch (Exception e)
        {
          Logger.ErrorFormat("Unable to add {0} term {1} from file {2} - {3}, ignoring", typeof(T), t.Key, xmlFilePath, e.Message);
        }
      }
      return true;
    }

    /// <summary>
    /// Load terms from xml file
    /// </summary>
    /// <param name="xmlFilePath">Name of file to load from</param>
    /// <returns>Terms loaded</returns>
    public static T[] LoadFromFile<T>(string xmlFilePath) where T : IStandardTerms
    {
      // Load
      var path = Environment.ExpandEnvironmentVariables(xmlFilePath);
      xmlFilePath = Path.IsPathRooted(path) ? path : Path.Combine(SystemContext.InstallDir, path);
      T[] list;
      try
      {
        var settings = new XmlReaderSettings { IgnoreWhitespace = true };
        using (var xmlReader = XmlReader.Create(xmlFilePath, settings))
        {
          var serializer = new SimpleXmlSerializer(typeof(T[]), GetKnownTypes<T>());
          list = (T[])serializer.ReadObject(xmlReader);
        }
      }
      catch (Exception e)
      {
        Logger.ErrorFormat("Unable to load {0} terms from file {1} : {2}, using built-in defaults", typeof(T), xmlFilePath, e.Message);
        return new T[] { };
      }
      return list;
    }

    /// <summary>
    /// Save Terms to file. Loggs error on failure
    /// </summary>
    /// <param name="xmlFilePath">Name of file to save to</param>
    /// <param name="data">Terms to save</param>
    public static void SaveToFile<T>(string xmlFilePath, IEnumerable<T> data)
    {
      try
      {
        var a = (data as T[]) ?? data.ToArray();
        var settings = new XmlWriterSettings
        {
          OmitXmlDeclaration = true,
          ConformanceLevel = ConformanceLevel.Fragment,
          Indent = true,
        };
        using (var xmlWriter = XmlWriter.Create(xmlFilePath, settings))
        {
          var serializer = new SimpleXmlSerializer(typeof(T[]), GetKnownTypes<T>());
          serializer.WriteObject(xmlWriter, a);
        }
      }
      catch (Exception e)
      {
        Logger.ErrorFormat("Unable to save reference rate terms to file {0} : {1}", xmlFilePath, e.Message);
      }
      return;
    }


    /// <summary>
    /// Load Reference indices from file
    /// </summary>
    public static T[] Load<T>(string xmlFilePath)
    {
      var settings = new XmlReaderSettings { IgnoreWhitespace = true };
      using (var xmlReader = XmlReader.Create(xmlFilePath, settings))
      {
        var serializer = new SimpleXmlSerializer(typeof(T[]), GetKnownTypes<T>());
        return (T[])serializer.ReadObject(xmlReader);
      }
    }

    /// <summary>
    /// Save Reference Indices to file
    /// </summary>
    public static void Save<T>(string xmlFilePath, IEnumerable<T> data)
    {
      var a = (data as T[]) ?? data.ToArray();
      var settings = new XmlWriterSettings
      {
        OmitXmlDeclaration = true,
        ConformanceLevel = ConformanceLevel.Fragment,
        Indent = true,
      };
      using (var xmlWriter = XmlWriter.Create(xmlFilePath, settings))
      {
        var serializer = new SimpleXmlSerializer(typeof(T[]), GetKnownTypes<T>());
        serializer.WriteObject(xmlWriter, a);
      }
      return;
    }

    /// <summary>
    ///  Get known types derived from T
    /// </summary>
    /// <typeparam name="T">Type</typeparam>
    /// <returns>List of all derived types</returns>
    private static IEnumerable<KeyValuePair<string, Type>> GetKnownTypes<T>()
    {
      return typeof(T).Assembly.GetTypes()
        .Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericType)
        .Select(t => new KeyValuePair<string, Type>(GetName(t), t));
    }

    /// <summary>
    ///  Get name of type
    /// </summary>
    /// <param name="t">Type</param>
    /// <returns>Name of type</returns>
    private static string GetName(Type t)
    {
      var fullName = t.FullName;
      var _ns = t.Namespace;
      return _ns != null && fullName.StartsWith(_ns) ? fullName.Substring(_ns.Length + 1) : null;
    }
  }
}
