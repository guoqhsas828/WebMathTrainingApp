using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Shared
{
  /// <summary>
  ///  Represent an empty array
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <remarks></remarks>
  public static class EmptyArray<T>
  {
    /// <summary>
    /// 
    /// </summary>
    public static readonly T[] Instance = new T[0];
  }
}
