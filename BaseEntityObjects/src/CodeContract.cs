using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BaseEntity.Shared
{
  /// <summary>
  /// Code Contract for basic arguments check 
  /// </summary>
  public static class CodeContract
  {
    /// <summary>
    /// Specifies a precondition contract for the enclosing method or property, and displays a message if the condition for the contract fails.
    /// </summary>
    /// <typeparam name="TException"></typeparam>
    /// <param name="condition"></param>
    /// <param name="message"></param>
    public static void Requires<TException>(bool condition, string message) where TException : Exception, new()
    {
      if (!condition)
      {
        Debug.WriteLine(message);
        throw new TException();
      }
    }

  }
}
