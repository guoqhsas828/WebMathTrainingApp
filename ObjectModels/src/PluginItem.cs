// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BaseEntity.Configuration
{
  /// <summary>
  /// 
  /// </summary>
  public class PluginItem
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="pluginType"></param>
    public PluginItem(string fileName, PluginType pluginType)
    {
      if (fileName == null)
      {
        throw new ArgumentNullException("fileName");
      }
      FileName = fileName;
      PluginType = pluginType;
      _lazyAssembly = new Lazy<Assembly>(LoadAssembly);
    }

    /// <summary>
    /// 
    /// </summary>
    public string FileName { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public PluginType PluginType { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public Assembly Assembly
    {
      get { return _lazyAssembly.Value; }
    }

    private Assembly LoadAssembly()
    {
      string assemblyName = Path.GetFileNameWithoutExtension(FileName);
      if (assemblyName == null)
      {
        throw new Exception(String.Format(
          "Invalid fileName [{0}]", FileName));
      }

      var assembly = Assembly.Load(new AssemblyName(assemblyName));

      var plugins = assembly.GetCustomAttributes().OfType<PluginAttribute>()
        .Select(a => (IPlugin)Activator.CreateInstance(a.PluginClassType)).ToList();

      foreach (var plugin in plugins)
      {
        plugin.CheckLicense();
      }

      return assembly;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return FileName;
    }

    private readonly Lazy<Assembly> _lazyAssembly;
  }
}