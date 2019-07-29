using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves.Bump
{
  /// <summary>
  ///   Implementation of various curve tenor comparers
  /// </summary>
  [Serializable]
  public abstract class CurveTenorComparer : IComparer<CurveTenor>, IComparer,
    IEqualityComparer<CurveTenor>, IEqualityComparer
  {
    #region Static Data and Properties

    private static readonly CurveTenorComparer byRef_ = new ByReferenceComparer();
    private static readonly CurveTenorComparer byName_ = new ByNameComparer();
    private static readonly CurveTenorComparer byDate_ = new ByCurveDateComparer();
    private static readonly CurveTenorComparer byQuoteKey_ = new ByQuoteKeyComparer();

    /// <summary>
    ///  Comparers tenors by reference
    /// </summary>
    public static CurveTenorComparer ByReference
    {
      get { return byRef_; }
    }

    /// <summary>
    ///  Comparers tenors by tenor name
    /// </summary>
    public static CurveTenorComparer ByName
    {
      get { return byName_; }
    }

    /// <summary>
    ///  Comparers tenors by Quote key
    /// </summary>
    internal static CurveTenorComparer ByQuoteKey
    {
      get { return byQuoteKey_; }
    }

    /// <summary>
    ///  Comparers tenors by tenor name
    /// </summary>
    public static CurveTenorComparer ByCurveDate
    {
      get { return byDate_; }
    }

    /// <summary>
    /// Gets the default comparer for curve tenors.
    /// </summary>
    /// <remarks>The current default is by-name comparer.  This may change in the future.</remarks>
    public static CurveTenorComparer Default
    {
      get { return byQuoteKey_; }
    }
    #endregion

    #region Abstract and Virtual methods

    /// <summary>
    /// Compare two curve tenors
    /// </summary>
    /// <param name="x">First curve tenor</param>
    /// <param name="y">Second curve tenor</param>
    /// <returns>Comparison of two curve tenors</returns>
    public abstract int Compare(CurveTenor x, CurveTenor y);

    /// <summary>
    /// Get hash code for curve tenor
    /// </summary>
    /// <param name="obj"></param>
    /// <returns>Hash code for curve tenor</returns>
    public abstract int GetHashCode(CurveTenor obj);

    /// <summary>
    /// True if two curve tenors are equal
    /// </summary>
    /// <param name="x">First curve tenor</param>
    /// <param name="y">Second curve tenor</param>
    /// <returns>True if curve tenors are equal</returns>
    public virtual bool Equals(CurveTenor x, CurveTenor y)
    {
      return Compare(x, y) == 0;
    }

    #endregion

    #region IComparer Members

    /// <summary>
    /// Compare two curve tenors
    /// </summary>
    /// <param name="x">First curve tenor</param>
    /// <param name="y">Second curve tenor</param>
    /// <returns>Comparison of two curve tenors</returns>
    public int Compare(object x, object y)
    {
      return Compare((CurveTenor) x, (CurveTenor) y);
    }

    #endregion

    #region IEqualityComparer Members

    /// <summary>
    /// Return true if two curve tenors equal
    /// </summary>
    /// <param name="x">First curve tenor</param>
    /// <param name="y">Second curve tenor</param>
    /// <returns>True if two curve tenors are equal</returns>
    public bool Equals(object x, object y)
    {
      return Equals((CurveTenor) x, (CurveTenor) y);
    }

    /// <summary>
    /// Get hash code for curve tenor
    /// </summary>
    /// <param name="obj">Curve tenor</param>
    /// <returns>Hash code for curve tenor</returns>
    public int GetHashCode(object obj)
    {
      return GetHashCode((CurveTenor) obj);
    }

    #endregion

    #region Nested Types

    [Serializable]
    private class ByReferenceComparer : CurveTenorComparer
    {
      public override int Compare(CurveTenor x, CurveTenor y)
      {
        throw new ToolkitException("No order defined for comparison by reference");
      }

      public override bool Equals(CurveTenor x, CurveTenor y)
      {
        return ReferenceEquals(x, y);
      }

      public override int GetHashCode(CurveTenor obj)
      {
        if (obj == null) return 0;
        return obj.GetHashCode();
      }
    }

    [Serializable]
    private class ByNameComparer : CurveTenorComparer
    {
      public override int Compare(CurveTenor x, CurveTenor y)
      {
        return String.CompareOrdinal(x.Name, y.Name);
      }

      public override bool Equals(CurveTenor x, CurveTenor y)
      {
        return x.Name == y.Name;
      }

      public override int GetHashCode(CurveTenor obj)
      {
        if (obj == null) return 0;
        var name = obj.Name;
        return (name ?? "").GetHashCode();
      }
    }

    [Serializable]
    private class ByQuoteKeyComparer : CurveTenorComparer
    {
      public override int Compare(CurveTenor x, CurveTenor y)
      {
        return String.CompareOrdinal(x.QuoteKey, y.QuoteKey);
      }

      public override bool Equals(CurveTenor x, CurveTenor y)
      {
        return x.QuoteKey == y.QuoteKey;
      }

      public override int GetHashCode(CurveTenor obj)
      {
        if (obj == null) return 0;
        var key = obj.QuoteKey;
        return (key ?? "").GetHashCode();
      }
    }

    [Serializable]
    private class ByCurveDateComparer : CurveTenorComparer
    {
      public override int Compare(CurveTenor x, CurveTenor y)
      {
        return Dt.Cmp(x.CurveDate, y.CurveDate);
      }

      public override bool Equals(CurveTenor x, CurveTenor y)
      {
        return x.CurveDate == y.CurveDate;
      }

      public override int GetHashCode(CurveTenor obj)
      {
        if (obj == null) return 0;
        return obj.CurveDate.GetHashCode();
      }
    }
    #endregion
  }
}
