/*
 * LCDSFitSolver.cs
 *
 *  -2008. All rights reserved.
 *
 * TBD: More thought re generalizing and expanding. RTD Dec'06
 *
 */
using System;

using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves
{

  /// <summary>
  ///   Solves for a target LCDS curve characteristic by varying another LCDS curve characteristic.
  /// </summary>
  ///
  /// <remarks>
  ///   <para>A variety of different LCDS curve characteristics can be solved for. For example
  ///   solving for the refinancing probability(or the LCDS recovery or the correlation between refinancing and defaults
  ///   which gives a specific tenor survival probability </para>
  /// </remarks>
  ///
  [Serializable]
  public class LCDSFitSolver
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(LCDSFitSolver));

    #region Types

    /// <summary>
    ///   LCDS curve Solver variables for solving
    /// </summary>
    public enum Variable
    {
      /// <summary>
      ///   Vary correlation (btw refinancing curve and credit curve) till you hit a target tenor survival probability
      /// </summary>
      Correlation,

      /// <summary>
      ///   Vary refinancing rate (curve) till you hit a target tenor survival probability
      /// </summary>
      Refinancing,

      /// <summary>
      ///   Vary recovery rate till you hit a target tenor survival probability
      /// </summary>
      Recovery,
    }


    /// <summary>
    ///   LCDSFit Solver target
    /// </summary>
    public enum Target
    {
      /// <summary>
      ///   Solve for target survival probability at a giben tenor
      /// </summary>
      SurvivalProbabilityAtTenor,
    }

    #endregion Types

    #region Constructors

    /// <summary>
    ///   Construct a LCDSFit Solver
    /// </summary>
    ///
    /// <param name="lcdsCurve">Calibrated LCDS curve</param>
    /// <param name="variable">LCDS field to vary to find target solution</param>
    /// <param name="tenorName">Maturity tenor</param>
    /// <param name="target">Target type for solver</param>
    /// <param name="targetValue">Target value</param>
    /// <param name="toleranceF">Relative accuracy of target</param>
    /// <param name="toleranceX">The accuracy of variable</param>
    ///
    public LCDSFitSolver(
      SurvivalCurve lcdsCurve,
      Variable variable,
      string tenorName,
      Target target, double targetValue,
      double toleranceF, double toleranceX
      )
    {
      this.lcdsCurve_ = lcdsCurve;
      this.variable_ = variable;
      this.target_ = target;
      this.targetValue_ = targetValue;
      this.tenorName_ = tenorName;
      this.toleranceF_ = toleranceF;
      this.toleranceX_ = toleranceX;
      this.solved_ = false;
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Clear solver results. Forces a re-solve next time results are requested
    /// </summary>
    public void Reset()
    {
      this.solved_ = false;
    }


    /// <summary>
    ///   Solve for a target LCDS characteristic (Survival Probability at given tenor) by varying another LCDS characteristic.
    /// </summary>
    ///
    /// <returns>True if solution found</returns>
    ///
    public SurvivalCurve Solve()
    {
      double spread = this.targetValue_;
      this.solution_ = 0.0;

      SurvivalCurve newLCDSCurve = (SurvivalCurve)lcdsCurve_.Clone();
      newLCDSCurve.Calibrator = (Calibrator)lcdsCurve_.SurvivalCalibrator.Clone();
      
      int tenorLCDSIndex_ = lcdsCurve_.Tenors.Index(this.tenorName_);
      if (tenorLCDSIndex_ >= 0)
        t_ = Tenor.Parse(lcdsCurve_.Tenors[tenorLCDSIndex_].Name);
      else throw new ToolkitException("The lcds curve does not contain specified tenor");

      switch (this.variable_)
      {
        case Variable.Refinancing:
          {
            // Set up solver function
            SolverFn fn = (SolverFn)(new SPGivenRefinancingFn(newLCDSCurve, this.tenorName_));
              
            // Set up root finder
            Brent rf = new Brent();
            rf.setToleranceX(this.toleranceX_);
            rf.setToleranceF(this.toleranceF_);
            rf.setLowerBounds(0.0);
            rf.setUpperBounds(1.0);

            // Solve
            this.solution_ = rf.solve(fn, spread, 0.0, 1.0);
            
            this.solved_ = true;
          }
          this.lcdsCurve_ = newLCDSCurve;
          break;

        case Variable.Recovery:
          {
            // Set up solver function
            SolverFn fn = (SolverFn)(new SPGivenRecoveryFn(newLCDSCurve, this.tenorName_));

            // Set up root finder
            Brent rf = new Brent();
            rf.setToleranceX(this.toleranceX_);
            rf.setToleranceF(this.toleranceF_);
            rf.setLowerBounds(0.0);
            rf.setUpperBounds(1.0);

            // Solve
            this.solution_ = rf.solve(fn, spread, 0.0, 1.0);

            this.solved_ = true;
          }
          this.lcdsCurve_ = newLCDSCurve;
          break;

        case Variable.Correlation:
          {
            // Set up solver function
            SolverFn fn = (SolverFn)(new SPGivenCorrelationFn(newLCDSCurve, this.tenorName_));

            // Set up root finder
            Brent rf = new Brent();
            rf.setToleranceX(this.toleranceX_);
            rf.setToleranceF(this.toleranceF_);
            rf.setLowerBounds(-1.0);
            rf.setUpperBounds(1.0);

            // Solve
            this.solution_ = rf.solve(fn, spread, -1.0, 1.0);

            this.solved_ = true;
          }
          this.lcdsCurve_ = newLCDSCurve;
          break;
      }

      return this.lcdsCurve_;
    }

    #endregion Methods

    #region Properties


    /// <summary>
    ///   True if solver run
    /// </summary>
    public bool Solved
    {
      get { return this.solved_; }
    }

    /// <summary>
    ///   Target value solution
    /// </summary>
    public double Solution
    {
      get { if (!this.Solved) this.Solve(); return solution_; }
    }

    #endregion // Properties

    #region Data

    private SurvivalCurve lcdsCurve_;
    //private SurvivalCurve cdsCurve_; Not Used. RTD Aug'07
    private Variable variable_;
    private string tenorName_;
    private Tenor t_;
    private Target target_;
    private double targetValue_;
    private double toleranceF_;
    private double toleranceX_;

    private bool solved_;
    private double solution_;

    #endregion Data

    #region Solvers

    /// <summary>
    ///   Refinancing curve to match (BE)Spread at given tenor 
    /// </summary>
    private class SPGivenRefinancingFn : SolverFn
    {
      public SPGivenRefinancingFn(SurvivalCurve lcdsCurve, string tenorName)
      {
        lcdsCurve_ = lcdsCurve;
        tenorIndex_ = lcdsCurve_.Tenors.Index(tenorName);
      }

      public override double evaluate(double x)
      {
        logger.DebugFormat("Trying ref curve {0}", x);
        
        // build flat counterparty curve (refinancing curve) ; flat at x% refinancing prob
        SurvivalCurve refinancingCurve = lcdsCurve_.SurvivalCalibrator.CounterpartyCurve;
        for (int i = 0; i < refinancingCurve.Count; i++)
          refinancingCurve.SetVal(i, x);
        refinancingCurve.Fit();

        CDS cds = (CDS)lcdsCurve_.Tenors[tenorIndex_].Product;
        return CurveUtil.ImpliedSpread(lcdsCurve_, cds.Maturity, cds.DayCount, cds.Freq, cds.BDConvention, cds.Calendar) * 10000.0;
      }

      private SurvivalCurve lcdsCurve_;
      private int tenorIndex_;
      
    }

    /// <summary>
    ///    Recovery curve to match (BE)Spread at given tenor 
    /// </summary>
    private class SPGivenRecoveryFn : SolverFn
    {
      public SPGivenRecoveryFn(SurvivalCurve lcdsCurve, string tenorName)
      {
        lcdsCurve_ = lcdsCurve;
        tenorIndex_ = lcdsCurve_.Tenors.Index(tenorName);
      }

      public override double evaluate(double x)
      {
        logger.DebugFormat("Trying ref curve {0}", x);
        
        // build flat recovery curve ; flat at x% recovery rate
        for (int i = 0; i < lcdsCurve_.SurvivalCalibrator.RecoveryCurve.Count; i++)
          lcdsCurve_.SurvivalCalibrator.RecoveryCurve.SetVal(i, x);
       
        CDS cds = (CDS)lcdsCurve_.Tenors[tenorIndex_].Product;
        return CurveUtil.ImpliedSpread(lcdsCurve_, cds.Maturity, cds.DayCount, cds.Freq, cds.BDConvention, cds.Calendar) * 10000.0;
      }

      private SurvivalCurve lcdsCurve_;
      private int tenorIndex_;
    }

    /// <summary>
    ///   Correlation to match (BE)Spread at given tenor 
    /// </summary>
    private class SPGivenCorrelationFn : SolverFn
    {
      public SPGivenCorrelationFn(SurvivalCurve lcdsCurve, string tenorName)
      {
        lcdsCurve_ = lcdsCurve;
        tenorIndex_ = lcdsCurve_.Tenors.Index(tenorName);
      }

      public override double evaluate(double x)
      {
        logger.DebugFormat("Trying ref curve {0}", x);
        
        // set correlation
        lcdsCurve_.SurvivalCalibrator.CounterpartyCorrelation = x;

        CDS cds = (CDS)lcdsCurve_.Tenors[tenorIndex_].Product;
        return CurveUtil.ImpliedSpread(lcdsCurve_, cds.Maturity, cds.DayCount, cds.Freq, cds.BDConvention, cds.Calendar) * 10000.0;
      }

      private SurvivalCurve lcdsCurve_;
      private int tenorIndex_;
    }

   
    #endregion Solvers

  } // class SyntheticCDOSolver
}
