/*
 * TrancheVolatilityCalc.cs
 *
 *  -2008. All rights reserved.
 *
 * Calculate implied tranche volatility
 *
 */
using System;

using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{
	///
	/// <summary>
	///   <para>Calculate implied volatility of <see cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO
	///   tranche </see></para>
	/// </summary>
	///
	/// <remarks>
	///   <para>This is a helper class to caculate the implied tranched volatility.  The implied
	///   tranche volatility is defined as the standard deviation of portfolio loss distribution
	///   which matches the expected loss on the tranche, assuming that the portfolio loss
	///   follows either normal or log-normal distribution.</para>
	///   
	///   <para>Formally, let <formula inline="true">X</formula> be a random variable representing
	///   the loss on the whole portfolio at CDO maturity date.  Let <formula inline="true">A</formula>
	///   and <formula inline="true">D</formula> be tranche attachment and detachment, respectively.
	///   Then the tranche loss is given by <formula inline="true">\min(\max(0,X-A),D-A)</formula>.
	///   Since the mean of <formula inline="true">X</formula> is known (and equals to the
	///   expected loss on the whole portfolio), what remains to be determined is the standard deviation
	///   of <formula inline="true">X</formula>.  The implied tranche volatility is the standard deviation
	///   which makes the following equation hold:</para>
	///   
	///   <para><formula>E[\min(\max(0,X-A),D-A)] = \mathrm{Expected\;Tranche\;Loss}</formula></para>
	///   
	///   <para>where the <c>Expected Tranche Loss</c> is calculated using our CDO pricer.</para>
	///   
	///   <para>The implied volatility calculated in this way provides an alternative measure of the risk
	///   of a tranche.  Under some circumstances, however, the actual distribution of portfolio
	///   loss may be too skewed to be represented by normal or log-normal distributions and the
	///   solution for the above equation may not exists for some tranches.  In these cases, NaN
	///   is returned.</para>
	/// </remarks>
	/// 
	/// <seealso cref="BaseEntity.Toolkit.Products.SyntheticCDO">Synthetic CDO Tranche Product</seealso>
	/// <seealso cref="BasketPricer">Basket Pricer</seealso>
	///
  [Serializable]
  public class TrancheVolatilityCalc : SolverFn
	{
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(TrancheVolatilityCalc));

		#region Constructors

		/// <summary>
		///   Constructor
		/// </summary>
		///
		public
		TrancheVolatilityCalc( SyntheticCDOPricer pricer )
		{
			pricer_ = pricer;
			BasketPricer basket = pricer.Basket;
			SyntheticCDO cdo = pricer.CDO;

			// We calculate total principal from tranche width and tranche notional
			double trancheWidth = cdo.Detachment - cdo.Attachment ;
			if( 1.0E-7 > trancheWidth )
				throw new ToolkitException( String.Format("Invalid cdo tranche {0} ~ {1}",
					cdo.Attachment, cdo.Detachment) );
			double totalPrincipal = Math.Abs(pricer.TotalPrincipal);

			attachment_ = cdo.Attachment * totalPrincipal;
			detachment_ = cdo.Detachment * totalPrincipal;
			mean_ = basket.BasketLoss( basket.Settle, cdo.Maturity ) * totalPrincipal;
			targetTrancheLoss_ = Math.Abs(pricer.LossToDate(cdo.Maturity));
		}

		#endregion // Constructors

		#region Methods

		/// <summary>
		///   Calculate the implied tranche volatility
		/// </summary>
		///
		/// <remarks>
		///   <para>Calculates the uniform one-factor correlation which implies a
		///   present value of zero for the tranche.</para>
		/// </remarks>
		///
		/// <param name="toleranceF">Relative accuracy of PV in calculation of implied correlations (or 0 for default)</param>
		/// <param name="toleranceX">The accuracy of implied correlations (or 0 for default)</param>
		///
		/// <returns>Implied volatility for tranche</returns>
		///
		public double
		ImpliedVolatility( double toleranceF, double toleranceX )
		{
			if( this.UseNormalDistribution ) 
			{
				logMA_ = ( attachment_ < 1.0E-7 ? Double.PositiveInfinity : Math.Log(mean_/attachment_) );
				logMD_ = Math.Log(mean_/detachment_);
			}

			// Set up root finder
			Brent rf = new Brent();

      if (toleranceF <= 0)
        toleranceF = 1.0 / Math.Abs(pricer_.TotalPrincipal);
			rf.setToleranceF( toleranceF );

			if (toleranceX <= 0) 
			{
				toleranceX = 100 * toleranceF;
				if (toleranceX > 0.0001)
					toleranceX = 0.0001;
			}
			rf.setToleranceX( toleranceX );

			rf.setLowerBounds(1E-10);

			// Solve
			double result = Double.NaN;
			result = rf.solve(this, targetTrancheLoss_, 0.5 );

			if( this.UseNormalDistribution ) 
			{
				result *= mean_ / (detachment_ - attachment_);
			}

			return result;
		}

		/// <summary>
		///  Core method providing target function values.
		/// </summary>
		/// <returns>evaluated objective function f(x)</returns>
		public override double evaluate(double x)
		{
			if( this.UseNormalDistribution )
				return calcTrancheLossNormal(x);
			else
				return calcTrancheLossLogNormal(x);
		}

		private double calcTrancheLossLogNormal(double s)
		{
			double h;
			double lossOverAttach = mean_;
			if( attachment_ > 1.0E-7 ) 
			{
				h = logMA_ / s;
				lossOverAttach = mean_ * Normal.cumulative(h + s/2, 0.0, 1.0)
					- attachment_ *  Normal.cumulative(h - s/2, 0.0, 1.0);
			}

			h = logMD_ / s;
			double lossOverDetach = mean_ * Normal.cumulative(h + s/2, 0.0, 1.0)
				- detachment_ *  Normal.cumulative(h - s/2, 0.0, 1.0);

			return lossOverAttach - lossOverDetach;
		}


        //
		// Private function for root find for tranche volatility.
		//   compute the expectatiom E[ min(max(X - A, 0), D - A) ]
		//   under the assumption that X is Gaussian
		//
		private double
		calcTrancheLossNormal(double s)
		{
			// the standard deviation
			double sigma = mean_ * s;

			// normalized detachment and probability
			double u = (detachment_ - mean_) / sigma;
			double Pu = Normal.cumulative(u, 0.0, 1.0);

			// integral from 0 to u, of x*exp(-x*x/2) dx
			double eu = 1 - Math.Exp(-u*u/2);
			if( u < 0 ) eu = -eu;

			// normalized attachment
			double l = (attachment_ - mean_) / sigma;
			double Pl = Normal.cumulative(l, 0.0, 1.0);

			// integral from 0 to l, of x*exp(-x*x/2) dx
			double el = 1 - Math.Exp(-l*l/2);
			if( l < 0 ) el = -el;

			// calculate E[X - A: A < X < D], where X ~ N( mean, sigma^2 )
			double y = (mean_ - attachment_) * (Pu - Pl) + sigma / Math.Sqrt(2 * Math.PI) * (eu - el);

			// calculate (D - A) * Prob(X >= D)
			y += (detachment_ - attachment_) * (1 - Pu);

			return y;
		}
        
		#endregion // Methods

		#region Properties

		/// <summary>
		///   Use normal distribution or log-normal distribution (default is log-normal)
		/// </summary>
		public bool UseNormalDistribution
		{
			get { return useNormalDistribution_; }
			set { useNormalDistribution_ = value; }
		}

		#endregion // Properties

		#region Data

		SyntheticCDOPricer pricer_;
		double attachment_;
		double detachment_;
		double mean_;
		double targetTrancheLoss_;

		bool useNormalDistribution_;
		double logMA_;
		double logMD_;
        
		#endregion // Data

	} // class TrancheVolatilityCalc
}
