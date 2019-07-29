using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Xml;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  /// types of windows Mini Dump
  /// </summary>
  [Flags]
  public enum MiniDumpType
  {
    /// <exclude/>
    None = -1,
    /// <exclude/>
    MiniDumpNormal = 0x00000000,
    /// <exclude/>
    MiniDumpWithDataSegs = 0x00000001,
    /// <exclude/>
    MiniDumpWithFullMemory = 0x00000002,
    /// <exclude/>
    MiniDumpWithHandleData = 0x00000004,
    /// <exclude/>
    MiniDumpFilterMemory = 0x00000008,
    /// <exclude/>
    MiniDumpScanMemory = 0x00000010,
    /// <exclude/>
    MiniDumpWithUnloadedModules = 0x00000020,
    /// <exclude/>
    MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
    /// <exclude/>
    MiniDumpFilterModulePaths = 0x00000080,
    /// <exclude/>
    MiniDumpWithProcessThreadData = 0x00000100,
    /// <exclude/>
    MiniDumpWithPrivateReadWriteMemory = 0x00000200,
    /// <exclude/>
    MiniDumpWithoutOptionalData = 0x00000400,
    /// <exclude/>
    MiniDumpWithFullMemoryInfo = 0x00000800,
    /// <exclude/>
    MiniDumpWithThreadInfo = 0x00001000,
    /// <exclude/>
    MiniDumpWithCodeSegs = 0x00002000,
  }

  /// <summary>
  /// Various triggers that will cause memory dump
  /// </summary>
  [Flags]
  public enum ErrorTrigger
  {
    /// <summary>
    /// None
    /// </summary>
    None =0x0,
    /// <summary>
    /// when toolkit raise win32 exceptions
    /// </summary>
    ToolkitError = 0x1,
    /// <summary>
    /// when toolkit raise win32 excetpions for first time, following exceptions are ignored.
    /// </summary>
    FirstToolkitError = 0x2,
    /// <summary>
    /// when whole application crashes
    /// </summary>
    ApplicationCrash =0x4,
    /// <summary>
    /// all cases
    /// </summary>
    ALL = -1 
  }

  /// <exclude />
  [Serializable]
  public class DebugHelperConfig
  {
    #region Data Members
    /// <summary>
    /// Memory Dump type
    /// </summary>
    public MiniDumpType DumpType = MiniDumpType.None; //by default, no dump at all
    /// <summary>
    /// The directory where all dump file will be saved
    /// </summary>
    public string DumpFolder = "%APPDATA%\\temp";//default location
    /// <summary>
    /// Events that will trigger the event dump
    /// </summary>
    public ErrorTrigger DumpTrigger =  ErrorTrigger.FirstToolkitError; //in most cases, this is waht we are interested.
    #endregion

    #region xml load 
    /// <summary>
    ///   Load configuration settings from an XML root node
    ///   This function used a very generic way such that when a new field is added to this class, the XML parser code 
    ///   doesn't need to be updated. The idea is from ToolkitConfiguration class.
    /// </summary>
    public static DebugHelperConfig LoadFromXML(XmlElement root)
    {
      try
      {
        var type = typeof (DebugHelperConfig);
        var allFieldInfos = type.GetFields();
        var fieldValues = new List<object>();
        var fieldInfos = new List<FieldInfo>();
        for (int i = 0; i < allFieldInfos.Length; ++i)
        {
          var field = allFieldInfos[i];
          var attrVal = root.GetAttribute(field.Name);
          if (!string.IsNullOrEmpty(attrVal))
          {
            var fieldType = field.FieldType;
            if (fieldType.IsEnum)
              fieldValues.Add( Enum.Parse(fieldType, attrVal, true));
            else
              fieldValues.Add( Convert.ChangeType(attrVal, fieldType));
            fieldInfos.Add(field);
          }
        }
        object o = new DebugHelperConfig();
        o = FormatterServices.PopulateObjectMembers(o, fieldInfos.ToArray(), fieldValues.ToArray());
        return (DebugHelperConfig) o;
      }
      catch (Exception ex)
      {
        throw new ToolkitConfigException("Failed to load DebugHelperConfig config", ex);
      }
    }
    #endregion
  }

  /// <exclude/>
  public partial class DebugHelper
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(DebugHelper));

    [DllImport("dbghelp.dll")]
    private static extern bool MiniDumpWriteDump(IntPtr hProcess,
                                                Int32 ProcessId,
                                                IntPtr hFile,
                                                MiniDumpType DumpType,
                                                IntPtr ExceptionParam,
                                                IntPtr UserStreamParam,
                                                IntPtr CallackParam);

    private static DebugHelperConfig config_;

    /// <exclude/>
    public static void Init()
    {
      try
      {
        //load config from xml
        var configxml = Configurator.GetConfigXml("DebugHelper", null);
        if (configxml==null)
          return;

        config_ = DebugHelperConfig.LoadFromXML(configxml);
        if (config_==null)
          return;

        if (config_.DumpType != MiniDumpType.None && config_.DumpTrigger!= ErrorTrigger.None)
        {
          //application unhandled exception
          if ((config_.DumpTrigger & ErrorTrigger.ApplicationCrash) != 0)
          {
            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += CurrentDomain_UnhandledException;
          }

          //toolkit "unknown exception"
          if ((config_.DumpTrigger & ErrorTrigger.ToolkitError) != 0)
            Native.DebugHelper.SetDumpOptions(config_.DumpFolder, (int)config_.DumpType, false);
          else if ((config_.DumpTrigger & ErrorTrigger.FirstToolkitError) != 0)
            Native.DebugHelper.SetDumpOptions(config_.DumpFolder, (int)config_.DumpType, true);

        }
      }
      catch (Exception ex)
      {
        logger.Error("Failed to setup DebugHelper.", ex);
      }
    }

    #region Handler for unhandled exception for current AppDomain

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
      //here to generate a unique file name
      if (string.IsNullOrEmpty(config_.DumpFolder) == false)
      {
        using (Process process = Process.GetCurrentProcess())
        {
          string filename = string.Format("debughelper-{0}-{1:MM-dd-yyyy-hh}-H{1:hh}-M{1:mm}.dmp", process.Id, DateTime.Now);
          var dumpFileName = Path.Combine(config_.DumpFolder, filename);
          CreateMiniDump(dumpFileName, config_.DumpType);
        }
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="filename">dump file location</param>
    /// <param name="dumpType">dump type</param>
    public static void CreateMiniDump(string filename, MiniDumpType dumpType)
    {
      try
      {
        if (dumpType == MiniDumpType.None || string.IsNullOrEmpty(filename))
          return;

        using (var fs = new FileStream(filename, FileMode.Create))
        {
          using (var process = Process.GetCurrentProcess())
          {
            MiniDumpWriteDump(process.Handle, process.Id,
                                             fs.SafeFileHandle.DangerousGetHandle(),
                                             config_.DumpType,
                                             IntPtr.Zero,
                                             IntPtr.Zero,
                                             IntPtr.Zero);
          }
        }
      }
      catch (Exception ex)
      {
        logger.Error("Failed inside CreateMiniDump.", ex);
      }
    }

    #endregion
  }

}
