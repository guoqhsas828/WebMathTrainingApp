/*
 * BgmCorrelation.cs
 *
 * Copyright (c)   2005-2011. All rights reserved.
 * 
 */
using System;
using System.Runtime.Serialization;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Models.BGM
{
  /// <summary>
  /// Bgm correlation enum type
  /// </summary>
  public enum BgmCorrelationType
  {
    /// <summary>Zero correlations</summary>
    ZeroCorrelation = 0,
    /// <summary>Perfect correlations</summary>
    PerfectCorrelation = 1,
    /// <summary>A full matrix of correlations</summary>
    CorrelationMatrix = 2,
    /// <summary>Schoenmakersand Coffey two parameters form</summary>
    SchoenmakersCoffey2Params = 3,
    /// <summary>Schoenmakersand Coffey three parameters form</summary>
    SchoenmakersCoffey3Params = 4,
    /// <summary>Rebonato three parameters form</summary>
    Rebonato3Params = 5,
    /// <summary>Factor correlation</summary>
    FactorCorrelation = 6
  }

  /// <summary>
  ///   Object representing correlations among underlying libor rates.
  /// </summary>
  [Serializable]
  public partial class BgmCorrelation : INativeSerializable
  {
    #region Methods
    /// <summary>
    /// Creates the BGM correlation.
    /// </summary>
    /// <param name="type">BgmCorrelationType enum type</param>
    /// <param name="dimension">Number of alive libor rates</param>
    /// <param name="data">Correlation data to calibrate from if we choose a parametric form</param>
    /// <returns>Correlation among libor rates</returns>
    public static BgmCorrelation CreateBgmCorrelation(
      BgmCorrelationType type, int dimension, double[,] data)
    {
      var corr = createCorrelation(dimension, (int) type, data);
      var retVal = new BgmCorrelation(getCPtr(corr).Handle, true);
      retVal.type_ = type;
      retVal.dim_ = dimension;
      retVal.data_ = data;
      return retVal;
    }

    /// <summary>
    /// Reduces the rank of the correlation matrix.
    /// </summary>
    /// <param name="rank">Target rank.</param>
    /// <returns>A new correlation with the reduced rank.</returns>
    public BgmCorrelation ReduceRank(int rank)
    {
      var corr = reduceRank(this, rank);
      var retVal = new BgmCorrelation(getCPtr(corr).Handle, true);
      retVal.type_ = BgmCorrelationType.FactorCorrelation;
      rank = retVal.Rank;
      int dim = retVal.Dimension;
      var data = new double[dim,rank];
      for (int i = -1; ++i < dim;)
      {
        for (int j = -1; ++j < rank;)
          data[i, j] = retVal.FactorAt(i, j);
      }
      retVal.data_ = data;
      return retVal;
    }

    /// <summary>
    /// Gets the correlation matrix.
    /// </summary>
    /// <returns>The correlation matrix as a two-dimensional array.</returns>
    public double[,] GetCorrelationMatrix()
    {
      int n = Dimension;
      var data = new double[n,n];
      for (int i = 0; i < n; ++i)
        for (int j = 0; j < n; ++j)
          data[i, j] = this[i, j];
      return data;
    }

    /// <summary>
    /// Gets the correlation of rate <c>i</c> and <c>j</c>.
    /// </summary>
    public double this[int i, int j]
    {
      get { return at(i, j); }
    }

    /// <summary>
    /// Return factor loadings for the ith libor rate
    /// </summary>
    /// <param name="i">Libor rate index</param>
    /// <param name="j">Factor index</param>
    /// <returns>Factor loadings</returns>
    public double FactorAt(int i, int j)
    {
      if(j >= rank() || i >= dim())
        return 0.0;
      return factorAt(i, j);
    }

    /// <summary>
    /// Gets the dimension of the correlation (number of rates).
    /// </summary>
    public int Dimension
    {
      get { return dim(); }
    }

    /// <summary>
    /// Rank of the matrix
    /// </summary>
    public int Rank
    {
      get { return rank(); }
    }

    
#if DEBUG
    /// <summary>
    /// Gets the correlation matrix as two dimensional array
    /// (For debug purpose only).
    /// </summary>
    /// <value>The correlation matrix.</value>
    public double[,] CorrelationMatrix
    {
      get { return GetCorrelationMatrix(); }
    }
#endif
    #endregion

    #region Data
    private BgmCorrelationType type_;
    private int dim_;
    private double[,] data_;
    #endregion

    #region ISerializable Members
    BgmCorrelation(SerializationInfo info, StreamingContext context)
    {
      if (BaseEntityPINVOKE.SWIGPendingException.Pending)
        throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();

      type_ = (BgmCorrelationType) info.GetInt32("type_");
      dim_ = info.GetInt32("dim_");
      data_ = (double[,]) info.GetValue("data_", typeof (double[,]));
      var corr = createCorrelation(dim_, (int) type_, data_);
      swigCPtr = corr.swigCPtr;
      swigCMemOwn = true;
    }

    void  ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    {
      info.AddValue("type_", (int)type_);
      info.AddValue("dim_", dim_);
      info.AddValue("data_", data_);
    }
    #endregion
  }
}
