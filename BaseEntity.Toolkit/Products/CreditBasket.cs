/*
 * CreditBasket.cs
 *
 */

using System;
using System.ComponentModel;
using System.Collections;

using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Products
{
  ///
	/// <summary>
	///   Basket of credits for a product
	/// </summary>
	///
	/// <remarks>
	///   <para>This represents a basket of reference credits for a
	///   credit basket product.</para>
	///
	///   <para>Note that there is a separation here between the actual
	///   definition and terms of the basket and this CreditBasket. Here
	///   we are only concerned with the reference credits from a pricing
	///   perspective. Each credit will reflect the relevant reference
	///   asset(s) defined in the basket.</para>
	/// </remarks>
	///
  [Serializable]
  [ReadOnly(true)]
	public class CreditBasket : BaseEntityObject, IEnumerable
	{
		#region Constructors

		/// <summary>
		///   constructor
		/// </summary>
    public
		CreditBasket()
		{
			underlyings_ = new ArrayList();
		}


		/// <summary>
		///   constructor
		/// </summary>
		///
		/// <param name="name">Name of basket</param>
		/// <param name="description">Description of basket</param>
		///
    public
		CreditBasket( string name, string description )
		{
			// Properties for validation
			Name = name;
			Description = description;
			underlyings_ = new ArrayList();
		}


		/// <summary>
		///   Clone
		/// </summary>
		public override object
		Clone()
		{
			CreditBasket obj = (CreditBasket)base.Clone();

			obj.underlyings_ = new ArrayList();
			foreach( CreditBasketUnderlying u in underlyings_ )
			{
				obj.underlyings_.Add(u.Clone());
			}

			return obj;
		}

		#endregion // Constructors

		#region Methods

    /// <summary>
    ///   Return IEnumerator for basket
    /// </summary>
    public IEnumerator
    GetEnumerator()
    {
      return underlyings_.GetEnumerator();
    }
    
		/// <summary>
		///   Add a reference entity to this basket
		/// </summary>
		///
		/// <param name="cbu">Index constituent</param>
		///
		public void
		Add(CreditBasketUnderlying cbu)
		{
			underlyings_.Add(cbu);
		}
    
		/// <summary>
    ///   CreditBasket underlyings
    /// </summary>
    public void
    Clear()
    {
      underlyings_.Clear();
    }

		#endregion Methods

		#region Properties

    /// <summary>
    ///   Name of basket
    /// </summary>
		public string Name
		{
			get { return name_; }
			set { name_ = value; }
		}


    /// <summary>
    ///   Description of basket
    /// </summary>
		public string Description
		{
			get { return description_; }
			set { description_ = value; }
		}


    /// <summary>
    ///   Basket underlyings
    /// </summary>
    public IList Underlyings
    {
      get { return underlyings_; }
      set { underlyings_ = value; }
    }


    /// <summary>
    ///   Number of underlying products in basket
    /// </summary>
		[Category("Base")]
		public int Count
		{
			get { return underlyings_.Count; }
		}


    /// <summary>
    ///  Get underlying by index
    /// </summary>
    public CreditBasketUnderlying
    this[ int index ]
    {
      get { return (CreditBasketUnderlying)underlyings_[index]; }
    }

		#endregion // Properties

		#region Data

		private string name_;
    private string description_;
		private IList underlyings_;

		#endregion // Data

	} // CreditBasket

}
