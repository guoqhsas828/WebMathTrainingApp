// 
//  -2015. All rights reserved.
// 

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.RateProjectors;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  using Builder = Func<IAssetReturnLeg, Dt, Dt, DiscountCurve,
    CalibratedCurve[], IAssetPriceIndex, IAssetReturnLegPricer>;

  /// <summary>
  /// Factory methods to build asset return leg pricer
  /// </summary>
  public static class AssetReturnLegPricerFactory
  {
    #region Pricer Extension Methods

    /// <summary>
    /// Gets the initial price.
    /// </summary>
    /// <param name="pricer">The asset return leg pricer.</param>
    /// <returns>System.Double.</returns>
    public static double GetInitialPrice(this IAssetReturnLegPricer pricer)
    {
      var leg = pricer.AssetReturnLeg;
      return double.IsNaN(leg.InitialPrice)
        ? pricer.GetPriceCalculator().GetPrice(leg.Effective).Value
        : leg.InitialPrice;
    }

    /// <summary>
    /// Create the asset price index for the specified asset.
    /// </summary>
    /// <param name="historicalObservations">The historical observations of the asset prices</param>
    /// <param name="priceType">Type of the quoted price (as in the historical observations)</param>
    /// <param name="underlyingAsset">The underlying asset</param>
    /// <param name="calendar">The business day calendar</param>
    /// <param name="settleDays">Number of business days from trade to settlement date</param>
    /// <param name="roll">Business day roll convention</param>
    /// <returns>IAssetPriceIndex.</returns>
    public static IAssetPriceIndex ToAssetPriceIndex(
      this RateResets historicalObservations,
      QuotingConvention priceType,
      IProduct underlyingAsset,
      Calendar calendar = new Calendar(),
      int settleDays = 0,
      BDConvention roll = BDConvention.Modified)
    {
      return new AssetPriceIndex(underlyingAsset.Description,
        QuotingConvention.FullPrice, underlyingAsset.Ccy,
        calendar, settleDays, roll, historicalObservations);
    }

    #endregion

    #region Public methods

    /// <summary>
    /// Registers the builder for the specified type of asset return leg.
    /// </summary>
    /// 
    /// <param name="assetReturnType">Type of the asset return.</param>
    /// <param name="builder">The pricer builder function.</param>
    /// <returns>The existing pricer builder associated with the pricer,
    ///   or null if no such builder exists</returns>
    /// 
    /// <remarks>
    ///  <para>By default the factory find all the types in the current
    ///  application domain which implement <see cref="IAssetReturnLegPricer"/>
    ///  interface, and collects the builders from their constructors with
    ///  appropriate parameters.  Normally there is no need to call this
    ///  method in order to register a builder.</para>
    /// 
    ///  <para>In the cases when it is desired to override the default behavior,
    ///  or when the factory fails to find the builder for the specific product type,
    ///  this method can be called to register the builders manually.</para>
    /// </remarks>
    public static Builder RegisterBuilder(Type assetReturnType, Builder builder)
    {
      Builder old = null;
      Builders.AddOrUpdate(assetReturnType, builder, (k, v) =>
      {
        old = v;
        return builder;
      });
      return old;
    }

    /// <summary>
    /// Creates the pricer for the specified asset return leg.
    /// </summary>
    /// 
    /// <param name="assetReturnLeg">The asset return leg.</param>
    /// <param name="asOf">The as-of date.</param>
    /// <param name="settle">The settle date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="referenceCurves">The reference curves.</param>
    /// <param name="assetPriceIndex">The asset price index</param>
    /// <returns>IAssetReturnLegPricer.</returns>
    /// <exception cref="System.ArgumentNullException">assetReturnLeg</exception>
    /// <exception cref="ToolkitException"></exception>
    /// 
    /// <remarks>
    ///  This method tries to infer the actual type of the pricer to create
    ///  based on the instance type of the asset return leg.  When the actual
    ///  pricer type is known, it is always better to directly call the pricer
    ///  constructor instead.  This method is only intended to be used in the
    ///  applications like Excel add-in which need an interface to create pricers
    ///  automatically based on the type of asset return leg.
    /// </remarks>
    public static IAssetReturnLegPricer CreatePricer(
      this IAssetReturnLeg assetReturnLeg,
      Dt asOf, Dt settle, DiscountCurve discountCurve,
      CalibratedCurve[] referenceCurves,
      IAssetPriceIndex assetPriceIndex)
    {
      if (assetReturnLeg == null)
        throw new ArgumentNullException("assetReturnLeg");

      Builder builder;
      var type = assetReturnLeg.GetType();
      if (!Builders.TryGetValue(type, out builder))
      {
        throw new ToolkitException(String.Format(
          "Unable to find a method to build the return leg pricer for {0}",
          type.FullName));
      }
      return builder(assetReturnLeg, asOf, settle, discountCurve,
        referenceCurves, assetPriceIndex);
    }

    #endregion

    #region Static data members

    private static readonly Lazy<ConcurrentDictionary<Type, Builder>> LazyBuildersMap
      = new Lazy<ConcurrentDictionary<Type, Builder>>(CreateMap);

    private static ConcurrentDictionary<Type, Builder> Builders => LazyBuildersMap.Value;

    #endregion

    #region Create builders from constructors

    private static int MatchParameters(ParameterInfo[] parameters,
      ConcurrentDictionary<Type, Builder> map)
    {
      var n = parameters.Length;
      if (n < 4) return 0;

      // The first 4 parameters must be 
      //   Product, AsOf, Settle, Discount Curve,
      // in that order.
      var productType = parameters[0].ParameterType;
      if (!typeof(IAssetReturnLeg).IsAssignableFrom(productType)
        || map.ContainsKey(productType) // ignore if it already has a builder
        || parameters[1].ParameterType != typeof (Dt)
        || parameters[2].ParameterType != typeof (Dt)
        || !typeof (DiscountCurve).IsAssignableFrom(parameters[3].ParameterType))
      {
        return 0;
      }
      if (n == 4)
      {
        return n;
      }

      // The rest parameters consist of 
      //   (1) zero or one parameter assignable to IAssetPriceIndex;
      //   (2) one of the following cases
      //      (2a) zero or one parameter assignable from CalibratedCurve[], or
      //      (2b) zero or more parameters assignable to CalibratedCurve.
      bool hasAssetPriceIndex = false, hasCurveList = false;
      IList<Type> curveTypes = null;
      for (int i = 4; i < parameters.Length; ++i)
      {
        var type = parameters[i].ParameterType;
        if (typeof(IAssetPriceIndex).IsAssignableFrom(type))
        {
          if (hasAssetPriceIndex) return 0;
          hasAssetPriceIndex = true;
          continue;
        }
        if (type.IsAssignableFrom(typeof(CalibratedCurve[])))
        {
          if (hasCurveList) return 0;
          hasCurveList = true;
          continue;
        }
        if (!typeof (CalibratedCurve).IsAssignableFrom(type))
          return 0;

        if (curveTypes == null) curveTypes = new List<Type>();
        else if(curveTypes.Contains(type)) return 0;
        curveTypes.Add(type);
      }

      if (hasCurveList && curveTypes != null)
      {
        return 0;
      }

      return n;
    }

    private static bool TryAddBuilder(ConstructorInfo ctor,
      ConcurrentDictionary<Type, Builder> map)
    {
      if (ctor == null) return false;

      var pars = ctor.GetParameters();
      int n = pars.Length;
      var args = new Expression[n];

      var largs = new[]
      {
        Expression.Parameter(typeof (IAssetReturnLeg), "assetReturnLeg"),
        Expression.Parameter(typeof (Dt), "asOf"),
        Expression.Parameter(typeof (Dt), "settle"),
        Expression.Parameter(typeof (DiscountCurve), "discountCurve"),
        Expression.Parameter(typeof (CalibratedCurve[]), "referenceCurves"),
        Expression.Parameter(typeof (IAssetPriceIndex), "assetPriceIndex"),
      };

      // product
      args[0] = Expression.Convert(largs[0], pars[0].ParameterType);
      // asOf
      args[1] = largs[1];
      // settle
      args[2] = largs[2];
      // discountCurve 
      args[3] = pars[3].ParameterType == largs[3].Type
        ? (Expression) largs[3]
        : Expression.Convert(largs[3], pars[3].ParameterType);

      var referenceCurves = largs[4];
      var priceIndex = largs[5];

      for (int i = 4; i < pars.Length; ++i)
      {
        var type = pars[i].ParameterType;
        if (priceIndex.Type.IsAssignableFrom(type))
        {
          args[i] = priceIndex.Type == type
            ? (Expression) priceIndex
            : Expression.Convert(priceIndex, type);
        }
        else if (typeof (CalibratedCurve).IsAssignableFrom(type))
        {
          args[i] = Expression.Call(GenericGet.MakeGenericMethod(type),
            referenceCurves);
        }
        else if (type.IsAssignableFrom(referenceCurves.Type))
        {
          args[i] = type == referenceCurves.Type
            ? (Expression) referenceCurves
            : Expression.Convert(referenceCurves, type);
        }
        else
        {
          throw new ArgumentException("Unable to process parameter"
            + $" {pars[i].Name} in the constructor of{ctor.DeclaringType}");
        }
      }
      var body = Expression.New(ctor, args);
      var fn = Expression.Lambda<Builder>(body, true, largs).Compile();
      map.AddOrUpdate(pars[0].ParameterType, fn, (k, v) => fn);

      return true;
    }

    /// <summary>
    /// Gets the first curve of the specified type from a list calibrated curves.
    /// </summary>
    /// <typeparam name="T">The type of the curve to find</typeparam>
    /// <param name="calibratedCurves">The calibrated curves</param>
    /// <returns>The first curve of type T, or null if no such curve exists</returns>
    internal static T Get<T>(IEnumerable<CalibratedCurve> calibratedCurves)
      where T : class
    {
      return calibratedCurves?.OfType<T>().FirstOrDefault();
    }

    private static MethodInfo _genericGet;

    private static MethodInfo GenericGet => _genericGet ?? (_genericGet =
      typeof (AssetReturnLegPricerFactory).GetMethod(nameof(Get),
        BindingFlags.Static | BindingFlags.NonPublic));

    private static bool AddBuilderFromConstructor(Type type,
      ConcurrentDictionary<Type, Builder> map)
    {
      const BindingFlags flags = BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic;
      var ctor = type.GetConstructors(flags)
        .Select(c => new {C = c, N = MatchParameters(c.GetParameters(), map)})
        .Where(o => o.N > 0)
        .OrderByDescending(o => o.N)
        .Select(o => o.C)
        .FirstOrDefault();
      return TryAddBuilder(ctor, map);
    }

    private static void Initialize(Assembly assembly,
      ConcurrentDictionary<Type, Builder> map)
    {
      if (assembly == null)
        return;

      var picerType = typeof (IAssetReturnLegPricer);
      foreach (var type in assembly.GetTypes().Where(t => t.IsClass
        && !t.IsAbstract && picerType.IsAssignableFrom(t)))
      {
        AddBuilderFromConstructor(type, map);
      }
    }

    private static ConcurrentDictionary<Type, Builder> CreateMap()
    {
      var map = new ConcurrentDictionary<Type, Builder>();
      Initialize(Assembly.GetExecutingAssembly(), map);
      return map;
    }

    #endregion

  }
}
