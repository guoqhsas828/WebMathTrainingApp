using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;

namespace BaseEntity.Configuration
{
  #region ObjectLoggerUtil

  /// <summary>
  /// Factory for creating instances of the ObjectLogAggregator and BinaryLogCache classes
  /// </summary>
  public class ObjectLoggerUtil
  {
    /// <summary>
    /// Creates a binary logger, this logger should be used specifically for diagnostics
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static IObjectLogger CreateObjectLogger(Type type)
    {
      return new ObjectLogger(LogUtil.GetLogger(type + ".ObjectLogger"));
    }

    /// <summary>
    /// Creates a binary logger, this logger should be used specifically for diagnostics
    /// </summary>
    /// <param name="type"></param>
    /// <param name="uniqueIdentifier"></param>
    /// <returns></returns>
    public static IObjectLogger CreateObjectLogger(Type type, string uniqueIdentifier)
    {
      return new ObjectLogger(LogUtil.GetLogger(string.Format("{0}.{1}.ObjectLogger", type, uniqueIdentifier)));
    }

    /// <summary>
    /// Creates a ObjectLogAggregator class which uses an automatically generated log file name to store binary data
    /// </summary>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static ILogAggregator CreateObjectLogAggregator(ILog logger)
    {
      return new ObjectLogAggregator(logger);
    }

    /// <summary>
    /// Creates a ObjectLogAggregator class which uses a custom log file name to store binary data
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="customFileNamePrefix"></param>
    /// <returns></returns>
    public static ILogAggregator CreateObjectLogAggregator(ILog logger, string customFileNamePrefix)
    {
      var key = new ObjectLogFileKey(customFileNamePrefix);
      return new ObjectLogAggregator(logger, key);
    }

    /// <summary>
    /// Creates a ObjectLogAggregator class which uses a custom log file to store binary data. The custom log file name is based on the calling classes method
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="methodBase"></param>
    /// <returns></returns>
    public static ILogAggregator CreateObjectLogAggregator(ILog logger, MethodBase methodBase)
    {
      var key = new ObjectLogFileKey(methodBase);
      return new ObjectLogAggregator(logger, key);
    }

    /// <summary>
    /// Creates a ObjectLogAggregator class which uses a custom log file to store binary data. The custom log file name is based on the calling classes method and custom prefex
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="methodBase"></param>
    /// <param name="prefex"></param>
    /// <returns></returns>
    public static ILogAggregator CreateObjectLogAggregator(ILog logger, MethodBase methodBase, string prefex)
    {
      var key = new ObjectLogFileKey(methodBase, prefex);
      return new ObjectLogAggregator(logger, key);
    }

    /// <exclude></exclude>
    public static int GetPath(string key)
    {
      if (Thread.GetNamedDataSlot(key) == null || Thread.GetData(Thread.GetNamedDataSlot(key)) == null)
      {
        return 0;
      }
      return (int)Thread.GetData(Thread.GetNamedDataSlot(key));
    }
    
    /// <exclude></exclude>
    public static void SetPath(int path, string key)
    {
      Thread.SetData(Thread.GetNamedDataSlot(key), path);
    }

    /// <summary>
    /// The BinaryLog Cache is used to retrieve binary data sotred using the ObjectLogAggregator. 
    /// This class therefore is used to load binary data into applications such as XL
    /// </summary>
    /// <param name="referenceFileName"></param>
    /// <returns></returns>
    public static BinaryLogCache InstantiateBinaryLogObjectCache(string referenceFileName)
    {
      return new BinaryLogCache(referenceFileName);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="task"></param>
    /// <param name="taskId"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public static string CreateDirectoryStructure(string task, long taskId, long id)
    {
      return string.Format("{0}\\{1}\\{2}", task, taskId, id);
    }
  }

  #endregion

  #region ILogAggregator

  /// <summary>
  /// Interface for the LogAggregators
  /// </summary>
  public interface ILogAggregator
  {
    /// <summary>
    /// Appends an additional object to the aggregation set
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    ILogAggregator Append(string key, object value);

    /// <summary>
    /// Appends an additional object to the aggregation set
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    ILogAggregator Append(Type key, object value);

