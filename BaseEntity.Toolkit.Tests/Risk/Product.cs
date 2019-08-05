using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Metadata;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Risk
{
  /// <summary>
  ///   Abstract parent class for all product implementations
  /// </summary>
  ///
  /// <remarks>
  ///   <para>Products are data representations of financial products.</para>
  ///
  ///   <para>Products are independent from any models which are
  ///   used to price them or any risk analysis performed.</para>
  /// </remarks>
  ///
  [Serializable]
  [Entity(SubclassMapping = SubclassMappingStrategy.Hybrid,
          PropertyMapping = PropertyMappingStrategy.Hybrid,
          AuditPolicy = AuditPolicy.History)]
  public abstract class Product : AuditedObject, IHasTags
  {
    #region Constructors

    /// <summary>
    ///   Default constructor
    /// </summary>
    protected Product()
    {
      notional_ = 1.0;
    }

    /// <summary>
    ///  Clone method
    /// </summary>
    /// <returns></returns>
    public override object Clone()
    {
      var other = (Product)base.Clone();

      // Dont clone Issuer
      other.Tags = CloneUtil.CloneToGenericList(Tags);

      return other;
    }

    #endregion

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    /// <param name="errors">List to append errors to</param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      ValidateCcy(errors);

      if (daysToSettle_ < 0)
        InvalidValue.AddError(errors, this, String.Format("DaysToSettle {0} must be +ve", daysToSettle_));

      ValidateNotional(errors);
      return;
    }

    /// <summary>
    /// Validate product currency.
    /// </summary>
    /// <remarks>
    /// Must be set unless product has cashflows in multiple currencies.
    /// </remarks>
    /// <param name="errors"></param>
    public virtual void ValidateCcy(ArrayList errors)
    {
      if (this.Ccy == Currency.None)
        InvalidValue.AddError(errors, this, "Ccy", "Currency on product must be specified. Cannot be None.");
      return;
    }

    /// <summary>
    /// Validate product notional
    /// </summary>
    /// <remarks>Must be positive unless the product notional is used to represent trade amount</remarks>
    /// <param name="errors"></param>
    protected virtual void ValidateNotional(ArrayList errors)
    {
      if (notional_ < 0.0)
        InvalidValue.AddError(errors, this, String.Format("Notional {0} must be +ve", notional_));
    }

    /// <summary>
    ///   Determine if a negative amount means its a Buy
    /// </summary>
    /// <remarks>
    ///   <para>For most products a positive notional is a buy. Credit products like CDS follow the
    ///   reverse convention so that a positive notional means long credit.</para>
    /// </remarks>
    /// <returns>true - negative amt is buy, false - positive amt is buy</returns>
    public virtual bool BuyIsNegative()
    {
      return false;
    }

    /// <summary>
    ///   Calculate natural settle date for this product if traded on specified asof date
    /// </summary>
    /// <param name="asOf"></param>
    /// <returns>Settlement date</returns>
    public abstract Dt CalcSettle(Dt asOf);

    /// <summary>
    ///   Determines whether the Product is active on the specified settlement date. 
    /// </summary>
    /// <remarks>
    ///   <para>For cash products this is if the product has matured.
    ///   For options, this is if the option has expired.</para>
    ///   <para>When pricing for settlement on the maturity or expiration
    ///   date, the trade is no longer active. Risk assumes pricing is
    ///   at the end of the business day.</para>
    /// </remarks>
    /// <param name="settle">Settlement date</param>
    /// <returns>true if the product is still active, false otherwise</returns>
    public virtual bool IsActive(Dt settle)
    {
      if (!this.Maturity.IsEmpty())
      {
        if (Dt.Cmp(this.Maturity, settle) < 0)
          return false;
      }
      return true;
    }

    /// <summary>
    ///   Determines whether the given settle date is valid for a trade with this product.
    ///   Typically, a product can not be traded after maturity, however, there are some exceptions, like defaulted bonds.
    /// </summary>
    public virtual bool IsValidSettle(Dt settle)
    {
      if (!settle.IsEmpty() && !Maturity.IsEmpty() &&
          Dt.Cmp(settle, Maturity) > 0) return false;
      return true;
    }

    /// <summary>
    ///  Helper function to add a Tag
    /// </summary>
    public void AddTag(Tag tag)
    {
      Tags.Add(tag);
    }

    #endregion

    #region Properties

    /// <summary>
    ///   Identifier for product. This could be a Bloomberg Id/etc.
    /// </summary>
    [StringProperty(MaxLength = 64, IsKey = true)]
    public string Name
    {
      get { return name_; }
      set { name_ = value; }
    }

    /// <summary>
    ///   Description of product
    /// </summary>
    [StringProperty(MaxLength = 80)]
    public string Description
    {
      get { return description_; }
      set { description_ = value; }
    }

    /// <summary>
    ///   Issue date
    /// </summary>
    [DtProperty]
    public Dt Issue
    {
      get { return issue_; }
      set { issue_ = value; }
    }

    /// <summary>
    ///   Effective date
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The date that interest starts to accrue.</para>
    ///
    ///   <para>For a credit derivative, this is the date that credit protection commences.</para>
    /// </remarks>
    ///
    [DtProperty]
    public Dt Effective
    {
      get { return effective_; }

      set
      {
        effective_ = value;
      }
    }

    /// <summary>
    ///   Maturity or maturity date
    /// </summary>
    [DtProperty]
    public Dt Maturity
    {
      get { return maturity_; }
      set { maturity_ = value; }
    }

    /// <summary>
    ///   Days to settlement
    /// </summary>
    [NumericProperty]
    public int DaysToSettle
    {
      get { return daysToSettle_; }
      set { daysToSettle_ = value; }
    }

    /// <summary>
    ///   Product primary currency
    /// </summary>
    [EnumProperty]
    public Currency Ccy
    {
      get { return ccy_; }
      set { ccy_ = value; }
    }

    /// <summary>
		/// This is the number of contracts in the product. For a derivative product this is typically 1.
		/// The Trade.Amount is the amount traded. 
		/// The Size of a trade is Trade.Amount * Trade.Product.Notional
		/// </summary>
    ///
    /// <remarks>
    /// <para>
    /// This is the number of contracts in the product. For a derivative product this is typically 1.
    /// The Trade.Amount is the amount traded. 
    /// The Size of a trade is Trade.Amount * Trade.Product.Notional
    /// </para>
    /// </remarks>
    [NumericProperty(Format = NumberFormat.Currency, RelatedProperty = "Ccy", AllowNullValue = false)]
    public double Notional
    {
      get { return notional_; }
      set { notional_ = value; }
    }



    /// <summary>
    /// Gets or sets the Product tags.
    /// </summary>
    [ComponentCollectionProperty(TableName = "ProductTag", CollectionType = "bag")]
    public IList<Tag> Tags
    {
      get { return tags_ ?? (tags_ = new List<Tag>()); }
      set { tags_ = value; }
    }

    #endregion Properties

    #region Data

    private string name_ = "";
    private string description_ = "";
    private Dt issue_;
    private Dt effective_;
    private Dt maturity_;
    private int daysToSettle_;
    private Currency ccy_;
    private double notional_;
    private IList<Tag> tags_;

    #endregion Data

  } // Product

  ///// <summary>
  ///// 
  ///// </summary>
  //public class ProductFilter : IDataFilter<Product>
  //{
  //  /// <summary>
  //  /// Name to find Product by
  //  /// </summary>
  //  public string Name { get; }

  //  /// <inheritdoc />
  //  public ProductFilter(string name, IMarketRepository repo)
  //  {
  //    Name = name;
  //    _repo = repo;
  //  }

  //  /// <inheritdoc />
  //  public IList<IKey> FindKeys()
  //  {
  //    return new List<IKey>() { new ProductKey(Name) };
  //  }

  //  /// <inheritdoc />
  //  public IList<Product> FindByKey(IList<IKey> keys)
  //  {
  //    var products = new List<Product>();
  //    var cache = GetOrInitProductCache();
  //    foreach (var key in keys)
  //    {
  //      if (!(key is ProductKey productKey))
  //        throw new System.ArgumentException($"{key} is not a ProductKey");

  //      products.Add(cache.FindByName(productKey.Name));
  //    }

  //    return products;
  //  }

  //  private ProductCache GetOrInitProductCache()
  //  {
  //    if (!_repo.TryGetTypeCacheAs<ProductCache>(typeof(Product), out var cache))
  //    {
  //      cache = new ProductCache();
  //      _repo.AddTypeCache(typeof(Product), cache);
  //    }

  //    return cache;
  //  }

  //  private readonly IMarketRepository _repo;

  //  #region ProductCache

  //  private class ProductCache : ITypeCache<Product>
  //  {
  //    public ProductCache(bool lazyLoad = true)
  //    {
  //      LazyLoad = lazyLoad;
  //      if (lazyLoad) return;

  //      foreach (Product p in ProductUtil.FindAll())
  //      {
  //        _nameCache[p.Name] = p;
  //        _idCache[p.ObjectId] = p;
  //      }
  //    }

  //    public Product FindByName(string name)
  //    {
  //      if (!_nameCache.TryGetValue(name, out var p) && LazyLoad)
  //      {
  //        p = ProductUtil.FindByName(name);
  //        if (p != null)
  //        {
  //          _nameCache[p.Name] = p;
  //          _idCache[p.ObjectId] = p;
  //        }
  //      }

  //      return p;
  //    }

  //    public Product FindById(long id)
  //    {
  //      if (!_idCache.TryGetValue(id, out var p) && LazyLoad)
  //      {
  //        p = ProductUtil.FindById(id);
  //        if (p != null)
  //        {
  //          _nameCache[p.Name] = p;
  //          _idCache[p.ObjectId] = p;
  //        }
  //      }

  //      return p;
  //    }

  //    /// <inheritdoc />
  //    public void Fill(Dt asOf, IEnumerable<Product> data)
  //    {
  //      foreach (Product p in data)
  //      {
  //        _nameCache[p.Name] = p;
  //        _idCache[p.ObjectId] = p;
  //      }
  //    }

  //    /// <inheritdoc />
  //    public IEnumerator GetEnumerator()
  //    {
  //      return _nameCache.Values.GetEnumerator();
  //    }

  //    public bool LazyLoad { get; }

  //    private readonly Dictionary<string, Product> _nameCache = new Dictionary<string, Product>();
  //    private readonly Dictionary<long, Product> _idCache = new Dictionary<long, Product>();

  //  } //ProductCache

  //  #endregion
  //} //ProductFilter


  /// <summary>
  /// 
  /// </summary>
  public class ProductKey : IKey
  {
    /// <summary>
    /// 
    /// </summary>
    public string Name { get; }

    /// <inheritdoc />
    public Type ValueType => typeof(Product);

    /// <inheritdoc />
    public ProductKey(string name)
    {
      Name = name;
    }
  }

}
