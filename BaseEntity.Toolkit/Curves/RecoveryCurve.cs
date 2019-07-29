/*
 * RecoveryCurve.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.ComponentModel;
using	System.Runtime.Serialization;

using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Curves
{

  /// <summary>
  ///   recovery curve jump state
  /// </summary>
  ///
  /// <remarks>
  ///   <para>All normal curves are marked as <c>NotRecovered</c>.
  ///
  ///   When a curve is marked
  ///   as <c>HasRecovered</c>, the recovery payment is assumed to happen before the pricing period, 
  ///   the payment associated with this curve is excluded and appropriate adjustments are
  ///   made accordingly.
  ///
  ///   If a curve is marked as <c>WillRecover</c>, then the recovery payment is assumed
  ///   to happen exactly at the begining of the pricing period and the future payment
  ///   is included in pricing.
  ///   </para>
  /// </remarks>
  public enum Recovered
  {
    /// <summary>No recovery</summary>
    NotRecovered,
    /// <summary>Has paid recovery</summary>
    HasRecovered,
    /// <summary>Will pay recovery</summary>
    WillRecover
  }


  ///
	/// <summary>
	///   Recovery curve
	/// </summary>
	///
	/// <remarks>
	///   A recovery curve contains a term structure of recovery
	///   rates.
	/// </remarks>
	///
  [Serializable]
  public class RecoveryCurve : Curve
  {
		#region Constructors

		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <remarks>
		///   <para>The default interpolation is linear between recovery
		///   rates</para>
		///
		///   <para>The default recovery type is percentage of face</para>
		///
		///   <para>The default recovery dispersion (beta) is 0</para>
		/// </remarks>
		///
		/// <param name="asOf">As-of date</param>
		///
		public
		RecoveryCurve(Dt asOf)
			: base( asOf )
		{
			recoveryType_ = RecoveryType.Face;
			recoveryDispersion_ = 0.0;
		}

		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <remarks>
		///   <para>The default interpolation is linear between recovery
		///   rates</para>
		///
		///   <para>The default recovery type is percentage of face</para>
		///
		///   <para>The default recovery dispersion (beta) is 0</para>
		/// </remarks>
		///
		/// <param name="asOf">As-of date</param>
		/// <param name="recoveryType">Recovery rate type</param>
		/// <param name="recoveryDispersion">Recovery dispersion assuming a beta distribution</param>
		///
		public
		RecoveryCurve(Dt asOf, RecoveryType recoveryType, double recoveryDispersion )
			: base( asOf )
		{
			RecoveryType = recoveryType;
			RecoveryDispersion = recoveryDispersion;
		}


		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <remarks>
		///   <para>Constructs a recovery curve with a single flat recovery rate.</para>
		///
		///   <para>The default recovery type is percentage of face</para>
		///
		///   <para>The default recovery dispersion (beta) is 0</para>
		/// </remarks>
		///
		/// <param name="asOf">As-of date</param>
		/// <param name="recoveryRate">RecoveryRate</param>
		///
		public
		RecoveryCurve(Dt asOf, double recoveryRate)
			: base( asOf, recoveryRate )
		{
			recoveryType_ = RecoveryType.Face;
			recoveryDispersion_ = 0.0;
		}

 	  #endregion // Constructors

		#region Methods

		/// <summary>
    ///   Get recovery rate given date
    /// </summary>
		///
    /// <param name="date">Date to interpolate for</param>
		///
    /// <returns>Recovery rate matching date</returns>
		///
		public double
		RecoveryRate(Dt date)
		{
			return ( (date < this.AsOf) ? Interpolate(this.AsOf): Interpolate(date) );
		}

    /// <summary>
    ///   Copy all data from another curve
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This function copies recovery dispersion and type
    ///   in addition to Curve.Set().
    ///   </para>
    /// </remarks>
    ///
    /// <param name="curve">Curve to copy</param>
    ///
    /// <exclude />
    public void
    Copy(RecoveryCurve curve)
    {
      base.Set(curve);
      this.Spread = curve.Spread;
      recoveryDispersion_ = curve.recoveryDispersion_;
      recoveryType_ = curve.recoveryType_;
      // Also copy jump date
      JumpDate = curve.DefaultSettlementDate;
    }

    #endregion // Methods

		#region Properties

    /// <summary>
		///   Recovery rate type
		/// </summary>
		[Category("Base")]
    public RecoveryType RecoveryType
    {
      get { return recoveryType_; }
      set { recoveryType_ = value; }
    }


    /// <summary>
		///   Recovery dispersion assuming a beta distribution
		/// </summary>
		[Category("Base")]
    public double RecoveryDispersion
    {
      get { return recoveryDispersion_; }
      set {
        if( value < 0 )
          throw new ArgumentException(String.Format("Invalid recovery dispersion. Must be +ve, not {0}", value));
        recoveryDispersion_ = value;
      }
    }

    /// <summary>
    ///   Recovery settle date
    /// </summary>
    [Category("Base")]
    public Dt DefaultSettlementDate
    {
      get { return JumpDate; }
    }

    /// <summary>
    ///   Recovery state of this survival curve
    /// </summary>
    [Category("Base")]
    public Recovered Recovered
    {
      get
      {
        if (JumpDate.IsEmpty())
        {
          return Recovered.NotRecovered;
        }
        else if (JumpDate <= AsOf)
        {
          return Recovered.HasRecovered;
        }
        else
        {
          return Recovered.WillRecover;
        }
      }
    }
    

    #endregion // Properties

		#region Data

		private RecoveryType recoveryType_;
		private double recoveryDispersion_;

    #endregion // Data

	} // class RecoveryCurve

}
