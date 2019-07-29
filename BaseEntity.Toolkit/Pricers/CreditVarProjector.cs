/*
 * CreditVarProjector.cs
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
using BaseEntity.Toolkit.Numerics.Rng;

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
	public class CreditVarProjector : BaseEntityObject
  {
		// Logger
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(CreditVarProjector));

    #region Constructors
		/// <summary>
		///   Constructor with default seed
		/// </summary>
		protected CreditVarProjector()
			: base()
		{
		  rng_ = null;
		  means_ = null;
		  stdevs_ = null;
		}
		
		/// <summary>
		///   Constructor
		/// </summary>
		/// <param name="means">means</param>
		/// <param name="stdevs">standard deviations</param>
		/// <param name="correlation">correlation matrix</param>
		public CreditVarProjector( double [] means,
															 double [] stdevs,
															 double [,] correlation )
			: base()
		{
			if( means.Length != stdevs.Length )
				throw new ArgumentException(String.Format("means (Length:{0}) and standard deviations (Length:{1} not match", means.Length, stdevs.Length) );
		  rng_ = new MultiNormalRng( correlation );
		  means_ = means;
			stdevs_ = stdevs;
		}
    #endregion Constructors

    #region Methods
		/// <summary>
		///   Calculate the projected variables
		/// </summary>
		public double [] Draw( )
		{
		  double [] x = new double[ means_.Length ];
			rng_.Draw( x );
			for( int i = 0; i < x.Length; ++i )
				x[i] = means_[i] + stdevs_[i] * x[i];
			return x;
		}

		/// <summary>
		///   Calculate the projected variables
		/// </summary>
		/// <param name="x">Array to receive the variables</param>
		public void Draw( double [] x )
		{
		  if( x.Length < means_.Length )
				throw new ArgumentException(String.Format("length of x not match generator dimension {1}", x, means_.Length));
			rng_.Draw( x );
			int N = means_.Length;
			for( int i = 0; i < N; ++i )
				x[i] = means_[i] + stdevs_[i] * x[i];
		}		
    #endregion Methods

		#region Properties
    /// <summary>
    ///   Number of variables (read only)
    /// </summary>
		public int Count
		{
		  get { return means_.Length; }
		}
 
		/// <summary>
		///   The core generator
		/// </summary>
		public RandomNumberGenerator CoreGenerator
		{
		  get { return rng_.CoreGenerator; }
			set { rng_.CoreGenerator = value; }
		}

		/// <summary>
		///   Seed of random number generator
		/// </summary>
		public uint Seed
		{
		  get { return rng_.Seed; }
			set { rng_.Seed = value; }
		}
		#endregion Properties

		#region Data
		private MultiNormalRng rng_; // decomposed correlation matrix
		private double [] means_;
		private double [] stdevs_;
		#endregion Data
	};

}
