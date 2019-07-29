/*
 * 
 *
 */

using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows.Expressions;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  ///  Represents a set of parameters required to calculate
  ///  the forward Pv of cash flows on a single exposure date.
  /// </summary>
  public class PvParameter : IEnumerable<Evaluable>
  {
    /// <summary>
    ///  The index of the first forward payment in the array of payments,
    ///  or -1 if not applicable 
    /// </summary>
    public readonly int StartIndex;

    /// <summary>
    ///   Discount factor on the exposure date
    /// </summary>
    public readonly Evaluable Discount;

    /// <summary>
    ///   The exposure date
    /// </summary>
    public Dt Date => GetDiscountDate(Discount);

    /// <summary>
    ///   The discount factor on the date
    /// </summary>
    public double DiscountFactor => Discount.Evaluate();

    private static Dt GetDiscountDate(Evaluable discount)
    {
      var scaled = discount as Scaled;
      if (scaled != null)
      {
        discount = scaled.Node;
      }
      return ((IDatedValue)discount).Date;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    internal PvParameter(int startIndex, Evaluable discount)
    {
      StartIndex = startIndex;
      Discount = discount;
    }

    IEnumerator<Evaluable> IEnumerable<Evaluable>.GetEnumerator()
    {
      yield return Discount;
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      yield return Discount;
    }
  }
}
