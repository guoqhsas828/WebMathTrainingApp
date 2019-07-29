// 
//  -2013. All rights reserved.
// 

using System;

namespace BaseEntity.Toolkit.Base
{
  /// <summary></summary>
  [Serializable]
  public class ContractMonthYear : Tuple<ContractMonth, int>, IComparable, IComparable<ContractMonthYear>
  {
    /// <summary></summary>
    public ContractMonthYear(ContractMonth m, int y)
      : base(m, y)
    {}

    /// <summary>Greater that operator</summary>
    public static bool operator >(ContractMonthYear d1, ContractMonthYear d2)
    {
      return (Cmp(d1, d2) > 0);
    }

    /// <summary>Greater or equal than operator</summary>
    public static bool operator >=(ContractMonthYear d1, ContractMonthYear d2)
    {
      return (Cmp(d1, d2) >= 0);
    }

    /// <summary>Less than operator</summary>
    public static bool operator <(ContractMonthYear d1, ContractMonthYear d2)
    {
      return (Cmp(d1, d2) < 0);
    }

    /// <summary>Less or equal to operator</summary>
    public static bool operator <=(ContractMonthYear d1, ContractMonthYear d2)
    {
      return (Cmp(d1, d2) <= 0);
    }

    /// <summary>Equal to operator</summary>
    public static bool operator ==(ContractMonthYear d1, ContractMonthYear d2)
    {
      return (Cmp(d1, d2) == 0);
    }

    /// <summary>Not equal to operator</summary>
    public static bool operator !=(ContractMonthYear d1, ContractMonthYear d2)
    {
      return (Cmp(d1, d2) != 0);
    }

    /// <summary></summary>
    public static int Cmp(ContractMonthYear date1, ContractMonthYear date2)
    {
      if (date1.Item2 < date2.Item2) return -1;
      if (date1.Item2 > date2.Item2) return 1;

      if ((int)date1.Item1 < (int)date2.Item1) return -1;
      if ((int)date1.Item1 > (int)date2.Item1) return 1;

      return 0;
    }

    /// <summary>GetHashCode override</summary>
    public override int GetHashCode()
    {
      return ((int)Item1 | Item2);
    }

    /// <summary>Equals operator override</summary>
    public override bool Equals(object other)
    {
      if (other is ContractMonthYear)
      {
        ContractMonthYear otherDt = (ContractMonthYear)other;
        return (Cmp(this, otherDt) == 0);
      }
      return false;
    }

    /// <summary>
    /// IComparable.CompareTo implementation.
    /// </summary>
    public int CompareTo(object obj)
    {
      if (obj is ContractMonthYear)
        return Cmp(this, (ContractMonthYear)obj);

      throw new ArgumentException("object is not a ContractMonthYear");
    }

    /// <summary>
    /// IComparable.CompareTo implementation.
    /// </summary>
    public int CompareTo(ContractMonthYear obj)
    {
      return Cmp(this, obj);
    }
  }
}