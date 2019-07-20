// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using BaseEntity.Configuration.Properties;
using log4net;
using log4net.Config;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Microsoft.Practices.Unity;
using Microsoft.Practices.Unity.Configuration;
#if NETSTANDARD2_0
using Unity;
#endif

namespace BaseEntity.Configuration
{
  /// <summary>
  ///   Utility class used to configure WebMathTraining modules from WebMathTraining.xml
  /// </summary>
  ///
  public static class Configurator
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(Configurator));

    private static volatile bool _phaseOneFirstTime = true;
    private static volatile bool _phaseTwoFirstTime = true;
    private static readonly object PhaseOneSyncObj = new object();
    private static readonly object PhaseTwoSyncObj = new object();

    private static string _initContainerName;
    private static IUnityContainer _rootContainer;
    private static List<PluginItem> _pluginItems;

    private static readonly ConcurrentDictionary<PluginType, IList<PluginItem>> PluginItemMap =
      new ConcurrentDictionary<PluginType, IList<PluginItem>>();

    /// <summary>
    /// Timing log level, higher priority than DEBUG, but lower than INFO
    /// </summary>
    public static Level TimingLevel = new Level(35000, "TIMING");

    /// <summary>
    /// Gets the unity configuration file path.
    /// </summary>
    public static string UnityConfigFile { get; private set; }

    /// <summary>
    /// Gets the unity configuration file path.
    /// </summary>
    public static string ServiceHostConfigFile { get; private set; }

    /// <summary>
    /// Gets the default container factory.
    /// </summary>
    public static IUnityContainerFactory DefaultContainerFactory { get; private set; }

    /// <summary>
    /// Returns the "init container" resolved using DefaultContainerFactory
    /// </summary>
    public static IUnityContainer DefaultContainer
    {
      get { return DefaultContainerFactory.Resolve(_initContainerName); }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T Resolve<T>()
    {
      return DefaultContainer.Resolve<T>();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IEnumerable<T> ResolveAll<T>()
    {
      return DefaultContainer.ResolveAll<T>();
    }

    /// <summary>
    ///   Returns the contents of ConfigFile as an XmlDocument
    /// </summary>
    private static XmlDocument XmlDoc { get; set; }

    /// <summary>
    ///   Parse command line arguments and initialise configuration
    /// </summary>
    /// <remarks>
    /// Parse command line arguments to set install directory, config file, etc.
    /// </remarks>
    public static void Init()
    {
      Init(new UnityContainer(), null);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rootContainerName"></param>
    public static void Init(string rootContainerName)
    {
      Init(new UnityContainer(), rootContainerName);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="rootContainer"></param>
    /// <param name="initContainerName"></param>
    public static void Init(IUnityContainer rootContainer, string initContainerName)
    {
      // Initialize SystemContext and log4net
      InitPhaseOne();

      // Initialize Unity and load plugins
      InitPhaseTwo(rootContainer, initContainerName, null);
    }

    /// <exclude/>
    public static void InitPhaseOne()
    {
      lock (PhaseOneSyncObj)
      {
        if (!_phaseOneFirstTime) return;

        // Allow process id and name to be included in log4net message
        var process = Process.GetCurrentProcess();
        GlobalContext.Properties["pid"] = process.Id;
        if (GlobalContext.Properties["pname"] == null)
          GlobalContext.Properties["pname"] = process.ProcessName;

        log4net.Util.LogLog.InternalDebugging = false;   // change to true if we want to debug log4net itself
        LogUtil.GetRepository().LevelMap.Add(TimingLevel);
        AddNewLogLevels();

        //here we configure log4net using default config file in resources such that any error message will be shown as debug trace
        using (var sr = new MemoryStream(Encoding.ASCII.GetBytes(Resources.defaultConfigXml)))
        {
          LogUtil.XmlConfigure(sr);
        }

        // Array used for dynamic log4net configuration
        string[] logLevels = null;

        string configFile = null;
        string installDir = null;

        // Parse command-line args
        var cmdLineArgs = Environment.GetCommandLineArgs();

        int argInd;
        for (argInd = 1; argInd < cmdLineArgs.Length; argInd++)
        {
          var arg = cmdLineArgs[argInd];
          if (arg.StartsWith("--qInstallDir="))
          {
            var idx = arg.IndexOf('=') + 1;
            var strValue = arg.Substring(idx).Trim('"');
            installDir = strValue;
          }
          else if (arg.StartsWith("--qConfigFile="))
          {
            var idx = arg.IndexOf('=') + 1;
            var strValue = arg.Substring(idx).Trim('"');
            configFile = strValue;
          }
          else if (arg.StartsWith("--qLogLevel="))
          {
            var idx = arg.IndexOf('=') + 1;
            var strValue = arg.Substring(idx);
            logLevels = strValue.Split(',');
          }
          else if (arg.StartsWith("--qInitContainer="))
          {
            var idx = arg.IndexOf('=') + 1;
            var strValue = arg.Substring(idx).Trim('"');
            _initContainerName = strValue;
          }
        }

        // Check for environment variable settings
        var envConfigFile = Environment.GetEnvironmentVariable("WebMathTraining_CONFIG_FILE");
        if (Logger.IsDebugEnabled)
        {
          Logger.DebugFormat("WebMathTraining_CONFIG_FILE: {0}", envConfigFile);
        }

        // only use WebMathTraining_CONFIG_FILE environment variable if there is no command option --qConfigFile=
        if (envConfigFile != null && String.IsNullOrEmpty(configFile))
          configFile = envConfigFile.Trim('"');

        var envInstallDir = Environment.GetEnvironmentVariable("WebMathTraining_INSTALL_DIR");
        if (Logger.IsDebugEnabled)
        {
          Logger.DebugFormat("WebMathTraining_INSTALL_DIR: {0}", envInstallDir);
        }

        // only use WebMathTraining_INSTALL_DIR environment variable if there is no command option --qInstallDir=
        if (envInstallDir != null && String.IsNullOrEmpty(installDir))
          installDir = envInstallDir.Trim('"');

        //configure SystemContext
        SystemContext.SetSystemConfigPaths(installDir, configFile);

        GlobalContext.Properties["qinstalldir"] = Path.GetFileName(SystemContext.InstallDir);

        #region Configure log4net

        var fileName = Path.Combine(SystemContext.InstallDir, "Log4Net.config");
        var envLog4NetConfigurationFile = Environment.GetEnvironmentVariable("WebMathTraining_LOG4NET_CONFIG_FILE");
        if (!string.IsNullOrEmpty(envLog4NetConfigurationFile))
        {
          fileName = envLog4NetConfigurationFile;
        }
        if (File.Exists(fileName))
        {
          LogUtil.XmlConfigureAndWatch(new FileInfo(fileName));
          AppenderParser.SetAppenderCache(new FileInfo(fileName));

          if (logLevels != null)
          {
            foreach (var logLevel in logLevels)
            {
              var nvp = logLevel.Split(':');
              if (nvp.Length != 2)
              {
                throw new ArgumentException(String.Format(
                  "ERROR: Invalid logLevel specified using --qLogLevel [{0}]", logLevel));
              }

              var log = LogUtil.GetLogger(nvp[0]);
              var logger = (Logger)log.Logger;
              logger.Level = logger.Hierarchy.LevelMap[nvp[1]];
            }
          }
        }

        #endregion

        XmlDoc = new XmlDocument();
        using (var fs = OpenConfigFile())
        using (var xmlReader = new XmlTextReader(fs))
        {
          XmlDoc.Load(xmlReader);
        }

        // Set standard command-line arguments
        var options = new List<CmdLineOption>
        {
          new VersionCmdLineOption("--version", "Display version info."),
          new HelpCmdLineOption("-?|-h|--help", "Display usage and exit."),
          new StringCmdLineOption("--qConfigFile", "ConfigFile", "Name of config file."),
          new StringCmdLineOption("--qInstallDir", "InstallDir", "Install directory."),
          new StringCmdLineOption("--qLogLevel", "LogLevel", "Comma-separated list of <logger>:<level> pairs."),
          new StringCmdLineOption("--qCustomLogLevels", "CustomLogLevels", "Comma-separated list of <levelName>:<levelValue> pairs."),
          new StringCmdLineOption("--qInitContainer", "InitContainer", "Unity container element used by Configurator.Init() to initialize process"),
        };

        CmdLineParser.SetStandardOptions(options);

        _phaseOneFirstTime = false;
      }
    }

    /// <exclude/>
    public static void InitPhaseTwo(string initContainerName)
    {
      InitPhaseTwo(new UnityContainer(), initContainerName, null);
    }

    /// <exclude/>
    public static void InitPhaseTwo(IUnityContainer rootContainer, string initContainerName, IPluginLoader pluginLoader)
    {
      lock (PhaseTwoSyncObj)
      {
        if (_phaseTwoFirstTime)
        {
          // Initialize Unity
          var envUnityConfigFile = Environment.GetEnvironmentVariable("WebMathTraining_UNITY_CONFIG_FILE");
          if (!string.IsNullOrEmpty(envUnityConfigFile))
          {
            UnityConfigFile = envUnityConfigFile;
          }
          else
          {
            UnityConfigFile = Path.Combine(SystemContext.InstallDir, "Unity.config");
            if (!File.Exists(UnityConfigFile))
              UnityConfigFile = Path.Combine(SystemContext.InstallDir, "Unity.dev.config");
          }

          // Initialize ServiceHostConfig
          var envServiceHostConfigFile = Environment.GetEnvironmentVariable("WebMathTraining_SERVICE_HOST_CONFIG_FILE");
          if (!string.IsNullOrEmpty(envUnityConfigFile))
          {
            ServiceHostConfigFile = envServiceHostConfigFile;
          }
          else
          {
            ServiceHostConfigFile = Path.Combine(SystemContext.InstallDir, "ServiceHost.config");
          }

          if (Environment.GetEnvironmentVariable("WebMathTraining_ENV_DEBUG") != null)
          {
            Console.WriteLine("WebMathTraining_ENV_DEBUG : InitPhaseTwo : {0} || {1} || {2} || {3}", 
              initContainerName, new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath, UnityConfigFile, SystemContext.ConfigFile);
          }

          _rootContainer = rootContainer ?? new UnityContainer();
          DefaultContainerFactory = new DefaultContainerFactory(_rootContainer, ReadUnitySection());

          if (!string.IsNullOrEmpty(initContainerName))
          {
            if (string.IsNullOrEmpty(_initContainerName))
            {
              _initContainerName = initContainerName;
            }
            else if (_initContainerName != initContainerName)
            {
              Logger.InfoFormat(
                "Override InitContainer [{0}] with [{1}] specified on command-line",
                initContainerName, _initContainerName);
            }
          }

          SystemContext.SetInitContainer(_initContainerName);
          Logger.InfoFormat("Container: {0}", _initContainerName);

          // Initialize plugins
          var container = DefaultContainerFactory.Resolve(_initContainerName);
          if (container != null)
          {
            _pluginItems = new List<PluginItem>();

            foreach (var item in pluginLoader != null ? new[] {pluginLoader} : container.ResolveAll<IPluginLoader>())
            {
              _pluginItems.AddRange(item.Load());
            }

            foreach (var plugin in container.ResolveAll<IPlugin>().ToList())
            {
              plugin.Init();
            }
          }

          _phaseTwoFirstTime = false;
        }
        else
        {
#if DEBUGTest 
          if (!ReferenceEquals(rootContainer, _rootContainer))
            throw new InvalidOperationException("Inconsistent InitPhaseTwo: rootContainer [" + rootContainer + "] != [" + _rootContainer + "]");
          if (initContainerName != null && initContainerName != _initContainerName)
            throw new InvalidOperationException("Inconsistent InitPhaseTwo: initContainerName [" + initContainerName + "] != [" + _initContainerName + "]");
          if (!ReferenceEquals(pluginLoader, _pluginLoader))
            throw new InvalidOperationException("Inconsistent InitPhaseTwo: pluginLoader [" + pluginLoader + "] != [" + _pluginLoader + "]");
#endif
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pluginType"></param>
    /// <returns></returns>
    public static IList<PluginItem> GetPlugins(PluginType pluginType)
    {
      if (_pluginItems == null)
      {
        throw new InvalidOperationException("PluginItem cache has not been initialized!");
      }

      return PluginItemMap.GetOrAdd(pluginType, GetPluginsOfType);
    }

    private static IList<PluginItem> GetPluginsOfType(PluginType pluginType)
    {
      var items = pluginType == PluginType.None ? _pluginItems : _pluginItems.Where(pi => pi.PluginType.HasFlag(pluginType)).ToList();

      var referenceMap = new Dictionary<string, ISet<string>>();
      foreach (var item in items)
      {
        referenceMap[item.Assembly.FullName] = GetReferences(item.Assembly, new HashSet<string>());
      }

      items.Sort((itemA, itemB) =>
      {
        var assemblyA = itemA.Assembly.FullName;
        var assemblyB = itemB.Assembly.FullName;
        if (referenceMap[assemblyA].Contains(assemblyB))
        {
          return 1;
        }
        if (referenceMap[assemblyB].Contains(assemblyA))
        {
          return -1;
        }
        return 0;
      });

      return items;
    }

    private static ISet<string> GetReferences(Assembly assembly, ISet<string> references)
    {
      foreach (var nameOfReferencedAssembly in assembly.GetReferencedAssemblies())
      {
        var referencedAssembly = Assembly.Load(nameOfReferencedAssembly);
        if (referencedAssembly.GlobalAssemblyCache || references.Contains(nameOfReferencedAssembly.FullName)) continue;
        references.Add(nameOfReferencedAssembly.FullName);
        GetReferences(referencedAssembly, references);
      }
      return references;
    }

    /// <summary>
    ///  Open config file
    /// </summary>
    private static FileStream OpenConfigFile()
    {
      FileStream fs = null;

      if (File.Exists(SystemContext.ConfigFile))
      {
        // Try hard to open the file
        for (var retry = 5; --retry >= 0;)
        {
          try
          {
            var fileInfo = new FileInfo(SystemContext.ConfigFile);
            fs = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            break;
          }
          catch (IOException ex)
          {
            if (retry == 0)
            {
              // The stream cannot be valid
              var msg = String.Format("Configurator: Failed to open XML config file [{0}]: {1} ", SystemContext.ConfigFile, ex.Message);
              Debug.Write(msg);
              Logger.Error(msg, ex); //because log4net is not configured yet, most likely we will not be able to see this message in log file
              fs = null;
            }

            Thread.Sleep(250);
          }
        }
      }

      if (fs == null)
      {
        var msg = "Cannot Open Configuration File [" + SystemContext.ConfigFile + "]";
        Debug.Write(msg);
        throw new Exception(msg);
      }

      Logger.DebugFormat("Loaded configuration file from {0}", SystemContext.ConfigFile);

      return fs;
    }

    /// <summary>
    /// Gets a particular XElement within the current WebMathTraining.xml file
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static XElement GetConfigXElement(string name)
    {
      var serviceElement = GetConfigXml(name, null);

      if (serviceElement == null)
      {
        return new XElement(name);
      }

      var xmldoc = new XmlDocument();
      xmldoc.AppendChild(xmldoc.ImportNode(serviceElement, true));
      return XElement.Parse(xmldoc.InnerXml);
    }

    /// <summary>
    ///   Returns specified (optionally validated) XML fragment
    /// </summary>
    ///
    /// <param name="moduleName"></param>
    /// <param name="moduleSchema"></param>
    ///
    public static XmlElement GetConfigXml(string moduleName, XmlSchema moduleSchema)
    {
      if (moduleName == null)
      {
        throw new ArgumentOutOfRangeException("moduleName", "Cannot be null!");
      }

      var rootNode = XmlDoc.DocumentElement;
      if (rootNode == null)
        return null;

      var nodeList = rootNode.GetElementsByTagName(moduleName);
      if (nodeList.Count == 0)
      {
        return null;
      }
      if (nodeList.Count > 1)
      {
        throw new Exception(String.Format("Multiple nodes matching moduleName={0}!", moduleName));
      }

      var newDoc = new XmlDocument();

      if (moduleSchema == null)
      {
        newDoc.AppendChild(newDoc.ImportNode(nodeList[0], true));
      }
      else
      {
        // Validate against schema
        var xmlFrag = nodeList[0].OuterXml;
        var context = new XmlParserContext(null, null, "", XmlSpace.Default);
        var configSchemas = new XmlSchemaSet();
        configSchemas.Add(moduleSchema);

        var readerSettings = new XmlReaderSettings();
        readerSettings.ValidationEventHandler += ValidationHandler;
        readerSettings.ValidationType = ValidationType.Schema;
        readerSettings.Schemas.Add(configSchemas);

        newDoc.Load(
          XmlReader.Create(xmlFrag, readerSettings, context));
      }

      Logger.DebugFormat("Found configration section for module {0}", moduleName);

      return newDoc.DocumentElement;
    }

    /// <summary>
    ///    Get attribute value of a configuration element
    /// </summary>
    /// <param name="configXml">Configuration element</param>
    /// <param name="attrName">Attribute name</param>
    /// <param name="type">Type of the object to return</param>
    /// <param name="defaultValue">Default value to return when the attribute is not set or is invalid</param>
    /// <returns>object represent the value of the attribute</returns>
    /// <exclude />
    public static object GetAttributeValue(XmlElement configXml, string attrName, Type type, object defaultValue)
    {
      var attrVal = configXml.GetAttribute(attrName);
      if (!string.IsNullOrEmpty(attrVal))
      {
        try
        {
          var value = Convert.ChangeType(attrVal, type);
          return value;
        }
        catch (FormatException)
        {
          Logger.WarnFormat("Invalid config value for {0}/{1}='{2}'; using default value='{3}'",
            configXml.Name, attrName, attrVal, defaultValue);
        }
      }
      return defaultValue;
    }

    /// <summary>
    /// A typed version of the GetAttributeValue function that uses generics.
    /// </summary>
    /// <param name="configXml">Configuration element</param>
    /// <param name="attrName">Attribute name</param>
    /// <param name="defaultValue">Default value to return when the attribute is not set or is invalid</param>
    /// <returns>object represent the value of the attribute</returns>
    /// <exclude />
    public static T GetAttributeValue<T>(XElement configXml, string attrName, T defaultValue)
    {
      var attrVal = configXml.Attribute(attrName);
      if (attrVal == null || string.IsNullOrEmpty(attrVal.Value))
        return defaultValue;

      try
      {
        return (T)Convert.ChangeType(attrVal.Value, typeof(T));
      }
      catch (FormatException)
      {
      }
      return defaultValue;
    }

    /// <summary>
    /// Reads the unity section.
    /// </summary>
    public static UnityConfigurationSection ReadUnitySection()
    {
      var fileName = UnityConfigFile;
      if (!File.Exists(fileName))
      {
        throw new FileNotFoundException(string.Format("Unity configuration file [{0}] was not found", fileName), fileName);
      }
      Logger.InfoFormat("Configuring Unity: {0}", fileName);
      var config = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap {ExeConfigFilename = fileName}, ConfigurationUserLevel.None);
      var section = config.GetSection("unity");
      return section as UnityConfigurationSection;
    }

    private static void ValidationHandler(object o, ValidationEventArgs args)
    {
      Logger.Error("ERROR: Config file [" + SystemContext.ConfigFile + "] not valid (" + args.Message + ")");
      throw args.Exception;
    }

    /// <summary>
    /// Processes custom log levels from the command line. We need to do this before Log4Net gets configured.
    /// </summary>
    private static void AddNewLogLevels()
    {
      var cmdLineArgs = Environment.GetCommandLineArgs();
      const string keyword = "--qCustomLogLevels=";

      foreach (string[] s in cmdLineArgs.Where(arg => arg.StartsWith(keyword)).Select(arg => arg.Substring(keyword.Length).Split(',', ';')).SelectMany(levels => levels.Select(level => level.Split(':'))))
      {
        int iLevel;
        if (!int.TryParse(s[1], out iLevel))
          continue;

        LogUtil.GetRepository().LevelMap.Add(s[0], iLevel);
      }
    }
  }
}
