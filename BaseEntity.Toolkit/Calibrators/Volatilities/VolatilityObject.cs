/*
 * VolatilityObject.cs
 *
 *  -2011. All Rights Reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   Interface of volatility object
  /// </summary>
  public interface IVolatilityObject : IBaseEntityObject
  {
    /// <summary>
    /// Gets the type of the underlying distribution.
    /// </summary>
    /// <value>The type of the distribution.</value>
    DistributionType DistributionType { get; }
  }

  /// <summary>
  ///   Interface of volatility object
  /// </summary>
  public interface IVolatilityCalculatorProvider : IVolatilityObject
  {
    /// <summary>
    /// Gets the volatility calculator for the specified product.
    /// </summary>
    /// <param name="product">The product.</param>
    /// <returns>The calculation function which takes the start (as-of) date
    ///   and returns the volatility from the start date to the expiry date.</returns>
    Func<Dt,double> GetCalculator(IProduct product);
  }

  /// <summary>
  ///  Provides the shift value for the shifted-lognormal
  ///  or shifted-SABR distribution
  /// </summary>
  interface IShiftValueProvider
  {
    /// <summary>
    /// Gets the shift value.
    /// </summary>
    /// <value>The shift value.</value>
    double ShiftValue { get; }
  }

  /// <summary>
  /// A special type of volatility surface that is flat on all dimensions.
  /// </summary>
  [Serializable]
  public class FlatVolatility : CalibratedVolatilitySurface,
    IVolatilityCalculatorProvider, IModelParameter, IShiftValueProvider
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="FlatVolatility"/> class.
    /// </summary>
    public FlatVolatility()
      : base(Dt.Today(), new IVolatilityTenor[] {new FlatTenor()},
        null, TheInterpolator)
    {}

    /// <summary>
    /// Gets or sets the volatility.
    /// </summary>
    /// <value>The volatility.</value>
    public double Volatility
    {
      get { return Tenors[0].QuoteValues[0]; }
      set { Tenors[0].QuoteValues[0] = value; }
    }

    /// <summary>
    /// Gets or sets the type of the underlying distribution.
    /// </summary>
    /// <value>The type of the distribution.</value>
    public DistributionType DistributionType { get; set; }

    /// <summary>
    /// Gets the shift value for shifted-lognormal distribution. 
    /// </summary>
    /// <value>The shift value.</value>
    public double ShiftValue { get; set; }

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    /// <remarks>
    /// By default validation is metadata-driven.  Entities that enforce
    /// additional constraints can override this method.  Methods that do
    /// override must first call Validate() on the base class.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      if (!(Volatility >= 0))
      {
        InvalidValue.AddError(errors, this, "Volatility",
          String.Format("Volatility {0} cannot be negative",
            Volatility));
      }
    }

    /// <summary>
    /// Gets the volatility calcultor for the specified product.
    /// </summary>
    /// <param name="product">The product.</param>
    /// <returns>The calculator.</returns>
    Func<Dt, double> IVolatilityCalculatorProvider.GetCalculator(IProduct product)
    {
      return (asOf) => Volatility;
    }

    double IModelParameter.Interpolate(Dt maturity, double strike, ReferenceIndex referenceIndex)
    {
      return Volatility;
    }

    #region Nested type: Flat interpolator

    private static readonly IVolatilitySurfaceInterpolator TheInterpolator
      = new FlatInterpolator();

    [Serializable]
    private class FlatInterpolator : IVolatilitySurfaceInterpolator
    {
      #region IVolatilitySurfaceInterpolator Members

      public double Interpolate(VolatilitySurface surface, Dt expiry, double strike)
      {
        var flat = surface as FlatVolatility;
        if (flat == null)
        {
          throw new ToolkitException("Not a flat volatility");
        }
        return flat.Volatility;
      }

      #endregion
    }

    #endregion

    #region Nested type: FlatTenor

    [Serializable]
    private class FlatTenor : BaseEntityObject, IVolatilityTenor
    {
      private readonly Dt _date = Dt.Today();
      private readonly double[] _vols = new double[1];

      #region IVolatilityTenor Members

      public Dt Maturity
      {
        get { return _date; }
      }

      public string Name
      {
        get { return "Flat"; }
      }

      public IList<double> QuoteValues
      {
        get { return _vols; }
      }

      #endregion
    }

    #endregion
  }
}
