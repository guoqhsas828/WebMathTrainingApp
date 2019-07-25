using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  Dt equatlity comparers with various hash functions.
  /// </summary>
  /// <remarks></remarks>
  public abstract class DtEqualityComparer : IEqualityComparer<Dt>, IEqualityComparer
  {
    private static readonly DtEqualityComparer hashByMonth_ = new ByMonthHash();

    /// <summary>
    /// Gets the hash by month.
    /// </summary>
    /// <remarks></remarks>
    public static DtEqualityComparer HashByMonth
    {
      get { return hashByMonth_; }
    }

    #region IEqualityComparer<Dt> Members

    /// <summary>
    /// Determines whether the two dates are equal.
    /// </summary>
    /// <param name="x">The first date to compare.</param>
    /// <param name="y">The second date to compare.</param>
    /// <returns>true if the specified dates are equal; otherwise, false.</returns>
    public bool Equals(Dt x, Dt y)
    {
      return x == y;
    }

    /// <summary>
    /// Get hash code for date
    /// </summary>
    /// <param name="obj">Date</param>
    /// <returns>Hash code for date</returns>
    public abstract int GetHashCode(Dt obj);

    #endregion

    #region IEqualityComparer Members

    /// <summary>
    /// Determines whether the specified objects are equal.
    /// </summary>
    /// <param name="x">The first object to compare.</param>
    /// <param name="y">The second object to compare.</param>
    /// <returns>true if the specified objects are equal; otherwise, false.</returns>
    /// <exception cref="T:System.ArgumentException">
    ///   <paramref name="x"/> and <paramref name="y"/> are of different types and neither one can handle comparisons with the other.</exception>
    /// <remarks></remarks>
    bool IEqualityComparer.Equals(object x, object y)
    {
      return (Dt)x == (Dt)y;
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <param name="obj">The <see cref="T:System.Object"/> for which a hash code is to be returned.</param>
    /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
    /// <exception cref="T:System.ArgumentNullException">The type of <paramref name="obj"/> is a reference type and <paramref name="obj"/> is null.</exception>
    /// <remarks></remarks>
    int IEqualityComparer.GetHashCode(object obj)
    {
      return GetHashCode((Dt) obj);
    }

    #endregion

    #region Nested Type
    private class ByMonthHash : DtEqualityComparer
    {
      public override int GetHashCode(Dt obj)
      {
        return obj.Year * 12 + obj.Month;
      }
    }
    #endregion
  }
}
