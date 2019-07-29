// 
//  -2015. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Swap contract composed of multiple swap legs. Could be floating, fixed or any combination thereof. 
  /// </summary>
  /// <remarks>
  ///   <para>An interest rate swap is an OTC contract between two counterparties to exchange a set
  ///   of cashflows over a defined period of time. Typically one 'leg' of the swap is a fixed
  ///   rate and the other is some variable rate relative to a floating interest rate such as LIBOR
  ///   or a currency. </para>
  /// </remarks>
  [Serializable]
  public class MultiLeggedSwap : Product
  {
    private IDictionary<string, SwapLeg> _swapLegs = new Dictionary<string, SwapLeg>(2);

    #region Constructors

    /// <summary>
    /// Swap constructor from multiple swap legs
    /// </summary>
    /// <param name="legs"></param>
    public MultiLeggedSwap(IList<SwapLeg> legs)
      : base(legs.Min(l => l.Effective),
        legs.Max(l => l.Maturity),
        legs.Select(l => l.Ccy).Distinct().Count() == 1 ? legs[0].Ccy : Currency.None)
    {
      int i = 0;
      foreach (var l in legs)
      {
        var id = string.Format("Leg{0}", i++);
        _swapLegs[id] = l;
      }
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var obj = (MultiLeggedSwap)base.Clone();
      obj._swapLegs = _swapLegs.ToDictionary(kvp => kvp.Key, kvp => (SwapLeg)kvp.Value.Clone());
      return obj;
    }

    /// <summary>
    /// Get Swap leg with key value
    /// </summary>
    /// <param name="key">Key value, default is string.Format("Leg{0}", i)</param>
    /// <returns></returns>
    public SwapLeg GetLeg(string key)
    {
      SwapLeg swapLeg = null;
      _swapLegs.TryGetValue(key, out swapLeg);
      return swapLeg;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Amortization schedule
    /// </summary>
    public IList<Amortization> AmortizationSchedule
    {
      set
      {
        foreach (var a in value)
        {
          foreach (var sl in _swapLegs.Values)
            sl.AmortizationSchedule.Add(a);
        }
      }
    }

    #endregion Properties
  }

}
