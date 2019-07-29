/*
 * RealMatrix.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///   Real matrix supporting simple matrix operations.
  /// </summary>
  public class RealMatrix : BaseEntityObject
  {
    #region Constructors
    /// <summary>
    /// Initializes a new instance of the <see cref="RealMatrix"/> struct.
    /// </summary>
    /// <param name="nrow">The number of rows.</param>
    /// <param name="ncol">The number of columns.</param>
    /// <param name="data">The data of matrix elements.</param>
    private RealMatrix(int nrow, int ncol, double[] data)
    {
      if (nrow < 0 || ncol < 0)
      {
        throw new ArgumentException("Dimensions cannot be negative.");
      }
      if (nrow == 0 || ncol == 0)
      {
        data_ = emptyMatrix_;
      }
      else if (data == null)
      {
        data_ = new double[nrow * ncol];
      }
      else if (data.Length != nrow * ncol)
      {
        throw new ArgumentException("Data size and matrix dimensions not match.");
      }
      else
      {
        data_ = data;
      }
      nrow_ = nrow;
      ncol_ = ncol;
      return;
    }

    /// <summary>
    /// Wraps the specified data as a matrix.
    /// </summary>
    /// <remarks>
    /// Any modification of the resulting matrix is made on
    /// the original data.
    /// </remarks>
    /// <param name="data">The data of matrix elements.</param>
    /// <param name="nrow">The number of rows.</param>
    /// <param name="ncol">The number columns.</param>
    /// <returns>The matrix wrapping the data</returns>
    public static RealMatrix Wrap(double[] data, int nrow, int ncol)
    {
      return new RealMatrix(nrow, ncol, data);
    }

    /// <summary>
    /// Return a new object that is a deep copy of this instance
    /// </summary>
    /// <returns></returns>
    /// <remarks>
    /// This method will respect object relationships (for example, component references
    /// are deep copied, while entity associations are shallow copied (unless the caller
    /// manages the lifecycle of the referenced object).
    /// </remarks>
    public override object Clone()
    {
      return new RealMatrix(nrow_, ncol_, CloneUtil.Clone(data_));
    }
    #endregion

    #region Operations
    /// <summary>
    /// Inject data into the matrix.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>This instance.</returns>
    public RealMatrix Inject(double[] data)
    {
      if (data_.Length != data.Length)
        throw new ArgumentException("Dimensions not match.");
      Array.Copy(data, data_, data_.Length);
      return this;
    }

    /// <summary>
    /// Adds matrix to another one and return this instance.
    /// </summary>
    /// <param name="m">The matrix to add from.</param>
    /// <returns>This instance.</returns>
    public RealMatrix AddTo(RealMatrix m)
    {
      var x = m.data_;
      if (data_.Length != x.Length)
        throw new ArgumentException("Dimensions not match.");
      for (int i = 0; i < x.Length; ++i)
        data_[i] += x[i];
      return this;
    }

    /// <summary>
    /// Implements the operator +.
    /// </summary>
    /// <param name="m1">The first matrix.</param>
    /// <param name="m2">The second matrix.</param>
    /// <returns>The result of the operator.</returns>
    public static RealMatrix operator +(RealMatrix m1, RealMatrix m2)
    {
      if (m1.RowCount != m2.RowCount)
        throw new ArgumentException("Rows not match.");
      if (m1.ColumnCount != m2.ColumnCount)
        throw new ArgumentException("Rows not match.");
      int n = m1.data_.Length;
      var r = new double[n];
      for (int i = 0; i < n; ++i)
      {
        r[i] = m1.data_[i] + m2.data_[i];
      }
      return new RealMatrix(m1.nrow_, m1.ncol_, r);
    }

    /// <summary>
    /// Implements the operator -.
    /// </summary>
    /// <param name="m1">The first matrix.</param>
    /// <param name="m2">The second matrix.</param>
    /// <returns>The result of the operator.</returns>
    public static RealMatrix operator -(RealMatrix m1, RealMatrix m2)
    {
      if (m1.RowCount != m2.RowCount)
        throw new ArgumentException("Rows not match.");
      if (m1.ColumnCount != m2.ColumnCount)
        throw new ArgumentException("Rows not match.");
      int n = m1.data_.Length;
      var r = new double[n];
      for (int i = 0; i < n; ++i)
      {
        r[i] = m1.data_[i] - m2.data_[i];
      }
      return new RealMatrix(m1.nrow_, m1.ncol_, r);
    }
    /// <summary>
    /// Implements the operator *.
    /// </summary>
    /// <param name="m1">The first matrix.</param>
    /// <param name="m2">The second matrix.</param>
    /// <returns>The result of the operator.</returns>
    public static RealMatrix operator *(RealMatrix m1, RealMatrix m2)
    {
      if (m1.ColumnCount != m2.RowCount)
        throw new ArgumentException("Matrices not match.");
      int nsum = m2.RowCount;
      int ncol = m2.ColumnCount;
      int nrow = m1.RowCount;
      var r = new RealMatrix(nrow, ncol, null);
      for (int i = 0; i < nrow; ++i)
      {
        for (int j = 0; j < ncol; ++j)
        {
          double sum = 0;
          for (int k = 0; k < nsum; ++k)
            sum += m1[i, k] * m2[k, j];
          r[i, j] = sum;
        }
      }
      return r;
    }

    /// <summary>
    /// Implements the operator *.
    /// </summary>
    /// <param name="m">The first matrix.</param>
    /// <param name="x">The Columen vector.</param>
    /// <returns>The result of the operator.</returns>
    public static RealMatrix operator *(RealMatrix m, double[] x)
    {
      return m * new RealMatrix(x.Length, 1, x);
    }

    /// <summary>
    /// Implements the operator *.
    /// </summary>
    /// <param name="x">The row vector.</param>
    /// <param name="m">The second matrix.</param>
    /// <returns>The result of the operator.</returns>
    public static RealMatrix operator *(double[] x, RealMatrix m)
    {
      return new RealMatrix(1, x.Length, x) * m;
    }

    /// <summary>
    /// Implements the operator *.
    /// </summary>
    /// <param name="m">The first matrix.</param>
    /// <param name="x">The scaler.</param>
    /// <returns>The result of the operator.</returns>
    public static RealMatrix operator *(RealMatrix m, double x)
    {
      int n = m.data_.Length;
      double[] r = new double[n];
      for (int i = 0; i < n; ++i)
        r[i] = m.data_[i] * x;
      return new RealMatrix(m.nrow_, m.ncol_, r);
    }

    /// <summary>
    /// Implements the operator *.
    /// </summary>
    /// <param name="x">The scaler.</param>
    /// <param name="m">The second matrix.</param>
    /// <returns>The result of the operator.</returns>
    public static RealMatrix operator *(double x, RealMatrix m)
    {
      return m * x;
    }

    /// <summary>
    /// The matrix data viewed as a one dimensional array.
    /// </summary>
    /// <remarks>
    /// Any change made to the returned array
    /// affects the original matrix.
    /// </remarks>
    /// <returns>One dimensional array</returns>
    public double[] AsArray()
    {
      return data_;
    }

    #endregion

    #region Views, Properties and Data
    /// <summary>
    /// Gets or sets the element at (i,j).
    /// </summary>
    public double this[int i, int j]
    {
      get { return data_[i * ncol_ + j]; }
      internal set { data_[i * ncol_ + j] = value; }
    }

    /// <summary>
    /// Gets the row count.
    /// </summary>
    /// <value>The row count.</value>
    public int RowCount
    {
      get { return nrow_; }
    }

    /// <summary>
    /// Gets the column count.
    /// </summary>
    /// <value>The column count.</value>
    public int ColumnCount
    {
      get { return ncol_; }
    }

    private readonly int nrow_, ncol_;
    private readonly double[] data_;
    private static readonly double[] emptyMatrix_ = new double[0];
    #endregion

    #region Static Methods
    /// <summary>
    ///  Performs decomposition of a positive semidefinite symmetric matrix.
    /// </summary>
    /// <param name="symmetricMatrix">The symmetric matrix.</param>
    /// <returns>True if returns a lower triangular matrix.</returns>
    public static bool SymmetricDecompose(RealMatrix symmetricMatrix)
    {
      int n = symmetricMatrix.RowCount;
      if(n!=symmetricMatrix.ColumnCount)
      {
        throw new ArgumentException("symmetricMatrix is not a square matrix.");
      }
      RealMatrix R_ = symmetricMatrix;
      try
      {
        double[] d = new double[n];
        for (int i = 0; i < n; ++i)
        {
          double di = d[i] = Math.Sqrt(R_[i, i]);
          R_[i, i] = 1;
          for (int j = 0; j < i; ++j)
            R_[i, j] = (R_[j, i] /= di*d[j]);
        }
        MatrixOfDoubles R = new MatrixOfDoubles(n, n, R_.AsArray());
        LinearAlgebra.Cholesky(R);
        for (int i = 0; i < n; ++i)
          for (int j = 0; j < n; ++j)
            R_[i, j] = d[i]*R.at(i, j);
        return true;
      }
      catch (Exception)
      {
        // Cholesky failed, try eigenvalue decomposition
        double[] d = new double[n];
        MatrixOfDoubles R = new MatrixOfDoubles(n, n, R_.AsArray());
        LinearAlgebra.SymmetricEVD(R, d, 1.0E-15);
        for (int i = 0; i < n; ++i)
          for (int j = 0; j < n; ++j)
            R_[i, j] = R.at(i, j);
        for (int i = 0; i < n; ++i)
        {
          for (int j = 0; j < n; ++j)
            R_[i, j] *= (d[j] <= 2.0E-16 ? 0.0 : Math.Sqrt(d[j]));
        }
      }
      return false;
    }
    #endregion
  } // class RealMatrix
}
