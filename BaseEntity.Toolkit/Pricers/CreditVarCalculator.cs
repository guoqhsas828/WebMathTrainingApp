/*
 * CreditVarCalculator.cs
 *
 * A class defines projector
 *
 *
 */
using System;
using System.ComponentModel;
using System.Collections;
using System.Runtime.Serialization;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Numerics
{
	/// <summary>
	///   Projector of scenarios for credit VaR
	/// </summary>
	///
	/// <remarks>
	///   This is a simple, experimental example.
	/// </remarks>
	///
  [Serializable]
	public class CreditVarCalculator : BaseEntityObject
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(CreditVarCalculator));

    #region Constructors
		/// <summary>
		///   Constructor with default seed
		/// </summary>
		protected CreditVarCalculator()
			: base()
		{
		  proj_ = null;
		  eval_ = null;
			values_ = null;
		}
		
		/// <summary>
		///   Constructor
		/// </summary>
		/// <param name="proj">Scenario projector</param>
		/// <param name="eval">Portfolio evaluator</param>
		/// <param name="scenarios">Number of simulation scenarios</param>
		public CreditVarCalculator( CreditVarProjector proj,
																CreditVarEvaluator eval,
																int scenarios )
			: base()
		{
		  proj_ = proj;
		  eval_ = eval;
			numScenarios_ = scenarios;
			values_ = null;
		}
    #endregion Constructors

    #region Methods
		/// <summary>
		///   Calculate the value at a given percentile
		/// </summary>
		/// <param name="percentile">Percentile</param>
		public double ValueAt( double percentile )
		{ 
		  if( values_ == null )
				Simulate( numScenarios_ );

			double quantile = percentile * numScenarios_;
			int low = (int)( quantile );
			if( low >= numScenarios_) low = numScenarios_ - 1;
			int high = (low < quantile ? low + 1 : low);
			if( high >= numScenarios_) high = numScenarios_ - 1;

			double val = 0.5 * ((double)values_[low] + (double)values_[high]);
			return val;
		}

		/// <summary>
		///   Simulate the distribution of portfolio values
		/// </summary>
		/// <remarks>
		///   Brutal force Monte Carlo, to be improved.
		/// </remarks>
		private void Simulate( int scenarios )
		{
		  values_ = new ArrayList();
			double [] x = new double[ proj_.Count ];
			for( int run = 0; run < scenarios; ++run )
			{
				proj_.Draw( x );
				double val = eval_.Evaluate( x );
				int pos = values_.BinarySearch(val);
				if( pos < 0 )
					pos = ~pos;
				values_.Insert(pos, val);
			}
			return ;
		}
    #endregion Methods

		#region Properties
    /// <summary>
    ///   Projector (read only)
    /// </summary>
    public CreditVarProjector Projector
    {
      get { return proj_; }
    }

    /// <summary>
    ///   Evaluator (read only)
    /// </summary>
    public CreditVarEvaluator Evaluator
    {
      get { return eval_; }
    }
		#endregion Properties

		#region Data
		private CreditVarProjector proj_;
		private CreditVarEvaluator eval_;
		private ArrayList values_;
		private int numScenarios_;
		#endregion Data
	};

}
