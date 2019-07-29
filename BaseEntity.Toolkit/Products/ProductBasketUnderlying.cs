/*
 * ProductBasketUnderlying.cs
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
	///   Underlying component of a basket
	/// </summary>
	///
  [Serializable]
  [ReadOnly(true)]
	public class ProductBasketUnderlying : BaseEntityObject
	{
		#region Constructors

		/// <summary>
		///   constructor
		/// </summary>
		///
		/// <remarks>
		///   Price is not set
		/// </remarks>
		///
		/// <param name="product">Product</param>
		/// <param name="percentage">Percentage of the total Notional amount</param>
		///
    public
		ProductBasketUnderlying(IProduct product, double percentage)
		{
			// Properties for validation
			product_ = product;
			Percentage = percentage;
		}


		/// <summary>
		///   constructor
		/// </summary>
		///
		/// <param name="product">Product</param>
		/// <param name="percentage">Percentage of the total Notional amount</param>
		/// <param name="price">Price</param>
		///
    public
		ProductBasketUnderlying(IProduct product, double percentage, double price)
		{
			// Properties for validation
			product_ = product;
			Percentage = percentage;
			Price = price;
		}


		/// <summary>
		///   Clone
		/// </summary>
		public override object
		Clone()
		{
			ProductBasketUnderlying obj = (ProductBasketUnderlying)base.Clone();

			obj.product_ = (IProduct)product_.Clone();

			return obj;
		}

		#endregion Constructors

		#region Properties

		/// <summary>
		///   Product
		/// </summary>
		public IProduct Product
		{
			get { return product_; }
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


    /// <summary>
    ///   Price for element of a basket
    /// </summary>
		public double Price
		{
			get { return price_; }
			set { price_ = value; }
		}

		#endregion Properties

		#region Data

		private IProduct product_;
		private double percentage_;
		private double price_;

    #endregion Data

	} // ProductBasketUnderlying

}
