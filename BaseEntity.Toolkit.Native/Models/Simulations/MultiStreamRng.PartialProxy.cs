/*
 * MultiStreamRng.PartialProxy.cs
 *
 * Copyright (c)   2002-2010. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BaseEntity.Toolkit.Models.Simulations
{

  #region MultiStreamRng

  /// <summary>
  /// Rng with multi-thread capability. 
  /// This is only a wrapper for native random number generator.
  /// No methods should be exported.  
  /// </summary>
  partial class MultiStreamRng
  {
    #region Properties

    ///<summary>
    /// Type of rng
    ///</summary>
    public MultiStreamRng.Type RngType { get; set; }

    #endregion

    #region Constructors
    /// <summary>
    /// Static constructor
    /// </summary>
    /// <param name="type">Random number generator type</param>
    /// <param name="factorCount">Dimension of driving Brownian vector</param>
    /// <param name="dates">Partition (used in Brownian bridge construction of Brownian path)</param>
    /// <returns>Generator</returns>
    public static MultiStreamRng Create(Type type,
      int factorCount, IReadOnlyList<double> dates)
    {
      if (type == Type.None)
        return null;

      int count = dates.Count;
      if (count <= 0)
        throw new ArgumentException("Simulation dates cannot be empty");
      double horizon = dates[count - 1];
      var partition = new double[count];
      for (int i = 0; i < count; ++i)
        partition[i] = dates[i] / horizon;

      var native = get((int)type, factorCount, partition);
      if (native == null) return null;

      Debug.Assert(native.swigCMemOwn == false);
      return new MultiStreamRng(getCPtr(native).Handle, true) { RngType = type };
    }

    /// <summary>
    /// Static constructor of "projective" generator based on Gaussian quadrature points
    /// </summary>
    /// <param name="quadRule">Number of quadrature points</param>
    /// <param name="factorCount">Dimension of driving Brownian vector</param>
    /// <param name="dates">Partition (used in Brownian bridge construction of Brownian path)</param>
    /// <returns>Generator</returns>
    public static MultiStreamRng Create(int quadRule,
      int factorCount, IReadOnlyList<double> dates)
    {
      int count = dates.Count;
      if (count <= 0)
        throw new ArgumentException("Simulation dates cannot be empty");
      double horizon = dates[count - 1];
      var partition = new double[count];
      for (int i = 0; i < count; ++i)
        partition[i] = dates[i] / horizon;

      var native = get((int)Type.Projective, factorCount, partition, quadRule);
      if (native == null) return null;

      Debug.Assert(native.swigCMemOwn == false);
      return new MultiStreamRng(getCPtr(native).Handle, true) { RngType = Type.Projective };
    }

    /// <summary>
    /// Clone current MultiStreamRng
    /// </summary>
    /// <returns>Deep copy</returns>
    public MultiStreamRng Clone()
    {
      MultiStreamRng native;
      if ((native = clone()) == null)
        return null;
      Debug.Assert(native.swigCMemOwn);
      native.RngType = RngType;
      return native;
    }

    #endregion

    #region Methods
    /// <summary>
    /// Draw an array of uniform in the random series starting from idx
    /// </summary>
    /// <param name="idx">Index in pseudo/quasi random numbers sequence</param>
    /// <param name="workspace">Workspace</param>
    public void Uniform(int idx, double[] workspace)
    {
      DrawUniform(idx, workspace);
    }
    #endregion
  }

  #endregion
}