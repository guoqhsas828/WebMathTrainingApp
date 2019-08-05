//
// Copyright (c)    2002-2018. All rights reserved.
//

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Numerics
{
  using System;

  using BaseEntity.Toolkit.Numerics;
  
  
  [TestFixture]
  public class TestSolver
  {
    const double epsilon = 0.00001;
    const int times = 400000;

    // Define the objective functions for testing

    // y = x^2 + 1 (no roots).
    class PositiveQuadratic : SolverFn
    {
      public override double evaluate( double x) 
      { 
        return x*x + 1.0; 
      }

      public override double derivative(double x) 
      { 
        return 2*x; 
      }

      public override bool isDerivativeImplemented() { return true; }
    }

    // y = sin(x+0.1)
    // roots are at -0.1 + n*pi , n=1,2,...
    class PhasedSine : SolverFn
    {
      public override double evaluate( double x ) 
      { 
        return Math.Sin(x+.1); 
      }

      public override double derivative(double x) 
      { 
        return Math.Cos(x+0.1); 
      }

      public override bool isDerivativeImplemented() { return true; }
    }

    // y = exp(x)-1.0
    // root is at x=0;
    class ShiftedExp : SolverFn
    {
      public override double evaluate( double x ) 
      { 
        return Math.Exp(x)-1; 
      }

      public override double derivative(double x) 
      { 
        return Math.Exp(x); 
      }

      public override bool isDerivativeImplemented() { return true; }
    }

    // y = sin(1/(x^2+0.00001)) - 0.1
    // root is at x=0;
    class LotsOfRoots : SolverFn
    {
      public override double evaluate( double x )
      {
        // Note: if we don't add the -0.1 here
        // it may be very hard to find a root unless we start looking
        // very close to x=0.
        return Math.Sin(1.0/(x*x+0.00001))-.1;
      }

      public override double derivative( double x )
      {
        double tmp = 1.0 / (x*x+0.00001);
        return Math.Cos(tmp)*(-2.0)*tmp*tmp*x;
      }
  
      public override bool isDerivativeImplemented()
      {
        return true;
      }
    }

    // y = d + c*x + b*x^2 + a*x^3
    public class Cubic : SolverFn
    {
      public Cubic( double a, double b, double c, double d )
      {
        this.a = a;
        this.b = b;
        this.c = c;
        this.d = d;
      }

      public override double evaluate( double x )
      {
        return d + x*(c + x*(b + x*a));
      }

      public override double derivative( double x )
      {
        return c + x*( 2*b + x*3.0*a );
      }

      public override bool isDerivativeImplemented() { return true; }

      private double a;
      private double b;
      private double c;
      private double d;
    }

    // Test the specified solver using the given objective function
    private static double RunIt( Solver rf, SolverFn fn, double initial )
    {
      // Set up solver target from delegates
      double x = 0;
      for (int i = 0; i < times; ++i)
      {
        rf.setInitialPoint(initial + i / 100000000);

        rf.solve( fn, 0.0 );
        x = rf.getCurrentSolution();
      }
      return x;
    }

    [Test, Smoke]
    public void Bisection_PositiveQuadratic()
    {
      Assert.Throws<SolverException>(() =>
      {
        double expected = 0.0;
        double result = RunIt(new Bisection(), new PositiveQuadratic(), expected + 1.2345);
      });
    }

    [Test, Smoke]
    public void Brent_PositiveQuadratic()
    {
      Assert.Throws<SolverException>(() =>
      {
        double expected = 0.0;
        double result = RunIt(new Brent(), new PositiveQuadratic(), expected + 1.2345);
      });
    }

    [Test, Smoke]
    public void Newton_PositiveQuadratic()
    {
      Assert.Throws<SolverException>(() =>
      {
        double expected = 0.0;
        double result = RunIt(new Newton(), new PositiveQuadratic(), expected + 1.2345);
      });
    }

    [Test, Smoke]
    public void Generic_PositiveQuadratic()
    {
      Assert.Throws<SolverException>(() =>
      {
        double expected = 0.0;
        double result = RunIt(new Generic(), new PositiveQuadratic(), expected + 1.2345);
      });
    }

    [Test, Smoke]
    public void Bisection_PhasedSine_1()
    {
      double expected = -0.1;
      double result = RunIt( new Bisection(), new PhasedSine(), expected + 1.2345 );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Bisection_PhasedSine_2()
    {
      double expected = -0.1;
      double result = RunIt( new Bisection(), new PhasedSine(), expected );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Brent_PhasedSine_1()
    {
      double expected = -0.1;
      double result = RunIt( new Brent(), new PhasedSine(), expected + 1.2345 );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Brent_PhasedSine_2()
    {
      double expected = -0.1;
      double result = RunIt( new Brent(), new PhasedSine(), expected );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Newton_PhasedSine_1()
    {
      double expected = -0.1;
      double result = RunIt( new Newton(), new PhasedSine(), expected + 1.2345 );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Newton_PhasedSine_2()
    {
      double expected = -0.1;
      double result = RunIt( new Newton(), new PhasedSine(), expected );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Generic_PhasedSine_1()
    {
      double expected = -0.1;
      double result = RunIt( new Generic(), new PhasedSine(), expected + 1.2345 );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Generic_PhasedSine_2()
    {
      double expected = -0.1;
      double result = RunIt( new Generic(), new PhasedSine(), expected );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Bisection_ShiftedExp_1()
    {
      double expected = 0.0;
      double result = RunIt( new Bisection(), new ShiftedExp(), expected + 1.2345 );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Bisection_ShiftedExp_2()
    {
      double expected = 0.0;
      double result = RunIt( new Bisection(), new ShiftedExp(), expected );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Brent_ShiftedExp_1()
    {
      double expected = 0.0;
      double result = RunIt( new Brent(), new ShiftedExp(), expected + 1.2345 );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Brent_ShiftedExp_2()
    {
      double expected = 0.0;
      double result = RunIt( new Brent(), new ShiftedExp(), expected );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Newton_ShiftedExp_1()
    {
      double expected = 0.0;
      double result = RunIt( new Newton(), new ShiftedExp(), expected + 1.2345 );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Newton_ShiftedExp_2()
    {
      double expected = 0.0;
      double result = RunIt( new Newton(), new ShiftedExp(), expected );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Generic_ShiftedExp_1()
    {
      double expected = 0.0;
      double result = RunIt( new Generic(), new ShiftedExp(), expected + 1.2345 );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Generic_ShiftedExp_2()
    {
      double expected = 0.0;
      double result = RunIt( new Generic(), new ShiftedExp(), expected );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Bisection_Cubic_1()
    {
      double expected = 0.377203;
      Cubic objFn = new Cubic( 1.0, 4.0, 1.0, -1.0 ); // roots near -3.651, -.722, and 0.38
      double result = RunIt( new Bisection(), objFn, expected + 1.2345 );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Bisection_Cubic_2()
    {
      double expected = 0.377203;
      Cubic objFn = new Cubic( 1.0, 4.0, 1.0, -1.0 ); // roots near -3.651, -.722, and 0.38
      double result = RunIt( new Bisection(), objFn, expected );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Brent_Cubic_1()
    {
      double expected = 0.377203;
      Cubic objFn = new Cubic( 1.0, 4.0, 1.0, -1.0 ); // roots near -3.651, -.722, and 0.38
      double result = RunIt( new Brent(), objFn, expected + 1.2345 );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Brent_Cubic_2()
    {
      double expected = 0.377203;
      Cubic objFn = new Cubic( 1.0, 4.0, 1.0, -1.0 ); // roots near -3.651, -.722, and 0.38
      double result = RunIt( new Brent(), objFn, expected );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Newton_Cubic_1()
    {
      double expected = 0.377203;
      Cubic objFn = new Cubic( 1.0, 4.0, 1.0, -1.0 ); // roots near -3.651, -.722, and 0.38
      double result = RunIt( new Newton(), objFn, expected + 1.2345 );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Newton_Cubic_2()
    {
      double expected = 0.377203;
      Cubic objFn = new Cubic( 1.0, 4.0, 1.0, -1.0 ); // roots near -3.651, -.722, and 0.38
      double result = RunIt( new Newton(), objFn, expected );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Generic_Cubic_1()
    {
      double expected = 0.377203;
      Cubic objFn = new Cubic( 1.0, 4.0, 1.0, -1.0 ); // roots near -3.651, -.722, and 0.38
      double result = RunIt( new Generic(), objFn, expected + 1.2345 );
      Assert.AreEqual( expected, result, epsilon );
    }

    [Test, Smoke]
    public void Generic_Cubic_2()
    {
      double expected = 0.377203;
      Cubic objFn = new Cubic( 1.0, 4.0, 1.0, -1.0 ); // roots near -3.651, -.722, and 0.38
      double result = RunIt( new Generic(), objFn, expected );
      Assert.AreEqual( expected, result, epsilon );
    }

  } // class TestSolver

} 

