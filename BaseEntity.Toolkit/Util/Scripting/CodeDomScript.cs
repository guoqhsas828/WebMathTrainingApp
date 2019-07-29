/*
 * CodeDomScript.cs
 *
 *  -2013. All rights reserved.
 *
 */
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using Microsoft.CSharp;

namespace BaseEntity.Toolkit.Util.Scripting
{
  /// <summary>
  ///  Scripts in any CodeDom supported language
  /// </summary>
  public sealed class CodeDomScript
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CodeDomScript));

    private CodeDomProvider _provider;
    private readonly Dictionary<string, Type> variables_ = new Dictionary<string, Type>();
    private readonly List<string> codeSnippets_ = new List<string>();

    /// <summary>
    /// Class to help create and compile a script in chosen language
    /// </summary>
    /// <param name="language">CodeDom supported Language</param>
    public CodeDomScript(string language)
    {
      // Create an instance whichever code provider that is needed
      var provOptions = new Dictionary<string, string> { { "CompilerVersion", "v4.0" } };
      _provider = CodeDomProvider.CreateProvider(language, provOptions);
    }

    /// <summary>
    /// variable names/types to push into scope
    /// </summary>
    public Dictionary<string, Type> Variables
    {
      get { return variables_; }
    }

    /// <summary>
    /// 
    /// </summary>
    public List<string> CodeSnippets
    {
      get { return codeSnippets_; }
    }


    #region Methods

    /// <summary>
    /// Compiles the specified source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="scriptName">Name of the script.</param>
    public void Compile(string source, string scriptName)
    {
      // Create a new CodeCompileUnit to contain  
      // the program graph.
      CodeCompileUnit compileUnit = new CodeCompileUnit();

      // Declare a new namespace 
      CodeNamespace samples = new CodeNamespace("BaseEntity.Scripting");
      // Add the new namespace to the compile unit.
      compileUnit.Namespaces.Add(samples);
      // Add the new namespace import for the System namespace.
      samples.Imports.Add(new CodeNamespaceImport("System"));
      samples.Imports.Add(new CodeNamespaceImport("BaseEntity.Toolkit.Base"));
      samples.Imports.Add(new CodeNamespaceImport("BaseEntity.Toolkit.Calibrators"));
      samples.Imports.Add(new CodeNamespaceImport("BaseEntity.Toolkit.Curves"));
      samples.Imports.Add(new CodeNamespaceImport("BaseEntity.Toolkit.Numerics"));
      samples.Imports.Add(new CodeNamespaceImport("BaseEntity.Toolkit.Pricers"));
      samples.Imports.Add(new CodeNamespaceImport("BaseEntity.Toolkit.Products"));

      foreach (var ns in Namespaces)
      {
        samples.Imports.Add(new CodeNamespaceImport(ns));
      }

      // Declare a new type called UserScript.
      CodeTypeDeclaration class1 = new CodeTypeDeclaration(scriptName ?? "UserScript");

      // Add the new type to the namespace type collection.
      samples.Types.Add(class1);


      // make variable properties on scoping object
      foreach (var pair in Variables)
      {
        AddProperty(class1, pair.Value, pair.Key);
      }

      foreach (var codeSnippet in CodeSnippets)
      {
        var snippetMethod = new CodeSnippetTypeMember(codeSnippet);
        class1.Members.Add(snippetMethod);
      }

      StringWriter src = new StringWriter();

      _provider.GenerateCodeFromCompileUnit(compileUnit, src, null);

      SourceCode = src.ToString();
      Compile();
    }

    private void AddProperty(CodeTypeDeclaration targetClass, Type type, string name)
    {
      var backingField = new CodeMemberField
      {
        Attributes = MemberAttributes.Private,
        Name = String.Format("_{0}{1}", Char.ToLower(name[0]), name.Substring(1)),
        Type = new CodeTypeReference(type)
      };

      targetClass.Members.Add(backingField);

      var property = new CodeMemberProperty();

      property.Name = name;
      property.Attributes = MemberAttributes.Public | MemberAttributes.Final;
      property.Type = new CodeTypeReference(type);
      property.HasGet = true;
      property.HasSet = true;
      property.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), backingField.Name)));
      property.SetStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), backingField.Name), new CodePropertySetValueReferenceExpression()));
      targetClass.Members.Add(property);
    }

    /// <summary>
    /// </summary>
    /// <typeparam name="TFunc"></typeparam>
    /// <param name="snippet"></param>
    /// <param name="funcName"></param>
    /// <param name="paramNames"></param>
    public void AddFuncFromCodeSnippet<TFunc>(string snippet, string funcName, params string[] paramNames)
    {
      var options = new CodeGeneratorOptions();
      options.BracingStyle = "C";
      CodeMemberMethod method1 = new CodeMemberMethod();

      var methodType = typeof(TFunc);
      var methodInfo = methodType.GetMethod("Invoke");

      method1.Name = funcName;
      method1.Attributes = MemberAttributes.Public | MemberAttributes.Final;
      method1.ReturnType = new CodeTypeReference(methodInfo.ReturnType);
      for (int i = 0; i < methodInfo.GetParameters().Length; i++)
      {
        var parameterInfo = methodInfo.GetParameters()[i];
        var paramName = paramNames.Any() ? paramNames[i] : parameterInfo.Name;
        method1.Parameters.Add(new CodeParameterDeclarationExpression(parameterInfo.ParameterType, paramName));
      }

      var src = String.Format("#line 1 \"{0}\" {1} {2}", funcName, Environment.NewLine, snippet);

      method1.Statements.Add(new CodeSnippetStatement(src));
      StringWriter sw = new StringWriter();
      _provider.GenerateCodeFromMember(method1, sw, options);
      CodeSnippets.Add(sw.ToString());
    }





    /// <summary>
    ///   Compile script
    /// </summary>
    ///
    protected void Compile()
    {
      // Add compiler parameters
      CompilerParameters compilerParams = new CompilerParameters();
      compilerParams.CompilerOptions = "/target:library"; // you can add /optimize
      compilerParams.GenerateExecutable = false;
      compilerParams.GenerateInMemory = true;
      compilerParams.IncludeDebugInformation = false;

      // Add some basic references
      compilerParams.ReferencedAssemblies.Add("mscorlib.dll");
      compilerParams.ReferencedAssemblies.Add("System.dll");
      compilerParams.ReferencedAssemblies.Add("System.Core.dll");
      compilerParams.ReferencedAssemblies.Add("System.Data.dll");
      compilerParams.ReferencedAssemblies.Add("Microsoft.CSharp.dll");
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
      CompilerResults results = _provider.CompileAssemblyFromSource(compilerParams, SourceCode);

      // Do we have any compiler errors
      if (results.Errors.Count > 0)
      {
        StringBuilder errMsg = new StringBuilder();

        errMsg.Append("Compile Error: ");
        foreach (CompilerError error in results.Errors)
          errMsg.Append("\r\n").Append(error.ToString());
        errMsg.Append("\r\n");
        throw new CompileErrorException(errMsg.ToString(), results.Errors);
      }

      // Save the actual assembly that was generated
      Assembly = results.CompiledAssembly;
    }


    #endregion // Methods

    /// <summary>
    ///   List of all assemplies referenced by this script
    /// </summary>
    public List<string> References
    {
      get { return references_; }
    }

    /// <summary>
    ///  List of all the namespaces used.
    /// </summary>
    /// <value>The namespaces.</value>
    public List<string> Namespaces
    {
      get { return namespaces_; }
    }


    /// <summary>
    ///   Compiled script
    /// </summary>
    public Assembly Assembly { get; set; }

    /// <summary>
    ///   Source code for script.
    /// </summary>
    public string SourceCode { get; protected set; }


    private readonly List<string> references_ = new List<string>();
    private readonly List<string> namespaces_ = new List<string>();

  }

  /// <summary>
  /// Exception that propagates CompilerErrorCollection
  /// </summary>
  public class CompileErrorException : Exception
  {
    /// <summary>
    /// Collection of CompilerErrors  
    /// </summary>
    public CompilerErrorCollection CompileErrors { get; set; }

    /// <summary>
    /// Create Exception that propagates CompilerErrorCollection
    /// </summary>
    /// <param name="message"></param>
    /// <param name="errors"></param>
    public CompileErrorException(string message, CompilerErrorCollection errors)
      : base(message)
    {
      CompileErrors = errors;
    }

    /// <summary>
    /// Create Exception that propagates CompilerErrorCollection
    /// </summary>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    /// <param name="errors"></param>
    public CompileErrorException(string message, Exception innerException, CompilerErrorCollection errors)
      : base(message, innerException)
    {
      CompileErrors = errors;
    }


  }

}
