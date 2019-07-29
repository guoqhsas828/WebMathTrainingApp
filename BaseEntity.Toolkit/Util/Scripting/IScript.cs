//
//  -2011. All rights reserved.
//

using System;

namespace BaseEntity.Toolkit.Util.Scripting
{
  /// <summary>
  ///  Scripting interface
  /// </summary>
  public interface IScript
  {
    /// <summary>
    /// Gets the types of arguments of the specified method.
    /// </summary>
    /// <param name="methodName">Name of the method.</param>
    /// <returns>An array of types</returns>
    Type[] GetArgumentTypes(string methodName);
    /// <summary>
    /// Gets the type of the return value of the specified method.
    /// </summary>
    /// <param name="methodName">Name of the method.</param>
    /// <returns>The type of the return value.</returns>
    Type GetReturnType(string methodName);
    /// <summary>
    /// Executes the specified method in the compiled script.
    /// </summary>
    /// <param name="methodName">Name of the method.</param>
    /// <param name="args">The arguments.</param>
    /// <returns>Whatever returned by the method.</returns>
    object Execute(string methodName, object[] args);
  }
}
