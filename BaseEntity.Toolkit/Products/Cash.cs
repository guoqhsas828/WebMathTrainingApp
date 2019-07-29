/*
 * Cash.cs
 *
 *
 */

using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using System.Collections;

namespace BaseEntity.Toolkit.Products
{

  ///
  /// <summary>
  ///   Cash deposit product
  /// </summary>
  ///
  /// <preliminary>
  ///   This class is preliminary and not supported for customer use. This may be
  ///   removed or moved to a separate product at a future date.
  /// </preliminary>
  ///
  [Serializable]
  [ReadOnly(true)]
  public class Cash : Product
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    protected
    Cash()
    { }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="ccy">Currency</param>
    ///
    public Cash(Currency ccy)
      : base(new Dt(1, 7, 2003), new Dt(1, 1, 2090), ccy)
    { }

    #endregion // Constructors

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    ///
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
    }

    #endregion // Methods

  } // class Cash

}
