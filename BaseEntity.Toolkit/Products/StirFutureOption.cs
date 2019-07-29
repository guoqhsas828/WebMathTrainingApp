// 
//   2017. All rights reserved.
// 

using System;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Option on a STIR Future
  /// </summary>
  /// <remarks>
  /// <para>STIR future options are options where the underlying asset is a STIR (Short term interest rate) Future.</para>
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="SingleAssetOptionBase" />
  /// <para><b>Options</b></para>
  /// <inheritdoc cref="StirFuture" />
  /// </remarks>
  /// <example>
  /// <para>The following example demonstrates constructing a STIR future option.</para>
  /// <code language="C#">
  ///   var stirFuture = StandardProductTermsUtil.GetStandardFuture&lt;StirFuture&gt;("ED", exchange, Dt.Today(), "Z16") as StirFuture;
  ///   Dt expirationDate = new Dt(16, 6, 2016);  // Expiration is 16 June 2016
  ///
  ///   var option = new StirFutureOption(
  ///     stirFuture,                             // Stir futures contract
  ///     expirationDate,                         // Option Expiration
  ///     OptionType.Call,                        // Call option 
  ///     OptionStyle.American,                   // American option
  ///     95.0                                    // Strike is 95.0
  ///   );
  /// </code>
  /// </example>
  [Serializable]
  [ReadOnly(true)]
  public class StirFutureOption : SingleAssetOptionBase
  {
    #region Constructors

    /// <summary>
    ///  Constructor
    /// </summary>
    /// <param name="underlying">Underlying BondFuture</param>
    /// <param name="expiration">Expiration date</param>
    /// <param name="type">Option type</param>
    /// <param name="style">Option style</param>
    /// <param name="strike">Strike price</param>
    public StirFutureOption(StirFuture underlying, Dt expiration, OptionType type, OptionStyle style, double strike)
      : base(underlying, expiration, type, style, strike)
    { }

    #endregion Constructors

    #region Properties

    /// <summary>
    /// Underlying Bond Future
    /// </summary>
    public StirFuture StirFuture => (StirFuture)Underlying;

    #endregion Properties

  }
}