    /// <summary>
    /// Appends an additional object to the aggregation set using both the classing class and custom key
    /// </summary>
    /// <param name="clazz"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    ILogAggregator Append(Type clazz, string key, object value);

    /// <summary>
    /// Saves the aggregation set. 
    /// </summary>
    /// <returns></returns>
    void Log();
  }

  #endregion

  #region ObjectLogAggregator

  /// <summary>
  /// A class used to serilize a series (aggregated set) of objects, which are then written to file
  /// </summary>
  public class ObjectLogAggregator : ILogAggregator
  {
    #region Constructors

    /// <summary>
    /// Default Constructor
    /// </summary>
    private ObjectLogAggregator()
    {
      _objs = new Dictionary<string, object>();
    }

    /// <summary>
    /// Creates a ObjectLogAggregator class which uses an automatically generated log file name to store binary data
    /// </summary>
    /// <param name="logger"></param>
    internal ObjectLogAggregator(ILog logger)
      : this()
    {
      _logger = logger;
    }

    /// <summary>
    /// Creates a ObjectLogAggregator class which uses a custom log file name to store binary data
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="objectLogFileKey"></param>
    internal ObjectLogAggregator(ILog logger, ObjectLogFileKey objectLogFileKey)
      : this(logger)
    {
      _objectLogFileKey = objectLogFileKey;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Appends an additional object to the aggregation set
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public ILogAggregator Append(string key, object value)
    {
      if (_objs.ContainsKey(key))
      {
        _objs[key] = value;
      }
      else
      {
        _objs.Add(key, value);       
      }
      return this;
    }

    /// <summary>
    /// Appends an additional object to the aggregation set
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public ILogAggregator Append(Type key, object value)
    {
      return Append(key.ToString(), value);
    }

    /// <summary>
    /// Appends an additional object to the aggregation set using both the classing class and custom key
    /// </summary>
    /// <param name="clazz"></param>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public ILogAggregator Append(Type clazz, string key, object value)
    {
      return Append(string.Format("{0}.{1}", clazz, key), value);
    }

    /// <summary>
    /// Saves the aggregation set. 
    /// NOTE: relies on Debug logging being enabled.
    /// </summary>
    public void Log()
    {
      if (_objectLogFileKey == null)
      {
        _logger.Debug(_objs);
      }
      else
      {
        var toLog = new Tuple<ObjectLogFileKey, Dictionary<string, object>>(_objectLogFileKey, _objs);
        _logger.Debug(toLog);
      }
      _objs.Clear();
    }

    #endregion

    #region Data

    private readonly ILog _logger;
    private readonly Dictionary<string, object> _objs;
    private ObjectLogFileKey _objectLogFileKey;

    #endregion
  }

  #endregion

  #region ObjectLoggerMetaUtil

  /// <summary>
  /// Util class for enabling object logging through the Risk Run Config UI
  /// </summary>
  public static class ObjectLoggerMetaUtil
  {
    private static IEnumerable<ObjectLoggerMeta> _objectLoggers;

    /// <summary>
    /// A list of all object loggers enabled in the current code base. This list is created using reflection and is cached on first use.
    /// </summary>
    public static IEnumerable<ObjectLoggerMeta> AvailableObjectLoggers
    {
      get
      {
        if (_objectLoggers == null)
        {
          var availableLoggers = new List<ObjectLoggerMeta>();
          var assemblies = AppDomain.CurrentDomain.GetAssemblies();
          foreach (var assembly in assemblies)
          {
            availableLoggers.AddRange(AvailableLoggers(assembly));
          }
          _objectLoggers = availableLoggers;
        }
        return _objectLoggers;
      }
    }

    /// <summary>
    /// Recovers the object info for a species object logger based on its name and class
    /// </summary>
    /// <param name="category"></param>
    /// <param name="loggerName"></param>
    /// <returns></returns>
    public static ObjectLoggerMeta GetObjectInfo(string category, string loggerName)
    {
      var objectLoggerInfo = AvailableObjectLoggers;
      foreach (var info in objectLoggerInfo)
      {
        if (info.Attribute.Name.Equals(loggerName) && info.Attribute.Category.Equals(category))
        {
          return info;
        }
      }
      return null;
    }

