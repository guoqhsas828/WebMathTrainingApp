/*
 * PassthruProduct.cs
 *
 *   2008. All rights reserved.
 *
 * Created by rsmulktis on 12/3/2008 11:02:54 AM
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using log4net;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Products
{
	/// <summary>
	/// Product used as a placeholder on a CalibratedCurve imported from a Curve. 
	/// </summary>
	/// 
	/// <remarks>
	/// This is a psuedo Product that exists only as part of a CalibratedCurve. The product is used 
	/// to pass values directly through calibration so that Curve instances can be converted easily 
	/// to CalibratedCurve instances and subsequently used in the Sensitivities framework.
	/// </remarks>
	/// 
	[Serializable]
	public class CurvePointHolder : Product
	{
		#region Data
		//logger
		private static ILog Log = LogManager.GetLogger(typeof(CurvePointHolder));

		// Data
		private double value_;
		#endregion

		#region Constructors
		/// <summary>
		/// Default Constructor
		/// </summary>
		public CurvePointHolder() : base()
		{
		}

		/// <summary>
		/// Constructor to initialize required values.
		/// </summary>
		/// 
		/// <param name="effective">The effective date of the product.</param>
		/// <param name="maturity">The maturity date of the product.</param>
		/// <param name="value">The value of the product.</param>
		/// 
		public CurvePointHolder(Dt effective, Dt maturity, double value) : base(effective, maturity)
		{
			value_ = value;
		}
		#endregion

		#region Properties
		/// <summary>
		/// The value to pass thru.
		/// </summary>
		public double Value
		{
			get { return value_; }
			set { value_ = value; }
		}
		#endregion
	}
}
