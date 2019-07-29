/*
 * CashflowPricerFactory.cs
 *
 */

using System;
using System.Collections;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Pricers
{

	///
  /// <summary>
	///   Helper class for Cashflow pricers.
	/// </summary>
	///
  public class CashflowPricerFactory : BaseEntityObject
  {

    /// <summary>
		///   Returns appropriate pricer given product.
		/// </summary>
		///
		/// <param name="product">Product to price</param>
		///
		/// <returns>new CashflowPricer matching given product</returns>
		///
    internal static ICashflowPricer
		PricerForProduct( IProduct product )
		{
			if( product is CDS )
			{
				return new CDSCashflowPricer((CDS)product);
			}
			else if( product is Bond )
			{
				return new BondPricer((Bond)product);
			}

			throw new ArgumentException("Product does not have a matching pricer");
		}

	} // class CashflowPricerFactory

}
