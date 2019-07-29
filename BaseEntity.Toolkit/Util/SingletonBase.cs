/*
 * 
 */
using System;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  /// Class SingletonBase.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  [Serializable]
  public class SingletonBase<T> where T: new()
  {
    /// <summary>
    /// The instance
    /// </summary>
    public static readonly T Instance = new T();
  }
}
