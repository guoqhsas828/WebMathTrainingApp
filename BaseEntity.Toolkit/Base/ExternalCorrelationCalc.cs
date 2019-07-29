/*
 * ExternalCorrelationCalc.cs
 *
 * Calibrate external copula correlation
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.ComponentModel;
using System.Collections;
using System.Runtime.Serialization;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   A class for calibrating the parameters for external factor correlations
  /// </summary>
  ///
  /// <remarks>
  ///   Given an array of CDO tranches with consecutive detachment points and spread quotes inside them,
  ///   use this class to find the best fit of the three correlation parameters (factor, mean shift and
  ///   standard deviation) which simultaneously make the pvs of the tranches as close to zero as possible.
  ///   In this way it intends to fit both correlations and correlation smile by a single correlation object.
  /// </remarks>
	///
	public unsafe class ExternalCorrelationCalc : BaseEntityObject
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(ExternalCorrelationCalc));

    #region Constructors

		/// <summary>
		///   Constructor
		/// </summary>
    public
		ExternalCorrelationCalc()
		{
		  tolX_ = 1.0e-4;
			tolF_ = 1.0e-4;
			correlation_ = null;
			pricers_ = null;
			objFn_ = null;
			fn_ = null;
		}

		#endregion // Constructors

		#region Methods

		/// <summary>
		///   Calibrate correlation parameters
		/// </summary>
		///
		/// <remarks>
		///   <para>This function assumes the underlying credits have the same factors, the same mean shifts 
		///   and the same standard deviations.  It tries to find a set of these three parameters 
		///   which minimizes the weighted sum of the absolute values of pvs on the CDOs.</para>
		///
		///   <para>The arrays of parameters and returned values should be at least of length 3, corresponding
		///   to the values for factor, mean shifts and standard deviation, in that order.</para>
		///
		///   <para>The arrays <c>initialX</c> and <c>deltaX</c> should chosen such that the solution is most
		///   probably in the hyper-cubic region bracketed by (<c>initialX</c>, <c>initialX + deltaX</c>).</para>
		///
		///   <para>Either of the arrays <c>initialX</c>, <c>deltaX</c>, <c>lowerX</c> and <c>upperX</c> can be null,
		///   in which case the default values are used.</para>
		/// </remarks>
		///
		/// <param name="basket">Basket pricer</param>
		/// <param name="cdos">Synthetic CDO products</param>
		/// <param name="discountCurve">Discount curve</param>
		/// <param name="weights">Weights</param>
		/// <param name="initialX">initial guesses of parameters</param>
		/// <param name="deltaX">initial deltas</param>
		/// <param name="lowerX">lower bounds of parameters</param>
		/// <param name="upperX">upper bounds of parameters</param>
		///
		/// <returns>the minimizer</returns>
		///
		public double[]
		Calibrate(
            BasketPricer basket,
            SyntheticCDO [] cdos,
            DiscountCurve discountCurve,
            double[] weights,
            double[] initialX,
            double[] deltaX,
            double[] lowerX,
            double[] upperX
            )
		{
		  if( cdos.Length != weights.Length )
				throw new ToolkitException("cdos and weights should have the same length");
			
			if( null == initialX )
			{
				initialX = new double[3];
				initialX[0]  =	0.55; // factor
				initialX[1]  =	1.00; // mu
				initialX[2]  =	1.00; // sigma
			}
			if( null == deltaX )
			{
				deltaX = new double[3];
				deltaX[0]  =	0.10; // factor
				if( initialX[0] > 0.9 )
					deltaX[0] = -0.10;
				deltaX[1]  =	1.00; // mu
				deltaX[2]  =	0.10; // sigma
			}
			if( null == lowerX )
			{
				lowerX = new double[3];
				lowerX[0]  =  0.00; // lower bound: factor
				lowerX[1]  = -3.50; // lower bound: mu
				lowerX[2]  =  0.01; // lower bound: sigma
			}
			if( null == upperX )
			{
				upperX = new double[3];
				upperX[0] =	 0.99; // upper bound: factor
				upperX[1] =  3.50; // upper bound: mu
				upperX[2] = 10.00; // upper bound: sigma
			}

			basket = (BasketPricer) basket.Clone();
            int basketSize = basket.Count;
            if (basket.Correlation is ExternalFactorCorrelation)
                correlation_ = (ExternalFactorCorrelation)basket.Correlation;
            else if (basket.Correlation is CorrelationTermStruct)
                throw new System.NotImplementedException("Cannot convert CorrelationTermStruct to ExternalFactorCorrelation yet");
            else
			{
				correlation_ = new ExternalFactorCorrelation( ((Correlation)basket.Correlation).Names, new double[basketSize*3] );
				basket.Correlation = correlation_;
			}

			pricers_ = new SyntheticCDOPricer[ cdos.Length ];
			for( int i = 0; i < cdos.Length; ++i )
				pricers_[i] = new SyntheticCDOPricer( cdos[i], basket, discountCurve , 1.0, null);
			weights_ = weights;

			// Set up optimizer
			const int numFit = 3;
			NelderMeadeSimplex opt = new NelderMeadeSimplex(numFit);
			opt.setInitialPoint(initialX);
			opt.setInitialDeltaX(deltaX);
			opt.setLowerBounds(lowerX);
			opt.setUpperBounds(upperX);
			opt.setToleranceF(tolF_);
			opt.setToleranceX(tolX_);
			opt.setMaxEvaluations(8000);
			opt.setMaxIterations(3200);

			fn_ = new Double_Vector_Fn( this.Evaluate );
			objFn_ = new DelegateOptimizerFn( numFit, fn_, null);

			// Fit
			opt.minimize(objFn_);

			// Save results
			double* x = opt.getCurrentSolution();
			double[] result = new double[3]{ x[0], x[1], x[2] };

			fn_ = null;
			objFn_ = null;
			correlation_ = null;
			pricers_ = null;

			return result;
		}

		/// <summary>
		///   Calibrate correlation parameters
		/// </summary>
		///
		/// <remarks>
		///   <para>This function assumes the underlying credits have the same factors, the same mean shifts 
		///   and the same standard deviations.  It tries to find a set of these three parameters 
		///   which minimizes the weighted sum of the absolute values of pvs on the CDOs.</para>
		///
		///   <para>The arrays of parameters and returned values should be at least of length 3, corresponding
		///   to the values for factor, mean shifts and standard deviation, in that order.</para>
		///
		///   <para>The arrays <c>initialX</c> and <c>deltaX</c> should chosen such that the solution is most
		///   probably in the hyper-cubic region bracketed by (<c>initialX</c>, <c>initialX + deltaX</c>).</para>
		///
		///   <para>Either of the arrays <c>initialX</c>, <c>deltaX</c>, <c>lowerX</c> and <c>upperX</c> can be null,
		///   in which case the default values are used.</para>
		/// </remarks>
		///
		/// <param name="pricers">Synthetic CDO pricers</param>
		/// <param name="weights">Weights</param>
		/// <param name="initialX">initial guesses of parameters</param>
		/// <param name="deltaX">initial deltas</param>
		/// <param name="lowerX">lower bounds of parameters</param>
		/// <param name="upperX">upper bounds of parameters</param>
		///
		/// <returns>the minimizer</returns>
		///
		public double[]
		Calibrate(
            SyntheticCDOPricer [] pricers,
            double[] weights,
            double[] initialX,
            double[] deltaX,
            double[] lowerX,
            double[] upperX
            )
		{
		  if( pricers == null || pricers.Length == 0 )
				throw new ArgumentException( "pricers cannot be null or have zero length" );
		  if( pricers.Length != weights.Length )
				throw new ArgumentException("pricers and weights should have the same length");
			
			if( null == initialX )
			{
				initialX = new double[3];
				initialX[0]  =	0.55; // factor
				initialX[1]  =	1.00; // mu
				initialX[2]  =	1.00; // sigma
			}
			if( null == deltaX )
			{
				deltaX = new double[3];
				deltaX[0]  =	0.10; // factor
				if( initialX[0] > 0.9 )
					deltaX[0] = -0.10;
				deltaX[1]  =	1.00; // mu
				deltaX[2]  =	0.10; // sigma
			}
			if( null == lowerX )
			{
				lowerX = new double[3];
				lowerX[0]  =  0.00; // lower bound: factor
				lowerX[1]  = -3.50; // lower bound: mu
				lowerX[2]  =  0.01; // lower bound: sigma
			}
			if( null == upperX )
			{
				upperX = new double[3];
				upperX[0] =	 0.99; // upper bound: factor
				upperX[1] =  3.50; // upper bound: mu
				upperX[2] = 10.00; // upper bound: sigma
			}

			BasketPricer basket = (BasketPricer)( pricers[0].Basket.Clone() );
            int basketSize = basket.Count;
			if( basket.Correlation is ExternalFactorCorrelation )
				correlation_ = (ExternalFactorCorrelation) basket.Correlation ;
            else if (basket.Correlation is CorrelationTermStruct)
                throw new System.NotImplementedException("Cannot convert CorrelationTermStruct to ExternalFactorCorrelation yet");
            else
			{
				correlation_ = new ExternalFactorCorrelation( ((Correlation)basket.Correlation).Names, new double[basketSize*3] );
				basket.Correlation = correlation_;
			}

			pricers_ = new SyntheticCDOPricer[ pricers.Length ];
			for( int i = 0; i < pricers.Length; ++i )
				pricers_[i] = new SyntheticCDOPricer( pricers[i].CDO, basket, pricers[i].DiscountCurve , 1.0, pricers[i].RateResets);
			weights_ = weights;

			// Set up optimizer
			const int numFit = 3;
			NelderMeadeSimplex opt = new NelderMeadeSimplex(numFit);
			opt.setInitialPoint(initialX);
			opt.setInitialDeltaX(deltaX);
			opt.setLowerBounds(lowerX);
			opt.setUpperBounds(upperX);
			opt.setToleranceF(tolF_);
			opt.setToleranceX(tolX_);
			opt.setMaxEvaluations(8000);
			opt.setMaxIterations(3200);

			fn_ = new Double_Vector_Fn( this.Evaluate );
			objFn_ = new DelegateOptimizerFn( numFit, fn_, null);

			// Fit
			opt.minimize(objFn_);

			// Save results
			double* x = opt.getCurrentSolution();
			double[] result = new double[3]{ x[0], x[1], x[2] };

			fn_ = null;
			objFn_ = null;
			correlation_ = null;
			pricers_ = null;

			return result;
		}

		private double
		Evaluate( double* x )
		{
		  correlation_.SetParameters( x[0], x[1], x[2] );
			pricers_[0].Basket.Reset();
			double diff = 0;
			for( int i = 0; i < pricers_.Length; ++i )
			{
			  // we use the infinite norm
			  double pv = pricers_[i].ProductPv() ;
				//diff = Math.Max( Math.Abs( weights_[i] * pv ), diff );
				diff += Math.Abs( weights_[i] * pv ) ;
			}
			return diff;
		}

		#endregion // Methods

		#region Properties

		/// <summary>
		///  Tolerance of solutions
		/// </summary>
		public double ToleranceX
		{
		  get { return tolX_; }
			set {
			  if( value <= 0 )
					throw new ArgumentException("tolerance must be positive");
				tolX_ = value;
			}
		}

		/// <summary>
		///  Tolerance of function values
		/// </summary>
		public double ToleranceF
		{
		  get { return tolF_; }
			set {
			  if( value <= 0 )
					throw new ArgumentException("tolerance must be positive");
				tolF_ = value;
			}
		}

		#endregion // Properties

		#region Data

		private double tolX_, tolF_;

		private ExternalFactorCorrelation correlation_;
		private SyntheticCDOPricer[] pricers_;
		private double[] weights_;
		private DelegateOptimizerFn objFn_; // Here because of subtle issues re persistance of unmanaged delegates. RTD
		private Double_Vector_Fn fn_;

    #endregion // Data

	} // class ExternalCorrelationCalc

}
