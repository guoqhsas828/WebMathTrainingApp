/*
 * SimulatedPath.PartialProxy.cs
 *
 * Copyright (c)   2002-2010. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Simulated path class
  /// </summary>
  partial class SimulatedPath
  {
    #region Properties

    /// <summary>
    /// Dimensionality of driving factors
    /// </summary>
    public int Dim
    {
      get { return GetDim(); }
    }


    /// <summary>
    /// Path weight 
    /// </summary>
    public double Weight
    {
      get { return GetWeight(); }
    }

    /// <summary>
    /// Unique id of the path
    /// </summary>
    public int Id
    {
      get { return GetIndex(); }
      set { SetIndex(value); }
    }

    #endregion
  }
}