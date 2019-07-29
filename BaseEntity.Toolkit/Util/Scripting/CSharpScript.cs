/*
 * CSharpScript.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.CSharp;
using BaseEntity.Toolkit.Base.Serialization;

namespace BaseEntity.Toolkit.Util.Scripting
{
  /// <summary>
  ///  Scripts in C# language.
  /// </summary>
  [Serializable]
  public sealed class CSharpScript : DotNetScript, IScript
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CSharpScript));

    #region Methods

    /// <summary>
    /// Compiles the specified source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="scriptName">Name of the script.</param>
    public void Compile(string source, string scriptName)
    {
      StringBuilder src = new StringBuilder();
      src.Append("using System;\n")
        .Append("using BaseEntity.Toolkit.Base;\n")
        .Append("using BaseEntity.Toolkit.Base.ReferenceIndices;\n")
        .Append("using BaseEntity.Toolkit.Cashflows;\n")
        .Append("using BaseEntity.Toolkit.Calibrators;\n")
        .Append("using BaseEntity.Toolkit.Curves;\n")
        .Append("using BaseEntity.Toolkit.Numerics;\n")
        .Append("using BaseEntity.Toolkit.Pricers;\n")
        .Append("using BaseEntity.Toolkit.Products;\n")
        .Append("namespace BaseEntity.Scripting {\n")
        .Append("public class UserScript {\n")
        .Append("#line 1 \"")
        .Append(scriptName ?? "User Script")
        .Append("\"\n")
        .Append(source)
        .Append("}}\n");
      SourceCode = src.ToString();
      Compile();
    }

    /// <summary>
    ///   Compile script
    /// </summary>
    ///
    protected override void Compile()
    {
      // Create an instance whichever code provider that is needed
      Dictionary<string, string> provOptions = new Dictionary<string, string>
      {
        {"CompilerVersion", "v4.0"}
      };
      // Get the provider for Microsoft.CSharp
      CSharpCodeProvider codeProvider = new CSharpCodeProvider(provOptions);

      // Add compiler parameters
      CompilerParameters compilerParams = new CompilerParameters();
      compilerParams.CompilerOptions = "/target:library"; // you can add /optimize
      compilerParams.GenerateExecutable = false;
      //compilerParams.GenerateInMemory = true;
      compilerParams.IncludeDebugInformation = false;

      // Add some basic references
      compilerParams.ReferencedAssemblies.Add("mscorlib.dll");
      compilerParams.ReferencedAssemblies.Add("System.dll");
      compilerParams.ReferencedAssemblies.Add("System.Core.dll");
      compilerParams.ReferencedAssemblies.Add("System.Data.dll");
      compilerParams.ReferencedAssemblies.Add("Microsoft.CSharp.dll");

      // Add any additional references needed
      foreach (string refAssembly in References)
      {
        try
        {
          var path = refAssembly;
          if (compilerParams.ReferencedAssemblies.OfType<string>().Any(s =>
            String.Compare(s, path, StringComparison.OrdinalIgnoreCase) == 0))
          {
            continue;
          }
          compilerParams.ReferencedAssemblies.Add(refAssembly);
        }
        catch { }
      }

      // Compile the code
      CompilerResults results = codeProvider.CompileAssemblyFromSource(compilerParams, SourceCode);

      // Do we have any compiler errors
      if (results.Errors.Count > 0)
      {
        StringBuilder errMsg = new StringBuilder();

        errMsg.Append("Compile Error: ");
        foreach (CompilerError error in results.Errors)
          errMsg.Append("\r\n").Append(error.ToString());
        errMsg.Append("\r\n");
        throw new Exception(errMsg.ToString());
      }

      // Save the actual assembly that was generated
      Assembly = results.CompiledAssembly;
      AssemblyData.Add(results.PathToAssembly, Assembly);
    }

    /// <summary>
    /// Executes the script with the specified arguments.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>Return value of the script</returns>
    public object Execute(object[] arguments)
    {
      const string method = "UserMethod";
      return Execute(method, arguments);
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
      if (Assembly == null) Compile();
    }

    #endregion // Methods
  }
}