    /// <summary>
    /// Recovers a list of object loggers which are required to active the queried object logger. The depdencies are tied to the 
    /// object logger attribute
    /// </summary>
    /// <param name="loggerInfo"></param>
    /// <returns></returns>
    public static IEnumerable<ObjectLoggerMeta> LoadDependentObjectLoggers(ObjectLoggerMeta loggerInfo)
    {
      return loggerInfo == null ? null : GetListOfDependentLoggers(loggerInfo.Attribute);
    }

    /// <summary>
    /// Checks if an appender exists with a specified name. The log4net architecture requires that all appenders have a unique key and therefore
    /// it can be guaranteed that no more than one appender will be found with a specific name.
    /// </summary>
    /// <param name="appenderName"></param>
    /// <returns></returns>
    public static bool AppenderExists(string appenderName)
    {
      return AppenderParser.AvailableAppenders != null && 
        AppenderParser.AvailableAppenders.Value.FirstOrDefault(aggregator => aggregator.Name != null && aggregator.Name.Equals(appenderName)) != null;
    }

    /// <summary>
    /// Enables a select set of object loggers with a specified object appender
    /// </summary>
    /// <param name="loggersToEnable"></param>
    /// <param name="objectLogAppender"></param>
    public static void EnableObjectLoggers(IEnumerable<ObjectLoggerMeta> loggersToEnable, string objectLogAppender)
    {
      if (AppenderParser.AvailableAppenders == null || AppenderParser.AvailableAppenders.Value == null || !AppenderExists(objectLogAppender))
      {
        return;
      }
      var appender = AppenderParser.AvailableAppenders.Value.Single(aggregator => aggregator.Name != null && aggregator.Name.Equals(objectLogAppender));
      foreach (var log in loggersToEnable)
      {
        if (log == null || log.Logger == null || log.Logger.Logger == null)
        {
          continue;
        }
        var logger = log.Logger.Logger as Logger;
        if (logger == null)
        {
          return;
        }
        logger.AddAppender(appender);
        logger.Level = Level.Debug;
      }
    }

    /// <summary>
    /// Enables a select set of object loggers with a specified object appender
    /// </summary>
    /// <param name="loggersToEnable"></param>
    /// <param name="appender"></param>
    public static void EnableObjectLoggers(IEnumerable<ObjectLoggerMeta> loggersToEnable, IAppender appender)
    {
      foreach (var log in loggersToEnable)
      {
        if (log == null || log.Logger == null || log.Logger.Logger == null)
        {
          continue;
        }
        var logger = log.Logger.Logger as Logger;
        if (logger == null)
        {
          return;
        }
        logger.AddAppender(appender);
        logger.Level = Level.Debug;
      }
    }

    /// <summary>
    /// Clears all associated appenders for the object loggers 
    /// </summary>
    public static void ResetAllObjectLoggers()
    {
      foreach (var log in AvailableObjectLoggers)
      {
        if (log == null || log.Logger == null || log.Logger.Logger == null)
        {
          continue;
        }
        var logger = log.Logger.Logger as Logger;
        if (logger == null)
        {
          return;
        }
        logger.Level = Level.Error; ;
        // cannot use RemoveAllAppenders as this will cause the object loggers to 
        // be closed for the remaining life of the thread and cannot use clear() as Appenders 
        // is a read only array
        while (logger.Appenders.Count > 0)
        {
          logger.RemoveAppender(logger.Appenders[0]);
        }
      }
    }

    /// <summary>
    /// Loads all possible loggers for a specific assembly. An object logger is loaded based on the applied attributes 
    /// which are retained for future useage as part of the ObjectLoggerInfo object
    /// </summary>
    /// <param name="assembly"></param>
    /// <returns></returns>
    private static IEnumerable<ObjectLoggerMeta> AvailableLoggers(Assembly assembly)
    {
      foreach (var type in assembly.GetTypes())
      {
        if (!type.GetCustomAttributes(typeof(ObjectLoggerEnabledAttribute), false).Any())
        {
          continue;
        }
        var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Static);

        foreach (var field in fields)
        {
          var attributes = Attribute.GetCustomAttributes(field);
          foreach (var attribute in attributes)
          {
            var objectLoggerAttribute = attribute as ObjectLoggerAttribute;
            if (objectLoggerAttribute == null)
            {
              continue; 
            }
            {
              ILog logger = null;
              if (TryLoadLogger(field, out logger))
              {
                yield return new ObjectLoggerMeta(objectLoggerAttribute, logger);
              }
            }
          }
        }
      }
    }

