/*
 * CreditVarEvaluator.cs
 *
 *
 */

using System;
using System.ComponentModel;
using System.Collections;
using System.Runtime.Serialization;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Numerics
{
	/// <summary>
	///   Evaluator of portfolio values for credit VaR
	/// </summary>
	///
	/// <remarks>
	///   This is a simple, experimental example.
	/// </remarks>
	///
  [Serializable]
	public class CreditVarEvaluator : BaseEntityObject
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(CreditVarEvaluator));

    #region Constructors
		/// <summary>
		///   Constructor with default seed
		/// </summary>
		protected CreditVarEvaluator()
			: base()
		{
		}
		
		/// <summary>
		///   Constructor
		/// </summary>
		/// <param name="pricers">The portfolio represented by pricers</param>
		/// <param name="curves">Credit curves to perturb</param>
		/// <param name="tenors">Ternors to perturb</param>
		public CreditVarEvaluator( IPricer [] pricers,
															 CalibratedCurve [] curves,
															 string [] tenors )
			: base()
		{
		  pricers_ = pricers;
		  curves_ = curves;
			tenors_ = tenors;
		}
    #endregion Constructors

    #region Methods
		/// <summary>
		///   Evaluate the value of the portfolio
		/// </summary>
		/// <remarks>
		///   In this simple example, we assume the inputs are the relative bump sizes.
		/// </remarks>
		/// <param name="inputs">Inputs</param>
		public double Evaluate( double [] inputs )
		{
		  double result = 0;

			// Bump the curves according to the new spreads
			CurveBump( curves_, tenors_, inputs, true, true, true);

			// First reset the pricers
			for( int i = 0; i < pricers_.Length; ++i )
  		  (pricers_[i]).Reset();

			// Then calculate the value of portfolio
			for( int i = 0; i < pricers_.Length; ++i )
				result += pricers_[i].Pv();

			// Reset the curve back to the original states
			CurveBump( curves_, tenors_, inputs, false, true, false);

			return result;
		}

		/// <summary>
		///   Restore initial states of the curves
		/// </summary>
		public void Restore()
		{
			CurveBump( curves_, tenors_, new double[curves_.Length], true, true, true);
		}


		private static void CurveBump( CalibratedCurve [] curves, string [] tenors, double [] bumpUnits, bool up, bool bumpRelative, bool refit)
		{
			// Validate
			if( curves == null || curves.Length == 0 )
				throw new ArgumentException( "No curves specifed to bump" );
			if( bumpUnits == null || bumpUnits.Length == 0 )
				throw new ArgumentOutOfRangeException( "bumpUnits", "No bump units specifed" );
			if( bumpUnits.Length != curves.Length )
				throw new ArgumentOutOfRangeException( "bumpUnits", "number of bumps should be match the number of curves" );

      BumpFlags flags = bumpRelative ? BumpFlags.BumpRelative : 0;
      if (!up) flags |= BumpFlags.BumpDown;

      for (int j = 0; j < curves.Length; j++)
			{
			  int minTenor = 100;   // First tenor bumped

				if( tenors == null || tenors.Length == 0 )
				{
					// Bump all tenor point(s)
				  minTenor = 0;
					foreach( CurveTenor t in curves[j].Tenors )
					{
						double a = t.BumpQuote(bumpUnits[j], flags);
					}
				}
				else
				{
					for( int i = 0; i < tenors.Length; i++ )
					{
						int idx = curves[j].Tenors.Index(tenors[i]);
						if( idx >= 0 )
						{
							if( idx < minTenor )
								minTenor = idx;
							double a = curves[j].Tenors[idx].BumpQuote(bumpUnits[j], flags);
						}
					}
				}

				// Refit curve
				if( refit )
					curves[j].ReFit( minTenor );
			}

			return ;
		}
    #endregion Methods

		#region Data
		private string [] tenors_;
		private CalibratedCurve [] curves_;
		private IPricer [] pricers_;
		#endregion Data
	};

}
