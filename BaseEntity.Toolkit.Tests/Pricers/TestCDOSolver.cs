//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Test CDO Solver functions based on external creadit data
  /// </summary>
  [TestFixture("TestCDOSolver_CDO0000015 Heterogeneous")]
  [TestFixture("TestCDOSolver_CDO0000015 SemiAnalytic")]
  [TestFixture("TestCDOSolver_SmallTolerance SemiAnalytic")]
  public class TestCDOSolver : TestCdoBase
  {
    public TestCDOSolver(string name) : base(name) { }

    #region SetUP
    /// <summary>
    ///   Create an array of CDO Pricers
    /// </summary>
    /// <returns>CDO Pricers</returns>
    [OneTimeSetUp]
    public void SetUpCDOSolver()
    {
      base.CreatePricers();
    }
    #endregion // SetUp

    #region Methods
    /// <summary>
    ///   Solve for Attachment
    /// </summary>
    /// <returns>solution</returns>
    [Test]
    public void SolveForAttachment()
    {
      ResultData rd = SolveFor(
        SyntheticCDOSolver.Variable.Attachment);
      MatchExpects(rd);
    }

    /// <summary>
    ///   Solve for Attachment
    /// </summary>
    /// <returns>solution</returns>
    [Test]
    public void SolveForDetachment()
    {
      ResultData rd = SolveFor(
        SyntheticCDOSolver.Variable.Detachment);
      MatchExpects(rd);
    }

    /// <summary>
    ///   Solve for Attachment
    /// </summary>
    /// <returns>solution</returns>
    [Test]
    public void SolveForTrancheCorrelation()
    {
      ResultData rd = SolveFor(
        SyntheticCDOSolver.Variable.TrancheCorrelation);
      MatchExpects(rd);
    }

    /// <summary>
    ///   Solve for all the targets
    /// </summary>
    /// <param name="variable">variable</param>
    /// <returns>solutions</returns>
    private ResultData SolveFor(
      SyntheticCDOSolver.Variable variable
      )
    {
      Timer timer = new Timer();
      
      int numTargets = labels.Length;
      int numPricers = cdoPricers_.Length;
      double[] result = new double[numPricers * numTargets];
      for (int i = 0, idx = 0; i < numPricers; ++i)
      {
        SyntheticCDOPricer pricer = cdoPricers_[i];
        result[idx++] = SolveForTarget(pricer, variable,
          SyntheticCDOSolver.Target.FlatPv, FlatPvTarget, 
          1 / (1 +Math.Abs(FlatPvTarget)),  timer);
        result[idx++] = SolveForTarget(pricer, variable,
          SyntheticCDOSolver.Target.FullPv, FullPvTarget,
          1 / (1 + Math.Abs(FullPvTarget)), timer);
        if (FeeTarget < 0)
        {
          double pvTarget = -FeeTarget * pricer.Notional;
          result[idx++] = SolveForTarget(pricer, variable,
            SyntheticCDOSolver.Target.FlatPv, pvTarget,
            1 / (1 + Math.Abs(pvTarget)), timer);
        }
        else
        {
          double pvTarget = -FeeTarget * pricer.Notional;
          result[idx++] = SolveForTarget(pricer, variable,
            SyntheticCDOSolver.Target.Fee, FeeTarget,
            1 / (1 + Math.Abs(pvTarget)), timer);
        }
        result[idx++] = SolveForTarget(pricer, variable,
          SyntheticCDOSolver.Target.Spread, SpreadTarget, 1.0, timer);
      }

      RoundTripCheck(result, variable);

      ResultData rd = LoadExpects();
      rd.TimeUsed = timer.Elapsed;
      rd.Results[0].Actuals = result;
      return rd;
    }

    /// <summary>
    ///   Solve for a specifi target
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <param name="variable">variable</param>
    /// <param name="target">target</param>
    /// <param name="targetValue">target value</param>
    /// <param name="timer">timer</param>
    /// <returns></returns>
    private double SolveForTarget(
      SyntheticCDOPricer pricer,
      SyntheticCDOSolver.Variable variable,
      SyntheticCDOSolver.Target target,
      double targetValue,
      double toleranceF,
      Timer timer
      )
    {
      // Solver should do cloning and do not change the original product
      //-
      // // Make a clone of the input pricer
      // pricer = (SyntheticCDOPricer)pricer.Clone();

      // Tolerance
      toleranceF = ToleranceF > 0 ? ToleranceF : toleranceF;
      double toleranceX = (ToleranceX > 0 ? ToleranceX : 1E-6);
      if (variable == SyntheticCDOSolver.Variable.TrancheCorrelation)
        toleranceX /= 10;

      // Match the pricer with target and return it
      timer.Resume();
      SyntheticCDOSolver solver = new SyntheticCDOSolver(
        pricer, variable, target, targetValue, toleranceF, toleranceX,Double.NaN,Double.NaN);
      double solution = solver.Solution;
      timer.Stop();

      return solution;
    }

    private void RoundTripCheck(
      double[] result,
      SyntheticCDOSolver.Variable variable)
    {
      base.CreatePricers();

      int numPricers = cdoPricers_.Length;
      for (int i = 0, idx = 0; i < numPricers; ++i)
      {
        SyntheticCDOPricer pricer = cdoPricers_[i];
        CheckFlatPv(pricer, variable, result[idx++], FlatPvTarget);
        CheckFullPv(pricer, variable, result[idx++], FullPvTarget);
        CheckFee(pricer, variable, result[idx++], FeeTarget);
        CheckPremium(pricer, variable, result[idx++], SpreadTarget);
      }
      return;
    }

    private static void CheckFlatPv(
      SyntheticCDOPricer pricer,
      SyntheticCDOSolver.Variable variable,
      double solution,
      double target)
    {
      pricer = SetSolution(pricer, variable, solution);
      double actual = pricer.FlatPrice();
      Assert.AreEqual(target, actual, pricer.Notional*1E-7, "FlatPv");
    }

    private static void CheckFullPv(
      SyntheticCDOPricer pricer,
      SyntheticCDOSolver.Variable variable,
      double solution,
      double target)
    {
      pricer = SetSolution(pricer, variable, solution);
      double actual = pricer.FullPrice();
      Assert.AreEqual(target, actual, pricer.Notional*1E-7, "FullPrice");
    }

    private static void CheckFee(
      SyntheticCDOPricer pricer,
      SyntheticCDOSolver.Variable variable,
      double solution,
      double target)
    {
      pricer = SetSolution(pricer, variable, solution);
      double actual = pricer.BreakEvenFee();
      Assert.AreEqual(target, actual, 1E-6, "Fee");
    }

    private static void CheckPremium(
      SyntheticCDOPricer pricer,
      SyntheticCDOSolver.Variable variable,
      double solution,
      double target)
    {
      pricer = SetSolution(pricer, variable, solution);
      double actual = pricer.BreakEvenPremium();
      Assert.AreEqual(target /10000, actual, 1E-6, "premium");
    }

    private static SyntheticCDOPricer SetSolution(
      SyntheticCDOPricer pricer,
      SyntheticCDOSolver.Variable variable,
      double solution)
    {
      switch (variable)
      {
        case SyntheticCDOSolver.Variable.Attachment:
          {
            SyntheticCDO cdo = (SyntheticCDO)pricer.CDO.Clone();
            double w = cdo.TrancheWidth;
            cdo.Attachment = solution;
            cdo.Detachment = solution + w;
            return new SyntheticCDOPricer(
              cdo, 
              pricer.Basket.Duplicate(),
              pricer.DiscountCurve,
              pricer.Notional);
          }
        case SyntheticCDOSolver.Variable.Detachment:
          {
            SyntheticCDO cdo = (SyntheticCDO)pricer.CDO.Clone();
            cdo.Detachment = solution;
            return new SyntheticCDOPricer(
              cdo,
              pricer.Basket.Duplicate(),
              pricer.DiscountCurve,
              pricer.Notional);
          }
        case SyntheticCDOSolver.Variable.TrancheCorrelation:
          {
            BasketPricer basket = pricer.Basket;
            if (basket is BaseCorrelationBasketPricer)
              basket = ((BaseCorrelationBasketPricer)basket).CreateDetachmentBasketPricer(false);
            basket.SetFactor(Math.Sqrt(solution));
            basket.Reset(SyntheticCDOPricer.ResetFlag.Correlation);
            return new SyntheticCDOPricer(
              pricer.CDO,
              basket,
              pricer.DiscountCurve,
              pricer.Notional);
          }
      }
      throw new System.Exception("Unknown SyntheticCDOSolver.Variable");
    }
    #endregion // Methods

    #region Properties
    /// <summary>
    ///   Flat Pv target
    /// </summary>
    public double FlatPvTarget { get; set; } = 0.0;

    /// <summary>
    ///   Full Pv target
    /// </summary>
    public double FullPvTarget { get; set; } = 0.0;

    /// <summary>
    ///   Spread target
    /// </summary>
    public double SpreadTarget { get; set; } = 500.0;

    /// <summary>
    ///   Fee target
    /// </summary>
    public double FeeTarget { get; set; } = 0.0;

    /// <summary>
    ///   Function tolerance
    /// </summary>
    public double ToleranceF { get; set; } = 1E-6;

    /// <summary>
    ///   Variable tolerance
    /// </summary>
    public double ToleranceX { get; set; } = 1E-7;

    #endregion // Properties

    #region Data

    private string[] labels = new string[]{"FlatPv", "FullPv", "Fee", "Spread"};
    #endregion // Data
  } // class TestCDOSolver
}
