/*
 * CurveTenor.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.ComponentModel;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves.TenorQuoteHandlers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Products.StandardProductTerms;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves
{
  ///
  /// <summary>
  ///   Single financial product observable for curve calibration.
  /// </summary>
  ///
  /// <remarks>
  ///   <para>Used by the calibrated curves to represent a single market
  ///   observable to calibrate to.</para>
  ///
  ///   <para>CurveTenors contain the product and market information to
  ///   value the product.</para>
  /// </remarks>
  ///
  [Serializable]
  [ReadOnly(true)]
  [TypeConverter(typeof(ExpandableObjectConverter))]
  [DebuggerDisplay("{CurveDate} {QuoteKey}")]
  public class CurveTenor : BaseEntityObject, IComparable
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Default floating rate coupon and financing spread are 0.0.</para>
    ///   <para>Default Weight is 1.</para>
    /// </remarks>
    ///
    /// <param name="name">Name to identify this curve tenor (tenor)</param>
    /// <param name="product">Product to price for this curve tenor</param>
    /// <param name="marketPv">Market (observed) full price</param>
    ///
    public CurveTenor(string name, IProduct product, double marketPv)
      : this(name, product, marketPv, 0.0, 0.0, 1.0, null)
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Default floating rate coupon and financing spread are 0.0.</para>
    ///   <para>Default Weight is 1.</para>
    /// </remarks>
    ///
    /// <param name="name">Name to identify this curve tenor</param>
    /// <param name="product">Product to price for this curve tenor</param>
    /// <param name="marketPv">Market (observed) full price</param>
    /// <param name="coupon">Current coupon for floating rate securities (if necessary)</param>
    /// <param name="finSpread">Individual financing spread for this tenor</param>
    /// <param name="weight">Weighting for product</param>
    ///
    public CurveTenor(string name, IProduct product, double marketPv,
      double coupon, double finSpread, double weight)
      : this(name,product,marketPv,coupon,finSpread,weight,null)
    {
    }

    public CurveTenor(string name, IProduct product, double marketPv,
      double coupon, double finSpread, double weight,
      ICurveTenorQuoteHandler quoteHandler)
    {
      // Save data, use properties to enforce validation
      this.name_ = name;
      this.product_ = product;
      this.MarketPv = marketPv;
      this.Weight = weight;
      this.ModelPv = 0.0;
      if (quoteHandler != null) quoteHandler_ = quoteHandler;
      if (product != null)
        this.originalQuote_ = quoteHandler_.GetCurrentQuote(this);
    }

    public CurveTenor(TenorQuote q, double weight) : this(q, weight, 0.0, null) 
    {  
    }

    public CurveTenor(TenorQuote q, double weight, double marketPv) : this(q, weight, marketPv, null)
    {
    }

    public CurveTenor(TenorQuote q, double weight, double marketPv, ICurveTenorQuoteHandler quoteHandler)
    {
      // Save data, use properties to enforce validation
      Weight = weight;
      originalQuote_ = q.Quote;
      quoteHandler_ = quoteHandler ?? CurveTenorQuoteHandlers.DefaultHandler;
      _q = q;
      if (q.Terms != null || !string.IsNullOrEmpty(q.Tenor))
        name_ = q.GetName();
      MarketPv = marketPv;
    }

    ///
    /// <summary>
    ///   Clone
    /// </summary>
    public override object
    Clone()
    {
      var obj = (CurveTenor)base.Clone();
      if (product_ != null) obj.product_ = (IProduct)product_.Clone();
      obj.quoteHandler_ = (ICurveTenorQuoteHandler)quoteHandler_.Clone();
      return obj;
    }

    #endregion // Constructors

    #region Properties

    /// <summary>
    ///   Name of tenor
    /// </summary>
    public string Name
    {
      get { return name_; }
      set { name_ = value; }
    }

    /// <summary>
    ///   Market present value (full price) to match
    /// </summary>
    public double MarketPv
    {
      get { return marketPv_; }
      // Brief change to accommodate the upfront fees for qSurvivalFitCDSEx
      set { marketPv_ = value; }
    }

    /// <summary>
    ///   Weighting for product
    /// </summary>
    public double Weight
    {
      get { return weight_; }
      private set
      {
        if (value < 0.0 || value > 1.0)
          throw new ArgumentException(String.Format("Invalid weight {0}. Must be between 0 and 1", value));
        weight_ = value;
      }
    }

    /// <summary>
    ///   Get the current tenor quote
    ///  <prelimninary/>
    /// </summary>
    /// <exclude />
    public IMarketQuote CurrentQuote
    {
      get { return quoteHandler_.GetCurrentQuote(this); }
    }

    /// <summary>
    ///   Original quote
    ///  <prelimninary/>
    /// </summary>
    /// <exclude />
    public IMarketQuote OriginalQuote
    {
      get { return originalQuote_; }
      set { originalQuote_ = value; }
    }

    #region Properties built on-the-fly by calibrators

    /// <summary>
    ///   Calculated model (full) price
    /// </summary>
    public double ModelPv
    {
      get { return modelPv_; }
      set { modelPv_ = value; }
    }

    /// <summary>
    ///   Product
    /// </summary>
    public IProduct Product
    {
      get { return product_; }
      set { product_ = value; }
    }

    /// <summary>
    ///   Maturity
    /// </summary>
    public Dt Maturity
    {
      get { return product_ == null ? Dt.Empty : product_.Maturity; }
    }

    /// <summary>
    /// Access public curve date
    /// </summary>
    public Dt CurveDate
    {
      get { return curveDate_.IsEmpty() ? Maturity : curveDate_; }
      set { curveDate_ = value; }
    }

    #endregion

    #region public properties

    /// <summary>
    /// Gets or sets the quote handler.
    /// </summary>
    /// <value>The quote handler.</value>
    public ICurveTenorQuoteHandler QuoteHandler
    {
      get { return quoteHandler_; }
      set { quoteHandler_ = value; }
    }

    /// <summary>
    /// Gets the product terms
    /// </summary>
    /// <value>The product terms</value>
    public IStandardProductTerms ProductTerms
    {
      get { return _q.Terms; }
    }

    /// <summary>
    /// Gets the tenor associated with the quote
    /// </summary>
    /// <value>The quote tenor</value>
    public string QuoteTenor
    {
      get { return _q.Tenor; }
    }

    /// <summary>
    /// Access public market quote
    /// </summary>
    public MarketQuote MarketQuote
    {
      get { return _q.Quote; }
      set { _q = new TenorQuote(_q.Tenor, value, _q.Terms, _q.ReferenceIndex); }
    }

    public ReferenceIndex ReferenceIndex
    {
      get { return _q.ReferenceIndex; }
    }

    /// <summary>
    /// Gets or sets the quote key (the string which uniquely identifies a quote).
    /// </summary>
    /// <value>The quote key.</value>
    /// <remarks></remarks>
    public string QuoteKey
    {
      get { return quoteKey_ ?? Name; }
      set { quoteKey_ = value; }
    }

    #endregion

    #region Obsolete properties

    /// <summary>
    ///   Current coupon for floating rate securities (if necessary)
    /// </summary>
    public double Coupon
    {
      get { return 0.0; }
    }

    /// <summary>
    ///   Individual financing spread for this tenor
    /// </summary>
    public double FinSpread
    {
      get { return 0.0; }
    }

    #endregion

    #endregion // Properties

    #region Methods

    /// <summary>
    ///   Convert to string
    /// </summary>
    public override string ToString()
    {
      if (_q.Tenor != null || _q.Terms != null)
      {
        return "Name = " + name_ + "; " +
          "Quote = " + CurrentQuote + "; " +
          "weight_ = " + weight_;
      }
      return "Name = " + name_ + "; " +
        "Product = " + product_ + "; " +
        "MarketPv = " + marketPv_ + "; " +
        "Quote = " + CurrentQuote + "; " +
        "modelPv_ = " + modelPv_ + "; " +
        "weight_ = " + weight_;
    }

    /// <summary>
    /// Compare tenors by maturity
    /// </summary>
    /// <param name="obj">CurveTenor object</param>
    /// <returns>Positive integer if this.Maturity>obj.Maturity, negative integer if obj.Maturity>this.Maturity, 0 otherwise</returns>
    public int CompareTo(object obj)
    {
      var tenor = obj as CurveTenor;
      if (tenor != null)
      {
        return Dt.Cmp(CurveDate, tenor.CurveDate);
      }
      throw new ToolkitException("Cannot compare to obj, it is not an instance of a CurveTenor.");
    }

    #endregion

    #region Data

    private string name_;
    private IProduct product_;
    private double marketPv_;
    private double weight_;
    [Mutable] private double modelPv_;
    private IMarketQuote originalQuote_;
    private ICurveTenorQuoteHandler quoteHandler_
      = CurveTenorQuoteHandlers.DefaultHandler;

    private Dt curveDate_;

    // This field is added in 10.3.0.
    private string quoteKey_;

    // These fields are added in 13.1.0.
    private TenorQuote _q;
    #endregion // Data

    #region Types
    /// <summary>
    ///   Generic Quote
    /// </summary>
    [Serializable]
    public class Quote : IMarketQuote
    {
      /// <summary>
      ///    Constructor
      /// </summary>
      /// <param name="type">Quoting convention</param>
      /// <param name="value">Quoted value</param>
      public Quote(QuotingConvention type, double value)
      {
        type_ = type; value_ = value;
      }

      /// <summary>
      ///   Quoted value
      /// </summary>
      public double Value
      {
        get { return value_; }
      }

      /// <summary>
      ///   Quote type
      /// </summary>
      public QuotingConvention Type
      {
        get { return type_; }
      }

      /// <summary>
      ///   Display quote content.
      /// </summary>
      /// <returns></returns>
      public override string ToString()
      {
        return String.Format("{0}, {1}", type_, value_);
      }

      private QuotingConvention type_;
      private double value_;
    }

    /// <summary>
    ///   Upfront fee quote
    /// </summary>
    [Serializable]
    public class UpfrontFeeQuote : Quote
    {
      /// <summary>
      ///   Constructor
      /// </summary>
      /// <param name="fee">Upfront fee</param>
      /// <param name="running">Running premium</param>
      public UpfrontFeeQuote(double fee, double running)
        : base(QuotingConvention.Fee, fee)
      {
        premium_ = running;
      }

      /// <summary>
      ///   Upfront fee
      /// </summary>
      public double Fee
      {
        get { return Value; }
      }

      /// <summary>
      ///   Upfront fee
      /// </summary>
      public double Premium
      {
        get { return premium_; }
      }

      private double premium_;
    }
    #endregion Types

  } // CurveTenor


}
