//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Tests.Helpers;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests
{
  [SetUpFixture]
  public class BaseEntityContext
  {
    private static readonly object SyncRoot = new object();

    /// <summary>
    /// Initializes this instance.
    /// </summary>
    /// <remarks></remarks>
    [OneTimeSetUp]
    public static void Initialize()
    {
      if (Initialized) return;

      //This is to avoid race condition inside ToolkitConfigurator.Init()
      lock (SyncRoot)
      {
        if (Initialized) return;

        SetToolkitPath();
        Parsers.Initialize();
        Configurator.Init("ToolkitUnitTest");
        Initialized = true;
      }
    }

    private static void SetToolkitPath()
    {
      var qnroot = Path.GetFullPath(InstallDir);
      var dllPath = FindNativeToolkitFolder(qnroot);
      if (SystemContext.IsWindows)
      {
        SetDllDirectory(dllPath);
      }
      DisableFma3();
    }

    private static string FindNativeToolkitFolder(string qnroot)
    {
      var codeBase = Assembly.GetExecutingAssembly().CodeBase;
      var binFolder = Path.GetDirectoryName(new Uri(codeBase).LocalPath);
      if (binFolder == null)
      {
        throw new InvalidOperationException("CodeBase for [" + Assembly.GetExecutingAssembly() + "] cannot be null");
      }

      var arch = IntPtr.Size == 8 ? "x64" : "x86";
      if (string.IsNullOrEmpty(qnroot) || !binFolder.Contains(qnroot))
        return Path.Combine(binFolder, arch);

      var path = Path.Combine(qnroot, "bin");
      var xpath = Path.Combine(path, arch);
      if (binFolder.Contains(path) && Directory.Exists(xpath))
      {
        return xpath;
      }

      path = Path.Combine(qnroot, "bin", "Release");
      xpath = Path.Combine(path, arch);
      if (binFolder.Contains(path) && Directory.Exists(xpath))
      {
        return xpath;
      }

      path = Path.Combine(qnroot, "bin", "Debug");
      xpath = Path.Combine(path, arch);
      return binFolder.Contains(path) && Directory.Exists(xpath)
        ? xpath : Path.Combine(binFolder, arch);
    }

    [DllImport("Kernel32", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string pathName);


    [DllImport("MagnoliaIGNative")]
    private static extern void DisableFma3();

    /// <summary>
    /// Gets a value indicating whether this <see cref="BaseEntityContext"/> has called the Init() method.
    /// </summary>
    /// <remarks></remarks>
    public static bool Initialized { get; private set; } = false;

    public static string InstallDir
    {
      get
      {
        if (string.IsNullOrEmpty(SystemContext.InstallDir))
        {
          Configurator.InitPhaseOne();
        }
        return SystemContext.InstallDir;
      }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is generating expects.
    /// </summary>
    /// <value><c>true</c> if this instance is generating expects; otherwise, <c>false</c>.</value>
    public static bool IsGeneratingExpects
    {
      get
      {
        var value = Environment.GetEnvironmentVariable("QUNIT_GENERATE_EXPECTS");
        return String.Compare(value, "true", StringComparison.OrdinalIgnoreCase) == 0
          || String.Compare(value, "yes", StringComparison.OrdinalIgnoreCase) == 0;
      }
    }
  }

}
