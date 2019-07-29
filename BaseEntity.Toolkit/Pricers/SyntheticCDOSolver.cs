/*
 * SyntheticCDOSolver.cs
 *
 *
 */
using System;
using System.Data;
using System.Collections;
using System.Text;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Calibrators.BaseCorrelation;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Pricers
{

  /// <summary>
  ///   Solves for a target CDO characteristic by varying another CDO characteristic.
  /// </summary>
  ///
  /// <remarks>
  ///   <para>A variety of different CDO characteristics can be solved for. For example
  ///   solving for the attachment point which gives a specific target pv while keeping
  ///   the tranche width constant.</para>
  /// </remarks>
  ///
  /// <seealso cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche Product</seealso>
  /// <seealso cref="SyntheticCDOPricer">Synthetic CDO Pricer</seealso>
  ///
  [Serializable]
  public class SyntheticCDOSolver
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SyntheticCDOSolver));

    #region Types

    /// <summary>
    ///   Synthetic CDO Solver variables for solving
    /// </summary>
    public enum Variable
    {
      /// <summary>
      ///   Vary the CDO attachment point while keeping tranche width constant
      /// </summary>
      Attachment,

      /// <summary>
      ///   Vary the CDO detachment point while attachment constant
      /// </summary>
      Detachment,

      /// <summary>
      ///   Vary the CDO trache correlation to match the target
      /// </summary>
      TrancheCorrelation,
    }


    /// <summary>
    ///   Synthetic CDO Solver target
    /// </summary>
    public enum Target
    {
      /// <summary>
      ///   Solve for target flat PV
      /// </summary>
      FlatPv,
      /// <summary>
      ///   Solve for target break even spread
      /// </summary>
      Spread,
      /// <summary>
      ///   Solve for target fee
      /// </summary>
      Fee,
      /// <summary>
      ///   Solve for target full PV
      /// </summary>
      FullPv,
      /// <summary>
      ///   Solve for the user specified price meausre
      /// </summary>
      /// <exclude />
      UserMeasure,
    }

    #endregion Types

    #region Config
    // Added 9.0
    private static readonly bool useSolverBrent2_ = true;
    #endregion // Config

    #region Constructors

    /// <summary>
    ///   Construct a CDO Solver with selected target
    /// </summary>
    /// 
    /// <param name="pricer">Pricer</param>
    /// <param name="variable">CDO field to vary to find target solution</param>
    /// <param name="target">Target type for solver</param>
    /// <param name="targetValue">Target value</param>
    /// <param name="toleranceF">Accuracy level of target</param>
    /// <param name="toleranceX">Accuracy level of variable</param>
    /// <param name="xLowerBound">lower bound on x</param>
    /// <param name="xUpperBound">upper bound on x</param>
    ///
    public SyntheticCDOSolver(
      SyntheticCDOPricer pricer,
      Variable variable,
      Target target, double targetValue,
      double toleranceF, double toleranceX,
      double xLowerBound, double xUpperBound
      )
    {
      if (pricer == null)
        throw new NullReferenceException("Pricer cannot be null");
      BasketPricer basket = pricer.Basket.Duplicate();
      basket.IsUnique = true;
      this.pricer_ = new SyntheticCDOPricer(
        (SyntheticCDO)pricer.CDO.Clone(), basket,
        pricer.DiscountCurve, pricer.Notional, pricer.RateResets);
      this.variable_ = variable;
      this.target_ = target;
      this.evalFn_ = DoublePricerFnBuilder.CreateDelegate(
        pricer.GetType(), PriceMeasure.FlatPrice);
      this.targetValue_ = targetValue;
      this.toleranceF_ = toleranceF;
      this.toleranceX_ = toleranceX;
      this.xLB_ = xLowerBound;
      this.xUB_ = xUpperBound;
      this.solved_ = false;
    }

    /// <summary>
    ///   Construct a CDO Solver with user specified price measure as target
    /// </summary>
    /// 
    /// <remarks>
    ///   In order for the solver to work properply, the use need to scale the
    ///   parameters <paramref name="toleranceF"/> and <paramref name="toleranceX"/>
    ///   the magnitudes of targets and variables.  To get optimal results, it is
    ///   recommended to normalize the <paramref name="pricer"/> notional to some
    ///   values (say 100) such that range the target values are not too big.
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="variable">CDO field to vary to find target solution</param>
    /// <param name="measure">Target price measure</param>
    /// <param name="targetValue">Target value</param>
    /// <param name="toleranceF">
    ///   Relative accuracy of target which implies a absolute accuracy level
    ///   given by <c>toleranceF * (1 + |target|)</c>.
    /// </param>
    /// <param name="toleranceX">
    ///   The accuracy of variable which implies a absolute accuracy level
    ///   approximately given by <c>toleranceX * (1 + |x|)</c>.
    /// </param>
    /// <param name="xLowerBound">lower bound on x</param>
    /// <param name="xUpperBound">upper bound on x</param>
    ///
    public SyntheticCDOSolver(
      SyntheticCDOPricer pricer,
      Variable variable,
      string measure, double targetValue,
      double toleranceF, double toleranceX,
      double xLowerBound, double xUpperBound
      )
    {
      if (pricer == null)
        throw new System.NullReferenceException("Pricer cannot be null");
      BasketPricer basket = pricer.Basket.Duplicate();
      basket.IsUnique = true;
      this.pricer_ = new SyntheticCDOPricer(
        (SyntheticCDO)pricer.CDO.Clone(), basket,
        pricer.DiscountCurve, pricer.Notional, pricer.RateResets);
      this.variable_ = variable;
      this.target_ = Target.UserMeasure;
      this.evalFn_ = CreateDelegate(measure);
      this.targetValue_ = targetValue;
      this.toleranceF_ = toleranceF;
      this.toleranceX_ = toleranceX;
      this.xLB_ = xLowerBound;
      this.xUB_ = xUpperBound;
      this.solved_ = false;
      this.min_ = 0.0;
      this.max_ = pricer.Copula.CopulaType == CopulaType.ExtendedGauss ? 5.0 : 1.0;
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
    ///   Solve for a target CDO characteristic by varying another CDO characteristic.
    /// </summary>
    ///
    /// <returns>True if solution found</returns>
    ///
    public bool Solve()
    {
      double pv = 0.0;
      switch (this.target_)
      {
        case Target.Fee:
          this.pricer_.CDO.Fee = this.targetValue_;
          // We need to set fee settle to include up-front fee in PVs
          this.pricer_.CDO.FeeSettle = Dt.Add(this.pricer_.Settle,1);
          break;
        case Target.Spread:
          this.pricer_.CDO.Premium = this.targetValue_ / 10000.0;
          break;
        case Target.FullPv:
          pv = this.targetValue_ - this.pricer_.Accrued();
          break;
        case Target.FlatPv:
        case Target.UserMeasure:
          pv = this.targetValue_;
          break;
      }

      this.solution_ = 0.0;
      switch (this.variable_)
      {
        case Variable.Attachment:
          {
            // Set up solver function
            SolverFn fn = new PvGivenAttachmentFn(this.pricer_, this.evalFn_);

            // Set up root finder
            Solver rf = useSolverBrent2_ ? (Solver)new Brent2() : (Solver)new Brent();
            rf.setToleranceX(this.toleranceX_);
            rf.setToleranceF((useSolverBrent2_ ? 1 : 0.001) * this.toleranceF_);
            double lb = Double.IsNaN(xLB_) ? 0.0 : xLB_;
            double ub = Double.IsNaN(xUB_) ? (1.0 - pricer_.CDO.TrancheWidth) : xUB_;
            rf.setLowerBounds(lb);
            rf.setUpperBounds(ub);

            // Solve
            try
            {
              // first try a local search
              this.solution_ = rf.solve(fn, pv, this.pricer_.CDO.Attachment);
            }
            catch (SolverException) 
            {
              // If fail, try a global search in case it stops prematurely
              this.solution_ = rf.solve(fn, pv, 0.0, 1 - this.pricer_.CDO.TrancheWidth);
            }

            // Update the pricer with the solution if necessary
            if (this.pricer_.CDO.Attachment != this.solution_)
              fn.evaluate(this.solution_);
            this.solved_ = true;
          }
          break;

        case Variable.Detachment:
          {
            // Set up solver function
            SolverFn fn = new PvGivenDetachmentFn(this.pricer_, this.evalFn_);

            // Set up root finder
            Solver rf = useSolverBrent2_ ? (Solver)new Brent2() : (Solver)new Brent();
            rf.setToleranceX(this.toleranceX_);
            rf.setToleranceF((useSolverBrent2_ ? 1 : 0.001) * this.toleranceF_);
            double lb = Double.IsNaN(xLB_) ? (pricer_.CDO.Attachment) : xLB_;
            double ub = Double.IsNaN(xUB_) ? 1.0 : xUB_;
            rf.setLowerBounds(lb);
            rf.setUpperBounds(ub);

            // Solve
            this.solution_ = rf.solve(fn, pv, this.pricer_.CDO.Detachment);

            // Update the pricer with the solution if necessary
            if (this.pricer_.CDO.Detachment != this.solution_)
              fn.evaluate(this.solution_);
            this.solved_ = true;
          }
          break;

        case Variable.TrancheCorrelation:
          {
            // The following calling sequence will not create a clone of pricer.
            // Instead the input pricer will be updated as a side effect.
            SyntheticCDOPricer pricer = this.pricer_;
            BasketPricer basket = pricer.Basket;
            if (basket is BaseCorrelationBasketPricer)
            {
              basket = ((BaseCorrelationBasketPricer)pricer.Basket).CreateDetachmentBasketPricer(false);
              basket.IsUnique = true;
              pricer = new SyntheticCDOPricer(pricer.CDO, basket, pricer.DiscountCurve, pricer.Notional, pricer.RateResets);
            }
            else if (basket.Correlation != basket.CorrelationTermStruct)
            {
              basket.Correlation = basket.CorrelationTermStruct;
              pricer = new SyntheticCDOPricer(pricer.CDO, basket, pricer.DiscountCurve, pricer.Notional, pricer.RateResets);
            }
            this.solution_ = CorrelationSolver.Solve(evalFn_, pv, pricer, toleranceF_, toleranceX_,
              Double.IsNaN(xLB_) ? min_ : xLB_, Double.IsNaN(xLB_) ? max_ : xUB_);

            this.solved_ = true;
          }
          break;
      }

      return this.solved_;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Pricer from solver
    /// </summary>
    public SyntheticCDOPricer Pricer
    {
      get { if (!this.Solved) this.Solve(); return pricer_; }
    }


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

    private readonly SyntheticCDOPricer pricer_;
    private readonly Double_Pricer_Fn evalFn_;
    private readonly Variable variable_;
    private readonly Target target_;
    private readonly double targetValue_;
    private readonly double toleranceF_;
    private readonly double toleranceX_;
    private readonly double min_ = 0.0;
    private readonly double max_ = 1.0;
    private readonly double xLB_ = Double.NaN;
    private readonly double xUB_ = Double.NaN;
 
    private bool solved_;
    private double solution_;

    #endregion Data

    #region Solvers

    /// <summary>
    ///   Pv given attachment solver function, general case
    /// </summary>
    private class PvGivenAttachmentFn : SolverFn
    {
      public PvGivenAttachmentFn(
        SyntheticCDOPricer pricer,
        Double_Pricer_Fn evaluator)
      {
        pricer_ = pricer;
        eval_ = evaluator;
        width_ = pricer.CDO.TrancheWidth;
      }

      public override double evaluate(double x)
      {
        logger.DebugFormat("Trying attachment {0}", x);
        SyntheticCDO cdo = pricer_.CDO;
        cdo.Attachment = x;
        cdo.Detachment = x + width_;
        pricer_.Reset(SyntheticCDOPricer.ResetFlag.Subordination);
        return eval_(pricer_);
      }

      private readonly SyntheticCDOPricer pricer_;
      private readonly Double_Pricer_Fn eval_;
      private readonly double width_;
    }


    /// <summary>
    ///   Pv given detachment solver function, general case
    /// </summary>
    private class PvGivenDetachmentFn : SolverFn
    {
      public PvGivenDetachmentFn(
        SyntheticCDOPricer pricer,
        Double_Pricer_Fn evaluator)
      {
        pricer_ = pricer;
        eval_ = evaluator;
      }

      public override double evaluate(double x)
      {
        logger.DebugFormat("Trying attachment {0}", x);
        SyntheticCDO cdo = pricer_.CDO;
        if (x <= cdo.Attachment)
          return 0;
        cdo.Detachment = x;
        pricer_.Reset(SyntheticCDOPricer.ResetFlag.Subordination);
        return eval_(pricer_);
      }

      private SyntheticCDOPricer pricer_;
      private Double_Pricer_Fn eval_;
    }

    #endregion Solvers

    #region User measures
    private static Double_Pricer_Fn CreateDelegate(string measure)
    {
      if (String.Compare(measure, "Spread01", true) == 0)
        return (p) => ((SyntheticCDOPricer)p).Spread01("Pv", 4.0, 0.0);
      if (String.Compare(measure, "Recovery01", true) == 0)
        return (p) => Sensitivities.Recovery01(p, 0.01, 0.0, true);
      if (String.Compare(measure, "Correlation01", true) == 0)
        return (p) => Sensitivities.Correlation01(p, 0.01, 0.0);
      if (String.Compare(measure, "Rate01", true) == 0)
        return (p) => Sensitivities.IR01(p, 0.04, 0.0, true);
      return DoublePricerFnBuilder.CreateDelegate(typeof(SyntheticCDOPricer), measure);
    }
    #endregion
  } // class SyntheticCDOSolver
}
