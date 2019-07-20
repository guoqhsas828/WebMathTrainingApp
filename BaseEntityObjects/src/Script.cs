/*
 * Script.cs
 *
 * Copyright (c) WebMathTraining 2002-2008. All rights reserved.
 *
 * $Id: Script.cs,v 1.7 2006/09/27 18:57:41 tzhang Exp $
 *
 */

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;

using System.Reflection;
using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;


namespace BaseEntity.Shared
{
  ///
  /// <summary>
  ///   Script compilation and execution engine
  /// </summary>
  ///
  public class Script
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Script));

    #region Constructors

    /// <summary>
    ///   Construct script
    /// </summary>
    /// <param name="code">Source code of script</param>
    ///
    public Script(string code)
    {
      // Some validation
      if (code.Length == 0)
        throw new Exception("No script source");

      sourceCode_ = code;
      references_ = new ArrayList();
      assembly_ = null;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Compile script
    /// </summary>
    ///
    public void Compile()
    {
      // Create an instance whichever code provider that is needed
      CodeDomProvider codeProvider = null;
      codeProvider = new CSharpCodeProvider();

      // Add compiler parameters
      CompilerParameters compilerParams = new CompilerParameters();
      compilerParams.CompilerOptions = "/target:library"; // you can add /optimize
      compilerParams.GenerateExecutable = false;
      compilerParams.GenerateInMemory = true;
      compilerParams.IncludeDebugInformation = false;

      // Add some basic references
      compilerParams.ReferencedAssemblies.Add("mscorlib.dll");
      compilerParams.ReferencedAssemblies.Add("System.dll");
      compilerParams.ReferencedAssemblies.Add("System.Data.dll");
      string assemblyPath = Assembly.GetExecutingAssembly().Location;
      compilerParams.ReferencedAssemblies.Add(assemblyPath);
      logger.Info(assemblyPath);

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
      assembly_ = results.CompiledAssembly;
    }


    /// <summary>
    ///   Execute script
    /// </summary>
    public object Run(string method, object[] args)
    {
      // If script not compiled, do it now
      if (Assembly == null)
        Compile();

      // Run
      try
      {
        //Use reflection to call the static Main function
        Module[] mods = Assembly.GetModules(false);
        Type[] types = mods[0].GetTypes();

        // Look for method and invoke
        foreach (Type type in types)
        {
          MethodInfo mi =
            type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
          if (mi != null)
          {
            return mi.Invoke(null, args);
          }
        }
      }
      catch (Exception ex)
      {
        throw new Exception("Error calling " + method, ex);
      }
      // else not found
      throw new Exception("Could not execute script - Unable to find method " + method);
    }

    #endregion // Methods

    #region Properties

    /// <summary>
    ///   Source code for script.
    /// </summary>
    public string SourceCode
    {
      get { return sourceCode_; }
    }


    /// <summary>
    ///   List of all assemplies referenced by this script
    /// </summary>
    private ArrayList References
    {
      get { return references_; }
    }


    /// <summary>
    ///   Compiled script
    /// </summary>
    private Assembly Assembly
    {
      get { return assembly_; }
    }

    #endregion // Properties

    #region Data

    private string sourceCode_;
    private ArrayList references_;
    private Assembly assembly_;

    #endregion // Data

  } // Script
} // namespace WebMathTraining.Shared
