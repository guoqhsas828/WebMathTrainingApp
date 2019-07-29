/*
 * RateCurve.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using	System.Runtime.Serialization;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Calibrators;

namespace BaseEntity.Toolkit.Curves
{
  ///
	/// <summary>
	///   Rate curve
	/// </summary>
	///
	/// <remarks>
	///   Contains a term structure of rates. The interface is in terms
	///   of interest rates.
	/// </remarks>
	///
	[Serializable]
  public class RateCurve : CalibratedCurve
  {
		#region Constructors

		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <remarks>
		///   <para>The default interpolation is linear between rates.</para>
		/// </remarks>
		///
		/// <param name="asOf">As-of date</param>
		///
		public
		RateCurve(Dt asOf)
			: base(new SimpleCalibrator(asOf))
		{}

 	  #endregion // Constructors

		#region Methods

		/// <summary>
    ///   Get rate given date
    /// </summary>
		///
    /// <param name="date">Date to interpolate for</param>
		///
    /// <returns>Rate matching date</returns>
		///
		public double
		Rate(Dt date)
		{
			return Interpolate(date);
		}

		/// <summary>
		/// Adds a value to the curve.
		/// </summary>
		/// 
		/// <param name="date">The date to add.</param>
		/// <param name="rate">The rate to add.</param>
		/// 
		public void AddRate(Dt date, double rate)
		{
			CurvePointHolder pt = new CurvePointHolder(this.AsOf, date, rate);
			this.Tenors.Add(new CurveTenor(date.ToStr("%m-%y"), pt, 0));
		}

    #endregion // Methods
	} // class RateCurve

}
