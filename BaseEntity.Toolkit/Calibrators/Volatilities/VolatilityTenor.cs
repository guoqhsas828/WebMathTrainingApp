/*
 * VolatilityTenor.cs
 *
 *  -2012. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   The base class of a volatility tenor.
  /// </summary>
  /// <remarks>
  ///   The Tenor class is designed to hold all the market data
  ///   of volatilities. If more data is needed, the user should
  ///   derive from this class and add other fields.
  /// </remarks>
  [Serializable]
  public abstract class VolatilityTenor : BaseEntityObject, IVolatilityTenor
  {
    private readonly string _name;
    private readonly Dt _maturity;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlainVolatilityTenor"/> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="maturity">The maturity.</param>
    /// <remarks></remarks>
    protected VolatilityTenor(string name, Dt maturity)
    {
      Debug.Assert(!String.IsNullOrEmpty(name));
      Debug.Assert(!maturity.IsEmpty());
      _maturity = maturity;
      _name = name;
    }

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
      var obj = (VolatilityTenor)base.Clone();
      return obj;
    }

    internal static IList<double> Clone(IList<double> list)
    {
      var cloneable = list as ICloneable;
      if(cloneable!=null)
      {
        return (IList<double>)cloneable.Clone();
      }
      throw new ToolkitException("No clone method provided.");
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
      if (Maturity.IsEmpty())
      {
        InvalidValue.AddError(errors, this, "Maturity",
          "Maturity cannot be null");
      }
      var vols = QuoteValues;
      if (vols == null || vols.Count == 0)
      {
        InvalidValue.AddError(errors, this, "Volatilities",
          "Volatilities cannot be empty");
      }
    }

    internal void ValidateElementsInRange(
      IList<double> data, string variableName,
      double low, double high,
      System.Collections.ArrayList errors)
    {
      for (int i = 0, n = data.Count; i < n; ++i)
      {
        double v = data[i];
        if (v < 0.0 || v > 10)
        {
          InvalidValue.AddError(errors, this,
            String.Format("{0}[{1}]", variableName, i),
            String.Format("Value must be between {0} and {1}",
              low, high));
        }
      }
      
    }

    /// <summary>
    /// Gets the maturity (expiry) date.
    /// </summary>
    /// <value>The maturity date.</value>
    /// 
    public Dt Maturity { get { return _maturity; } }

    /// <summary>
    /// Gets the tenor name.
    /// </summary>
    /// <value>The tenor name.</value>
    public String Name { get { return _name; } }

    /// <summary>
    /// Gets the volatility quote values.
    /// </summary>
    /// <value>The volatility quote values.</value>
    /// <remarks>The quote values may not be the same as the implied volatilities.
    /// For example, when the underlying options are quoted with prices,
    /// the quote values are prices instead of volatilities.
    /// When the underlying options are quoted as ATM call, Risk Reversals and Butterflies,
    /// the quote values are ATM volatilities and RR/BF deviations.</remarks>
    public abstract IList<double> QuoteValues { get; }
  }

  /// <summary>
  /// Minimum implementation of the <see cref="IVolatilityTenor">IVolatilityTenor</see>
  /// interface.
  /// </summary>
  [Serializable]
  internal class BasicVolatilityTenor : VolatilityTenor
  {
    private readonly IList<double> _values; 
    public BasicVolatilityTenor(string name,
      Dt maturity, IList<double> quoteValues)
      :base(name,maturity)
    {
      _values = quoteValues;
    }
    public override IList<double> QuoteValues
    {
      get { return _values; }
    }
    public override string ToString()
    {
      return Name ?? Maturity.ToString();
    }
  }
}
