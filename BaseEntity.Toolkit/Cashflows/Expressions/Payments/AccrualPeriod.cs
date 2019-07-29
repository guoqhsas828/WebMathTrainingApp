using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows.Expressions.Utilities;

namespace BaseEntity.Toolkit.Cashflows.Expressions.Payments
{
  [DebuggerDisplay("{DebugDisplay}")]
  internal sealed class AccrualPeriod : IStructuralEquatable, IDebugDisplay
  {
    internal readonly Dt Begin, End;
    internal readonly DayCount DayCount;
    internal readonly double Fraction;

    public static AccrualPeriod Create(Dt begin, Dt end, DayCount dc)
    {
      return Evaluable.Unique(new AccrualPeriod(begin, end, dc));
    }

    private AccrualPeriod(Dt begin, Dt end, DayCount dc)
    {
      Begin = begin;
      End = end;
      DayCount = dc;
      Fraction = Dt.Fraction(begin, end, dc);
    }

    internal double Diff()
    {
      return Dt.Diff(Begin, End, DayCount);
    }

    #region IStructuralEquatable Members

    public bool Equals(object other, IEqualityComparer comparer)
    {
      var period = other as AccrualPeriod;
      return period != null && period.Begin == Begin && period.End == End
        && period.DayCount == DayCount;
    }

    public int GetHashCode(IEqualityComparer comparer)
    {
      return HashCodeCombiner.Combine(Begin.GetHashCode(), End.GetHashCode(), (int)DayCount);
    }

    #endregion

    #region IDebugDisplay members

    public string DebugDisplay
    {
      get
      {
        return string.Format("Period({0}, {1}, {2})",
          Begin, End, DayCount);
      }
    }

    #endregion
  }

}
