/*
 * PlainVolatilityTenor.cs
 *
 *  -2011. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{

  /// <summary>
  ///   The plain volatility tenor consisting of a list of strikes
  ///   and a list of the corresponsing volatilities.
  /// </summary>
  [Serializable]
  public class PlainVolatilityTenor : VolatilityTenor
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="PlainVolatilityTenor"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="maturity">The maturity.</param>
    /// <remarks></remarks>
    public PlainVolatilityTenor(string name, Dt maturity)
      : base(name, maturity)
    { }

    /// <summary>
    /// Return a new object that is a deep copy of this instance
    /// </summary>
    /// <returns></returns>
    /// <remarks>
    /// This method will respect object relationships (for example, component references
    /// are deep copied, while entity associations are shallow copied (unless the caller
    /// manages the life cycle of the referenced object).
    /// </remarks>
    public override object Clone()
    {
      var obj = (PlainVolatilityTenor)base.Clone();
      obj.Volatilities = Clone(Volatilities);
      obj.Strikes = Clone(Strikes);
      return obj;
    }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    /// <remarks>
    /// By default validation is metadata-driven.  Entities that enforce
    /// additional constraints can override this method.  Methods that do
    /// override must first call Validate() on the base class.
    /// </remarks>
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);
      ValidateElementsInRange(Volatilities, "Volatilities", 0.0, 10.0, errors);
    }

    /// <summary>
    /// Gets or sets the volatilities.
    /// </summary>
    /// <value>The volatilities.</value>
    public IList<double> Volatilities { get; set; }

    /// <summary>
    /// Gets or sets the strikes.
    /// </summary>
    /// <value>The strikes.</value>
    public IList<double> Strikes { get; set; }

    /// <summary>
    /// Gets the volatility quote values.
    /// </summary>
    /// <value>The volatility quote values.</value>
    /// <remarks>The quote values may not be the same as the implied volatilities.
    /// For example, when the underlying options are quoted with prices,
    /// the quote values are prices instead of volatilities.
    /// When the underlying options are quoted as ATM call, Risk Reversals and Butterflies,
    /// the quote values are ATM volatilities and RR/BF deviations.</remarks>
    public override IList<double> QuoteValues
    {
      get { return Volatilities; }
    }
  }
}
