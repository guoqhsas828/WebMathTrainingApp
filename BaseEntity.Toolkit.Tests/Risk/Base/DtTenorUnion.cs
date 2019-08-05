/*
 * DtTenorUnion.cs
 *
 */

using System;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  /// <summary>
  /// Helper class to represent an object that can be either a Tenor or a Dt
  /// </summary>
  [Serializable]
  public struct DtTenorUnion : IComparable, IFormattable
  {
    private Dt? dt_;
    private Tenor? tenor_;
    private Dt from_; 
    
    ///<summary>
    /// Initialize with a Dt
    ///</summary>
    public DtTenorUnion(Dt dt)
    {
      dt_ = dt;
      tenor_ = null;
      from_ = Dt.Today(); 
    }

    ///<summary>
    /// Initialize with a Tenor
    ///</summary>
    public DtTenorUnion(Tenor tenor)
    {
      tenor_ = tenor;
      dt_ = null;
      from_ = Dt.Today(); 
    }

    ///<summary>
    /// Initialize with a Tenor and a specific date to begin from when converting Tenor to Dt 
    ///</summary>
    public DtTenorUnion(Tenor tenor, Dt from)
    {
      tenor_ = tenor;
      dt_ = null;
      from_ = from;
    }

    ///<summary>
    /// Is this instance a Tenor
    ///</summary>
    public bool IsTenor
    {
      get { return tenor_.HasValue; }
    }

    ///<summary>
    /// Is this instance a Dt
    ///</summary>
    public bool IsDt
    {
      get { return dt_.HasValue; }
    }

    ///<summary>
    /// First try to parse string into a Tenor then a Dt. 
    ///</summary>
    public static DtTenorUnion Parse(string s)
    {
      return Parse(s, Dt.Today());
    }

     ///<summary>
    /// First try to parse string into a Tenor then a Dt. 
    ///</summary>
    public static DtTenorUnion Parse(string s, Dt from)
    {
       Tenor tenor;
       if (Tenor.TryParse(s, out tenor))
         return new DtTenorUnion(tenor, from);
       else
         return new DtTenorUnion(new Dt(DateTime.Parse(s)));
    }

    

    ///<summary>
    /// Get value as a Dt
    ///</summary>
    ///<returns>Date if set, otherwise Dt.Empty</returns>
    public Dt ToDt()
    {
      if (IsDt)
        return dt_.Value;
      else
        return Dt.Empty;
    }

    ///<summary>
    /// Get value as a Tenor
    ///</summary>
    ///<returns>Tenor is set, otherwise empty Tenor</returns>
    public Tenor ToTenor()
    {
      if (IsTenor)
        return tenor_.Value; 
      else
        return Tenor.Empty;
    }

    #region Implementation of IComparable

    /// <summary>
    /// Compares the current object with another object of the same type. Sorts by date. If IsTenor, converts tenor to date from Today. 
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
    public int CompareTo(object other)
    {
      if(!(other is DtTenorUnion))
        throw new ArgumentException(String.Format("Cannot compare {0} to a DtTenorUnion", other));

      Dt dt = IsDt ? dt_.Value : Dt.Add(from_, tenor_.Value);

      var otherDtTenorUnion = (DtTenorUnion)other;
      Dt otherDt = otherDtTenorUnion.IsDt ? otherDtTenorUnion.ToDt() : Dt.Add(otherDtTenorUnion.from_, otherDtTenorUnion.ToTenor());

      return dt.CompareTo(otherDt);
    }
    
    #endregion

    #region Implementation of IFormattable

    /// <summary>
    /// Returns the Dt or Tenor value as a string
    /// </summary>
    public override string ToString()
    {
      if (IsDt)
        return dt_.Value.ToString();
      else
        return tenor_.Value.ToString(); 
    }

    /// <summary>
    /// Formats the value of the current instance using the specified format.
    /// </summary>
    /// <returns>
    /// A <see cref="T:System.String"/> containing the value of the current instance in the specified format.
    /// </returns>
    /// <param name="format">The <see cref="T:System.String"/> specifying the format to use.
    ///                     -or- 
    ///                 null to use the default format defined for the type of the <see cref="T:System.IFormattable"/> implementation. 
    ///                 </param><param name="formatProvider">The <see cref="T:System.IFormatProvider"/> to use to format the value.
    ///                     -or- 
    ///                 null to obtain the numeric format information from the current locale setting of the operating system. 
    ///                 </param><filterpriority>2</filterpriority>
    public string ToString(string format, IFormatProvider formatProvider)
    {
      if (IsDt)
        return dt_.Value.ToString(format, formatProvider);
      else
        return tenor_.Value.ToString(format, formatProvider); 
    }

    #endregion IFormattable


    #region Operators

    /// <summary>Greater that operator</summary>
    public static bool operator >(DtTenorUnion d1, DtTenorUnion d2) { return (d1.CompareTo(d2) > 0); }

    /// <summary>Greater or equal than operator</summary>
    public static bool operator >=(DtTenorUnion d1, DtTenorUnion d2) { return (d1.CompareTo(d2) >= 0); }

    /// <summary>Less than operator</summary>
    public static bool operator <(DtTenorUnion d1, DtTenorUnion d2) { return (d1.CompareTo(d2) < 0); }

    /// <summary>Less or equal to operator</summary>
    public static bool operator <=(DtTenorUnion d1, DtTenorUnion d2) { return (d1.CompareTo(d2) <= 0); }

    /// <summary>Equal to operator</summary>
    public static bool operator ==(DtTenorUnion d1, DtTenorUnion d2) { return (d1.Equals(d2)); }

    /// <summary>Not equal to operator</summary>
    public static bool operator !=(DtTenorUnion d1, DtTenorUnion d2) { return (!d1.Equals(d2)); }

    #endregion Operators

    ///<summary>
    /// Equivalent of Tenor.Empty
    ///</summary>
    public static DtTenorUnion Empty
    {
      get { return new DtTenorUnion(Tenor.Empty);}
    }

    /// <summary>
    /// Part of operator == overload.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
      if (obj == null || !(obj is DtTenorUnion))
        return false;

      DtTenorUnion other = (DtTenorUnion)obj;
      if(IsDt)
      {
        if(!other.IsDt)
          return false;
        return dt_.Value.Equals(other.ToDt());
      }
      else
      {
        if (!other.IsTenor)
          return false;
        return tenor_.Value.Equals(other.ToTenor()) && from_.Equals(other.from_);
      }
    }

    /// <summary>
    /// Part of operator == overload.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
      if (IsDt)
        return dt_.Value.GetHashCode();
      else
        return tenor_.Value.GetHashCode() ^ from_.GetHashCode();
    }
  }
}
