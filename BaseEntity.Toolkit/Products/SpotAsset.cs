using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  #region Asset Spot Contract
  /// <summary>
  /// Asset spot contract. 
  /// This product is just a place holder for spot value in CalibratedCurve tenors. 
  /// </summary>
  [Serializable]
  [ReadOnly(true)]
  internal sealed class SpotAsset : Product
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="spot">ISpot input, stored by reference</param>
    public SpotAsset(ISpot spot)
      : base(spot.Spot, spot.Spot, spot.Ccy)
    {
      _spot = spot;
    }

    #region Properties
    /// <summary>
    /// Spot price
    /// </summary>
    public double SpotPrice
    {
      get { return _spot.Value; }
      set { _spot.Value = value;  }
    }
    
    /// <summary>
    /// Spot date
    /// </summary>
    public Dt Spot
    {
      get { return _spot.Spot; }
    }

    #endregion

    private readonly ISpot _spot;
  }
  #endregion
}
