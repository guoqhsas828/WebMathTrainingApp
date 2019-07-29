using System;
using System.Runtime.InteropServices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;
namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  /// Linear solver for 
  /// </summary>

  public class LinearSolvers
  {
    [DllImport("BaseEntityNative", CallingConvention=CallingConvention.Cdecl)]
    private unsafe static extern void factorize_LU(double* A, int rows, int cols, int* piv);

    [DllImport("BaseEntityNative", CallingConvention = CallingConvention.Cdecl)]
    private unsafe static extern bool solve_LU(double* A,  int* pivots, double* b, int n, double* x);

    [DllImport("BaseEntityNative", CallingConvention = CallingConvention.Cdecl)]
    private unsafe static extern bool solve_Frwd(double* A, double* b, int n, double* x);

    [DllImport("BaseEntityNative", CallingConvention = CallingConvention.Cdecl)]
    private unsafe static extern bool solve_Bkwd(double* A, double* b, int n, double* x);

    [DllImport("BaseEntityNative", CallingConvention = CallingConvention.Cdecl)]
    private unsafe static extern void factorize_SVD(double* A, int m, int n, double* w, double* V);

    [DllImport("BaseEntityNative", CallingConvention = CallingConvention.Cdecl)]
    private unsafe static extern void solve_SVD(double* U, int m, int n, double* w, double* V,
                                           double* b, double* x, double cutoff);

    [DllImport("BaseEntityNative", CallingConvention = CallingConvention.Cdecl)]
    private unsafe static extern void project_SVD(double* U, int m, int n, double* w, double* V,
                                           double* x, double* y);

    /// <summary>
    /// Factorize matrix a into l u, where l is lower triangular and u is upper triangular
    /// </summary>
    /// <param name="A">nxm matrix</param>
    /// <param name="pivots"> Records the permutations used by the partial pivoting algorithm.</param> 
    public static void FactorizeLU(double[,] A, int[] pivots)
    {
      unsafe
      {
        fixed (double* pA = A)
        {
          fixed (int* pp = pivots)
          {
            factorize_LU(pA, A.GetLength(0), A.GetLength(1), pp);
          }
        }
      }
      return;
    }

    /// <summary>
    /// Solve a linear system by LU decomposition. This only works for square matrices
    /// </summary>
    /// <param name="A">Factorized matrix</param>
    /// <param name="pivots">Index of pivots elements</param> 
    /// <param name="b">Right hand side</param>
   /// <param name="x">Overwritten by the solution</param>
    /// <returns>True if system is not singular</returns>
    public static void SolveLU(double[,] A, int[] pivots, double[] b, double[] x)
    {
      if(A.GetLength(0)!=A.GetLength(1))
        throw new ArgumentException("This routine can only be used to solve square systems");
      unsafe
      {
        fixed (double* pA = A, pb = b, px = x)
        {
          fixed (int* pp = pivots)
          {
            bool solved = solve_LU(pA, pp, pb, b.Length, px);
            if (!solved)
              throw new ToolkitException("System is singular: solution not found");
          }
        }
      }
    }

    /// <summary>
    /// Solve a lower triangular system by forward substitution
    /// </summary>
    /// <param name="A">Lower triangular matrix</param>
    /// <param name="b">Right hand side</param>
    /// <param name="x">Overwritten by the solution</param>
    public static void SolveForward(double[,] A, double[] b, double[] x)
    {
      unsafe
      {
        fixed (double* pA = A, pb = b, px = x)
        {
          bool solved = solve_Frwd(pA, pb, b.Length, px);
          if (!solved)
            throw new ToolkitException("System is singular: solution not found");
        }
      }

    }

    /// <summary>
    /// Solve an upper triangular system by backward substitution
    /// </summary>
    /// <param name="A">Upper triangular matrix</param>
    /// <param name="b">Right hand side</param>
    /// <param name="x">Overwritten by the solution</param>
    public static void SolveBackward(double[,] A, double[] b, double[] x)
    {
      unsafe
      {
        fixed (double* pA = A, pb = b, px = x)
        {
          bool solved = solve_Bkwd(pA, pb, b.Length, px);
          if (!solved)
            throw new ToolkitException("System is singular: solution not found");
        }
      }
    }

    /// <summary>
    /// Returns SVD decomposition U W V' of a non square matrix. In input a is the starting matrix. 
    /// In ouptut a is the left unitary matrix in the svd decomposition. 
    /// </summary>
    /// <param name="A">m x n matrix. Overwritten by U</param>
    /// <param name="W">Overwritten by W</param>
    /// <param name="V">Overwritten by V</param>
    public static void FactorizeSVD(double[,] A, double[] W, double[,] V)
    {
      
      unsafe 
      {
        fixed(double* pA = A, pW = W, pV = V)
        {
          factorize_SVD(pA, A.GetLength(0), A.GetLength(1), pW, pV);
        }
      }
    }


    /// <summary>
    /// Solves in least squares sense the system Ax = b using SVD decomposition
    /// </summary>
    /// <param name="U">Left unitary matrix in SVD decomposition</param>
    /// <param name="W">Diagonal matrix of singular values</param>
    /// <param name="V">Right unitary matrix in SVD decomposition</param>
    /// <param name="b">Right hand side</param>
    /// <param name="x">Least Square Solution</param>
    public static void SolveSVD(double[,] U, double[] W, double[,] V, double[] b, double[] x)
    {

      unsafe
      {
        SolveSVD(U, W, V, b, x, 0.0);
      }
    }
    /// <summary>
    /// Solves in least squares sense the system Ax = b using SVD decomposition
    /// </summary>
    /// <param name="U">Left unitary matrix in SVD decomposition</param>
    /// <param name="W">Diagonal matrix of singular values</param>
    /// <param name="V">Right unitary matrix in SVD decomposition</param>
    /// <param name="b">Right hand side</param>
    /// <param name="x">Least Square Solution</param>
    /// <param name="cutoff">The cutoff for small SVD diagonal values</param>
    public static void SolveSVD(double[,] U, double[] W, double[,] V, double[] b, double[] x, double cutoff)
    {

      unsafe
      {
        fixed (double* pU = U, pW = W, pV = V, pb = b, px = x)
        {
          solve_SVD(pU, U.GetLength(0), U.GetLength(1), pW, pV, pb, px, cutoff);
        }
      }
    }
     /// <summary>
    /// L2 projection b on the subspace generated by UWV* 
    /// </summary>
    /// <param name="U">Left unitary matrix in SVD decomposition</param>
    /// <param name="W">Diagonal matrix of singular values</param>
    /// <param name="V">Right unitary matrix in SVD decomposition</param>
    /// <param name="b">Vector in <m>R^n</m></param>
    /// <param name="y">UU*b</param>

    public static void ProjectSVD(double[,] U, double[] W, double[,] V, double[] b, double[] y)
     {
       unsafe
       {
         fixed (double* pU = U, pW = W, pV = V, pb = b, py = y)
         {
           project_SVD(pU, U.GetLength(0), U.GetLength(1), pW, pV, pb, py);
         }
       }
     }
  }
}
