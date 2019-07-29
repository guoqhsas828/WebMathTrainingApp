/*
 * RefixPeriod.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;

using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  ///
	/// <summary>
	///   Definition for an element of a CB refixing schedule.
	/// </summary>
	///
	/// <remarks>
	///   This class defines the refixing schedule of a Convertable bond.
	/// </remarks>
	///
  [Serializable]
  public class RefixPeriod : BaseEntityObject
  {
		//
		// Constructors/Destructors
		//

		/// <summary>
		///   Constructor
		/// </summary>
		protected RefixPeriod()
		{}


		/// <summary>
		///   Constructor
		/// </summary>
		///
    /// <param name="startDate">start of call period</param>
		/// <param name="restriction">Move restriction</param>
		/// <param name="cap">Cap on refixing strike</param>
		/// <param name="floor">Floor on refixing strike</param>
		/// <param name="multiplier">Multiplier</param>
		///
    public RefixPeriod(Dt startDate, string restriction, double cap, double floor, double multiplier)
		{
			// Use properties to get validation
			StartDate = startDate;
			Restriction = restriction;
			Cap = cap;
			Floor = floor;
			Multiplier = multiplier;
		}


		//
		// Methods
		//

		//
		// Properties
		//

		/// <summary>
    ///   Start of call period
    /// </summary>
		public Dt StartDate
		{
			get { return startDate_; }
			set {
				if( !value.IsValid() )
					throw new ArgumentException(String.Format("Invalid start date {0}", value));
				startDate_ = value;
			}
		}

    /// <summary>
    ///   Move restriction
    /// </summary>
		public string Restriction
		{
			get { return restriction_; }
			set {
				restriction_ = value;
			}
		}


    /// <summary>
    ///   Cap on refixing strike
    /// </summary>
		public double Cap
		{
			get { return cap_; }
			set {
				if( value <= 0.0 )
					throw new ArgumentException(String.Format("Invalid cap price {0}. Must be +ve", value));
				cap_ = value;
			}
		}


    /// <summary>
    ///   Floor on refixing strike
    /// </summary>
		public double Floor
		{
			get { return floor_; }
			set {
				if( value <= 0.0 )
					throw new ArgumentException(String.Format("Invalid floor price {0}. Must be +ve", value));
				floor_ = value;
			}
		}


		/// <summary>
		///   Multiplier
		/// </summary>
		public double Multiplier
		{
			get { return multiplier_; }
			set {
				multiplier_ = value;
			}
		}


		//
		// Data
		//

		private Dt startDate_;
		private string restriction_;
		private double cap_;
		private double floor_;
		private double multiplier_;

  } // RefixPeriod

}

