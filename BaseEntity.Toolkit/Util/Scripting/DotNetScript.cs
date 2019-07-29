/*
 * DotNetScript.cs
 *
 *  -2010. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;

namespace BaseEntity.Toolkit.Util.Scripting
{
  /// <summary>
  ///  Base class for all the .Net languages.
  /// </summary>
  [Serializable]
  public abstract class DotNetScript
  {
    private static readonly ILog logger = LogManager.GetLogger(typeof (DotNetScript));

    #region Methods

    /// <summary>
    /// Compiles this instance.
    /// </summary>
    protected abstract void Compile();

    /// <summary>
    /// Gets the argument types.
    /// </summary>
    /// <param name="methodName">Name of the method.</param>
    /// <returns></returns>
    public Type[] GetArgumentTypes(string methodName)
    {
      MethodInfo mi = GetMethod(null, methodName);
      return Array.ConvertAll(mi.GetParameters(), (p) => p.ParameterType);
    }

    /// <summary>
    /// Gets the type of the return.
    /// </summary>
    /// <param name="methodName">Name of the method.</param>
    /// <returns></returns>
    public Type GetReturnType(string methodName)
    {
      MethodInfo mi = GetMethod(null, methodName);
      return mi.ReflectedType;
    }

    /// <summary>
    /// Gets the compiled method.
    /// </summary>
    /// <param name="typeName">Name of the type.</param>
    /// <param name="methodName">Name of the method.</param>
    /// <returns>The compiled method</returns>
    private MethodInfo GetMethod(string typeName, string methodName)
    {
      if (String.IsNullOrEmpty(typeName))
      {
        typeName = "BaseEntity.Scripting.UserScript";
      }
      if (String.IsNullOrEmpty(methodName))
      {
        methodName = EntryMethod;
      }

      // If script not compiled, do it now
      if (Assembly == null)
        Compile();

      // Run
      try
      {
        //Use reflection to call the static Main function
        Module[] mods = Assembly.GetModules(false);
        Type type = mods[0].GetType(typeName);

        MethodInfo mi = type.GetMethod(methodName,
          BindingFlags.Public | BindingFlags.Static);
        if (mi != null)
        {
          return mi;
        }
      }
      catch (Exception ex)
      {
        throw new Exception("Error calling " + methodName, ex);
      }
      // else not found
      throw new Exception("Could not execute script - Unable to find method "
        + methodName);
    }

    /// <summary>
    /// Executes the specified method.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="args">The args.</param>
    /// <returns></returns>
    public object Execute(string method, object[] args)
    {
      MethodInfo mi = GetMethod(null, method);
      return mi.Invoke(null, args);
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Source code for script.
    /// </summary>
    public string SourceCode { get; protected set; }

    /// <summary>
    ///   Source code for script.
    /// </summary>
    public string EntryMethod
    {
      get { return entryMethod_ ?? "ScriptMain"; }
      set { entryMethod_ = value; }
    }

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
    protected Assembly Assembly
    {
      get { return _assembly; }
      set { _assembly = value; }
    }

    #endregion // Properties

    #region Data

    private readonly List<string> references_ = new List<string>();
    private readonly List<string> namespaces_ = new List<string>();
    private string entryMethod_;

    [NonSerialized] private Assembly _assembly;

    #endregion // Data
  }
}