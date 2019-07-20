// 
// Copyright (c) WebMathTraining 2002-2016. All rights reserved.
// 

using System;
using System.Collections.Generic;

namespace BaseEntity.Shared
{
  /// <summary>
  /// Pair class that is serializable (unlike <see cref="KeyValuePair{TKey,TValue}" />).
  /// </summary>
  [Serializable]
  public class Pair<T, U> : IComparable<Pair<T, U>>, IComparable where T : IComparable
  {
    #region Data

    // Data
    private T _key;
    private U _value;

    #endregion

    #region Constructors

    /// <summary>
    /// Default Constructor
    /// </summary>
    public Pair()
    {
      _key = default(T);
      _value = default(U);
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    public Pair(T key, U val)
    {
      _key = key;
      _value = val;
    }

    /// <summary>
    /// Copy Constructor.
    /// </summary>
    /// <param name="pair"></param>
    public Pair(Pair<T, U> pair)
    {
      _key = pair.Key;
      _value = pair.Value;
    }

    #endregion

    #region Properties

    /// <summary>
    /// The key value.
    /// </summary>
    public T Key
    {
      get { return _key; }
      set { _key = value; }
    }

    /// <summary>
    /// The value.
    /// </summary>
    public U Value
    {
      get { return _value; }
      set { _value = value; }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Compares 2 pairs based on key.
    /// </summary>
    /// 
    /// <param name="obj">The other pair.</param>
    /// 
    /// <returns>Result</returns>
    /// 
    public int CompareTo(object obj)
    {
      return CompareTo((Pair<T, U>) obj);
    }


    /// <summary>
    /// Compares 2 pairs based on key.
    /// </summary>
    /// 
    /// <param name="other">The other pair</param>
    /// 
    /// <returns>Result</returns>
    /// 
    public int CompareTo(Pair<T, U> other)
    {
      return Key.CompareTo(other.Key);
    }

    /// <summary>
    /// Converts the Pair\langle T,U \rangle to a KeyValuePair \langle T,U \rangle.
    /// </summary>
    /// <returns></returns>
    public KeyValuePair<T, U> ToKeyValuePair()
    {
      return new KeyValuePair<T, U>(_key, _value);
    }

    #endregion
  }
}