// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using log4net;

namespace BaseEntity.Configuration
{
  /// <summary>
  ///   Encapsulates the logic to determine the install directory and
  ///   the directory containing per-user settings.
  /// </summary>
  public class SystemContext 
  {
    private static readonly ILog Log = LogManager.GetLogger(typeof(SystemContext));

    private static string _userDir;

    /// <summary>
    ///   Current excel instance
    /// </summary>
    public static object ExcelApplicationInstance { get; set; }

    /// <summary>
    ///   Returns the full path of the WebMathTraining config file
    /// </summary>
    public static string ConfigFile { get; private set; }

    /// <summary>
    /// Gets the initialization container.
    /// </summary>
    /// <value>
    /// The initialization container.
    /// </value>
    public static string InitContainer { get; private set; }

    /// <summary>
    ///ONLY support following three cases:
    ///1. configFile and installDir are set to absolute paths
    ///2. configFile is set to be just a file name, and installDir is not be set.
    ///3. both are not set. default value configFile (WebMathTraining.xml) will be used.
    /// </summary>
    /// <param name="installDir">installation dir</param>
    /// <param name="configFile">path of configuration file</param>
    internal static void SetSystemConfigPaths(string installDir, string configFile)
    {
      if (!string.IsNullOrWhiteSpace(InstallDir) || !string.IsNullOrWhiteSpace(ConfigFile))
        throw new InvalidOperationException("SetSystemConfigPaths should only be called only once!");

      var configFileSpecified = !string.IsNullOrEmpty(configFile);
      var installDirSpecified = !string.IsNullOrEmpty(installDir);

      if (installDirSpecified) //case #1 described in the summary section
      {
        if (!configFileSpecified) //ERROR. configFile is not specified, but InstallDir is specified.
          throw new InvalidOperationException("If InstallDir is specified, ConfigFile has to be specified.");

        if (!IsAbsolutePath(installDir)) //ERROR: InstallDir is specified, but not an absolute path
          throw new InvalidOperationException(string.Format("InstallDir '{0}' has to be an absolute path.", installDir));

        ConfigFile = configFile;
        InstallDir = installDir;
      }
      else if (configFileSpecified) //case #2 described in the summary section
      {
        if (IsAbsolutePath(configFile)) //ERROR. configFile is set to absolute path when InstallDir is not specified.
          throw new InvalidOperationException(string.Format("ConfigFile '{0}' cannot be absolute path if InstallDir is not specified.", configFile));
        if (IsRelativePath(configFile)) //ERROR: configFile is specified as a relative path.
          throw new InvalidOperationException(string.Format("ConfigFile '{0}' cannot contain relative path", configFile));

        InstallDir = GetDefaultInstallDir(configFile);
        ConfigFile = Path.Combine(InstallDir, configFile);
      }
      else //case #3 described in the summary section
      {
        string defaultConfigFile = GetDefaultConfigFileName();
        InstallDir = GetDefaultInstallDir(defaultConfigFile);
        ConfigFile = Path.Combine(InstallDir, defaultConfigFile);
      }

      Log.DebugFormat("InstallDir={0}", InstallDir);
      Log.DebugFormat("ConfigFile={0}", ConfigFile);
    }

    internal static void SetInitContainer(string initContainer)
    {
      InitContainer = initContainer;
      Log.InfoFormat("InitContainer={0}", InitContainer);
    }

    private static bool IsRelativePath(string path)
    {
      return !IsAbsolutePath(path) && (path.IndexOfAny(new[] {Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar}) != -1);
    }

    private static bool IsAbsolutePath(string path)
    {
      return !string.IsNullOrEmpty(Path.GetPathRoot(path));
    }

    /// <summary>
    ///   Name of config file
    /// </summary>
    public static string ConfigFileName
    {
      get
      {
        if (ConfigFile == null)
          throw new InvalidOperationException("ConfigFile is not set!");
        return Path.GetFileName(ConfigFile);
      }
    }