    /// <summary>
    /// Recovers a list of dependent loggers for a specific object logger. The dependencies are based on predefined lists 
    /// which are defined in the object logger's ObjectLoggerAttribute
    /// </summary>
    /// <param name="objectLoggerAttribute"></param>
    /// <returns></returns>
    private static IEnumerable<ObjectLoggerMeta> GetListOfDependentLoggers(ObjectLoggerAttribute objectLoggerAttribute)
    {
      var depdendentLoggers = new List<ObjectLoggerMeta>();

      if (objectLoggerAttribute.Dependencies != null)
      {
        foreach (var dependency in objectLoggerAttribute.Dependencies)
        {
          // all object logger names are tagged with thi terminating string
          var key = dependency + ".ObjectLogger";
          depdendentLoggers.AddRange(_objectLoggers.Where(l => l.Logger.Logger.Name.Equals(key)));
        }
      }
      return depdendentLoggers;
    }

    private static bool TryLoadLogger(FieldInfo fieldInfo, out ILog log)
    {
      log = null;
      log = fieldInfo.GetValue(log) as ILog;
      return log != null;
    }
  }

  #endregion

  #region AppenderParser

  /// <summary>
  /// Parser class to load all appenders in the log4net config not just those which are required for the specified loggers
  /// as defined in the log4net config. This allows for the dynamic allocation to appenders defined but not used.
  /// </summary>
  public class AppenderParser : XmlHierarchyConfigurator
  {
    /// <summary>
    /// The use of lazy initiallisation means that the appenders are only used in the classes which require the appenders to be applied. 
    /// </summary>
    internal static Lazy<IEnumerable<IAppender>> AvailableAppenders = new Lazy<IEnumerable<IAppender>>(LoadAppenderCache);
    internal static FileInfo Log4NetConfigFile;
    /// <summary>
    /// Creates a cache of appenders from the log4net config file
    /// </summary>
    public static void SetAppenderCache(FileInfo info)
    {
      Log4NetConfigFile = info;
    }

    private static IEnumerable<IAppender> LoadAppenderCache()
    {
      var doc = new XmlDocument();
      if (Log4NetConfigFile == null || !File.Exists(Log4NetConfigFile.FullName))
      {
        return null;
      }
      doc.Load(Log4NetConfigFile.FullName);
      if (doc.DocumentElement == null)
      {
        return null;
      }
      var element = doc.DocumentElement.SelectSingleNode("log4net") as XmlElement;
      return new AppenderParser().LoadAppenders(element);
    }

    /// <summary>
    /// Default constructor for the AppenderParser class
    /// </summary>
    protected AppenderParser()
      : base((Hierarchy)LogManager.GetRepository(Assembly.GetCallingAssembly()))
    {
      _availableAppenders = new List<IAppender>();
    }

    /// <summary>
    /// Loads all the appenders which are encaptulated in the specified xml element 
    /// </summary>
    /// <param name="element"></param>
    protected IEnumerable<IAppender> LoadAppenders(XmlElement element)
    {
      if (element == null || element.OwnerDocument == null)
      {
        return null;
      }
      var appenders = element.OwnerDocument.GetElementsByTagName("appender").Cast<XmlElement>().ToArray<XmlElement>();
      foreach (var appenderXmlDefintion in appenders)
      {
        _availableAppenders.Add(base.ParseAppender(appenderXmlDefintion));
      }
      return _availableAppenders;
    }

    private readonly List<IAppender> _availableAppenders;
  }

  #endregion

  #region BinaryLogCache

  /// <summary>
  /// The BinaryLog Cache is used to retrieve binary data sotred using the ObjectLogAggregator. 
  /// </summary>
  public class BinaryLogCache
  {
    #region Constructor

