/*
 * HWDynamicBasketPricer.cs
 *
 *  -2008. All rights reserved.
 *
 * $Id $
 *
 */

using System;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers.BasketPricers
{
  /// <summary>
  ///   Pricer based on Hull-White dynamic model of portfolio risk
  /// </summary>
  [Serializable]
  public class HWDynamicBasketPricer : BasketPricer
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(HWDynamicBasketPricer));

		#region Constructors

    /// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="asOf">As-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="maturity">Maturity date</param>
		/// <param name="survivalCurves">Survival Curve calibrations of individual names</param>
		/// <param name="recoveryCurves">Recovery curves of individual names</param>
		/// <param name="principals">Principals of individual names</param>
		/// <param name="parameters">Parameters of the dynamic process</param>
		/// <param name="stepSize">Size of steps used to approximate the loss distribution as a function of time</param>
		/// <param name="stepUnit">Time unit of the steps: Days, Weeks, Months or Years</param>
		/// <param name="lossLevels">Levels at which the loss distributions are constructed.</param>
		///
    public HWDynamicBasketPricer(
      Dt asOf,
      Dt settle,
      Dt maturity,
      SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      double[] principals,
      double[] parameters,
      int stepSize,
      TimeUnit stepUnit,
      Array lossLevels)
      : base(asOf, settle, maturity, survivalCurves, recoveryCurves, principals,
        new Copula(CopulaType.Poisson),
        CorrelationTermStruct.Generalized(CurveUtil.CurveNames(survivalCurves), parameters, new Dt[] { Dt.Empty }),
        stepSize, stepUnit, lossLevels)
    {
      logger.DebugFormat("Creating Homogeneous Basket asof={0}, settle={1}, maturity={2}, principal={3}", asOf, settle, maturity, principals[0]);

      // Validate
      //
      if (recoveryCurves != null && recoveryCurves.Length != survivalCurves.Length)
        throw new ArgumentException(String.Format("Number of recoveries {0} must equal number of names {1}",
          recoveryCurves.Length, survivalCurves.Length));
      if (principals.Length != survivalCurves.Length)
        throw new ArgumentException(String.Format("Number of principals {0} must equal number of names {1}",
          principals.Length, survivalCurves.Length));

      // Check all notionals are the same
      for (int i = 1; i < principals.Length; i++)
        if (principals[i] != principals[0])
          throw new ToolkitException(String.Format("Principals must be uniform ({0} != {1})", principals[i], principals[0]));

      // Do not add complements to loss levels
      this.LossLevelAddComplement = false;

      // Set basket specific data memebers
      this.Parameters = (parameters == null ? new double[3] : parameters);
      this.distribution_ = null;
      this.distributionComputed_ = false;

      logger.Debug("Homogeneous Basket created");

      return;
    }

		/// <summary>
		///   Clone
		/// </summary>
		public override object Clone()
		{
      HWDynamicBasketPricer obj = (HWDynamicBasketPricer)base.Clone();

      obj.distribution_ = distribution_ == null ? null : distribution_.clone();

			return obj;
		}

    /// <summary>
    ///   Duplicate a basket pricer
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Duplicate() differs from Clone() in that it copies by references all the 
    ///   basic data and numerical options defined in the BasketPricer class.  But it is
    ///   not the same as the MemberwiseClone() function, since it does not copy by reference
    ///   the computational data such as LossDistributions in SemiAnalyticBasketPricer class.
    ///   </para>
    /// 
    ///   <para>This function provides an easy way to construct objects performing
    ///   independent calculations on the same set of input data.  We will get rid of it
    ///   once we have restructured the basket architecture by furthur separating the basket data,
    ///   the numerical options and the computational devices.</para>
    /// </remarks>
    /// 
    /// <returns>Duplicated basket pricer</returns>
    /// <exclude />
    public override BasketPricer Duplicate()
    {
      HWDynamicBasketPricer obj = (HWDynamicBasketPricer)base.Duplicate();

      // Make clone of computation devices
      obj.Parameters = CloneUtil.Clone(Parameters);
      obj.distribution_ = distribution_ == null ? null : distribution_.clone();

      return obj;
    }
    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Compute the cumulative loss distribution
    /// </summary>
    ///
    /// <remarks>
    ///   The returned array has two columns, the first of which contains the 
    ///   loss levels and the second column contains the corresponding cumulative
    ///   probabilities or expected base losses.
    /// </remarks>
    ///
    /// <param name="wantProbability">If true, return probabilities; else, return expected base losses</param>
    /// <param name="date">The date at which to calculate the distribution</param>
    /// <param name="lossLevels">Array of lossLevels (should be between 0 and 1)</param>
    public override double[,] CalcLossDistribution(
      bool wantProbability, Dt date, double[] lossLevels)
    {
      Timer timer = null;
      if (logger.IsDebugEnabled)
      {
        timer = new Timer();
        timer.start();
        logger.Debug("Computing loss distribution for Heterogeneous basket");
      }

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;
      if (Dt.Cmp(start, date) > 0)
        throw new ArgumentOutOfRangeException("date", "date is before portfolio start");
      if (Dt.Cmp(Maturity, date) < 0)
        throw new ArgumentOutOfRangeException("date", "date is after maturity");

      // initialize loss levels
      double lossRate = (1 - AverageRecoveryRate);
      UniqueSequence<double> list = new UniqueSequence<double>();
      for (int i = 0; i < lossLevels.Length; ++i)
      {
        // Adjust tranche levels by previous defaults (which had hapened
        // before settle)
        double level = AdjustTrancheLevel(false, lossLevels[i]);

        // By its nature the distribution is disrete. To avoid unexpected
        // results, we round numbers to nearest effective decimal points,
        // to make sure, for example,  2.0 does not become somthing like
        // 1.999999999999954
        decimal x = (decimal)(level / lossRate);
        level = (double)Math.Round(x, EffectiveDigits);
        if (level > 1.0)
          level = 1.0;
        list.Add(level);
      }

      // initialize distributions
      Curve2D distribution = new Curve2D();
      InitializeDistributions(start, new Dt[]{start, date}, list, distribution);
      
      HullWhiteDynamicBasketModel.ComputeDistributions(
        wantProbability, 0, distribution.NumDates(), Parameters,
        SurvivalCurves, Principals, RecoveryRates, distribution);

      int N = distribution.NumLevels();
      double[,] results = new double[N, 2];
      for (int i = 0; i < N; ++i)
      {
        double level = distribution.GetLevel(i);
        results[i, 0] = RestoreTrancheLevel(false, level * lossRate);
        results[i, 1] = distribution.Interpolate(date, level);
        if (!wantProbability)
          results[i, 1] = RestoreTrancheLevel(false, results[i, 1] * lossRate / Count);
      }

      if (timer != null)
      {
        timer.stop();
        logger.DebugFormat("Completed loss distribution in {0} seconds", timer.getElapsed());
      }
      return results;
    }

    /// <summary>
    ///   Compute the accumlated loss on a tranche
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the cumulative losses</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    /// 
    /// <returns>The expected accumulative loss on a tranche</returns>
    public override double AccumulatedLoss(
      Dt date, double trancheBegin, double trancheEnd)
    {
      if (!distributionComputed_)
        ComputeAndSaveDistribution();

      double loss = 0;
      AdjustTrancheLevels(false, ref trancheBegin, ref trancheEnd, ref loss);

      double lossRate = 1 - AverageRecoveryRate;
      if (lossRate < 1.0E-12)
        return loss;
      trancheBegin /= lossRate;
      if (trancheBegin > 1.0) trancheBegin = 1.0;
      trancheEnd /= lossRate;
      if (trancheEnd > 1.0) trancheEnd = 1.0;
      double defaults = distribution_.Interpolate(0, date, trancheBegin, trancheEnd);
      loss += defaults * lossRate * loadingFactor_;
      return loss;
    }

    /// <summary>
    ///   Compute the amortized amount on a tranche
    /// </summary>
    ///
    /// <param name="date">The date at which to calculate the amortized values</param>
    /// <param name="trancheBegin">The attachment point of the tranche</param>
    /// <param name="trancheEnd">The detachment point of the tranche</param>
    /// 
    /// <returns>The expected accumulative amortization on a tranche</returns>
    public override double AmortizedAmount(
      Dt date, double trancheBegin, double trancheEnd)
    {
      if (0 == AverageRecoveryRate)
        return 0.0;

      if (!distributionComputed_)
        ComputeAndSaveDistribution();

      double amortized = 0;
      double tBegin = 1 - trancheEnd;
      double tEnd = 1 - trancheBegin;
      AdjustTrancheLevels(true, ref tBegin, ref tEnd,  ref amortized);
      if (AverageRecoveryRate < 1.0E-12)
        return 0.0;
      double multiplier = 1.0 / AverageRecoveryRate;
      tBegin *= multiplier;
      if (tBegin > 1.0) tBegin = 1.0;
      tEnd *= multiplier;
      if (tEnd > 1.0) tEnd = 1.0;
      double defaults = distribution_.Interpolate(0, date, tBegin, tEnd);
      amortized += defaults * AverageRecoveryRate * loadingFactor_;
      return amortized;
    }

    /// <summary>
    ///   Reset the pricer such that in the next request for AccumulatedLoss()
    ///   or AmortizedAmount(), it recompute everything.
    /// </summary>
    public override void Reset()
    {
      distributionComputed_ = false;
    }

    /// <summary>
    ///   Compute the whole distribution, save the result for later use
    /// </summary>
    private void ComputeAndSaveDistribution()
    {
      Timer timer = null;

      if (logger.IsDebugEnabled)
      {
        timer = new Timer();
        timer.start();
        logger.Debug("Computing distribution for Homogeneous basket");
      }

      Dt start = PortfolioStart.IsEmpty() ? Settle : PortfolioStart;

      // Initialize distributions
      int startDateIndex = this.RecalcStartDateIndex;
      if (distribution_ == null || startDateIndex < 0)
      {
        if (distribution_ == null)
          distribution_ = new Curve2D();
        InitializeDistributions(start, TimeGrid, CalcLossLevels(CookedLossLevels), distribution_);
        startDateIndex = 0;
      }

      HullWhiteDynamicBasketModel.ComputeDistributions(
        false, startDateIndex, distribution_.NumDates(), Parameters,
        SurvivalCurves, Principals, RecoveryRates, distribution_);
      distributionComputed_ = true;

      if (timer != null)
      {
        timer.stop();
        logger.DebugFormat("Completed basket distribution in {0} seconds", timer.getElapsed());
      }

      return;
    }

    /// <summary>
    ///    Initialize distribution object
    /// </summary>
    /// <param name="start">start date</param>
    /// <param name="dates">date grid</param>
    /// <param name="levels">loss levels</param>
    /// <param name="distributions">distribution of losses</param>
    private void InitializeDistributions(
      Dt start, IList<Dt> dates, IList<double> levels, Curve2D distributions)
    {
      int nDates = dates.Count;
      int nLevels = levels.Count;

      distributions.Initialize(nDates, nLevels, 1);
      distributions.SetAsOf(start);
      for (int i = 0; i < nDates; ++i)
        distributions.SetDate(i, dates[i]);
      for (int i = 0; i < nLevels; ++i)
        distributions.SetLevel(i, levels[i]);

      return;
    }

    /// <summary>
    ///   Calculate loss levels as the ratio of the number of defaults
    ///   to the number of total names.
    /// </summary>
    private IList<double> CalcLossLevels(IList<double> lossLevels)
    {
      loadingFactor_ = (1 - DefaultedPrincipal / TotalPrincipal) / Count;

      double recoveryRate = this.AverageRecoveryRate;
      double factorLoss = (recoveryRate > (1 - 1.0E-12) ? 0.0 : 1 / (1 - recoveryRate));
      double factorAmor = (recoveryRate < 1.0E-12 ? 0.0 : 1 / recoveryRate);
      UniqueSequence<double> list = new UniqueSequence<double>();
      foreach (double loss in lossLevels)
      {
        double level = loss * factorLoss;
        if (level > 1.0)
          level = 1.0;
        list.Add(level);
        level = (1 - loss) * factorAmor;
        if (level > 1.0)
          level = 1.0;
        list.Add(level);
      }
      return list;
    }
    #endregion Methods

    #region Calibrators
    /// <summary>
    ///   Calculate the implied model parameters
    /// </summary>
    ///
    /// <param name="tranches">Array of CDO tranches</param>
    /// <param name="weights">weights on tranches</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="basket">A dynamic basket pricer</param>
    ///
    /// <returns>Dynamic parameters</returns>
    ///
    public static double[] ImpliedParameters(
      SyntheticCDO[] tranches,
      double[] weights,
      DiscountCurve discountCurve,
      BasketPricer basket
      )
    {
      // Important counts
      int nCDOs = tranches.GetLength(0);

      // Initialize variables
      basket = basket.Duplicate();
      double[] parameters = ((CorrelationTermStruct)basket.Correlation).Correlations;

      // Create pricers
      SyntheticCDOPricer[] pricers = new SyntheticCDOPricer[nCDOs];
      for (int i = 0; i < nCDOs; ++i)
      {
        SyntheticCDO cdo = (SyntheticCDO)tranches[i].Clone();
        pricers[i] = new SyntheticCDOPricer(cdo, basket, discountCurve);
      }

      // Setup the optimizers
      if (null == weights || 0 == weights.Length)
      {
        weights = new double[nCDOs];
        for (int i = 0; i < nCDOs; ++i)
          weights[i] = 1.0;
      }

      // Constructor a calibrator
      Optimizer calibrator = new Optimizer(pricers, parameters, weights);

      // Fit the CDO quotes
      double[] result = calibrator.Fit();

      return result;
    }

    //-
    // Helper class optimizers
    //-
    unsafe class Optimizer
    {
      public Optimizer(SyntheticCDOPricer[] pricers, double[] parameters, double[] weights)
      {
        pricers_ = pricers;
        parameters_ = parameters;
        weights_ = weights;
        basket_ = pricers[0].Basket;
        return;
      }

      private double
      Evaluate(double* x)
      {
        basket_.Reset();
        basket_.ResetCorrelation();
        parameters_[0] = x[0];
        parameters_[1] = x[1];
        parameters_[2] = x[2];

        double diff = 0.0;
        for (int i = 0; i < pricers_.Length; ++i)
        {
          // we use the infinite norm
          double pv = pricers_[i].Pv() / pricers_[i].Notional;
          //diff = Math.Max( Math.Abs( weights_[i] * pv ), diff );
          //diff += Math.Abs( weights_[i] * pv ) ;
          diff += weights_[i] * pv * pv;
        }
        return diff;
      }

      internal double[] Fit()
      {
        double[] initX = new double[]{
          parameters_[0], parameters_[1],parameters_[2]};

        // Set up optimizer
        int numFit = initX.Length;
        NelderMeadeSimplex opt = new NelderMeadeSimplex(numFit);
        opt.setInitialPoint(initX);

        double tolF_ = 1.0E-5;
        double tolX_ = 1.0E-3;
        opt.setToleranceF(tolF_);
        opt.setToleranceX(tolX_);
        opt.setLowerBounds(0.0);

        opt.setMaxEvaluations(8000);
        opt.setMaxIterations(3200);

        fn_ = new Double_Vector_Fn(this.Evaluate);
        objFn_ = new DelegateOptimizerFn(numFit, fn_, null);

        double* x = null;
        int repeat = 2;
        do
        {
          // Fit
          opt.minimize(objFn_);

          // Save results
          x = opt.getCurrentSolution();
          double f = Math.Sqrt(Evaluate(x));
          if (f < tolF_)
            repeat = 1;
        } while (--repeat > 0);


        double[] result = new double[numFit];
        for (int i = 0; i < numFit; ++i)
          result[i] = x[i];

        fn_ = null;
        objFn_ = null;

        return result;
      }

      private double[] parameters_;
      private BasketPricer basket_;
      private SyntheticCDOPricer[] pricers_;
      private double[] weights_;

      private DelegateOptimizerFn objFn_; // Here because of subtle issues re persistance of unmanaged delegates. RTD
      private Double_Vector_Fn fn_;
    };


    #endregion Calibrators

    #region Parameters to Dynamic Correlations
    /// <summary>
    ///   Create a correlation term structure from the dynamic paramters
    ///   <preliminary/>
    /// </summary>
    /// <param name="names">Endtity names</param>
    /// <param name="parameters">Dynamic parameters</param>
    /// <param name="start">Start date</param>
    /// <param name="end">End date</param>
    /// <returns>CorrelationTermStruct object</returns>
    public static CorrelationTermStruct CorrelationFromParameters(
      string[] names, double[] parameters, Dt start, Dt end)
    {
      const int NP = 3;
      if (parameters.Length != NP)
        throw new System.ArgumentException(String.Format(
          "HW parameters must be {0}", NP));

      Dt[] dates = new Dt[] { start, end };
      double [] data = new double[2*NP];
      data[0] = data[NP] = parameters[0];
      data[1] = data[NP+1] = parameters[1];
      data[2] = 0.0; data[NP + 2] = parameters[2] * Dt.FractDiff(start, end) / 365.0;
      return CorrelationTermStruct.Generalized(names, data, dates);
    }
    #endregion Parameters to Dynamic Correlations

    #region Properties
    /// <summary>
    ///   Dynamic parameters
    /// </summary>
    public double[] Parameters
    {
      get { return ((CorrelationTermStruct)Correlation).Correlations; }
      set { ((CorrelationTermStruct)Correlation).Correlations = value; }
    }

    /// <summary>
    ///   Jump size
    /// </summary>
    public double JumpSize
    {
      get { return Parameters[0]; }
      set { Parameters[0] = value; }
    }

    /// <summary>
    ///   Logarithm of the slope of jump size
    /// </summary>
    public double Beta
    {
      get { return Parameters[1]; }
      set { Parameters[1] = value; }
    }

    /// <summary>
    ///   Intensity of jump process
    /// </summary>
    public double Intensity
    {
      get { return Parameters[2]; }
      set { Parameters[2] = value; }
    }

    /// <summary>
    ///   Computed distribution for basket
    /// </summary>
    public Curve2D Distribution
    {
      get { return distribution_; }
      set { distribution_ = value; }
    }

    #endregion Properties

    #region Data
    // intermediate values, set by ComputeAndSaveDistribution()
    private Curve2D distribution_;
    private bool distributionComputed_;

    // intermediate values, set by CalcLossLevels()
    private double loadingFactor_;
    #endregion Data
  }

}
