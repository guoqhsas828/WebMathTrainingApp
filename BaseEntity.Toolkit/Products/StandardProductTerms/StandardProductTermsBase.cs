//
// StandardProductTerms.cs
//   2015. All rights reserved.
//

using System;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Products.StandardProductTerms
{
  /// <summary>
  ///   Convenient base class of market-standard products implementing <see cref="IStandardProductTerms"/>
  /// </summary>
  /// <summary>
  ///   <para>Product terms implementing <see cref="IStandardProductTerms"/> do not have to inherit from this
  ///   class but it is provided as a convenience.</para>
  /// </summary>
  [Serializable]
  public abstract class StandardProductTermsBase : BaseEntityObject, IStandardProductTerms
  {
    #region Constructors

    /// <summary>
    ///   Default constructor
    /// </summary>
    protected StandardProductTermsBase()
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="description">Description</param>
    protected StandardProductTermsBase(string description)
    {
      Description = description;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Unique key for this term
    /// </summary>
    public abstract string Key { get; }

    /// <summary>
    ///   Description
    /// </summary>
    public string Description { get; protected set; }

    #endregion Properties

    #region methods

    /// <summary>
    /// Returns the base name of a quote
    /// </summary>
    /// <returns></returns>
    public virtual string GetQuoteName(string tenor)
    {
      return Key;
    }

    #endregion
  }
}
