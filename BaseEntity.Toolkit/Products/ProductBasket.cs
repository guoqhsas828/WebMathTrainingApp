/*
 * ProductBasket.cs
 *
 *
 */

using System;
using System.ComponentModel;
using System.Collections;
using System.Runtime.Serialization;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  ///
  /// <summary>
  ///   Basket of underlying assets for a product.
  /// </summary>
  ///
  /// <remarks>
  ///   This represents a basket of products for reference
  ///   in a basket-related product.
  /// </remarks>
  ///
  [Serializable]
  [ReadOnly(true)]
  public class ProductBasket : BaseEntityObject, IEnumerable
  {
    #region Constructors

    /// <summary>
    ///   constructor
    /// </summary>
    protected
    ProductBasket()
    {
      underlyings_ = new ArrayList();
    }


    /// <summary>
    ///   Clone
    /// </summary>
    public override object
    Clone()
    {
      ProductBasket obj = (ProductBasket)base.Clone();

      obj.underlyings_ = new ArrayList();
      foreach (ProductBasketUnderlying u in underlyings_)
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
    ///   Add underlying to basket
    /// </summary>
    ///
    /// <param name="product">Product</param>
    /// <param name="percentage">Percentage of the total Notional amount</param>
    ///
    public void
    Add(IProduct product, double percentage)
    {
      ProductBasketUnderlying u = new ProductBasketUnderlying(product, percentage);
      underlyings_.Add(u);
    }
    
    /// <summary>
    ///   Add underlying to basket
    /// </summary>
    ///
    /// <param name="product">Product</param>
    /// <param name="percentage">Percentage of the total Notional amount</param>
    /// <param name="price">Price</param>
    ///
    public void
    Add(IProduct product, double percentage, double price)
    {
      ProductBasketUnderlying u = new ProductBasketUnderlying(product, percentage, price);
      underlyings_.Add(u);
    }
    
    /// <summary>
    ///   ProductBasket underlyings
    /// </summary>
    public void
    Clear()
    {
      underlyings_.Clear();
    }
    
    /// <summary>
    ///   Validate basket
    /// </summary>
    ///
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      foreach (ProductBasketUnderlying u in this)
      {
        u.Product.Validate(errors);
      }

      return;
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
    ///   Total notional for the whole basket
    /// </summary>
    [Category("Base")]
    public double Notional
    {
      get { return notional_; }
      set { notional_ = value; }
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
    public ProductBasketUnderlying
    this[int index]
    {
      get { return (ProductBasketUnderlying)underlyings_[index]; }
    }

    #endregion // Properties

    #region Data

    private string name_;
    private string description_;
    private double notional_;
    private ArrayList underlyings_;

    #endregion // Data

  } // ProductBasket

}