    /// <summary>
    ///  Determine whether running on Windows
    /// </summary>
    public static bool IsWindows => 
#if NETSTANDARD2_0
      RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#else
      true;
#endif

    /// <summary>
    ///  Determine whether running on Linux
    /// </summary>
    public static bool IsLinux =>
#if NETSTANDARD2_0
      RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
#else
      false;
#endif

    /// <summary>
    ///   Default config file name
    /// </summary>
    public static string GetDefaultConfigFileName()
    {
      return "Configurator.xml";
    }

    /// <summary>
    ///   Directory where WebMathTraining is installed
    /// </summary>
    public static string InstallDir { get; private set; }

    /// <summary>
    ///   Default Install WebMathTraining directory 
    /// </summary>
    private static string GetDefaultInstallDir(string configFileName)
    {
      if (Path.IsPathRooted(configFileName))
        throw new ArgumentException("configFileName should not contain path information.", configFileName);

      string path;

      Assembly currentAssembly = Assembly.GetExecutingAssembly();
      AppDomain currentDomain = AppDomain.CurrentDomain;

      //check if assembly is shadow copied or it is in GAC
      if (currentAssembly.GlobalAssemblyCache || currentDomain.ShadowCopyFiles)
      {
        //The following code will fail to get the right directory name in case of the directory path
        // has '#' character. Here we assume windows directory doesn't have '#' character.
        Uri uri = new Uri(Path.GetDirectoryName(currentAssembly.CodeBase));
        path = uri.LocalPath;
      }
      else
      {
        //To address the '#' problem, we will use the location property instead of CodeBase
        //NOTE: we can not use Location property if this assembly is in GAC.
        path = Path.GetDirectoryName(currentAssembly.Location);
      }

      string startPath = path;
      string prevPath = null;
      while (true)
      {
        if ((prevPath != null) && (prevPath == path))
          break;

        var fileInfo = new FileInfo(Path.Combine(path, configFileName));
        if (fileInfo.Exists)
        {
          return path;
        }

        prevPath = path;

        path = Path.GetFullPath(Path.Combine(path, ".."));
      }

      // Must be at the root of the file system
      throw new Exception(String.Format("Unable to locate config file [{0}], after searching from [{1}] to [{2}].",
        configFileName, startPath, path));
    }

    /// <summary>
    ///   User private directory
    /// </summary>
    public static string UserPrivateDirectory
    {
      get { return _userDir; }
      set
      {
        if (_userDir != null)
          throw new InvalidOperationException("Cannot change UserPrivateDirectory once set");

        _userDir = value;
      }
    }

#if NETSTANDARD2_0 || NETSTANDARD2_1
    private static bool SetEnvironmentVariable(string lpName, string lpValue)
    {
      return true;
    }
#else
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetEnvironmentVariable(string lpName, string lpValue);
#endif

    /// <exclude />
    // add the directory of a file into path
    public static void AddFilePathToSystemPathEnviromentVariable(string fileFullPath)
    {
      string path = Path.GetDirectoryName(fileFullPath);
      ProcessStartInfo startInfo = Process.GetCurrentProcess().StartInfo;
      StringDictionary environmentVar = startInfo.EnvironmentVariables;
      string pathEnvironmentVar = environmentVar["PATH"];
      if (pathEnvironmentVar.IndexOf(path, StringComparison.Ordinal) == -1)
      {
        // It is found that using unmanaged API to set PATH environment variable is mandatory 
        // (at least on Windows).  For completeness, we also set the .NET environment variable.
        pathEnvironmentVar = string.Concat(path, ";", pathEnvironmentVar);

        // Set .NET PATH environment variable
        environmentVar["PATH"] = pathEnvironmentVar;

        // Set OS environment variable
        if (SetEnvironmentVariable("PATH", pathEnvironmentVar) == false)
        {
          throw new Exception("setting PATH failed!");
        }
      }

      //Also add the path to AssemblyResolver
      AssemblyResolver.AddSearchPath(path);
    }
  }
}