/*
 * SoftCallTriggerPeriod.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using BaseEntity.Metadata;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{

	///
  /// <summary>
	///   Definition for an element of a soft call trigger object.
	/// </summary>
	///
  [Serializable]
  public class SoftCallTriggerPeriod : BaseEntityObject
  {
		#region Constructors

    /// <summary>
		///   Constructor
		/// </summary>
		protected SoftCallTriggerPeriod()
		{}

		/// <summary>
		///   Constructor
		/// </summary>
		///
    /// <param name="endDate">end of call period</param>
    /// <param name="trigger">Call protect trigger</param>
		///
    public
		SoftCallTriggerPeriod(Dt endDate, double trigger)
		{	
			endDate_ = endDate;
			trigger_ = trigger;
		}

		#endregion Constructors

 
		#region Properties

    /// <summary>
    ///   End of call period
    /// </summary>
    [DtProperty]
    public Dt EndDate
		{
			get { return endDate_; }
		}
    
    
    /// <summary>
    ///   Trigger price
    /// </summary>
    [NumericProperty]
    public double Trigger
		{
			get { return trigger_; }
		}
    


		#endregion Properties
		#region Data

    private readonly Dt endDate_;
    private readonly double trigger_ = 1;

		#endregion Data

  } // CallPeriod
} 
