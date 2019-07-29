/*
 * VisualBasicScript.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using Microsoft.VisualBasic;

namespace BaseEntity.Toolkit.Util.Scripting
{
  /// <summary>
  ///  Script in Visual Basic language.
  /// </summary>
  public sealed class VisualBasicScript : DotNetScript, IScript
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(VisualBasicScript));

    #region Methods

    /// <summary>
    /// Compiles the specified source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="scriptName">Name of the script.</param>
    public void Compile(string source, string scriptName)
    {
      StringBuilder src = new StringBuilder();
      src.Append("Imports System\n")
        .Append("Imports BaseEntity.Shared\n")
        .Append("Imports BaseEntity.Toolkit.Base\n")
        .Append("Imports BaseEntity.Toolkit.Calibrators\n")
        .Append("Imports BaseEntity.Toolkit.Curves\n")
        .Append("Imports BaseEntity.Toolkit.Numerics\n")
        .Append("Imports BaseEntity.Toolkit.Pricers\n")
        .Append("Imports BaseEntity.Toolkit.Products\n")
        .Append("Namespace BaseEntity.Scripting\n")
        .Append("Public Class UserScript\n")
        // what is the VB equivalent of #line directive?
        .Append(source)
        .Append("\nEnd Class\n")
        .Append("\nEnd Namespace\n");
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
        {"CompilerVersion", "v3.5"}
      };
      // Get the provider for Microsoft.CSharp
      VBCodeProvider codeProvider = new VBCodeProvider(provOptions);

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

      // Add any aditional references needed
      foreach (string refAssembly in References)
      {
        try
        {
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
    }

    #endregion // Methods
  }
}
