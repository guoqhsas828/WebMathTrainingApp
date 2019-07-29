// 
//  -2015. All rights reserved.
// 

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BaseEntity.Configuration;

namespace BaseEntity.Toolkit.Util.Configuration
{
  /// <summary>
  /// 
  /// </summary>
  public class ToolkitPlugin : IPlugin
  {
    /// <exclude />
    public void CheckLicense()
    {
    }

    /// <summary>
    /// 
    /// </summary>
    public void Init()
    {
      if (SystemContext.IsWindows) SetToolkitPath();

      ToolkitConfigurator.Init();
    }

    private static void SetToolkitPath()
    {
      var codeBase = Assembly.GetExecutingAssembly().CodeBase;
      var binFolder = Path.GetDirectoryName(new Uri(codeBase).LocalPath);
      if (binFolder == null)
      {
        throw new InvalidOperationException("CodeBase for [" + Assembly.GetExecutingAssembly() + "] cannot be null");
      }
      var arch = IntPtr.Size == 8 ? "x64" : "x86";
      SetDllDirectory(Path.Combine(binFolder, arch == "x86" ? "x86" : "x64"));
    }

    [DllImport("Kernel32", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string pathName);
  }
}