/*
 * MultiNormalRng.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using log4net;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Numerics.Rng
{
  ///
  /// <summary>
  ///   Pseudo-random number generator
  /// </summary>
  ///
  /// <remarks>
  ///   This is a class to generate deviates with multivariate
  ///   normal distributions.
  /// </remarks>
  ///
  [Serializable]
  public class MultiNormalRng : RandomNumberGenerator
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (MultiNormalRng));

    #region Constructors

    /// <summary>
    ///   Default constructor
    /// </summary>
    protected MultiNormalRng()
    {
      R_ = new double[1,1];
      R_[0, 0] = 1.0;
      z_ = new double[1];
      lowTriag_ = false;
    }

    /// <summary>
    ///   Constructor with a correlation matrix
    /// </summary>
    /// <param name="correlation">correlation matrix</param>
    public MultiNormalRng(double[,] correlation)
    {
      Decompose(correlation);
    }

    /// <summary>
    ///   Constructor with pairwise correlation stored in an array
    /// </summary>
    /// <param name="correlation">correlation matrix</param>
    /// <param name="dim">dimension</param>
    public MultiNormalRng(int dim, double[] correlation)
    {
      Decompose(dim, correlation);
    }

    /// <summary>
    ///   Constructor with a correlation matrix
    /// </summary>
    /// <param name="rng">core generator</param>
    /// <param name="correlation">correlation matrix</param>
    public MultiNormalRng(RandomNumberGenerator rng, double[,] correlation) : base(rng)
    {
      Decompose(correlation);
    }

    /// <summary>
    ///   Constructor with correlation stored in an array
    /// </summary>
    /// <param name="rng">core generator</param>
    /// <param name="dim">dimension</param>
    /// <param name="correlation">correlation matrix</param>
    public MultiNormalRng(RandomNumberGenerator rng, int dim, double[] correlation) : base(rng)
    {
      Decompose(dim, correlation);
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      MultiNormalRng obj = (MultiNormalRng) base.Clone();

      int N = R_.GetLength(0);
      double[,] R = new double[N,N];
      double[] z = new double[N];
      for (int i = 0; i < N; ++i)
      {
        z[i] = z_[i];
        for (int j = 0; j < N; ++j) R[i, j] = R_[i, j];
      }
      obj.R_ = R;
      obj.z_ = z;

      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///    Decompose correlation matrix
    /// </summary>
    private void Decompose(double[,] correlation)
    {
      int N = correlation.GetLength(0);
      if (N <= 0) throw new ArgumentOutOfRangeException("N", "zero length correlation not allowed");
      if (N != correlation.GetLength(1))
        throw new ArgumentException(String.Format("correlation ({0}x{1}) matrix not squared", correlation.GetLength(0),
                                                  correlation.GetLength(1)));
      R_ = new double[N,N];
      for (int i = 0; i < N; ++i) for (int j = 0; j < N; ++j) R_[i, j] = correlation[i, j];

      Decompose();
      return;
    }

    /// <summary>
    ///    Decompose correlation matrix
    /// </summary>
    private void Decompose(int N, double[] correlation)
    {
      if (N <= 0) throw new ArgumentOutOfRangeException("N", "zero length correlation not allowed");
      if (N*N > correlation.Length)
        throw new ArgumentException(String.Format("correlation (length:{0}) not support size ({1}x{1})",
                                                  correlation.Length, N));
      R_ = new double[N,N];
      for (int i = 0; i < N; ++i) for (int j = 0; j < N; ++j) R_[i, j] = correlation[i + j*N];

      Decompose();
      return;
    }

    /// <summary>
    ///    Decompose correlation matrix
    /// </summary>
    private void Decompose()
    {
      z_ = new double[R_.GetLength(0)];

      try
      {
        MatrixOfDoubles R = new MatrixOfDoubles(R_);
        LinearAlgebra.Cholesky(R);
        for (int i = 0; i < R_.GetLength(0); ++i) for (int j = 0; j < R_.GetLength(1); ++j) R_[i, j] = R.at(i, j);
        lowTriag_ = true;
        return;
      }
      catch (Exception)
      {
        // Cholesky failed, try eigenvalue decomposition
        int N = R_.GetLength(0);
        double[] d = new double[N];
        MatrixOfDoubles R = new MatrixOfDoubles(R_);
        LinearAlgebra.SymmetricEVD(R, d, 1.0E-15);
        for (int i = 0; i < R_.GetLength(0); ++i) for (int j = 0; j < R_.GetLength(1); ++j) R_[i, j] = R.at(i, j);
        for (int i = 0; i < N; ++i)
        {
          for (int j = 0; j < N; ++j) R_[i, j] *= (d[j] <= 2.0E-16 ? 0.0 : Math.Sqrt(d[j]));
        }
        lowTriag_ = false;
      }
    }

    /// <summary>Generate an array of variates with standard normal distribution</summary>
    /// <param name="x">Array to received generated random numbers</param>
    /// <exclude />
    public void Draw(double[] x)
    {
      int N = z_.Length;
      if (x.Length < z_.Length) throw new ArgumentException(String.Format("length of x not match generator dimension {1}", x, z_.Length));

      base.StdNormal(z_);

      for (int i = 0; i < N; ++i)
      {
        double ri = 0;
        if (lowTriag_)
        {
          for (int j = 0; j <= i; ++j) ri += R_[i, j]*z_[j];
        }
        else
        {
          for (int j = 0; j < N; ++j) ri += R_[i, j]*z_[j];
        }
        x[i] = ri;
      }

      return;
    }

    #endregion Methods

    #region Properties

    #endregion Properties

    #region Data

    private double[,] R_; // decomposed correlation matrix
    private bool lowTriag_;
    private double[] z_;

    #endregion Data
  }
}
