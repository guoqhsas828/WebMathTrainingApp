/*
 *   2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities.ForeignExchanges
{
  /// <summary>
  ///   The base class of FxVolatility tenors.
  /// </summary>
  /// <remarks>
  /// </remarks>
  [Serializable]
  public abstract class FxVolatilityTenor : PlainVolatilityTenor
  {
    private readonly FxVolatilityQuoteFlags _flags;

    /// <summary>
    /// Initializes a new instance of the <see cref="FxVolatilityTenor"/> class.
    /// </summary>
    /// <param name="name">The tenor name.</param>
    /// <param name="expiry">The expiry date.</param>
    /// <param name="flags">The quote flags.</param>
    /// <remarks></remarks>
    protected FxVolatilityTenor(string name, Dt expiry, FxVolatilityQuoteFlags flags)
      : base(name, expiry)
    {
      if ((_flags & FxVolatilityQuoteFlags.SpotAtm) != 0
        && (_flags & FxVolatilityQuoteFlags.ForwardAtm) != 0)
      {
        throw new ToolkitException("SpotAtm and ForwardAtm cannot be both set.");
      }
      _flags = flags;
    }

    /// <summary>
    ///   Gets the FX volatility quote convention.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public abstract VolatilityQuoteType QuoteType { get; }

    /// <summary>
    ///   Gets a value indicating forward delta.
    /// </summary>
    /// <remarks>
    ///   If this property is true, then the delta is defined with respect to the discounted forward FX rate<math>
    ///     \Delta^F \equiv \frac{\partial P}{\partial e^{-r_d T} F} = \omega \Phi(\omega d_1)
    ///   </math>Otherwise, the delta is defined with respect to the spot rate<math>
    ///     \Delta^S \equiv \frac{\partial P}{\partial S} = \omega e^{-r_f T} \Phi(\omega d_1) = e^{-r_f T}\Delta^F
    ///   </math> where <m>\omega = 1</m> for call and <m>\omega = -1</m> for put.
    ///   The above formula provides a way to convert the two deltas.<br />
    /// </remarks>
    public bool ForwardDelta
    {
      get { return (_flags & FxVolatilityQuoteFlags.ForwardDelta) != 0; }
    }

    /// <summary>
    ///   Gets a value indicating premium included delta.
    /// </summary>
    /// <remarks>
    ///   If this property is true, then the premium is paid in the foreign currency. Correspondingly, the delta is given by<math>
    ///     \Delta^{\mathrm{FI}} = \Delta^F - \frac{P}{e^{-r_d T}F} =\omega \frac{K}{F} \Phi(\omega d_2)
    ///   </math><br />for forward delta and by<math>
    ///     \Delta^{\mathrm{SI}} = \Delta^S - \frac{P}{S} =\omega e^{-r_f T} \frac{K}{F} \Phi(\omega d_2) = e^{-r_f T}\Delta^{FI}
    ///   </math>for spot delta, where <m>\omega = 1</m> for call and <m>\omega = -1</m> for put.
    ///   The above formula provides a way to convert the two deltas.<br />
    /// </remarks>
    public bool PremiumIncludedDelta
    {
      get { return (_flags & FxVolatilityQuoteFlags.PremiumIncludedDelta) != 0; }
    }

    /// <summary>
    ///   Gets a value indicating one volatility butterfly.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public bool OneVolalityBufferfly
    {
      get { return (_flags & FxVolatilityQuoteFlags.OneVolatilityBufferfly) != 0; }
    }

    /// <summary>
    ///   Gets a value indicating currency 2 based strangle.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public bool Ccy2Strangle
    {
      get { return (_flags & FxVolatilityQuoteFlags.Ccy2Strangle) != 0; }
    }

    /// <summary>
    ///   Gets a value indicating currency 2 based risk reversal.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public bool Ccy2RiskReversal
    {
      get { return (_flags & FxVolatilityQuoteFlags.Ccy2RiskReversal) != 0; }
    }

    /// <summary>
    ///   Gets the ATM setting.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public AtmKind AtmSetting
    {
      get
      {
        return (_flags & FxVolatilityQuoteFlags.ForwardAtm) != 0
          ? AtmKind.Forward
          : ((_flags & FxVolatilityQuoteFlags.SpotAtm) != 0
            ? AtmKind.Spot
            : AtmKind.DeltaNeutral);
      }
    }

    /// <summary>
    ///   Gets a value indicating whether to use forward FX as ATM strike.
    /// </summary>
    /// <remarks>
    /// </remarks>
    internal bool ForwardFxAsAtm
    {
      get { return (_flags & FxVolatilityQuoteFlags.ForwardAtm) != 0; }
    }

    /// <summary>
    ///   Gets a value indicating whether to use spot FX as ATM strike.
    /// </summary>
    /// <remarks>
    /// </remarks>
    internal bool SpotFxAsAtm
    {
      get { return (_flags & FxVolatilityQuoteFlags.SpotAtm) != 0; }
    }

    /// <summary>
    ///   Gets the strike volatility pairs.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public abstract StrikeVolatilityPair[] StrikeVolatilityPairs { get; }

    /// <summary>
    ///   Gets the quotes.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public abstract VolatilityQuote[] Quotes { get; }

    /// <summary>
    /// Gets or sets the volatiltities.
    /// </summary>
    /// <value>The volatiltities.</value>
    public IList<double> Volatiltities { get; protected set; }

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