    /// <summary>
    /// Constructs a class used to build an binary log object cache based on a specific binary log file
    /// </summary>
    /// <param name="referenceFile"></param>
    public BinaryLogCache(string referenceFile)
    {
      _referenceFile = referenceFile;
      _valid = false;
      _objs = new Dictionary<string, object>();
      Init();
    }

    private void Init()
    {
      var directoryFullName = Path.GetDirectoryName(_referenceFile);

      if (directoryFullName == null)
      {
        return;
      }

      var fileEntries = Directory.GetFiles(directoryFullName);
      _referenceFiles = new List<string>();
      foreach (var element in fileEntries)
      {
        if (Path.GetExtension(element) != null && Path.GetExtension(element).Contains("bin"))
        {
          _referenceFiles.Add(element);
        }
      }

      _valid = _referenceFiles.Count() != 0;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Generates the Object Cache which is stored in a Dictionary
    /// </summary>
    public void BuildObjectCache()
    {
      if (!_valid)
      {
        return;
      }

      foreach (var element in _referenceFiles)
      {
        var lines = File.ReadAllLines(element);
        foreach (var line in lines)
        {
          // get file name
          string fileName;
          {
            var tuple = line.Split(new string[] { "ObjectSerializedAndStoredAt:" }, StringSplitOptions.RemoveEmptyEntries);
            if (tuple.Length != 2)
            {
              continue;
            }
            fileName = tuple[1].Trim().Substring(0, tuple[1].Trim().Length - 1);
          }
          AppendObject(fileName);
        }
      }
    }

    #endregion

    #region helper methods

    private void AppendObject(string fileName)
    {
      using (var fs = new FileStream(fileName, FileMode.Open))
      {
        var formatter = new BinaryFormatter();
        var obj = formatter.Deserialize(fs);
        var dictionary = obj as Dictionary<string, object>;
        if (dictionary == null)
        {
          return;
        }
        foreach (var entry in dictionary)
        {
          if (!_objs.ContainsKey(entry.Key))
          {
            _objs.Add(entry.Key, entry.Value);
          }
        }
      }
    }

    #endregion

    #region Properties

    /// <summary>
    /// The set of binary objects recovered from the series of log files processed
    /// </summary>
    public Dictionary<string, object> BinaryLogObjectCache
    {
      get { return _objs; }
    }

    #endregion

    #region Data

    private readonly string _referenceFile;
    private bool _valid;
    private List<string> _referenceFiles;
    private Dictionary<string, object> _objs;

    #endregion
  }

  #endregion

  #region ObjectLogKey

  /// <summary>
  /// 
  /// </summary>
  public class ObjectLogFileKey
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="customFileName"></param>
    public ObjectLogFileKey(string customFileName)
    {
      _method = customFileName;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="methodBase"></param>
    public ObjectLogFileKey(MethodBase methodBase)
    {
      if (methodBase.ReflectedType != null)
      {
        _nameSpace = CleanInput(methodBase.ReflectedType.Namespace);
        _class = CleanInput(methodBase.ReflectedType.Name);
      }
      _method = CleanInput(methodBase.Name);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="methodBase"></param>
    /// <param name="prefex"></param>
    public ObjectLogFileKey(MethodBase methodBase, string prefex)
    {
      if (methodBase.ReflectedType != null)
      {
        _nameSpace = CleanInput(methodBase.ReflectedType.Namespace);
        _class = CleanInput(methodBase.ReflectedType.Name);
      }
      _method = CleanInput(methodBase.Name);
      _prefix = CleanInput(prefex);
    }

    /// <summary>
    /// 
    /// </summary>
    public void AssignParameterisedObjectLogFile()
    {
      log4net.ThreadContext.Properties["namespace"] = _nameSpace;
      log4net.ThreadContext.Properties["class"] = _class;
      if (_prefix == null)
      {
        log4net.ThreadContext.Properties["tag"] = _method;
      }
      else
      {
        log4net.ThreadContext.Properties["tag"] = string.Format("{0}.{1}", _method, _prefix);
      }
    }

    private static string CleanInput(string strIn)
    {
      return strIn.Replace(@"\", string.Empty).Replace(@"/", string.Empty);
    }

    private readonly string _nameSpace;
    private readonly string _class;
    private readonly string _method;
    private readonly string _prefix;
  }

  #endregion
}
