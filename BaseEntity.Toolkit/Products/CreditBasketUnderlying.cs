/*
 * CreditBasketUnderlying.cs
 *
 *
 */

using System;
using System.ComponentModel;

using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Products
{
  ///
	/// <summary>
	///   Underlying component of a credit basket
	/// </summary>
	///
  [Serializable]
  [ReadOnly(true)]
	public class CreditBasketUnderlying : BaseEntityObject
	{
		#region Constructors

    /// <summary>
    ///   constructor
    /// </summary>
    protected CreditBasketUnderlying() 
    {
    }

		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="referenceEntity">Reference Entity</param>
		/// <param name="percentage">Percentage of the total Notional amount</param>
		///
    public
		CreditBasketUnderlying(string referenceEntity, double percentage)
		{
			ReferenceEntity = referenceEntity;
			Percentage = percentage;
		}

		/// <summary>
		///   Clone
		/// </summary>
		public override object
		Clone()
		{
			return (CreditBasketUnderlying)base.Clone();
		}

		#endregion Constructors

		#region Properties

		/// <summary>
		///   Reference entity name
		/// </summary>
		///
		public string ReferenceEntity
		{
			get { return referenceEntity_; }
			set { referenceEntity_ = value; }
		}

		/// <summary>
    ///   Percentage of the full Notional amount for this element
    /// </summary>
		///
		public double	Percentage
		{
			get { return percentage_; }
			set { percentage_ = value; }
		}

		#endregion Properties

		#region Data

    private string referenceEntity_;
    private double percentage_;

    #endregion Data

	} // CreditBasketUnderlying
}
