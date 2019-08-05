/*
 * TenorValuePair.cs
 *
 *
 */
using System;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  ///<summary>
  /// Component class for storing a Tenor and value
  ///</summary>
  [Component]
  [Serializable]
  public class TenorValuePair : BaseEntityObject, IComparable<TenorValuePair> 
  {
    /// <summary>
    /// Default Constructor
    /// </summary>
    public TenorValuePair()
    {
    }

    /// <summary>
    /// </summary>
    /// <param name="t">A tenor</param>
    /// <param name="val">the value</param>
    public TenorValuePair(Tenor t, double val)
    {
      tenor_ = t;
      value_ = val; 
    }

    ///<summary>
    ///</summary>
    [TenorProperty]
    public Tenor Tenor
    {
      get { return tenor_; }
      set { tenor_ = value; }
    }

    ///<summary>
    ///</summary>
    [NumericProperty]
    public double Value
    {
      get { return value_; }
      set { value_ = value; }
    }

    private Tenor tenor_;
    private double value_;

    #region Implementation of IComparable<TenorValuePair>

    /// <summary>
    /// Compares the current object with another object of the same type.
    /// </summary>
    /// <returns>
    /// A 32-bit signed integer that indicates the relative order of the objects being compared. The return value has the following meanings: 
    ///                     Value 
    ///                     Meaning 
    ///                     Less than zero 
    ///                     This object is less than the <paramref name="other"/> parameter.
    ///                     Zero 
    ///                     This object is equal to <paramref name="other"/>. 
    ///                     Greater than zero 
    ///                     This object is greater than <paramref name="other"/>. 
    /// </returns>
    /// <param name="other">An object to compare with this object.
    ///                 </param>
    public int CompareTo(TenorValuePair other)
    {
      return this.Tenor.CompareTo(other.Tenor);
    }

    #endregion
  }
}
