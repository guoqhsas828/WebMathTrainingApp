//
// ScriptCompilerInfo.cs
//   201. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Util.Scripting
{
  /// <summary>
  ///  Class ScriptCompilerInfo
  /// </summary>
  /// <exclude>For internal use only.</exclude>
  [Serializable]
  public class ScriptCompilerInfo
  {
    #region Instance members
    /// <summary>
    /// The script name
    /// </summary>
    public readonly string ScriptName;
    /// <summary>
    /// The assembly name
    /// </summary>
    public readonly string AssemblyName;
    /// <summary>
    /// The type name
    /// </summary>
    public readonly string TypeName;
    /// <summary>
    /// The method name
    /// </summary>
    public readonly string MethodName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptCompilerInfo" /> class.
    /// </summary>
    /// <param name="scriptName">Name of the script.</param>
    /// <param name="assemblyName">Name of the assembly.</param>
    /// <param name="typeName">Name of the type.</param>
    /// <param name="methodName">Name of the method.</param>
    private ScriptCompilerInfo(string scriptName,
      string assemblyName, string typeName, string methodName)
    {
      ScriptName = scriptName;
      AssemblyName = assemblyName;
      TypeName = typeName;
      MethodName = methodName;
    }
    #endregion

    #region Static members

    private static readonly Dictionary<string, ScriptCompilerInfo> _compilers
      = new Dictionary<string, ScriptCompilerInfo>
        {
          {
            "CSharpDealScript", new ScriptCompilerInfo(
              "CSharpDealScript",
              "BaseEntity.Toolkit.Deal",
              "BaseEntity.Toolkit.Deals.Scripting.CSharpDealScript",
              "CompileScript")
            },
          {
            "VisualBasicDealScript", new ScriptCompilerInfo(
              "VisualBasicDealScript",
              "BaseEntity.Toolkit.Deal",
              "BaseEntity.Toolkit.Deals.Scripting.VisualBasicDealScript",
              "CompileScript")
            },
          {
            "PythonDealScript", new ScriptCompilerInfo(
              "PythonDealScript",
              "BaseEntity.Toolkit.Deal",
              "BaseEntity.Toolkit.Deals.Scripting.PythonDealScript",
              "CompileScript")
            },
        };

    private static bool _firstTime = true;

    private static void Initialize()
    {
      var root = Configurator.GetConfigXml("ScriptManager", null);
      if (root == null) return;
      var elements = root.GetElementsByTagName("Compiler");
      if (elements.Count == 0) return;
      foreach (XmlElement elem in elements)
      {
        var info = ToolkitConfigUtil.LoadElement<ScriptCompilerInfo>(elem);
        if (_compilers.ContainsKey(info.ScriptName))
          _compilers[info.ScriptName] = info;
        else
          _compilers.Add(info.ScriptName, info);
      }
    }

    internal static Func<string, string, string[], IScript> GetCompiler(
      string scriptName)
    {
      // On the first time call, try load any configuration.
      if (_firstTime)
      {
        lock (_compilers)
        {
          if (_firstTime)
          {
            Initialize();
            _firstTime = false;
          }
        }
      }

      ScriptCompilerInfo info;
      if (!_compilers.TryGetValue(scriptName, out info))
        return null;
      var filename = info.AssemblyName + ".dll";
      var path = filename;
      if (!File.Exists(path))
      {
        var dir = typeof(Configurator).Assembly.Location;
        dir = Path.GetDirectoryName(dir);
        path = Path.Combine(dir, filename);
        if (!File.Exists(path))
        {
          path = Path.Combine(SystemContext.InstallDir, "bin", filename);
        }
        if (!File.Exists(path))
        {
          throw new ScriptingException(String.Format(
            "{0}: file not found", filename));
        }
      }
      var assembly = Assembly.LoadFrom(path);
      var type = assembly.GetType(info.TypeName);
      if (type == null)
      {
        throw new ScriptingException(String.Format(
          "{0}: type could not load", info.TypeName));
      }
      var method = type.GetMethod(info.MethodName, BindingFlags.Public |
        BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
      if (method == null)
      {
        throw new ScriptingException(String.Format(
          "{0}: method not found", info.TypeName));
      }
      return (Func<string, string, string[], IScript>)Delegate
        .CreateDelegate(typeof(Func<string, string, string[], IScript>),
          method.IsStatic ? null : Activator.CreateInstance(type),
          method);
    }

    #endregion
  }
}
