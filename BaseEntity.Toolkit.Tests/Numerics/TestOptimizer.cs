//
// Test of optimization routines
// Copyright (c)    2002-2018. All rights reserved.
//

using System;
using System.Collections.Generic;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Numerics;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Numerics
{
  /// <summary>
  ///  <list type="bullet"></list>
  /// </summary>
  [TestFixture]
  public class TestOptimizer 
  {
    #region Types
    [Flags]
    enum TestMethods
    {
      None = 0,
      Simplex = 1,
      BFGS = 2,
      BFGSB = 4,
      NLS = 8,
      Bounded = Simplex | BFGSB,
      All = Simplex | BFGS | BFGSB | NLS
    }
    interface IOptimizerTestFn
    {
      int DimensionX { get; }
      int DimensionY { get; }
      IList<double> Solution { get; }
      void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g);
    }
    #endregion

    #region Banana function

    [Test, Smoke, Ignore("Not stable")]
    public void Banana()
    {
      testFn(new BananaFn(), true, TestMethods.All);
    }

    class BananaFn : IOptimizerTestFn
    {
      public int DimensionX => 2;
      public int DimensionY => 1;

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        if (f.Count != 0)
        {
          double tmp1 = x[1] - x[0] * x[0];
          double tmp2 = (1 - x[0]);
          f[0] = 100.0 * tmp1 * tmp1 + tmp2 * tmp2 + 1.0;
        }
        if (g.Count != 0)
        {
          double tmp1 = x[1] - x[0] * x[0];
          g[0] = -400.0 * (tmp1) * x[0] - 2.0 * (1 - x[0]);
          g[1] = 200.0 * (tmp1);
        }
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 1.0, 1.0 };
    }
    #endregion

    #region Corner solutions
    [Test, Smoke]
    public void LowerBound()
    {
      testFn(new LowerBoundFn(), true, TestMethods.Bounded);
    }
    [Test, Smoke]
    public void UpperBound()
    {
      testFn(new LowerBoundFn(), true, TestMethods.Bounded);
    }

    class LowerBoundFn : IOptimizerTestFn
    {
      public int DimensionX => 2;

      public int DimensionY => 1;

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        if (f.Count != 0)
        {
          f[0] = (x[0] + x[1]);
        }
        if (g.Count != 0)
        {
          g[0] = 1.0;
          g[1] = 1.0;
        }
      }
      public IList<double> Solution => result_;
      private double[] result_ = { -10.0, -10.0 };
    }

    class UpperBoundFn : IOptimizerTestFn
    {
      public int DimensionX => 2;

      public int DimensionY => 1;

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        if (f.Count != 0)
        {
          f[0] = (-x[0] - x[1]);
        }
        if (g.Count != 0)
        {
          g[0] = -1.0;
          g[1] = -1.0;
        }
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 10.0, 10.0 };
    }
    #endregion

    #region Random function
    [Test, Smoke]
    public void Random()
    {
      testFn(new RandFn(), false,
        TestMethods.Simplex | TestMethods.BFGS | TestMethods.BFGSB);
    }

    // Test objective function
    class RandFn : IOptimizerTestFn
    {
      private const int seed = 7919; // a prime number
      private Random rng = new Random(seed);

      public int DimensionX => 2;

      public int DimensionY => 1;

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        if (f.Count != 0)
        {
          double num = (double)rng.Next();
          double den = (double)Int32.MaxValue;
          f[0] = num / den;
        }
        if (g.Count != 0)
        {
          for (int i = 0; i < DimensionX; ++i)
            g[i] = (double)rng.Next() / (double)Int32.MaxValue;
        }
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 0.0, 0.0 };
    }
    #endregion

    #region AlmostQuadratic
    [Test, Smoke, Ignore("Not stable")]
    public void AlmostQuadratic()
    {
      testFn(new AlmostQuadraticFn(), true, TestMethods.All);
    }

    // Test objective function
    class AlmostQuadraticFn : IOptimizerTestFn
    {
      public int DimensionX => 2;
      public int DimensionY => 1;

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        if (f.Count != 0)
        {
          double tmp1 = x[1] - x[0] * x[0];
          double tmp2 = (1 - x[0]);
          f[0] = 100.0 * tmp1 * tmp1 + tmp2 * tmp2 + 1.0 + 0.00000001 * Math.Sin(x[0] * x[1]);
        }
        if (g.Count != 0)
        {
          double tmp1 = x[1] - x[0] * x[0];
          g[0] = -400.0 * (tmp1) * x[0] - 2.0 * (1 - x[0]) + 0.00000001 * Math.Cos(x[0] * x[1]) * x[1];
          g[1] = 200.0 * (tmp1) + 0.00000001 * Math.Cos(x[0] * x[1]) * x[0];
        }
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 1.0, 1.0 };
    }
    #endregion

    #region Indefinite Quadratic
    [Test, Smoke]
    public void IndefiniteQuadratic()
    {
      testFn(new IndefiniteQuadraticFn(3), true, TestMethods.Bounded);
    }

    class IndefiniteQuadraticFn : IOptimizerTestFn
    {
      public IndefiniteQuadraticFn(int n)
      {
        C = new double[n, n];
        C[0, 0] = -1;
        for (int i = 1; i < n; i++)
          C[i, i] = 1;
      }
      public int DimensionX => 3;

      public int DimensionY => 1;

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        if (f.Count != 0)
        {
          int n = DimensionX;
          f[0] = 0.5 * QuadraticForm(x, C, x);
        }
        if (g.Count != 0)
        {
          int n = DimensionX;
          double[] Y = LeftMultiply(x, C);
          for (int i = 0; i < n; i++)
          {
            g[i] = Y[i];
          }
        }
        return;
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 10.0, 0.0, 0.0 };
      private double QuadraticForm(IReadOnlyList<double> x, double[,] A, IReadOnlyList<double> y)
      {
        int N = A.GetLength(0);

        double sum = 0.0;

        for (int i = 0; i < N; ++i)
        {
          for (int j = 0; j < N; ++j)
          {
            sum += y[i] * (A[i, j] + A[j, i]) * x[j];
          }
        }
        sum *= 0.5;

        return sum;
      }
      private double[] LeftMultiply(IReadOnlyList<double> x, double[,] A)
      {
        int n = A.GetLength(1);
        double[] y = new double[n];
        LeftMultiply(x, A, y);
        return y;
      }
      private void LeftMultiply(IReadOnlyList<double> x, double[,] A, double[] y)
      {
        int m = A.GetLength(0);
        int n = A.GetLength(1);

        for (int j = 0; j < n; ++j)
        {
          double sum = 0.0;
          for (int i = 0; i < m; i++)
          {
            sum += x[i] * A[i, j];
          }
          y[j] = sum;
        }
      }

      private double[,] C;
    }
    #endregion

    #region Rosenbeck
    [Test, Smoke]
    public void Rosenbeck()
    {
      testFn(new RosenbeckFn(4), true, TestMethods.NLS);
    }

    class RosenbeckFn : IOptimizerTestFn
    {
      public RosenbeckFn(int m)
      {
        DimensionY = m;
      }
      public int DimensionX => 2;

      public int DimensionY { get; }

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        const double ROSD = 105.0;
        int m = DimensionY;
        if (f.Count != 0)
        {
          for(int i =0;i<m;++i){
            f[i] = ((1.0-x[0])*(1.0-x[0]) + ROSD*(x[1]-x[0]*x[0])*(x[1]-x[0]*x[0]));
          }
        }
        if(g.Count!=0){
          for(int i=0,j=0;i<m;++i){
            g[j++]=(-2 + 2*x[0]-4*ROSD*(x[1]-x[0]*x[0])*x[0]);
            g[j++]=(2*ROSD*(x[1]-x[0]*x[0]));
          }
        }
        return;
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 1.0, 1.0 };
    }
    #endregion Rosenbeck

    #region ModifiedRosenbeck
    [Test, Smoke]
    public void ModifiedRosenbeck()
    {
      double[] lb = { 0.0, 0.0 };
      double[] ub = { 10.0, 10.0 };
      double[] x0 = { -1.2, 1.0 };
      testFn(new ModifiedRosenbeckFn(3), true,
        TestMethods.NLS, lb, ub, x0);
    }

    class ModifiedRosenbeckFn : IOptimizerTestFn
    {
      public ModifiedRosenbeckFn(int m)
      {
        DimensionY = m;
      }
      public int DimensionX => 2;

      public int DimensionY { get; }

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        int m = DimensionY;
        const double MODROSLAM = 1E02;
        if (f.Count != 0)
        {
          for (int i = 0; i < m; i += 3)
          {
            f[i] = 10 * (x[1] - x[0] * x[0]);
            f[i + 1] = 1.0 - x[0];
            f[i + 2] = MODROSLAM;
          }
        }

        if (g.Count != 0)
        {
          for (int i = 0, j = 0; i < m; i += 3)
          {
            g[j++] = -20.0 * x[0];
            g[j++] = 10.0;

            g[j++] = -1.0;
            g[j++] = 0.0;

            g[j++] = 0.0;
            g[j++] = 0.0;
          }

        }
        return;
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 1.0, 1.0 };
    }
    #endregion ModifiedRosenbeck

    #region Powells
    [Test, Smoke]
    public void Powells()
    {
      double[] lb = { 0.0, 0.0 };
      double[] ub = { 10.0, 10.0 };
      double[] x0 = { 3.0, 1.0 };
      testFn(new PowellsFn(2), true,
        TestMethods.NLS, lb, ub, x0);
    }
    class PowellsFn : IOptimizerTestFn
    {
      public PowellsFn(int m)
      {
        DimensionY = m;
      }
      public int DimensionX => 2;

      public int DimensionY { get; }

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        int m = DimensionY;
        if (f.Count != 0)
        {
          for (int i = 0; i < m; i += 2)
          {
            f[i] = x[0];
            f[i + 1] = 10.0 * x[0] / (x[0] + 0.1) + 2 * x[1] * x[1];
          }
        }

        if (g.Count != 0)
        {
          for (int i = 0, j = 0; i < m; i += 2)
          {
            g[j++] = 1.0;
            g[j++] = 0.0;

            g[j++] = 1.0 / ((x[0] + 0.1) * (x[0] + 0.1));
            g[j++] = 4.0 * x[1];
          }

        }
        return;
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 0.0, 0.0 };
    }
    #endregion Powells

    #region Woods
    [Test, Smoke]
    public void Woods()
    {
      double[] lb = { 0.0, 0.0, 0, 0 };
      double[] ub = { 10.0, 10.0, 10, 10 };
      double[] x0 = { -3.0, -1.0, -3, -1 };
      testFn(new WoodsFn(6), true,
        TestMethods.NLS, lb, ub, x0);
    }
    class WoodsFn : IOptimizerTestFn
    {
      public WoodsFn(int m)
      {
        DimensionY = m;
      }
      public int DimensionX => 4;

      public int DimensionY { get; }

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        int m = DimensionY;
        if (f.Count != 0)
        {
          for (int i = 0; i < m; i += 6)
          {
            f[i] = 10.0 * (x[1] - x[0] * x[0]);
            f[i + 1] = 1.0 - x[0];
            f[i + 2] = Math.Sqrt(90.0) * (x[3] - x[2] * x[2]);
            f[i + 3] = 1.0 - x[2];
            f[i + 4] = Math.Sqrt(10.0) * (x[1] + x[3] - 2.0);
            f[i + 5] = (x[1] - x[3]) / Math.Sqrt(10.0);
          }
        }

        if (g.Count != 0)
        {
          for (int i = 0, j = 0; i < m; i += 6)
          {
            g[j++] = -20 * x[0];
            g[j++] = 10.0;
            g[j++] = 0.0;
            g[j++] = 0.0;

            g[j++] = -1.0;
            g[j++] = 0.0;
            g[j++] = 0.0;
            g[j++] = 0.0;

            g[j++] = 0.0;
            g[j++] = 0.0;
            g[j++] = -2.0 * Math.Sqrt(90.0) * x[2];
            g[j++] = Math.Sqrt(90.0);

            g[j++] = 0.0;
            g[j++] = 0.0;
            g[j++] = -1.0;
            g[j++] = 0.0;

            g[j++] = 0.0;
            g[j++] = Math.Sqrt(10.0);
            g[j++] = 0.0;
            g[j++] = Math.Sqrt(10.0);

            g[j++] = 0.0;
            g[j++] = 1.0 / Math.Sqrt(10.0);
            g[j++] = 0.0;
            g[j++] = -1.0 / Math.Sqrt(10.0);
          }
        }
        return;
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 1.0, 1.0, 1, 1 };
    }
    #endregion Woods

    #region Meyers
    [Test, Smoke]
    public void Meyers()
    {
      const int m = 16;
      double[] lb = { 0.0, 0.0, 0};
      double[] ub = { 10.0, 10.0, 10 };
      double[] x0 = { 8.85, 4.0, 2.5 };
      testFn(new MeyersFn(m), true,
        TestMethods.NLS, lb, ub, x0);
    }
    class MeyersFn : IOptimizerTestFn
    {
      public MeyersFn(int m)
      {
        DimensionY = m;
        y = ArrayUtil.NewArray(Math.Max(m,16), -1.0);
        y[0] = 34.780; y[1] = 28.610; y[2] = 23.650; y[3] = 19.630;
        y[4] = 16.370; y[5] = 13.720; y[6] = 11.540; y[7] = 9.744;
        y[8] = 8.261; y[9] = 7.030; y[10] = 6.005; y[11] = 5.147;
        y[12] = 4.427; y[13] = 3.820; y[14] = 3.307; y[15] = 2.872;
      }
      public int DimensionX => 3;

      public int DimensionY { get; }

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        int m = DimensionY;
        if (f.Count != 0)
        {
          for (int i = 0; i < m; ++i)
          {
            double ui = 0.45 + 0.05 * i;
            f[i] = x[0] * Math.Exp(10.0 * x[1] / (ui + x[2]) - 13.0) - y[i];
          }
        }

        if (g.Count != 0)
        {
          for (int i = 0, j = 0; i < m; ++i)
          {
            double ui = 0.45 + 0.05 * i;
            double tmp = Math.Exp(10.0 * x[1] / (ui + x[2]) - 13.0);

            g[j++] = tmp;
            g[j++] = 10.0 * x[0] * tmp / (ui + x[2]);
            g[j++] = -10.0 * x[0] * x[1] * tmp / ((ui + x[2]) * (ui + x[2]));
          }
        }
        return;
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 2.481778, 6.181346, 3.502236 };
      private double[] y;
    }
    #endregion Meyers

    #region HelicalValley
    [Test, Smoke]
    public void HelicalValley()
    {
      double[] lb = { -10, -10, -10 };
      double[] ub = { 10.0, 10.0, 10 };
      double[] x0 = { -1, 0, 0 };
      testFn(new HelicalValleyFn(), true,
        TestMethods.NLS, lb, ub, x0);
    }
    class HelicalValleyFn : IOptimizerTestFn
    {
      public int DimensionX => 3;

      public int DimensionY => 3;

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        const double M_PI = 3.14159265358979323846;
        if (f.Count != 0)
        {
          double theta;

          if (x[0] < 0.0)
            theta = Math.Atan(x[1]/x[0])/(2.0*M_PI) + 0.5;
          else if (0.0 < x[0])
            theta = Math.Atan(x[1]/x[0])/(2.0*M_PI);
          else
            theta = (x[1] >= 0) ? 0.25 : -0.25;

          f[0] = 10.0*(x[2] - 10.0*theta);
          f[1] = 10.0*(Math.Sqrt(x[0]*x[0] + x[1]*x[1]) - 1.0);
          f[2] = x[2];
        }

        if (g.Count != 0)
        {
          int i = 0;
          double x0 = x[0], x1 = x[1];
          if ((x0 == 0.0) && (x1 == 0.0))
          {
            x0 = x[0] + 1E-6;
            x1 = x[1] + 1E-6;
          }

          double tmp = x0*x0 + x1*x1;

          g[i++] = 50.0*x1/(M_PI*tmp);
          g[i++] = -50.0*x0/(M_PI*tmp);
          g[i++] = 10.0;

          g[i++] = 10.0*x0/Math.Sqrt(tmp);
          g[i++] = 10.0*x1/Math.Sqrt(tmp);
          g[i++] = 0.0;

          g[i++] = 0.0;
          g[i++] = 0.0;
          g[i++] = 1.0;
        }
        return;
      }

      public IList<double> Solution => result_;
      private double[] result_ = { 1, 0, 0 };
    }
    #endregion HelicalValley

    #region HockSchittkowski01
    [Test, Smoke]
    public void HockSchittkowski01()
    {
      double[] lb = { -10.0, -1.5 };
      double[] ub = { 10.0, 10.0 };
      double[] x0 = { -2, 1 };
      testFn(new HockSchittkowski01Fn(), true,
        TestMethods.NLS, lb, ub, x0);
    }
    class HockSchittkowski01Fn : IOptimizerTestFn
    {
      public int DimensionX => 2;

      public int DimensionY => 2;

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        if (f.Count != 0)
        {
          double t = x[0] * x[0];
          f[0] = 10.0 * (x[1] - t);
          f[1] = 1.0 - x[0];
        }

        if (g.Count != 0)
        {
          int j = 0;

          g[j++] = -20.0 * x[0];
          g[j++] = 10.0;

          g[j++] = -1.0;
          g[j++] = 0.0;
        }
        return;
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 1, 1 };
    }
    #endregion HockSchittkowski01

    #region HockSchittkowski21
    [Test, Smoke]
    public void HockSchittkowski21()
    {
      double[] lb = { 2.0, -50 };
      double[] ub = { 50, 50 };
      double[] x0 = { -1, -1 };
      testFn(new HockSchittkowski21Fn(), true,
        TestMethods.NLS, lb, ub, x0);
    }
    class HockSchittkowski21Fn : IOptimizerTestFn
    {
      public int DimensionX => 2;

      public int DimensionY => 2;

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        if (f.Count != 0)
        {
          f[0] = x[0] / 10.0;
          f[1] = x[1];
        }

        if (g.Count != 0)
        {
          int j = 0;

          g[j++] = 0.1;
          g[j++] = 0.0;

          g[j++] = 0.0;
          g[j++] = 1.0;
        }
        return;
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 2, 0 };
    }
    #endregion HockSchittkowski21

    #region HATFLDB
    [Test, Smoke]
    public void HATFLDB()
    {
      double[] lb = { -50, -50, -50, -50 };
      double[] ub = { 50, 0.8, 50, 50 };
      double[] x0 = { 0.1, 0.1, 0.1, 0.1 };
      testFn(new HATFLDBFn(), true,
        TestMethods.NLS, lb, ub, x0);
    }
    class HATFLDBFn : IOptimizerTestFn
    {
      public int DimensionX => 4;

      public int DimensionY => 4;

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        if (f.Count != 0)
        {
          f[0] = x[0] - 1.0;

          for (int i = 1; i < 4; ++i)
            f[i] = x[i - 1] - Math.Sqrt(x[i]);
        }

        if (g.Count != 0)
        {
          int j = 0;

          g[j++] = 1.0;
          g[j++] = 0.0;
          g[j++] = 0.0;
          g[j++] = 0.0;

          g[j++] = 1.0;
          g[j++] = -0.5 / Math.Sqrt(x[1]);
          g[j++] = 0.0;
          g[j++] = 0.0;

          g[j++] = 0.0;
          g[j++] = 1.0;
          g[j++] = -0.5 / Math.Sqrt(x[2]);
          g[j++] = 0.0;

          g[j++] = 0.0;
          g[j++] = 0.0;
          g[j++] = 1.0;
          g[j++] = -0.5 / Math.Sqrt(x[3]);
        }
        return;
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 0.947214, 0.8, 0.64, 0.4096 };
    }
    #endregion HATFLDB

    #region HATFLDC
    [Test, Smoke]
    public void HATFLDC()
    {
      double[] lb = { -50, -50, -50, -50 };
      double[] ub = { 50, 50, 50, 50 };
      double[] x0 = { 0.9, 0.9, 0.9, 0.9 };
      testFn(new HATFLDCFn(), true,
        TestMethods.NLS, lb, ub, x0);
    }
    class HATFLDCFn : IOptimizerTestFn
    {
      public int DimensionX => 4;

      public int DimensionY => 4;

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        if (f.Count != 0)
        {
          f[0] = x[0] - 1.0;

          const int m = 4;
          for (int i = 1; i < m - 1; ++i)
            f[i] = x[i - 1] - Math.Sqrt(x[i]);

          f[m - 1] = x[m - 1] - 1.0;
        }

        if (g.Count != 0)
        {
          int j = 0;

          g[j++] = 1.0;
          g[j++] = 0.0;
          g[j++] = 0.0;
          g[j++] = 0.0;

          g[j++] = 1.0;
          g[j++] = -0.5 / Math.Sqrt(x[1]);
          g[j++] = 0.0;
          g[j++] = 0.0;

          g[j++] = 0.0;
          g[j++] = 1.0;
          g[j++] = -0.5 / Math.Sqrt(x[2]);
          g[j++] = 0.0;

          g[j++] = 0.0;
          g[j++] = 0.0;
          g[j++] = 0.0;
          g[j++] = 1.0;
        }
        return;
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 1,1,1,1 };
    }
    #endregion HATFLDC

    #region EqubCombustion
    [Test, Smoke]
    public void EqubCombustion()
    {
      double[] lb = { 0.0001, 0.0001, 0.0001, 0.0001, 0.0001 };
      double[] ub = { 100, 100, 100, 100, 100 };
      double[] x0 = { 0.0001, 0.0001, 0.0001, 0.0001, 0.0001 };
      testFn(new EqubCombustionFn(), true,
        TestMethods.NLS, lb, ub, x0);
    }
    class EqubCombustionFn : IOptimizerTestFn
    {
      public int DimensionX => 5;

      public int DimensionY => 5;

      public void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
      {
        const double R = 10;
        const double R5 = 0.193;
        const double R6 = 4.10622 * 1e-4;
        const double R7 = 5.45177 * 1e-4;
        const double R8 = 4.4975 * 1e-7;
        const double R9 = 3.40735 * 1e-5;
        const double R10 = 9.615 * 1e-7;

        const int m = 5, n = 5;
        if (f.Count != 0)
        {
          f[0] = x[0] * x[1] + x[0] - 3 * x[4];
          f[1] = 2 * x[0] * x[1] + x[0] + 3 * R10 * x[1] * x[1] + x[1] * x[2] * x[2] + R7 * x[1] * x[2] + R9 * x[1] * x[3] + R8 * x[1] - R * x[4];
          f[2] = 2 * x[1] * x[2] * x[2] + R7 * x[1] * x[2] + 2 * R5 * x[2] * x[2] + R6 * x[2] - 8 * x[4];
          f[3] = R9 * x[1] * x[3] + 2 * x[3] * x[3] - 4 * R * x[4];
          f[4] = x[0] * x[1] + x[0] + R10 * x[1] * x[1] + x[1] * x[2] * x[2] + R7 * x[1] * x[2] + R9 * x[1] * x[3] + R8 * x[1] + R5 * x[2] * x[2] + R6 * x[2] + x[3] * x[3] - 1.0;
        }

        if (g.Count != 0)
        {
          int j;
          for (j = 0; j < m * n; ++j) g[j] = 0.0;

          j = 0;
          g[j] = x[1] + 1;
          g[j + 1] = x[0];
          g[j + 4] = -3;

          j += m;
          g[j] = 2 * x[1] + 1;
          g[j + 1] = 2 * x[0] + 6 * R10 * x[1] + x[2] * x[2] + R7 * x[2] + R9 * x[3] + R8;
          g[j + 2] = 2 * x[1] * x[2] + R7 * x[1];
          g[j + 3] = R9 * x[1];
          g[j + 4] = -R;

          j += m;
          g[j + 1] = 2 * x[2] * x[2] + R7 * x[2];
          g[j + 2] = 4 * x[1] * x[2] + R7 * x[1] + 4 * R5 * x[2] + R6;
          g[j + 4] = -8;

          j += m;
          g[j + 1] = R9 * x[3];
          g[j + 3] = R9 * x[1] + 4 * x[3];
          g[j + 4] = -4 * R;

          j += m;
          g[j] = x[1] + 1;
          g[j + 1] = x[0] + 2 * R10 * x[1] + x[2] * x[2] + R7 * x[2] + R9 * x[3] + R8;
          g[j + 2] = 2 * x[1] * x[2] + R7 * x[1] + 2 * R5 * x[2] + R6;
          g[j + 3] = R9 * x[1] + 2 * x[3];
        }
        return;
      }
      public IList<double> Solution => result_;
      private double[] result_ = { 0.0034302, 31.326497, 0.0683504, 0.85952899, 0.036962 };
    }
    #endregion EqubCombustion

    #region Helpers
    private static void testSolver(
      string name,
      Optimizer opt, IOptimizerTestFn fn,
      bool success, int maxEval,
      double[] lb, double[] ub, double[] x0)
    {
      opt.setLowerBounds(lb);
      opt.setUpperBounds(ub);
      opt.setInitialPoint(x0);
      opt.setMaxEvaluations(maxEval);
      opt.setMaxIterations(maxEval);

      DelegateOptimizerFn optimizerFn = DelegateOptimizerFn.Create(
        fn.DimensionX, fn.DimensionY, fn.Evaluate, true);

      bool passed = true;
      try
      {
        opt.Minimize(optimizerFn);
      }
      catch (Exception ex)
      {
        Console.WriteLine("ERROR: {0} : {1}", name, ex.Message);
        passed = false;
      }

      Assert.AreEqual(success, passed, name + ":converged");
      if (!success && !passed) return;

      var x = opt.CurrentSolution;
      var result = fn.Solution;
      for (int i = 0; i < fn.DimensionX; i++)
      {
        Assert.AreEqual(result[i], x[i], 0.00001, name + ":x" + (i + 1));
      }
      return;
    }

    // Test a specific objective fn against all the optimizers
    // Return true if passed
    private static void testFn(IOptimizerTestFn fn, bool success, TestMethods tests)
    {
      testFn(fn, success, tests, null, null, null);
    }

    // Test a specific objective fn against all the optimizers
    // Return true if passed
    private static void testFn(IOptimizerTestFn fn, bool success, TestMethods tests,
      double[] lb, double[] ub, double[] x0)
    {
      int n = fn.DimensionX;
      if (lb == null)
        lb = BaseEntity.Shared.ArrayUtil.NewArray(n, -10.0);
      if (ub == null)
        ub = BaseEntity.Shared.ArrayUtil.NewArray(n, 10.0);
      if (x0 == null)
        x0 = BaseEntity.Shared.ArrayUtil.NewArray(n, 0.5);

      Optimizer opt;
      if ((tests & TestMethods.Simplex) != 0)
      {
        opt = new NelderMeadeSimplex(fn.DimensionX);
        opt.setToleranceF(1e-12);
        testSolver("Simplex", opt, fn, success, 10000, lb, ub, x0);
      }
      if ((tests & TestMethods.BFGS) != 0)
      {
        opt = new BFGS(fn.DimensionX);
        opt.setToleranceF(1e-12);
        opt.setToleranceGrad(1e-12);
        opt.setToleranceX(1e-12);
        testSolver("BFGS", opt, fn, success, 10000, lb, ub, x0);
      }
      if ((tests & TestMethods.BFGSB) != 0)
      {
        opt = new BFGSB(fn.DimensionX);
        opt.setToleranceF(1e-14);
        opt.setToleranceGrad(1e-14);
        opt.setToleranceX(1e-15);
        testSolver("BFGSB", opt, fn, success, 10000, lb, ub, x0);
      }
      if ((tests & TestMethods.NLS) != 0)
      {
        opt = new NLS(fn.DimensionX);
        opt.setToleranceF(1e-17);
        opt.setToleranceGrad(1e-17);
        opt.setToleranceX(5e-16);
        testSolver("NLS", opt, fn, success, 50000, lb, ub, x0);
      }
    }
    #endregion
  } // class TestOptimizer
}
