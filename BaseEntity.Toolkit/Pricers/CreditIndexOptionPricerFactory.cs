//
// CreditIndexOptionPriceFactory.cs
//  -2008. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;
using Evaluator = System.Func<BaseEntity.Toolkit.Pricers.IPricer, double>;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///  Providing static methods related to Credit Index Option Pricer.
  /// </summary>
  /// <remarks>
  /// <para>This class provides a set of handy factory methods for constructing
  /// Credit Index Option pricers.</para>
  /// </remarks>
  /// <seealso cref="CDXOptionPricerBlack"/>
  /// <seealso cref="CDXOptionPricerModifiedBlack"/>
  /// <seealso cref="CDXOptionPricerMoriniBrigo"/>
  public static class CreditIndexOptionPricerFactory
  {
    #region Pricer builder

    /// <summary>
    /// Creates the CDX option pricer.
    /// </summary>
    /// <param name="option">The CDX option.</param>
    /// <param name="pricingDate">The pricing date.</param>
    /// <param name="settleDate">The settle date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="marketQuote">The market quote.</param>
    /// <param name="cdxSettleDate">The CDX settle date.</param>
    /// <param name="marketRecoveryRate">The market recovery rate.</param>
    /// <param name="basketSize">Number of names in the CDX index.</param>
    /// <param name="survivalCurves">The survival curves by names.</param>
    /// <param name="modelType">Type of the model.</param>
    /// <param name="data">Any additional model data (can be null).</param>
    /// <param name="volatilitySurface">The volatility surface.</param>
    /// <param name="notional">The notional.</param>
    /// <param name="upfrontPayment">The upfront payment.</param>
    /// <returns>ICreditIndexOptionPricer.</returns>
    /// <exception cref="System.ArgumentException">Must specify a portfolio or basket size.</exception>
    /// <example>
    /// <code language="C#">
    ///   DiscountCurve discountCurve;
    ///   SurvivalCurve [] survivalCurves;
    ///   CalibratedVolatilitySurface volSurface;
    ///   CDXOptionModelData modelData;
    ///   Dt asOf, settle, cdxSettle;
    /// 
    ///   // Set up discountCurve, survivalCurves, volSurface, asOf and settle dates.
    ///   // ...
    /// 
    ///   // Create underlying credit index
    ///   CDX note = new CDX(
    ///     new Dt(20, 6, 2004),                 // Effective date of the CDS index
    ///     new Dt(20, 6, 2009),                 // Maturity date of the CDS index
    ///     Currency.USD,                        // Currency of the CDS index
    ///     40/10000,                            // CDS index premium (40bp)
    ///     DayCount.Actual360,                  // Daycount of the CDS index premium
    ///     Frequency.Quarterly,                 // Payment frequency of the CDS index premium
    ///     BDConvention.Following,              // Business day convention of the CDS index
    ///     Calendar.NYB                         // Calendar for the CDS Index
    ///   );
    /// 
    ///   // Create the credit index option
    ///   CDXOption cdxOption = new CDXOption(
    ///     new Dt(20, 9, 2004),                 // Effective date of option
    ///     Currency.USD,                        // Currency of the CDS
    ///     note,                                // Underlying index note
    ///     new Dt(20, 11, 2004),                // Option expiration
    ///     PayerReceiver.Payer,                 // Option type
    ///     OptionStyle.European,                // Option style
    ///     0.0075,                              // Option strike of 75bp
    ///     false                                // Strike spreads instead of values
    ///   );
    /// 
    ///   // Create a pricing for the CDX Option.
    ///   var pricer = cdxOption.CreatePricer(
    ///     cdxOption,                           // Index Option
    ///     asOf,                                // Pricing date
    ///     settle,                              // Settlement date
    ///     discountCurve,                       // Discount curve
    ///     60/10000.0,                          // current market spread (60bps)
    ///     cdxSettle,                           // Settlement date of index
    ///     0.4,                                 // Market recovery rate for index
    ///     125,                                 // Size of index basket
    ///     survivalCurves,                      // Survival curves
    ///     CDXOptionModelType.Black,            // Black model
    ///     modelData,                           // Model data
    ///     volSurface                           // volatility surface
    ///     1e6,                                 // Notional
    ///     null                                 // Upfront payment if any
    ///   );
    /// </code>
    /// </example>
    public static ICreditIndexOptionPricer CreatePricer(
      this CDXOption option,
      Dt pricingDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      MarketQuote marketQuote,
      Dt cdxSettleDate,
      double marketRecoveryRate,
      int basketSize,
      SurvivalCurve[] survivalCurves,
      CDXOptionModelType modelType,
      CDXOptionModelData data,
      VolatilitySurface volatilitySurface,
      double notional,
      Payment upfrontPayment)
    {
      return CreatePricer(option, pricingDate, settleDate, discountCurve, marketQuote, cdxSettleDate, marketRecoveryRate, basketSize, survivalCurves, modelType,
        data, volatilitySurface, notional, Double.NaN, upfrontPayment);
    }

    /// <summary>
    /// Creates the CDX option pricer.
    /// </summary>
    /// <param name="option">The CDX option.</param>
    /// <param name="pricingDate">The pricing date.</param>
    /// <param name="settleDate">The settle date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="marketQuote">The market quote.</param>
    /// <param name="cdxSettleDate">The CDX settle date.</param>
    /// <param name="marketRecoveryRate">The market recovery rate.</param>
    /// <param name="basketSize">Number of names in the CDX index.</param>
    /// <param name="survivalCurves">The survival curves by names.</param>
    /// <param name="modelType">Type of the model.</param>
    /// <param name="data">Any additional model data (can be null).</param>
    /// <param name="volatilitySurface">The volatility surface.</param>
    /// <param name="notional">The notional.</param>
    /// <param name="modelBasis">Model basis</param>
    /// <param name="upfrontPayment">The upfront payment.</param>
    /// <returns>ICreditIndexOptionPricer.</returns>
    /// <exception cref="System.ArgumentException">Must specify a portfolio or basket size.</exception>
    /// <example>
    /// <code language="C#">
    ///   DiscountCurve discountCurve;
    ///   SurvivalCurve [] survivalCurves;
    ///   CalibratedVolatilitySurface volSurface;
    ///   CDXOptionModelData modelData;
    ///   Dt asOf, settle, cdxSettle;
    /// 
    ///   // Set up discountCurve, survivalCurves, volSurface, asOf and settle dates.
    ///   // ...
    /// 
    ///   // Create underlying credit index
    ///   CDX note = new CDX(
    ///     new Dt(20, 6, 2004),                 // Effective date of the CDS index
    ///     new Dt(20, 6, 2009),                 // Maturity date of the CDS index
    ///     Currency.USD,                        // Currency of the CDS index
    ///     40/10000,                            // CDS index premium (40bp)
    ///     DayCount.Actual360,                  // Daycount of the CDS index premium
    ///     Frequency.Quarterly,                 // Payment frequency of the CDS index premium
    ///     BDConvention.Following,              // Business day convention of the CDS index
    ///     Calendar.NYB                         // Calendar for the CDS Index
    ///   );
    /// 
    ///   // Create the credit index option
    ///   CDXOption cdxOption = new CDXOption(
    ///     new Dt(20, 9, 2004),                 // Effective date of option
    ///     Currency.USD,                        // Currency of the CDS
    ///     note,                                // Underlying index note
    ///     new Dt(20, 11, 2004),                // Option expiration
    ///     PayerReceiver.Payer,                 // Option type
    ///     OptionStyle.European,                // Option style
    ///     0.0075,                              // Option strike of 75bp
    ///     false                                // Strike spreads instead of values
    ///   );
    /// 
    ///   // Create a pricing for the CDX Option.
    ///   var pricer = cdxOption.CreatePricer(
    ///     cdxOption,                           // Index Option
    ///     asOf,                                // Pricing date
    ///     settle,                              // Settlement date
    ///     discountCurve,                       // Discount curve
    ///     60/10000.0,                          // current market spread (60bps)
    ///     cdxSettle,                           // Settlement date of index
    ///     0.4,                                 // Market recovery rate for index
    ///     125,                                 // Size of index basket
    ///     survivalCurves,                      // Survival curves
    ///     CDXOptionModelType.Black,            // Black model
    ///     modelData,                           // Model data
    ///     volSurface                           // volatility surface
    ///     1e6,                                 // Notional
    ///     null                                 // Upfront payment if any
    ///   );
    /// </code>
    /// </example>
    public static ICreditIndexOptionPricer CreatePricer(
      this CDXOption option,
      Dt pricingDate,
      Dt settleDate,
      DiscountCurve discountCurve,
      MarketQuote marketQuote,
      Dt cdxSettleDate,
      double marketRecoveryRate,
      int basketSize,
      SurvivalCurve[] survivalCurves,
      CDXOptionModelType modelType,
      CDXOptionModelData data,
      VolatilitySurface volatilitySurface,
      double notional,
      double modelBasis,
      Payment upfrontPayment)
    {
      // data is optional, allowed to be null.
      if (((data ?? (data = new CDXOptionModelData())).Choice
        & CDXOptionModelParam.MarketPayoffConsistent) != 0)
      {
        return new CreditIndexOptionPricer(option,
          pricingDate, settleDate, discountCurve,
          marketQuote, cdxSettleDate, marketRecoveryRate, basketSize,
          survivalCurves, 1.0, 0.0, modelType, data, volatilitySurface, modelBasis)
        {
          Notional = notional,
          Payment = upfrontPayment,
        }.Validated();
      }

      if (volatilitySurface == null)
      {
        volatilitySurface = CalibratedVolatilitySurface.FromFlatVolatility(
          pricingDate, 0.0);
      }
      var quote = marketQuote.Value;
      CDXOptionPricer pricer;
      switch (modelType)
      {
      case CDXOptionModelType.Black:
        if (survivalCurves == null)
          pricer = new CDXOptionPricerBlack(option, pricingDate, settleDate,
                                            discountCurve, basketSize, quote, volatilitySurface);
        else
          pricer = new CDXOptionPricerBlack(option, pricingDate, settleDate,
                                            discountCurve, survivalCurves, quote, volatilitySurface);
        break;
      case CDXOptionModelType.BlackPrice:
        if (survivalCurves == null)
          pricer = new CDXOptionPricerBlack(option, pricingDate, settleDate,
                                            discountCurve, basketSize, quote, volatilitySurface);
        else
          pricer = new CDXOptionPricerBlack(option, pricingDate, settleDate,
                                            discountCurve, survivalCurves, quote, volatilitySurface);
        ((CDXOptionPricerBlack)pricer).PriceVolatilityApproach = true;
        break;
      case CDXOptionModelType.BlackArbitrageFree:
        if (basketSize <= 0)
        {
          throw new ArgumentException("Must specify a portfolio or basket size.");
        }
        if (data.Correlation == null)
        {
          data.Correlation = new SingleFactorCorrelation(
            new string[basketSize], 0.0);
        }
        pricer = new CDXOptionPricerMoriniBrigo(option, pricingDate, settleDate,
                                                discountCurve, survivalCurves, basketSize, quote, volatilitySurface,
                                                data.Correlation, data.Copula, data.Accuracy);
        break;
      case CDXOptionModelType.ModifiedBlack:
      default:
        if (survivalCurves == null)
          pricer = new CDXOptionPricerModifiedBlack(option, pricingDate, settleDate,
                                                    discountCurve, basketSize, quote, volatilitySurface);
        else
          pricer = new CDXOptionPricerModifiedBlack(option, pricingDate, settleDate,
                                                    discountCurve, survivalCurves, quote, volatilitySurface);
        if (!Double.IsNaN(data.ModifiedBlackCenter))
          ((CDXOptionPricerModifiedBlack)pricer).Center = data.ModifiedBlackCenter;
        break;
      }
      pricer.Notional = notional;
      pricer.Payment = upfrontPayment;
      pricer.ModelParam = data.Choice;
      pricer.MarketRecoveryRate = marketRecoveryRate;
      if (option.StrikeIsPrice || marketQuote.Type == QuotingConvention.FlatPrice)
        pricer.QuotingConvention = QuotingConvention.FlatPrice;

      // Validate option pricer
      pricer.Validate();

      return pricer;
    }

    private static ICreditIndexOptionPricer Validated(
      this ICreditIndexOptionPricer pricer)
    {
      var qnobj = pricer as IBaseEntityObject;
      if(qnobj != null) qnobj.Validate();
      return pricer;
    }
    #endregion

    #region Volatility interpolation

    /// <summary>
    /// Interpolates the volatility of credit index option.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="date">The expiry date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="strikeIsPrice">if set to <c>true</c>, the inpu strike is price; otherwise, it is spread.</param>
    /// <param name="modelType">The CDX model type for the output volatility</param>
    /// <returns>System.Double.</returns>
    /// <remarks>
    ///   If the surface is not built with credit option quotes, this function simply returns a volatility
    ///   number calculated by the generic method <c>VolatilitySurface.Interpolate(date, strike)</c>.
    ///   Otherwise, proper conversions of strike and volatility are performed.
    /// </remarks>
    public static double InterpolateCdxVolatility(
      this VolatilitySurface surface,
      Dt date, double strike, bool? strikeIsPrice,
      CDXOptionModelType? modelType)
    {
      return CdxVolatilityUnderlying.Interpolate(surface,
        date, strike, strikeIsPrice, modelType);
    }

    #endregion

    #region Pricer evaluators

    private static Dictionary<string, Evaluator> _evaluators;

    internal static Evaluator GetEvaluator(string measure)
    {
      if (_evaluators == null)
      {
        System.Threading.Interlocked.Exchange(ref _evaluators,
          BuildEvaluators());
      }
      Evaluator fn;
      return _evaluators.TryGetValue(measure, out fn) ? fn : null;
    }

    private static Dictionary<string, Evaluator> BuildEvaluators()
    {
      var dict = new Dictionary<string, Evaluator>(
        StringComparer.OrdinalIgnoreCase);
      foreach(var pair in GetEvaluators(typeof(ICreditIndexOptionPricer))
        .Concat(GetEvaluators(typeof(CreditIndexOptionPricerFactory)))
        .Concat(GetEvaluators(typeof(IPricer))))
      {
        if(dict.ContainsKey(pair.Key)) continue;
        dict.Add(pair.Key, pair.Value);
      }
      return dict;
    }

    private static IEnumerable< KeyValuePair<string, Evaluator>>
      GetEvaluators(Type type)
    {
      const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.FlattenHierarchy;
      var pricer = typeof(IPricer);
      var paramTypes = new[] { pricer };
      var methods = type.GetMethods(bf);
      foreach (var method in methods)
      {
        if (method.ReturnType != typeof(double)) continue;
        var pars = method.GetParameters();
        if (method.IsStatic)
        {
          if (pars.Length != 1 || !pricer.IsAssignableFrom(
            pars[0].ParameterType))
          {
            continue;
          }
        }
        else
        {
          if (pars.Length != 0 || !pricer.IsAssignableFrom(type))
            continue;
        }
        var name = GetName(method);
        var fn = CreateDelegate(name, method, paramTypes);
        yield return new KeyValuePair<string, Evaluator>(name, fn);
      }
    }

    private static Evaluator CreateDelegate(string name,
      MethodInfo m, Type[] paramTypes)
    {
      // Now we create delegate by dynamic method.
      var dm = new DynamicMethod(name + "_dyn",
        typeof(double), paramTypes, typeof(CreditIndexOptionPricerFactory));
      ILGenerator il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
      if (m.IsFinal || m.IsStatic)
        il.Emit(OpCodes.Call, m);
      else
        il.Emit(OpCodes.Callvirt, m);
      il.Emit(OpCodes.Ret);
      return (Evaluator)dm.CreateDelegate(typeof(Evaluator));
    }

    private static string GetName(MethodInfo method)
    {
      const string get = "get_";
      var s = method.Name;
      return (method.IsSpecialName && s.StartsWith(get))
        ? s.Substring(get.Length)
        : s;
    }
    #endregion

    #region Obsolete methods
    /// <summary>
    ///  Calculate the implied volatility for the specified fair value.
    /// </summary>
    /// <param name="pricer">The credit index pricer.</param>
    /// <param name="fv">The fair value.</param>
    /// <returns>System.Double.</returns>
    [Obsolete("Replaced by ImplyVolatility(fairPrice)")]
    public static double IVol(this ICreditIndexOptionPricer pricer, double fv)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      return cdxoPicer != null
        ? cdxoPicer.IVol(fv)
        : pricer.ImplyVolatility(fv);
    }

    /// <summary>
    ///  Calculate the probability in the money.
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <returns>System.Double.</returns>
    [Obsolete("Replaced by CalculateExerciseProbability()")]
    public static double ProbabilityInTheMoney(
      this ICreditIndexOptionPricer pricer)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      return cdxoPicer != null
        ? cdxoPicer.ProbabilityInTheMoney()
        : pricer.CalculateExerciseProbability(Double.NaN);
    }

    /// <summary>
    /// Calculate the probability in the money.
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <param name="volatility">The volatility.</param>
    /// <returns>System.Double.</returns>
    [Obsolete("Replaced by CalculateExerciseProbability()")]
    public static double ProbabilityInTheMoney(
      this ICreditIndexOptionPricer pricer,
      double volatility)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      return cdxoPicer != null
        ? cdxoPicer.ProbabilityInTheMoney(volatility)
        : pricer.CalculateExerciseProbability(volatility);
    }

    /// <summary>
    ///  Calculate the market value of the credit index option.
    /// </summary>
    /// <param name="pricer">The credit index pricer.</param>
    /// <returns>System.Double.</returns>
    [Obsolete("Replaced by FairValue()")]
    public static double MarketValue(
      this ICreditIndexOptionPricer pricer)
    {
      return FairValue(pricer);
    }

    /// <summary>
    /// Calculta the market value of the credit index option.
    /// </summary>
    /// <param name="pricer">The credit index pricer.</param>
    /// <param name="volatility">The volatility.</param>
    /// <returns>System.Double.</returns>
    [Obsolete("Replaced by FairValue()")]
    public static double MarketValue(
      this ICreditIndexOptionPricer pricer,
      double volatility)
    {
      return FairValue(pricer, volatility);
    }

    /// <summary>
    ///   Calculate the discounted exercise value at expiration date
    /// </summary>
    /// 
    /// <remarks>This function is for backward compatibility only.</remarks>
    ///
    /// <returns>The exercise value per unit notional, discounted back to as-of date</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer =
    ///     new CDXOptionPricer( cdxOption, asOf, settle, discountCurve,
    ///                          survivalCurves,  spread, vol );
    ///   pricer.ModelType = model;
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate exercise price
    ///   double exerciseValue = pricer.ExerciseValue();
    ///
    /// </code>
    /// </example>
    /// <exclude />
    [Obsolete("Not well defined.  Use ForwardStrikeValue instead.")]
    public static double ExerciseValue(this ICreditIndexOptionPricer pricer)
    {
      double v = ExercisePrice(pricer) - 1;
      v *= pricer.DiscountCurve.DiscountFactor(
        pricer.AsOf, pricer.CDXOption.Expiration);
      return v;
    }

    /// <summary>
    ///  Calculate the strike value.
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <returns>System.Double.</returns>
    /// <exclude />
    [Obsolete("Not well defined.  Use ForwardStrikeValue instead.")]
    public static double StrikeValue(this ICreditIndexOptionPricer pricer)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      return cdxoPicer != null
        ? cdxoPicer.StrikeValue()
        : pricer.ForwardStrikeValue;
    }

    /// <summary>
    ///   Index upfront value per unit notional
    /// </summary>
    ///
    /// <remarks>
    ///   This is the present value of the expected forward value
    ///   plus the front end protection.  The expectation is taken
    ///   based on the particular distribution of spread or price.
    ///   The value is per unit notional and 
    ///   is discounted by both the survival probability from settle to expiry
    ///   and the discount factor from as-of to expiry.
    /// </remarks>
    ///
    /// <returns>Index upfront value</returns>
    [Obsolete("Not well defined. Better use AtTheMoneyForwardValue")]
    public static double IndexUpfrontValue(this ICreditIndexOptionPricer pricer)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      return cdxoPicer != null
        ? cdxoPicer.IndexUpfrontValue()
        : pricer.AtTheMoneyForwardValue * pricer.DiscountCurve.DiscountFactor(
          pricer.AsOf, pricer.CDXOption.Expiration);
    }

    /// <summary>
    ///   Index upfront price (unit-based)
    /// </summary>
    ///
    /// <remarks>
    ///   This is simply <formula inline="true">(1 + \mathrm{IndexUpfrontValue})</formula>, where IndexUpfrontValue
    ///   is the present value of the expected forward value
    ///   plus the front end protection.  The expectation is taken
    ///   based on the particular distribution of spread or price.
    ///   The IndexUpfrontValue is per unit notional and
    ///   is discounted by both the survival probability from settle to expiry
    ///   and the discount factor from as-of to expiry.
    /// </remarks>
    ///
    /// <returns>Index upfront price (1 = par)</returns>
    [Obsolete("Not well defined. Better use AtTheMoneyForwardValue")]
    public static double IndexUpfrontPrice(this ICreditIndexOptionPricer pricer)
    {
      return 1 + IndexUpfrontValue(pricer);
    }

    /// <summary>
    /// Calculate the fair value of the Option
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <param name="volatility">The volatility.</param>
    /// <returns>System.Double.</returns>
    /// <exclude />
    public static double FairValue(
      this ICreditIndexOptionPricer pricer,
      double volatility)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      return cdxoPicer != null
        ? cdxoPicer.MarketValue(volatility)
        : (pricer.CalculateFairPrice(volatility) * pricer.Notional);
    }
    #endregion

    #region Extension methods

    /// <summary>
    ///   Set the current index factor and the existing losses
    ///   to be included in front end protection.
    /// </summary>
    /// <param name="pricer">The credit index option pricer</param>
    /// <param name="factor">The current index factor</param>
    /// <param name="losses">The existing losses included front end protection</param>
    public static void SetIndexFactorAndLosses(
      this ICreditIndexOptionPricer pricer, double factor, double losses)
    {
      var p = pricer as CreditIndexOptionPricer;
      if (p == null) return;
      if (!(factor <= 1.0 && factor >= 0.0))
        throw new ArgumentException(String.Format("Invalid index factor {0}", factor));
      if (!(losses >= 0.0 && losses + factor <= 1.0))
        throw new ArgumentException(String.Format("Invalid index loss {0}", losses));
      p.CurrentFactor = factor;
      p.ExistingLoss = losses;
    }

    /// <summary>
    ///  Calculate the fair price (per original notional) of the credit index option.
    /// </summary>
    /// <param name="pricer">The credit index pricer.</param>
    /// <returns>System.Double.</returns>
    public static double OptionPremium(this ICreditIndexOptionPricer pricer)
    {
      return FairValue(pricer)/pricer.CurrentNotional;
    }

    /// <summary>
    /// Calculate the fair value of the Option
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <returns>System.Double.</returns>
    /// <exclude/>
    public static double FairValue(this ICreditIndexOptionPricer pricer)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      return cdxoPicer != null
        ? cdxoPicer.MarketValue()
        : (pricer.CalculateFairPrice(pricer.Volatility) * pricer.Notional);
    }

    /// <summary>
    ///  Calculate the fair price (per original notional) of the credit index option.
    /// </summary>
    /// <param name="pricer">The credit index pricer.</param>
    /// <returns>System.Double.</returns>
    public static double FairPrice(this ICreditIndexOptionPricer pricer)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      return cdxoPicer != null
        ? cdxoPicer.FairPrice()
        : pricer.CalculateFairPrice(Double.NaN);
    }

    /// <summary>
    /// Calculta the fair price of the credit index option.
    /// </summary>
    /// <param name="pricer">The credit index pricer.</param>
    /// <param name="volatility">The volatility.</param>
    /// <returns>System.Double.</returns>
    public static double FairPrice(
      this ICreditIndexOptionPricer pricer,
      double volatility)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      return cdxoPicer != null
        ? cdxoPicer.FairPrice(volatility)
        : pricer.CalculateFairPrice(volatility);
    }

    /// <summary>
    ///   Daily basis point volatility
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The daily basis point volatility assuming a 252 business day
    ///   year.</para>
    ///
    ///   <formula>
    ///     DailyVol(bp) = \frac{ForwardSpread \times AnnualVol}{\sqrt{252}}
    ///   </formula>
    ///   <para><i>where:</i></para>
    ///   <para><i>ForwardSpread = Adjusted Forward Spread in basis points</i></para>
    ///   <para><i>AnnualVol = Annualised percentage volatility</i></para>
    ///
    ///   <para>This can be shown to be roughly the daily breakeven volatility. If our
    ///   daily spread move is greater than this amount then buying volatility is
    ///   profitable.</para>
    ///
    /// </remarks>
    ///
    /// <returns>the daily basis point volatility</returns>
    ///
    public static double BpVolatility(this ICreditIndexOptionPricer pricer)
    {
      return pricer.AdjustedForwardSpread() * pricer.Volatility / Math.Sqrt(252);
    }

    /// <summary>
    ///   Calculate adjusted forward spread
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <returns>System.Double.</returns>
    public static double AdjustedForwardSpread(
      this ICreditIndexOptionPricer pricer)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      return cdxoPicer != null
        ? cdxoPicer.AdjustedForwardSpread()
        : pricer.AtmForwardSpread();
    }

    /// <summary>
    ///   Calculate effective forward spread depending on where AdjustSpread flag is set.
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <returns>System.Double.</returns>
    public static double EffectiveForwardSpread(
      this ICreditIndexOptionPricer pricer)
    {
      var cdxoPricer = pricer as CDXOptionPricer;
      return cdxoPricer != null
        ? (cdxoPricer.AdjustSpread
          ? cdxoPricer.AdjustedForwardSpread()
          : cdxoPricer.ForwardSpread())
        : pricer.AtmForwardSpread();
    }

    private static double AtmForwardSpread(this ICreditIndexOptionPricer pricer)
    {
      var pv01 = pricer.ForwardPv01;
      return pv01.Equals(0.0)
        ? 0.0
        : (pricer.AtTheMoneyForwardValue/pricer.ForwardPv01
          + pricer.CDXOption.CDX.Premium);
    }

    /// <summary>
    ///   Calculates expected forward value of the underlying CDX at expiration date
    /// </summary>
    ///
    /// <returns>Forward value at expiration date</returns>
    public static double ForwardValue(
      this ICreditIndexOptionPricer pricer)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      return cdxoPicer != null
        ? cdxoPicer.ForwardValue()
        : (pricer.ForwardUpfrontValue * pricer.Notional);
    }

    /// <summary>
    ///   Calculate the intrinsic value of option
    /// </summary>
    ///
    /// <returns>Intrinsic dollar value (pv to pricing as-of date)</returns>
    public static double Intrinsic(this ICreditIndexOptionPricer pricer)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      return cdxoPicer != null
        ? cdxoPicer.Intrinsic()
        : pricer.OptionIntrinsicValue * pricer.Notional;
    }

    /// <summary>
    ///   Calculate the strike price at expiration date
    /// </summary>
    ///
    /// <returns>The strike price (1-based and not discounted)</returns>
    public static double ExercisePrice(this ICreditIndexOptionPricer pricer)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      return cdxoPicer != null
        ? cdxoPicer.ExercisePrice()
        : (pricer.ForwardPv01.Equals(0.0)
          ? 0.0
          : (1 - pricer.ForwardStrikeValue));
    }

    /// <summary>
    /// Gets the current spread of the underlying index.
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <returns>System.Double.</returns>
    public static double GetIndexSpread(this ICreditIndexOptionPricer pricer)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      return cdxoPicer != null ? cdxoPicer.Spread : CalculateIndexSpread(pricer);
    }

    /// <summary>
    ///   Calculate the vega for a CDX Option
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The vega is calculated as the difference between the current
    ///   fair value, and the fair value after increasing the volatility by
    ///   the specified bump size.</para>
    /// </remarks>
    ///
    /// <param name="pricer">The option pricer</param>
    /// <param name="bump">Bump for volatility in percent (0.01 = 1 percent)</param>
    ///
    /// <returns>The CDS Option vega</returns>
    ///
    /// <example>
    /// <code language="C#">
    ///
    ///   // ...
    ///   // Create a pricing for the CDX Option.
    ///   CDXOptionPricer pricer =
    ///     new CDSOptionPricer( cdxOption, asOf, settle, discountCurve,
    ///                          survivalCurves, quotedSpread, volatility);
    ///   pricer.ModelType = model;
    ///   pricer.AdjustSpread = adjustSpread;
    ///
    ///   // Calculate option vega for a 1pc move up in volatility
    ///   double vega = pricer.Vega(0.01);
    ///
    /// </code>
    /// </example>
    ///
    public static double Vega(this ICreditIndexOptionPricer pricer,
      double bump)
    {
      return VegaAt(pricer, pricer.Volatility, bump);
    }

    /// <summary>
    /// Calculate the vega at the specified volatility.
    /// </summary>
    /// <param name="pricer">The option pricer.</param>
    /// <param name="volatility">The volatility.</param>
    /// <param name="bump">The bump.</param>
    /// <returns>System.Double.</returns>
    /// <exception cref="BaseEntity.Toolkit.Util.ToolkitException">
    /// </exception>
    public static double VegaAt(this ICreditIndexOptionPricer pricer,
      double volatility, double bump)
    {
      var cdxo = pricer.CDXOption;
      if (cdxo.Expiration < pricer.AsOf)
      {
        return 0.0;
      }
      if (Double.IsNaN(volatility) || volatility < 0)
      {
        throw new ToolkitException(String.Format(
          "Invalid volatility ({0})", volatility));
      }

      double y0 = FairValue(pricer, volatility);
      double y1 = FairValue(pricer, volatility + bump);
      return y1 - y0;
    }

    #region Spread Delta
    /// <summary>
    ///  Calculates the Spread01 with the specified bump sizes.
    /// </summary>
    /// <param name="pricer">The option pricer.</param>
    /// <param name="upBump">Up bump size in basis points (1bp = 0.0001).</param>
    /// <param name="downBump">Down bump size in basis points (1bp = 0.0001).</param>
    /// <param name="bumpFlags">bump flags(BumpRelative, BumpInPlace, etc.)</param>
    /// <returns>System.Double.</returns>
    /// <exception cref="ToolkitException"></exception>
    public static double Spread01(this ICreditIndexOptionPricer pricer,
      double upBump, double downBump, BumpFlags bumpFlags)
    {
      var cdxoPicer = pricer as CDXOptionPricer;
      if (cdxoPicer != null) 
        return cdxoPicer.Spread01(upBump, downBump, bumpFlags);

      if (!(upBump + downBump > 1E-12))
      {
        throw new ToolkitException(String.Format(
          "Invalid bump sizes: up {0}, down {1}", upBump, downBump));
      }

      var spread = CalculateIndexSpread(pricer);
      spread += upBump / 10000.0;
      var pvUp = pricer.BumpedSpreadPv(spread);
      spread -= (upBump + downBump)/10000.0;
      var pvDown = pricer.BumpedSpreadPv(spread);
      return (pvUp - pvDown) / (upBump + downBump) * 100.0;
    }


    private static double BumpedSpreadPv(
      this ICreditIndexOptionPricer pricer,
      double spread)
    {
      return pricer.Update(new MarketQuote(spread,
        QuotingConvention.CreditSpread))
        .CalculateFairPrice(Double.NaN);
    }
    #endregion

    #region Market Delta

    /// <summary>
    ///   Calculate Market Delta
    /// </summary>
    /// <remarks>Market delta is defined as the ratio of the change in option price
    ///  to the change in index upfront for a 1bp widening of index spread.
    ///  It describes how the option price changes with the underlying index price.</remarks>
    /// <param name="pricer">The CDX option pricer</param>
    /// <param name="bumpSize">Bump size in raw number</param>
    /// <returns>Delta in raw number</returns>
    public static double MarketDelta(
      this ICreditIndexOptionPricer pricer,
      double bumpSize)
    {
      // backward compatible.
      return MarketDelta(pricer, bumpSize, true);
    }

    /// <summary>
    ///   Calculate Market Delta
    /// </summary>
    /// <param name="pricer">The option pricer.</param>
    /// <param name="bumpSize">Size of the bump in raw number.</param>
    /// <param name="useDealPremium">if set to <c>true</c>, use deal premium.</param>
    /// <returns></returns>
    /// <exclude/>
    public static double MarketDelta(this ICreditIndexOptionPricer pricer,
      double bumpSize, bool useDealPremium)
    {
      if (Math.Abs(bumpSize) < 1.0E-8)
        throw new ArgumentException("bumpSize size too small");
      return CalculateMarketDelta(pricer, bumpSize, useDealPremium,
        pricer is CDXOptionPricer ? CDXOptionPricer.BumpedPvFn : BumpedPvs);
    }



    /// <summary>
    /// Slove strike with given delta for Cdx option pricer
    /// </summary>
    /// <param name="pricer">Index option pricer</param>
    /// <param name="delta">the input market delta</param>
    /// <param name="bumpSize">The bumpsize(in raw number) for the delta calculated</param>
    /// <returns>The solved strike</returns>
    public static double SolveStrikeFromDelta(this ICreditIndexOptionPricer pricer,
      double delta, double bumpSize)
    {
      var isPayer = pricer.CDXOption.Type == OptionType.Put;

      if (isPayer && delta < 0.0)
        throw new ArgumentException("The delta of payer index option cannot be negative");

      if (!isPayer && delta > 0.0)
        throw new ArgumentException("The delta of receiver index option cannot be positive");

      if (!double.IsNaN(pricer.CDXOption.Strike))
      {
        if (Math.Abs(pricer.MarketDelta(bumpSize) - delta) < 1E-6)
          return pricer.CDXOption.Strike;
      }
      var solver = new Brent2();
      solver.setToleranceX(1e-10);
      solver.setToleranceF(1e-10);
      solver.setLowerBounds(0.0);

      var cdxPricer = pricer.GetPricerForUnderlying();
      var origQuote = cdxPricer.MarketQuote;

      var option = pricer.CDXOption;
      var originalStrike = option.Strike;

      Func<double, double> fn = s =>
      {
        pricer.CDXOption.Strike = s;
        return pricer.MarketDelta(bumpSize);
      };

      try
      {
        if (option.StrikeIsPrice)
        {
          var indexSpread = CalculateIndexSpread(pricer);
          var up = cdxPricer.SpreadToPrice(0.5 * indexSpread);
          var low = Math.Max(0.0, cdxPricer.SpreadToPrice(3 * indexSpread));
          return solver.solve(fn, null, delta, low, up);
        }
        return solver.solve(fn, null, delta, 0.5 * origQuote, 2.0 * origQuote);
      }
      catch (SolverException ex)
      {
        return Double.NaN;
      }
      finally
      {
        pricer.CDXOption.Strike = originalStrike;
      }
    }

    /// <summary>
    ///   Calculate Market Gamma
    /// </summary>
    ///
    /// <remarks>Market gamma is defined as the change in market delta
    ///  for a 1bp bump of index spread.</remarks>
    ///
    /// <param name="pricer">The option pricer.</param>
    /// <param name="upBump">Up bump size in raw number</param>
    /// <param name="downBump">Down bump size in raw number</param>
    /// <param name="scale">If true, scale gamma by bump size</param>
    ///
    /// <returns>Gamma in raw number</returns>
    /// <exclude />
    public static double MarketGamma(this ICreditIndexOptionPricer pricer,
      double upBump, double downBump, bool scale)
    {
      return MarketGamma(pricer, upBump, downBump, scale, true);
    }

    /// <summary>
    /// Markets the gamma.
    /// </summary>
    /// <param name="pricer">The option pricer.</param>
    /// <param name="upBump">Up bump.</param>
    /// <param name="downBump">Down bump.</param>
    /// <param name="scale">if set to <c>true</c> [scale].</param>
    /// <param name="useDealPremium">if set to <c>true</c> [use deal premium].</param>
    /// <returns></returns>
    /// <exclude/>
    public static double MarketGamma(this ICreditIndexOptionPricer pricer,
      double upBump, double downBump, bool scale, bool useDealPremium)
    {
      return CalculateMarketGamma(pricer, upBump, downBump, scale, useDealPremium,
        pricer is CDXOptionPricer ? CDXOptionPricer.BumpedPvFn : BumpedPvs);
    }

    private static double CalculateMarketGamma(
      this ICreditIndexOptionPricer pricer,
      double upBump, double downBump, bool scale, bool useDealPremium,
      Func<ICreditIndexOptionPricer, double, bool, double[]> bumpedPvs)
    {
      const double tolerance = 1E-7;
      if (upBump <= tolerance && downBump <= tolerance)
      {
        throw new ArgumentException(String.Format(
          "upBump ({0}) and downBump ({1}) cannot be both negative or both close to 0",
          upBump, downBump));
      }

      if (pricer is CDXOptionPricer && ((CDXOptionPricer) pricer).FullReplicatingMethod)
      {
        throw new ArgumentException("Gamma calculation with full replicating not supported in" +
                                    "legacy CDX option pricer");
      }

      var flag = pricer is CDXOptionPricer ?
        useDealPremium : ((CreditIndexOptionPricer)pricer).FullReplicatingMethod;

      double[] basePvs = bumpedPvs(pricer, 0.0, flag);
      if (upBump <= tolerance || downBump <= tolerance)
      {
        // One sided gamma
        var gammaBump = upBump > tolerance ? upBump : (-downBump);
        var deltaBump = gammaBump / 10;
        if (deltaBump > 0.0001) { deltaBump = 0.0001; }
        var pvs0 = basePvs;
        var pvs1 = bumpedPvs(pricer, deltaBump, flag);
        double delta1 = (pvs1[0] - pvs0[0]) / (pvs1[1] - pvs0[1]);
        pvs0 = bumpedPvs(pricer, gammaBump, flag);
        pvs1 = bumpedPvs(pricer, gammaBump + deltaBump, flag);
        double delta2 = (pvs1[0] - pvs0[0]) / (pvs1[1] - pvs0[1]);
        return scale ? ((delta2 - delta1) / (10000 * gammaBump)) : (delta2 - delta1);
      }
      // symmetric gamma
      double[] upPvs = bumpedPvs(pricer, upBump, flag);
      double[] downPvs = bumpedPvs(pricer, -downBump, flag);
      double upDelta = (upPvs[0] - basePvs[0]) / (upPvs[1] - basePvs[1]);
      double downDelta = (downPvs[0] - basePvs[0]) / (downPvs[1] - basePvs[1]);
      double gamma = upDelta - downDelta;
      if (scale) gamma /= 5000 * (upBump + downBump);
      return gamma;
    }

    /// <summary>
    /// Calculate Market Delta
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <param name="useDealPremium">if set to <c>true</c>, use deal premium.</param>
    /// <param name="bumpedPvs">The bumped PVS.</param>
    /// <returns>System.Double.</returns>
    /// <exception cref="System.ArgumentException">bumpSize size too small</exception>
    /// <exclude />
    private static double CalculateMarketDelta(
      ICreditIndexOptionPricer pricer,
      double bumpSize, bool useDealPremium,
      Func<ICreditIndexOptionPricer, double, bool, double[]> bumpedPvs)
    {
      if (Math.Abs(bumpSize) < 1.0E-8)
        throw new ArgumentException("bumpSize size too small");
      if (pricer is CDXOptionPricer && ((CDXOptionPricer) pricer).FullReplicatingMethod)
      {
        throw new ArgumentException("Delta calculation with full " +
                   "replicating not supported in legacy CDX option pricer");
      }
      var flag = pricer is CDXOptionPricer ?
        useDealPremium : ((CreditIndexOptionPricer)pricer).FullReplicatingMethod;
      double[] pvs0 = bumpedPvs(pricer, 0.0, flag);
      double[] pvs1 = bumpedPvs(pricer, bumpSize, flag);
      return (pvs1[0] - pvs0[0]) / (pvs1[1] - pvs0[1]);
    }

    /// <summary>
    /// Calculate option price and underlying index upfront after bumping the market spread
    /// or underlying survival curves
    /// </summary>
    /// <param name="pricer">The pricer.</param>
    /// <param name="bumpSize">bump size</param>
    /// <param name="useUnderlying">if set to <c>true</c>, use underlying for index value 
    /// calculation; otherwise, use the market spread.</param>
    /// <returns>option price and underlying index upfront</returns>
    private static double[] BumpedPvs(
      ICreditIndexOptionPricer pricer,
      double bumpSize, bool useUnderlying)
    {
      double optionValue, indexValue;
      if (useUnderlying)
      {
        var p = (CreditIndexOptionPricer)pricer;
        var survivalCurves = p.SurvivalCurves;
        if (survivalCurves == null)
            throw new ArgumentException("The underlying survival curves " +
              "cannot be null to use the full replicating method.");
        var savedCurves = survivalCurves.CloneObjectGraph();
        try
        {
          if (!bumpSize.AlmostEquals(0.0))
          {
            CurveUtil.BumpQuotes(survivalCurves, null, QuotingConvention.CreditSpread,
              new[] {bumpSize*10000}, BumpFlags.RefitCurve, null);
            p.Reset();
          }
          optionValue = p.CalculateFairPrice(p.Volatility);
          indexValue = CalculateIndexValue(p, true);
          return new[] {optionValue, indexValue};
        }
        finally
        {
          CurveUtil.CurveRestoreQuotes(p.SurvivalCurves, savedCurves);
          CurveUtil.CurveSet(p.SurvivalCurves, savedCurves);
          p.Reset();
        }
      }
      var spread = CalculateIndexSpread(pricer);
      pricer = pricer.Update(new MarketQuote(
        spread + bumpSize, QuotingConvention.CreditSpread));
      var v = pricer.Volatility;
      optionValue = pricer.CalculateFairPrice(v);
      indexValue = CalculateIndexValue(pricer, false);
      return new[] {optionValue, indexValue};
    }

    private static double CalculateIndexValue(ICreditIndexOptionPricer pricer,
      bool useUnderlying)
    {
      var cdxPricer = pricer.GetPricerForUnderlying();
      var value = useUnderlying
        ? (cdxPricer.Accrued()- cdxPricer.IntrinsicValue(false))/cdxPricer.CurrentNotional
        : 1 - cdxPricer.MarketPrice();
      return cdxPricer.SurvivalCurves.IsNullOrEmpty() ? value
        : (pricer.CurrentNotional/pricer.Notional*value);
    }

    #endregion

    #region Market Theta
    /// <summary>
    ///   Calculate Market Theta
    /// </summary>
    ///
    /// <remarks>This is defined as the change in option value due to 1 day passage of time
    /// assuming everything else remains unchanged.  Unlike the normal theta function, this one is based
    /// on market data only and it does not require any credit curve present.</remarks>
    ///
    /// <param name="pricer">The option pricer.</param>
    /// <param name="toAsOf">Pricing as-of date for future pricing</param>
    /// <param name="toSettle">Settlement date for future pricing</param>
    /// <param name="keepForwards">Keep the front end and forward protections unchanged with the move of pricing dates</param>
    ///
    /// <returns>Theta in raw number</returns>
    public static double MarketTheta(this ICreditIndexOptionPricer pricer,
      Dt toAsOf, Dt toSettle, bool keepForwards = false)
    {
      double v0 = FairValue(pricer);
      if (keepForwards)
      {
        var p = pricer as CreditIndexOptionPricer;
        if (p == null)
        {
          throw new NotImplementedException(
            "Theta with forward unchanged not supported in legacy CDX option pricer");
        }
        var v1 = p.CalculateFairPrice(toAsOf, pricer.Volatility)
          / p.DiscountCurve.DiscountFactor(p.AsOf, toAsOf)
          * p.Notional;
        return v1 - v0;
      }
      Dt origAsOf = pricer.AsOf;
      Dt origSettle = pricer.Settle;
      try
      {
        pricer.AsOf = toAsOf;
        pricer.Settle = toSettle;
        pricer.Reset();
        double v1 = MarketValue(pricer);
        return (v1 - v0);
      }
      finally
      {
        //this.AdjustSpread = origAdjustSpread;
        pricer.Settle = origSettle;
        pricer.AsOf = origAsOf;
        pricer.Reset();
      }
    }

    #endregion

    #endregion

    #region Check default names
    /// <summary>
    /// Calculate the remaining notional at expiry and the loss pv
    /// from the names defaulted between settle and expiry
    /// </summary>
    /// <param name="survivalCurves">The survival curves.</param>
    /// <param name="recoveryCurves">The recovery curves.</param>
    /// <param name="weights">The weights.</param>
    /// <param name="asOf">The pricing date.</param>
    /// <param name="begin">Protection begin date (inclusive).</param>
    /// <param name="end">Protection end date (exclusive).</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="basketSize">Size of the basket.</param>
    /// <param name="loss">Default loss pv</param>
    /// <param name="remain">Proportion of the remaining notional at expiry</param>
    internal static void CheckDefaultedNames(
      this SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves,
      double[] weights,
      Dt asOf, Dt begin, Dt end,
      DiscountCurve discountCurve,
      int basketSize, bool handleFactors,
      out double loss, out double remain)
    {
      loss = 0; remain = 1;
      if (survivalCurves == null || survivalCurves.Length == 0
        || (survivalCurves.Length == 1 && survivalCurves[0] == null))
      {
        return;
      }

      // Do new calculation if defaultSensitivityCurve is set.
      if (IsSingleInternalSensitivityCurve(survivalCurves))
      {
        var creditCurve = survivalCurves[0];

        if (basketSize <= 0 || creditCurve.DefaultDate.IsEmpty())
        {
          return;
        }

        // Check if the default date is between settle and expriry.
        Dt defaultDate = creditCurve.DefaultDate;
        if (defaultDate < begin || defaultDate >= end)
          return;

        var recoveryCurve = GetFirstOrDefault(recoveryCurves)
          ?? GetRecoveryCurve(creditCurve);
        var recoveryRate = GetRecoveryRate(recoveryCurve, defaultDate);
        // For default sensitivity, we always assume one name
        // defaulted in the market pricing approach.
        loss = (1.0 - recoveryRate) / basketSize;
        if (discountCurve != null)
          loss *= discountCurve.DiscountFactor(asOf, defaultDate);
        remain = 1.0 - 1.0 / basketSize;
        return;
      }

      double aw = 1.0 / survivalCurves.Length;
      double total = 0, defaulted = 0;
      for (int i = 0; i < survivalCurves.Length; ++i)
      {
        double wi = weights != null ? weights[i] : aw;
        total += wi;

        Dt defaultDate = survivalCurves[i].DefaultDate;
        if (defaultDate.IsEmpty() || defaultDate >= end)
          continue; // no default before the end date

        if (defaultDate < begin)
        {
          if (handleFactors) defaulted += wi;
          continue; // no default on or after begin date
        }
        defaulted += wi;

        // In most cases, this part rarely hit
        if (recoveryCurves == null)
          recoveryCurves = survivalCurves.Select(GetRecoveryCurve).ToArray();
        double recoveryRate = GetRecoveryRate(recoveryCurves[i], defaultDate);
        double li = wi * (1 - recoveryRate);
        if (discountCurve != null)
          li *= discountCurve.DiscountFactor(asOf, defaultDate);
        loss += li;
      }
      if (total > 1E-14)
      {
        loss /= total;
        remain -= defaulted / total;
      }
      else
      {
        loss = remain = 0;
      }
      return;
    }

    private static bool IsSingleInternalSensitivityCurve(SurvivalCurve[] curves)
    {
      return curves != null && curves.Length == 1
        && (curves[0].Flags & CurveFlags.Internal) != 0;
    }

    private static T GetFirstOrDefault<T>(this IEnumerable<T> list)
    {
      return list == null ? default(T) : list.FirstOrDefault();
    }

    private static RecoveryCurve GetRecoveryCurve(SurvivalCurve survivalCurve)
    {
      var cal = survivalCurve.Calibrator as SurvivalCalibrator;
      return cal == null ? null : cal.RecoveryCurve;
    }

    private static double GetRecoveryRate(RecoveryCurve recoverCurve, Dt date)
    {
      return recoverCurve == null ? 0.4 : recoverCurve.RecoveryRate(date);
    }
    #endregion

    #region Some basic CDX calculations

    internal static double CalculateIndexModelPv(
      this ICreditIndexOptionPricer pricer)
    {
      return CalculateIndexModelPv(pricer.GetPricerForUnderlying());
    }

    private static double CalculateIndexSpread(
      ICreditIndexOptionPricer pricer)
    {
      var cdxPricer = pricer.GetPricerForUnderlying();
      switch (cdxPricer.QuotingConvention)
      {
      case QuotingConvention.CreditSpread:
        return cdxPricer.MarketQuote;
      case QuotingConvention.FlatPrice:
        return cdxPricer.PriceToSpread(cdxPricer.MarketQuote);
      case QuotingConvention.None:
        break;
      default:
        throw new NotSupportedException(String.Format(
          "{0} quote convention not supported yet",
          cdxPricer.QuotingConvention));
      }
      if (cdxPricer.SurvivalCurves.IsNullOrEmpty())
      {
        throw new ArgumentException(
          "Must provide index quote or survival curves");
      }
      double price = 1 + CalculateIndexModelPv(cdxPricer);
      return cdxPricer.PriceToSpread(price);
    }

    internal static double AdjustSpreadOrDefault(
      this ICreditIndexOptionPricer optionPricer, double cleanPv,
      SurvivalCurve[] sensitivityCurves)
    {
      var cdxPricer = optionPricer.GetPricerForUnderlying();
      var basketSize = cdxPricer.BasketSize;
      if (basketSize < 1 ||
        !IsSingleInternalSensitivityCurve(sensitivityCurves))
      {
        return cleanPv;
      }
      var sc = sensitivityCurves[0];
      if (sc == null) return cleanPv;

      var discountCurve = cdxPricer.DiscountCurve;
      var option = optionPricer.CDXOption;
      Dt defaultDate = sc.DefaultDate;
      if (defaultDate > option.Effective && defaultDate <= option.Expiration)
      {
        var dfltPv = (1 - cdxPricer.MarketRecoveryRate)
          * discountCurve.DiscountFactor(cdxPricer.AsOf, defaultDate);
        return ((basketSize - 1) * cleanPv - dfltPv) / basketSize;
      }

      var cdsPricer = new CDSCashflowPricer(CreateCompatibleCds(option.CDX),
        cdxPricer.AsOf, cdxPricer.Settle, discountCurve, sc, 0, TimeUnit.None)
      {
        RecoveryCurve = new RecoveryCurve(cdxPricer.AsOf, cdxPricer.MarketRecoveryRate)
      };
      return cdsPricer.ProductPv() - cdsPricer.Accrued();
    }

    /// <summary>
    /// Calculates the clean pv implied from market quote.
    /// </summary>
    /// <param name="cdxPricer">The CDX pricer.</param>
    /// <returns>The clean pv implied from market quote, or NaN if no market quote available.</returns>
    /// <exception cref="System.NotSupportedException"></exception>
    private static double CalculateIndexMarketPv(
      CDXPricer cdxPricer)
    {
      switch (cdxPricer.QuotingConvention)
      {
      case QuotingConvention.CreditSpread:
        return cdxPricer.SpreadToPrice(cdxPricer.MarketQuote) - 1;
      case QuotingConvention.FlatPrice:
        return cdxPricer.MarketQuote - 1;
      case QuotingConvention.None:
        return Double.NaN;
      default:
        throw new NotSupportedException(String.Format(
          "{0}: quote convention not supported yet",
          cdxPricer.QuotingConvention));
      }
    }

    /// <summary>
    /// Calculates the clean pv implied based on portfolio survival curves.
    /// </summary>
    /// <param name="pricer">The CDX pricer.</param>
    /// <returns>The clean pv based on portfolio survival curves, or NaN if no survival curves available.</returns>
    /// <exception cref="System.NotSupportedException"></exception>
    private static double CalculateIndexModelPv(CDXPricer pricer)
    {
      if (pricer.SurvivalCurves.IsNullOrEmpty())
        return Double.NaN;
      // To please CDXPricer, set dumy quote which is not used in intrinsic calculation.
      pricer = (CDXPricer)pricer.ShallowCopy();
      pricer.MarketQuote = pricer.CDX.Premium;
      pricer.QuotingConvention = QuotingConvention.CreditSpread;
      pricer.Reset();
      // Calculate the intrinsic clean pv.
      return pricer.IntrinsicValue(false) - pricer.Accrued();
    }

    internal static double CalculateIndexModelBasis(
      this CDX cdx,
      Dt pricingDate,
      Dt indexSettleDate,
      DiscountCurve discountCurve,
      double recoveryRate,
      SurvivalCurve[] survivalCurves,
      MarketQuote quote,
      out double marketPv)
    {
      var pricer = new CDXPricer(cdx, pricingDate, indexSettleDate,
        discountCurve, survivalCurves)
      {
        MarketQuote = quote.Value,
        QuotingConvention = quote.Type,
        MarketRecoveryRate = recoveryRate
      };
      var modelPv = CalculateIndexModelPv(pricer);
      if (Double.IsNaN(quote.Value))
      {
        if (Double.IsNaN(modelPv))
          throw new ArgumentException("Must provide index quote or survival curves");
        marketPv = modelPv;
        return 0.0;
      }
      marketPv = CalculateIndexMarketPv(pricer);
      if (Double.IsNaN(marketPv))
      {
        if (Double.IsNaN(modelPv))
          throw new ArgumentException("Failed to calculate both model pv and market pv");
        marketPv = modelPv;
        return 0;
      }
      return marketPv - modelPv;
    }

    internal static CDS CreateCompatibleCds(this CDX note)
    {
      CDS cds = new CDS(note.Effective, note.Maturity, note.Ccy, note.FirstPrem,
        note.Premium, note.DayCount, note.Freq, note.BDConvention, note.Calendar);
      cds.CopyScheduleParams(note);
      return cds;
    }

    internal static double GetForwardStrike(
      this ICreditIndexOptionPricer pricer,
      bool isPriceVolatility)
    {
      return isPriceVolatility
        ? (1 - pricer.GetForwardStrikeValue())
        : pricer.GetForwardStrikeSpread();
    }

    internal static double GetNumerairLevel(
      this ICreditIndexOptionPricer pricer,
      bool isPriceVolatility)
    {
      if (pricer is CDXOptionPricerBlack)
      {
        return isPriceVolatility ? 1.0 : pricer.ForwardPv01;
      }
      var df = pricer.DiscountCurve.DiscountFactor(
        pricer.AsOf, pricer.CDXOption.Expiration);
      return df*pricer.CurrentNotional/pricer.Notional
        *(isPriceVolatility ? 1.0 : pricer.ForwardPv01);
    }

    internal static bool IsPriceVolatility(
      this ICreditIndexOptionPricer cdxoPricer)
    {
      var p = cdxoPricer as CreditIndexOptionPricer;
      if (p != null)
      {
        return p.IsPriceVolatilityModel;
      }
      var q = cdxoPricer as CDXOptionPricerBlack;
      return q != null && q.PriceVolatilityApproach;
    }

    private static double GetForwardStrikeValue(
      this ICreditIndexOptionPricer cdxoPricer)
    {
      var cdxo = cdxoPricer.CDXOption;
      if (cdxo.StrikeIsPrice &&
        cdxoPricer.CurrentNotional.AlmostEquals(cdxoPricer.Notional))
      {
        return 1 - cdxo.Strike;
      }
      var p = cdxoPricer as CDXOptionPricer;
      return p != null
        ? (1 - p.SpreadToPrice(p.Strike))
        : cdxoPricer.ForwardStrikeValue;
    }

    private static double GetForwardStrikeSpread(
      this ICreditIndexOptionPricer cdxoPricer)
    {
      var p = cdxoPricer as CDXOptionPricer;
      if (p != null)
      {
        return p.StrikeIsPrice ? p.PriceToSpread(p.Strike) : p.Strike;
      }
      var premium = cdxoPricer.CDXOption.CDX.Premium;
      return cdxoPricer.ForwardStrikeValue/cdxoPricer.ForwardPv01 + premium;
    }

    #endregion
  }
}
