using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;

namespace BaseEntity.Toolkit.Util.Scripting
{
  /// <summary>
  ///   The manager allowing register and enumerate supported scripts.
  /// </summary>
  public static class ScriptManager
  {
    private static readonly ILog logger = LogManager.GetLogger(typeof(ScriptManager));

    /// <summary>
    /// Compiles the script.
    /// </summary>
    /// <param name="source">The source codes</param>
    /// <param name="scriptType">The scripting language type.</param>
    /// <param name="entryMethod">The entry method</param>
    /// <param name="assemblyReferences">The assembly references</param>
    /// <returns>A compiled script object</returns>
    public static IScript Compile(
      string source,
      string scriptType,
      string entryMethod,
      string[] assemblyReferences)
    {
      if(String.IsNullOrEmpty(scriptType))
      {
        throw new ArgumentException("Cannot be empty","scriptType");
      }
      Func<string, string, string[], IScript> fn;
      if (!_compilers.TryGetValue(scriptType, out fn))
      {
        fn = ScriptCompilerInfo.GetCompiler(scriptType);
        if (fn == null)
        {
          throw new ArgumentException(String.Format(
            "{0}: Unknown script type", scriptType));
        }
        _compilers.Add(scriptType, fn);
      }
      return fn(source,entryMethod,assemblyReferences);
    }

    /// <summary>
    ///   Register a supported script type.
    /// </summary>
    public static void RegisterSupportedScript(
      string scriptType,
      Func<string, string, string[], IScript> scriptCompiler)
    {
      if (String.IsNullOrEmpty(scriptType))
      {
        throw new ArgumentException("Cannot be empty", "scriptType");
      }
      if (_compilers.ContainsKey(scriptType) && logger.IsDebugEnabled)
      {
        logger.Debug(String.Format("Overload script type {0}", scriptType));
      }
      _compilers[scriptType] = scriptCompiler;
    }

    /// <summary>
    ///   Gets the list of supported script types.
    /// </summary>
    public static IEnumerable<string> SupportedScripts
    {
      get { return _compilers.Keys; }
    }

    #region Private data and helpers
    private static readonly Dictionary<string, Func<string, string, string[],
      IScript>> _compilers = new Dictionary<string, Func<string, string,
      string[], IScript>>
      {
        {"CSharp", CSharpScriptCompiler},
        {"VisualBasic", VisualBasicScriptCompiler},
      };

    private static IScript CSharpScriptCompiler(string source,
      string entryMethod, string[] references)
    {
      var script = (CSharpScript)SetReferences(new CSharpScript(), references);
      script.Compile(String.Join("\n", source), null);
      return script;
    }

    private static IScript VisualBasicScriptCompiler(string source,
      string entryMethod, string[] references)
    {
      var script = (VisualBasicScript)SetReferences(new VisualBasicScript(), references);
      script.Compile(String.Join("\n", source), null);
      return script;
    }

    /// <summary>
    /// Sets up the references used by the script.
    /// </summary>
    /// <param name="script">The script</param>
    /// <param name="references">The references</param>
    /// <returns>The script with the references</returns>
    private static DotNetScript SetReferences(
      DotNetScript script, IEnumerable<string> references)
    {
      var mypath = Assembly.GetExecutingAssembly().CodeBase;
      var url = new Uri(mypath);
      mypath = url.IsFile ? url.LocalPath
        : Assembly.GetExecutingAssembly().Location;
      var dir = Path.GetDirectoryName(mypath) ?? "";
      script.References.Add(mypath);
      script.References.Add(Path.Combine(dir, "BaseEntity.Shared.dll"));
      script.References.Add(Path.Combine(dir, "BaseEntity.Toolkit.Base.dll"));
      if (references != null)
      {
        foreach (var path in references)
        {
          var p = path;
          if (!Path.IsPathRooted(p) && p.StartsWith("BaseEntity."))
          {
            var pp = Path.Combine(dir, p);
            if (File.Exists(pp)) p = pp;
          }
          if (script.References.Contains(p)) continue;
          script.References.Add(p);
        }
      }
      if (!script.References.Contains("System.XML.dll"))
      {
        script.References.Add("System.XML.dll");
      }
      return script;
    }

    #endregion

  }
}
