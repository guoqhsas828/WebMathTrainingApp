/*
 * CapBlackPricer.cs
 *
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Cap floor pricer configuration
  /// </summary>
  [Serializable]
  public class CapFloorPricerConfig
  {
    /// <summary>
    /// Use Actual365 day count
    /// </summary>
    public readonly bool TimeToExpiryInActual365 = true;

    // Backward compatible: false; recommended: true.
    internal readonly bool UsingIndexDayCountForProjection = false;
  }

  /// <summary>
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.Cap">Cap</see> using the
  ///   <see cref="BaseEntity.Toolkit.Models.Black">Black Model</see>.</para>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.Cap" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.Black" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.Cap">Cap Product</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Models.Black">Black model</seealso>
  [Serializable]
  public class CapFloorPricer : CapFloorPricerBase, IPricer<Cap>
  {
    #region Constructors
    /// <summary>
    ///   Construct a Cap pricer
    /// </summary>
    ///
    /// <param name="product">Cap to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="referenceCurve">Floating rate reference curve (if required)</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="volatility">Volatility (eg 0.2 = 20pc)</param>
    ///
    public CapFloorPricer(Cap product,
                          Dt asOf,
                          Dt settle,
                          DiscountCurve referenceCurve,
                          DiscountCurve discountCurve,
                          IVolatilityObject volatility)
      : base(product, asOf, settle, referenceCurve, discountCurve, volatility)
    {
    }

    #endregion

    /// <summary>
    ///   Cap Product
    /// </summary>
    public new Cap Cap
    {
      get { return (Cap)Product; }
    }

    /// <summary>
    /// Projects the forward rate.
    /// </summary>
    /// <param name="caplet">The caplet.</param>
    /// <returns>System.Double.</returns>
    public override double ProjectRate(CapletPayment caplet)
    {
      var dc = UsingIndexDayCountForProjection
        ? Cap.ReferenceRateIndex.DayCount : Cap.DayCount;
      return ReferenceCurve.F(caplet.RateFixing, caplet.TenorDate, dc, Frequency.None);
    }

    private bool UsingIndexDayCountForProjection =>
      ToolkitConfigurator.Settings.CapFloorPricer.UsingIndexDayCountForProjection;

    Cap IPricer<Cap>.Product { get { return Cap; } }
  }

  /// <summary>
  /// Class CapFloorPricerBase.
  /// </summary>
  [Serializable]
  public abstract partial class CapFloorPricerBase : PricerBase, IPricer, IRatesLockable
  {
    #region Constructors

    /// <summary>
    /// Creates the cap/floor pricer based on the product type.
    /// </summary>
    /// <param name="product">The cat/floor product</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">The settle</param>
    /// <param name="referenceCurve">The reference curve</param>
    /// <param name="discountCurve">The discount curve</param>
    /// <param name="volatility">The volatility</param>
    /// <param name="convexityParameters">The convexity adjustment parameters</param>
    /// <returns>CapFloorPricer for caps and floors;
    ///  CmsCapFloorPricer for CMS caps and floors</returns>
    public static CapFloorPricerBase CreatePricer(CapBase product,
      Dt asOf, Dt settle, DiscountCurve referenceCurve,
      DiscountCurve discountCurve, IVolatilityObject volatility,
      RateModelParameters convexityParameters = null)
    {
      var cmscap = product as CmsCap;
      if (cmscap != null)
      {
        return new CmsCapFloorPricer(cmscap, asOf, settle,
          referenceCurve, discountCurve, volatility, convexityParameters);
      }
      return new CapFloorPricer((Cap)product, asOf, settle,
        referenceCurve, discountCurve, volatility);
    }

    public static CapFloorPricerBase CreatePricer(CapBase cap,
      Dt asOf, Dt settle, IEnumerable<RateReset> resets,
      DiscountCurve referenceCurve, DiscountCurve discountCurve,
      IVolatilityObject volatility,
      RateModelParameters convexityParameters = null)
    {
      var pricer = CreatePricer(cap, asOf, settle, referenceCurve,
        discountCurve, volatility, convexityParameters);
      CollectionUtil.Add(pricer.Resets, resets);
      return pricer;
    }

    /// <summary>
    ///   Construct a Cap pricer
    /// </summary>
    ///
    /// <param name="product">Cap to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="referenceCurve">Floating rate reference curve (if required)</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="volatility">Volatility (eg 0.2 = 20pc)</param>
    ///
    protected CapFloorPricerBase(CapBase product,
                          Dt asOf,
                          Dt settle,
                          DiscountCurve referenceCurve,
                          DiscountCurve discountCurve,
                          IVolatilityObject volatility)
      : base(product, asOf, settle)
    {
      referenceCurve_ = referenceCurve;
      discountCurve_ = discountCurve;
      volatilityType_ = volatility.DistributionType == DistributionType.Normal
        ? VolatilityType.Normal : VolatilityType.LogNormal;
      volatilityCube_ = volatility;
      resets_ = new List<RateReset>();
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Projects the reference rate.
    /// </summary>
    /// <param name="caplet">The caplet.</param>
    /// <returns>System.Double.</returns>
    public abstract double ProjectRate(CapletPayment caplet);

    /// <summary>
    /// Gets the payment schedule.
    /// </summary>
    /// <param name="paymentSchedule">The payment schedule.</param>
    /// <param name="from">From.</param>
    /// <returns>PaymentSchedule.</returns>
    public override PaymentSchedule GetPaymentSchedule(
      PaymentSchedule paymentSchedule, Dt @from)
    {
      if (paymentSchedule == null)
      {
        paymentSchedule = new PaymentSchedule();
      }
      var index = Cap.ReferenceRateIndex;
      var projectionParams = new ProjectionParams
      {
        ProjectionType = index is SwapRateIndex
          ? ProjectionType.SwapRate
          : ProjectionType.SimpleProjection,
      };
      var projector = CouponCalculator.Get(AsOf, 
        index, ReferenceCurve, ReferenceCurve,
        projectionParams);
      var convexityAdjustment = ForwardAdjustment.Get(AsOf,
        DiscountCurve, ConvexityParameters, projectionParams);
      if (from.IsEmpty()) from = Settle;
      foreach (var caplet in Caplets.OfType<CapletPayment>())
      {
        if (caplet.PayDt <= from) continue;

        caplet.RateProjector = projector;
        caplet.ForwardAdjustment = convexityAdjustment;
        caplet.VolatilityObject = VolatilityCube;
        caplet.VolatilityStartDt = AsOf;
        paymentSchedule.AddPayment(caplet);
      }
      return paymentSchedule;
    }

    /// <summary>
    /// Reset the pricer
    /// </summary>
    public override void Reset()
    {
      // Clean up
      caplets_ = null;
      currentRate_ = null;

      // Base
      base.Reset();
    }

    /// <summary>
    /// Reset a specific thing on the pricer.
    /// </summary>
    /// 
    /// <param name="what">What to reset</param>
    /// 
    public override void Reset(ResetAction what)
    {
      // Handle caplet resets
      if (what == CapletChanged)
      {
        caplets_ = null;
      }
      else if(what == ProductChanged)
      {
      }
      else
      {
        base.Reset(what);
      }
    }

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// 
    /// <param name="errors">Array of resulting errors</param>
    /// 
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;

      base.Validate(errors);

      // Inavliad discount curve
      if (discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));

      // Invalid current rate
      RateResetUtil.Validate(Resets, errors);
    }

    /// <summary>
    ///   Calculate the present value <formula inline="true">Pv = Full Price \times Notional</formula> of the cap
    /// </summary>
    ///
    /// <returns>Present value to the settlement date of the cap</returns>
    ///
    public override double ProductPv()
    {
      return EffectiveNotional * CapPv().Item1;
    }

    /// <summary>
    /// Calculate the Cap model value and the caplet volatility list used in valuation
    /// </summary>
    /// <returns></returns>
    public Tuple<double, IList<double>> CapPv()
    {
      return CapPv(AsOf, Settle, Cap, Caplets, DiscountCurve, ForwardRate, VolatilityType, VolatilityCube);
    }

    /// <summary>
    ///   Calculate the intrinsic cap value
    /// </summary>
    ///
    /// <returns>Sum of discounted cap payments</returns>
    ///
    public double Intrinsic()
    {
      double intrinsic = 0;
      var pmtSchedule = Caplets;
      // Go through schedule and price caplets
      foreach (CapletPayment caplet in pmtSchedule)
      {
        if (caplet.PayDt > Settle)
        {
          double rate = ForwardRate(caplet);

          rate *= caplet.IndexMultiplier;
          var dt = caplet.PeriodFraction;
          double capletIntrinsic = 0.0;
          if (Cap.OptionDigitalType != OptionDigitalType.None)
            capletIntrinsic = DigitalOption.BlackP(OptionStyle.European, Cap.OptionType, Cap.OptionDigitalType, 0.0,
                                                   rate, Cap.Strike, 0.0, Cap.DigitalFixedPayout);
          else
          {
            if (Cap.Type == CapFloorType.Cap && rate > 0 && rate > Cap.Strike)
              capletIntrinsic = (rate - Cap.Strike);
            if (Cap.Type == CapFloorType.Floor && rate > 0 && rate < Cap.Strike)
              capletIntrinsic = (Cap.Strike - rate);
          }
          intrinsic += dt*capletIntrinsic*DiscountCurve.DiscountFactor(caplet.PayDt);
        }
      }
      // Done
      return intrinsic * EffectiveNotional;
    }

    /// <summary>
    /// Calculates the current forward rate for a caplet's reset.
    /// </summary>
    /// 
    /// <param name="caplet">The caplet</param>
    /// 
    /// <returns>double</returns>
    /// 
    public double ForwardRate(CapletPayment caplet)
    {
      return ForwardRate(caplet,Settle,AsOf);
    }


    internal double ForwardRate(CapletPayment caplet, Dt settle, Dt asOf)
    {
      if (caplet.Expiry <= settle)
      {
        if (caplet.RateResetState == RateResetState.Missing &&
          !RateResetUtil.ProjectMissingRateReset(caplet.Expiry, asOf, caplet.RateFixing))
        {
          throw new ToolkitException(String.Format("Missing Rate Reset for {0}", caplet.Expiry));
        }
        if (caplet.RateResetState == RateResetState.ObservationFound)
        {
          return caplet.Rate;
        }
      }
      return ProjectRate(caplet);
    }

    /// <summary>
    /// Present value of the caplet.
    /// </summary>
    /// 
    /// <param name="caplet">The caplet</param>
    /// 
    /// <returns>double</returns>
    /// 
    public double CapletPv(CapletPayment caplet)
    {
      return EffectiveNotional*CapletPv(AsOf, Settle, Cap, caplet, DiscountCurve, ForwardRate,
                                        VolatilityType, VolatilityCube).Item1;
    }

    /// <summary>
    ///   Calculates implied volatility for IR Cap
    /// </summary>
    ///
    /// <returns>double</returns>
    ///
    public double ImpliedVolatility()
    {
      return ImpliedVolatility(VolatilityType, ProductPv());
    }

    /// <summary>
    ///   Calculates implied volatility for IR Cap
    /// </summary>
    ///
    /// <param name="fv">Fair value of IR cap in dollars</param>
    ///
    /// <returns>double</returns>
    ///
    public double ImpliedVolatility(double fv)
    {
      return ImpliedVolatility(VolatilityType, fv);
    }

    /// <summary>
    ///   Calculates implied volatility for IR Cap
    /// </summary>
    ///
    /// <param name="volType">Type of volatility to imply</param>
    ///
    /// <returns>double</returns>
    ///
    public double ImpliedVolatility(VolatilityType volType)
    {
      return ImpliedVolatility(volType, ProductPv());
    }

    /// <summary>
    ///   Calculates implied volatility for IR Cap
    /// </summary>
    ///
    /// <param name="volType">Volatility type to imply</param>
    /// <param name="fv">Fair value of IR cap in dollars</param>
    ///
    /// <returns>double</returns>
    ///
    public double ImpliedVolatility(VolatilityType volType, double fv)
    {
      // Validate non-zero value
      if (Math.Abs(fv) <= 1e-12)
        return 0;
      // Set up root finder
      // Setup function to solve
      Double_Double_Fn f = delegate(double vol, out string msg)
                           {
                             double val = 0;
                             try
                             {
                               msg = null;
                               var cube = new RateVolatilityCube(new RateVolatilityFlatCalibrator(AsOf, new[] {AsOf},
                                                                                                  volType, Cap.GetRateIndex(), new[] {vol}));
                               cube.Fit();
                               val = EffectiveNotional * CapPv(AsOf, Settle, Cap, Caplets, DiscountCurve, ForwardRate,
                                                               volType, cube).Item1;
                             }
                             catch (Exception ex)
                             {
                               msg = ex.ToString();
                             }
                             return val;
                           };

      // Solve
      string desc;
      double lb0 = (volType == VolatilityType.LogNormal) ? 0.001 : 0.00001;
      double ub0 = (volType == VolatilityType.LogNormal) ? 2.0 : 0.20;
      //be careful with digital options, whose price is not an increasing function of vol
      double ftol = Math.Min(ToleranceF > 0 ? ToleranceF : 1e-6, Math.Abs(fv) / 10);
      double xtol = ToleranceX > 0 ? ToleranceX
        : ((volType == VolatilityType.LogNormal) ? 1e-3 : 1e-5);
      if (Cap.OptionDigitalType == OptionDigitalType.None)
      {
        double x0 = (volType == VolatilityType.LogNormal) ? 0.1 : 0.0010;
        var solverFn = new DelegateSolverFn(f, null);
        var rf = new Brent();

        rf.setToleranceX(xtol);
        rf.setToleranceF(ftol);
        rf.setLowerBounds(1E-10);
        double v = f(x0, out desc);
        return (v >= fv) ? rf.solve(solverFn, fv, lb0, x0) : rf.solve(solverFn, fv, x0, ub0);
      }
      else
      {

        try
        {
          
          string msg;
          Func<double, double> chart = omega => lb0 + (0.5 + Math.Atan(omega)/ Math.PI) * (ub0 - lb0);
          double x = 0, y, e;
          int iter = 0;
          while (Math.Abs(e = (y = f(chart(x), out msg)) - fv) > ftol)
          {
            double yp = 1e4*(f(chart(x + 1e-4), out msg) - y);
            if (iter > 50)
              throw new ToolkitException("Maximum number of iterations  exceeded.");
            if(Math.Abs(yp) < 1e-8)
            {
              return chart(x);//return the extremum
            }
            x -= e / yp;
            ++iter;
          }
          return chart(x);
        }
        catch
          (Exception ex)
        {
          throw new ToolkitException(String.Format("Failed to solve for Implied volatility. The following error was thrown {0}", ex.Message));
        }
      }
    }

    /// <summary>
    /// The change in value of the Cap due to a 1 Vol increase in Implied Volatility.
    /// </summary>
    /// <remarks>
    ///   <para>For <paramref name="volType"/> of <see cref="BaseEntity.Toolkit.Base.VolatilityType.LogNormal"/> 1 Vol is 1%, while 
    ///     for <see cref="BaseEntity.Toolkit.Base.VolatilityType.Normal" /> 1 Vol is 1bp.
    ///   </para>
    /// </remarks>
    /// <returns>double</returns>
    /// 
    public double ImpliedVolatility01(VolatilityType volType)
    {
      // Get base pv
      double pvBase = ProductPv();
      // Get the implied vol
      var ivol = ImpliedVolatility();
      // Make sure we have log normal vols
      ivol = ConvertCapVol(ivol, VolatilityType, volType);
      // Shift
      ivol += (volType == VolatilityType.LogNormal ? 0.01 : 0.0001);
      // Reprice
      double pvShift = EffectiveNotional*CapPv(AsOf, Settle, Cap, Caplets, DiscountCurve, ForwardRate,
                                               volType, new RateVolatilityCube(AsOf, ivol, volType)).Item1;
      // Delta
      return (pvShift - pvBase);
    }

    /// <summary>
    /// Converts a Cap volatility for this Cap.
    /// </summary>
    /// <param name="vol"></param>
    /// <param name="fromType"></param>
    /// <param name="toType"></param>
    /// <returns></returns>
    private double ConvertCapVol(double vol, VolatilityType fromType, VolatilityType toType)
    {
      return LogNormalToNormalConverter.ConvertCapVolatility(AsOf, Settle, DiscountCurve, Cap, vol, Resets, fromType, toType);
    }

    /// <summary>
    /// (Rate) Delta of the given caplet.
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Calculated via Black model analytic formula.</para>
    /// </remarks>
    /// 
    /// <param name="caplet">Caplet</param>
    /// 
    /// <returns>double</returns>
    /// 
    public double CapletDeltaBlack(CapletPayment caplet)
    {
      // Value caplet
      double delta, gamma, vega, theta;
      CapletPv(AsOf, Settle, Cap, caplet, DiscountCurve, ForwardRate,
        VolatilityType, VolatilityCube, out delta, out gamma, out theta, out vega);

      // Done
      return delta*EffectiveNotional;
    }


    
    /// <summary>
    /// (Rate) Gamma of the given caplet.
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Calculated via Black model analytic formula.</para>
    /// </remarks>
    /// 
    /// <param name="caplet">Caplet</param>
    /// 
    /// <returns>double</returns>
    /// 
    public double CapletGamma(CapletPayment caplet)
    {
      // Value caplet
      double delta, gamma, vega, theta;
      CapletPv(AsOf, Settle, Cap, caplet, DiscountCurve, ForwardRate,
        VolatilityType, VolatilityCube, out delta, out gamma, out theta, out vega);

      // Done
      return gamma;
    }

    /// <summary>
    /// Vega of the given caplet.
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Calculated via Black model analytic formula.</para>
    /// </remarks>
    /// 
    /// <param name="caplet">Caplet</param>
    /// 
    /// <returns>double</returns>
    /// 
    public double CapletVegaBlack(CapletPayment caplet)
    {
      // Value caplet 
      double delta, gamma, vega, theta;
      CapletPv(AsOf, Settle, Cap, caplet, DiscountCurve, ForwardRate,
        VolatilityType, VolatilityCube, out delta, out gamma, out theta, out vega);

      // Done
      return (vega)*EffectiveNotional;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="caplet"></param>
    /// <returns></returns>
    public double CapletDeltaSabr(CapletPayment caplet)
    {
      var calibrator = GetRateVolatilitySabrCalibrator();
      if (calibrator != null)
      {
        if (caplet.Expiry <= Settle)
        {
          return 0.0;
        }
        double rate, T;
        rate = ProjectRate(caplet);
        T = CalculateTime(AsOf, caplet.Expiry, Cap.DayCount);


        var alpha = calibrator.Alpha.Interpolate(caplet.Expiry);
        var beta = calibrator.Beta.Interpolate(caplet.Expiry);
        var rho = calibrator.Rho.Interpolate(caplet.Expiry);
        var nu = calibrator.Nu.Interpolate(caplet.Expiry);
        //Note that we do not need to multiply by the notional amount here ,because we are making an internal call to the 
        //caplet delta black and caplet vega black which take care of the notional amount scaling 
        var vega = CapletVegaBlack(caplet);
        var delta = CapletDeltaBlack(caplet);
        if (VolatilityType == VolatilityType.LogNormal)
        {
          var derSabrF = SabrRateModel.DeltaSabrDeltaF(alpha, beta, rho, nu, rate, caplet.Strike, T, VolatilityType);
          var derSabrAlpha = SabrRateModel.DeltaSabrDeltaAlpha(alpha, beta, rho, nu, rate, caplet.Strike, T,
                                                               VolatilityType);
          return delta + vega*(derSabrF + (derSabrAlpha*rho*nu/Math.Pow(rate, beta)));
        }
        else
        {
          var derSabrF = SabrRateModel.DeltaSabrDeltaF(alpha, beta, rho, nu, rate, caplet.Strike, T, VolatilityType);
          return delta + vega*derSabrF;
        }
      }
      return 0.0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="caplet"></param>
    /// <returns></returns>
    public double CapletVegaSabr(CapletPayment caplet)
    {
      var calibrator = GetRateVolatilitySabrCalibrator();
      if (calibrator != null)
      {
        if (caplet.Expiry <= Settle)
          return 0.0;
        var rate = ProjectRate(caplet);
        var T = CalculateTime(AsOf, caplet.Expiry, Cap.DayCount);

        var alpha = calibrator.Alpha.Interpolate(caplet.Expiry);
        var beta = calibrator.Beta.Interpolate(caplet.Expiry);
        var rho = calibrator.Rho.Interpolate(caplet.Expiry);
        var nu = calibrator.Nu.Interpolate(caplet.Expiry);
        var vega = CapletVegaBlack(caplet);


        var derSabrAlpha = SabrRateModel.DeltaSabrDeltaAlpha(alpha, beta, rho, nu, rate, caplet.Strike, T,
                                                             VolatilityType);
        var derSabrAlphaAtm = SabrRateModel.DeltaSabrDeltaAlpha(alpha, beta, rho, nu, rate, rate, T, VolatilityType);
        return vega*(derSabrAlpha/derSabrAlphaAtm);
      }
      return 0.0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="caplet"></param>
    /// <returns></returns>
    public double CapletVannaSabr(CapletPayment caplet)
    {
      var calibrator = GetRateVolatilitySabrCalibrator();
      if (calibrator != null)
      {
        if (caplet.Expiry <= Settle)
          return 0.0;
        double rate = ProjectRate(caplet);
        double T = CalculateTime(AsOf, caplet.Expiry, Cap.DayCount);

        var alpha = calibrator.Alpha.Interpolate(caplet.Expiry);
        var beta = calibrator.Beta.Interpolate(caplet.Expiry);
        var rho = calibrator.Rho.Interpolate(caplet.Expiry);
        var nu = calibrator.Nu.Interpolate(caplet.Expiry);
        //The Caplet VEga Black function should take care of the scaling by notional amount
        var vega = CapletVegaBlack(caplet);

        var derSabrRho = SabrRateModel.DeltaSabrDeltaRho(alpha, beta, rho, nu, rate, caplet.Strike, T, VolatilityType);

        return vega*(derSabrRho);
      }
      return 0.0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="caplet"></param>
    /// <returns></returns>
    public double CapletVolgaSabr(CapletPayment caplet)
    {
      var calibrator = GetRateVolatilitySabrCalibrator();
      if (calibrator != null)
      {
        if (caplet.Expiry <= Settle)
        {
          return 0.0;
        }
        double rate = ProjectRate(caplet);
        double T = CalculateTime(AsOf, caplet.Expiry, Cap.DayCount);

        var alpha = calibrator.Alpha.Interpolate(caplet.Expiry);
        var beta = calibrator.Beta.Interpolate(caplet.Expiry);
        var rho = calibrator.Rho.Interpolate(caplet.Expiry);
        var nu = calibrator.Nu.Interpolate(caplet.Expiry);
        var vega = CapletVegaBlack(caplet);

        var derSabrNu = SabrRateModel.DeltaSabrDeltaNu(alpha, beta, rho, nu, rate, caplet.Strike, T, VolatilityType);

        return vega*(derSabrNu);

      }
      return 0.0;

    }

    /// <summary>
    /// Theta of the given caplet.
    /// </summary>
    /// <remarks>
    ///   <para>Calculated via Black model analytic formula.</para>
    /// </remarks>
    /// <param name="caplet">Caplet</param>
    /// <returns>double</returns>
    public double CapletTheta(CapletPayment caplet)
    {
      // Value caplet
      double delta, gamma, vega, theta;
      CapletPv(AsOf, Settle, Cap, caplet, DiscountCurve, ForwardRate,
               VolatilityType, VolatilityCube, out delta, out gamma, out theta, out vega);
      // Done
      return theta*EffectiveNotional;
    }

    /// <summary>
    ///   Calculate the DV 01
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The DV 01 is the change in PV (MTM)
    ///   if the underlying discount curve is shifted in parallel up by
    ///   one basis point.</para>
    ///
    ///   <para>The DV 01 is calculated by calculating the PV (MTM)
    ///   then bumping up the underlying discount curve by 1 bp
    ///   and re-calculating the PV and returning the difference in value.</para>
    /// </remarks>
    ///
    /// <returns>DV 01</returns>
    ///
    public double Rate01()
    {
      return Sensitivities.IR01(this, 1, 1, true);
    }

    /// <summary>
    ///   Calculate Gamma
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The Gamma is the change in DV01
    ///   if is shifted in parallel up by one basis point.</para>
    ///   <para>The Gamma is calculated by calculating the DV01 of the
    ///   underlying then bumping up the rates by one basis point
    ///   and re-calculating the PV and returning the difference in value.</para>
    /// </remarks>
    ///
    /// <returns>Gamma of the IR Cap</returns>
    ///
    public double RateGamma()
    {
      return Sensitivities.RateGamma(this, 10, 10, true);
    }

    /// <summary>
    ///   Black model sensitivity to a change in interest rates.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The Vega is the sensitivity of the cap to a change 
    ///   in volatility.</para>
    ///
    ///   <para>The Cap Vega is calculated by averaging the Vega for each 
    ///   Caplet as specified by the Black Model.</para>
    /// </remarks>
    ///
    /// <returns>double</returns>
    ///
    public double VegaBlack()
    {
      double vega = 0;
      foreach (CapletPayment caplet in Caplets)
      {
        if (Settle < caplet.Expiry)
        {
          var dt = caplet.PeriodFraction;
          var discountFactor = ReferenceCurve.DiscountFactor(caplet.PayDt);
          var factor = (VolatilityType == VolatilityType.LogNormal) ? 0.01 : 0.0001;
          vega += CapletVegaBlack(caplet)*dt*discountFactor*factor;
        }
      }
      return vega;
    }

    /// <summary>
    /// SABR model sensitivity to a change in volatility.
    /// </summary>
    /// 
    /// <returns>double</returns>
    /// 
    public double VegaSabr()
    {
      double vega = 0;
      foreach (CapletPayment caplet in Caplets)
      {
        if (Settle < caplet.Expiry)
        {
          var dt = caplet.PeriodFraction;
          var discountFactor = ReferenceCurve.DiscountFactor(caplet.PayDt);
          var factor = (VolatilityType == VolatilityType.LogNormal) ? 0.01 : 0.0001;
          vega += CapletVegaSabr(caplet)*dt*discountFactor*factor;
        }
          
      }
      return vega;
    }

    /// <summary>
    /// SABR model sensitivity to a change in .
    /// </summary>
    /// 
    /// <returns>double</returns>
    /// 
    public double VannaSabr()
    {
      double vanna = 0;
      foreach (CapletPayment caplet in Caplets)
      {
        if (Settle < caplet.Expiry)
        {
          var dt = caplet.PeriodFraction;
          var discountFactor = ReferenceCurve.DiscountFactor(caplet.PayDt);
          var factor = (VolatilityType == VolatilityType.LogNormal) ? 0.01 : 0.0001;
          vanna += CapletVannaSabr(caplet)*dt*discountFactor*factor;
        }
      }
      return vanna;
    }

    /// <summary>
    /// SABR model sensitivity to a change in .
    /// </summary>
    /// <returns></returns>
    public double VolgaSabr()
    {
      double volga = 0;
      foreach (CapletPayment caplet in Caplets)
      {
        if (Settle < caplet.Expiry)
        {
          var dt = caplet.PeriodFraction;
          var discountFactor = ReferenceCurve.DiscountFactor(caplet.PayDt);
          var factor = (VolatilityType == VolatilityType.LogNormal) ? 0.01 : 0.0001;
          volga += CapletVolgaSabr(caplet)*dt*discountFactor*factor;
        }
          
      }
      return volga;
    }

    /// <summary>
    /// SABR model sensitivity to a change in interest rates.
    /// </summary>
    /// 
    /// <returns>double</returns>
    /// 
    public double DeltaSabr()
    {
      double delta = 0;
      foreach (CapletPayment caplet in Caplets)
      {
        var indexMultiplier = caplet.IndexMultiplier;
        if (Settle < caplet.PayDt)
        {
          var dt = caplet.PeriodFraction;
          var discountFactor = ReferenceCurve.DiscountFactor(caplet.PayDt);

          //We first compute the partial derivative of the discount factor at pay date wrt to the forward rate 
          //While calculating the delta wrt to the forward rate we assume the forward rate to be an independent variable 
          //Since Di = Di-1/(1+fi*taui) . And Caplet Pv = Di*taui*BS(fi,K,sigma,T) . The Delta will be computed using the chain rule 

          var prevDf = ReferenceCurve.DiscountFactor(caplet.RateFixing);

          var forwardRate = CheckResetInForwardRate ? ForwardRate(caplet) : ProjectRate(caplet);
          var T = CalculateTime(AsOf, caplet.Expiry, Cap.DayCount);
          var vol = GetValidVolatility(VolatilityCube, caplet, forwardRate, Cap.ReferenceRateIndex, ref T);

          forwardRate *= indexMultiplier;
          if (VolatilityType == VolatilityType.Normal)
            vol *= Math.Abs(indexMultiplier);

          double d = 0, v = 0, g = 0, t = 0;
          var optionPrice = (Cap.OptionDigitalType != OptionDigitalType.None)
                              ? DigitalOption.BlackP(OptionStyle.European, Cap.OptionType, Cap.OptionDigitalType, T,
                                                     forwardRate, caplet.Strike, vol, caplet.DigitalFixedPayout, ref d,
                                                     ref g, ref t, ref v)
                              : Black.P(Cap.OptionType, T, forwardRate, caplet.Strike, vol, ref d, ref g, ref t, ref v);

          var deltaDfDeltaF = -(optionPrice*prevDf*dt*dt*EffectiveNotional*caplet.Notional)/
                              Math.Pow(1 + forwardRate*dt, 2);
          delta += (deltaDfDeltaF + CapletDeltaSabr(caplet)*dt*discountFactor)*0.0001;
        }
      }
      return delta;
    }

    /// <summary>
    /// Black model sensitivity to a change in interest rates.
    /// </summary>
    /// 
    /// <returns>double</returns>
    /// 
    public double DeltaBlack()
    {
      double delta = 0;
      foreach (CapletPayment caplet in Caplets)
      {
        if (Settle < caplet.PayDt)
        {
          var indexMultiplier = caplet.IndexMultiplier;
          var dt = caplet.PeriodFraction;
          var discountFactor = ReferenceCurve.DiscountFactor(caplet.PayDt);
          //We first compute the partial derivative of the discount factor at pay date wrt to the forward rate 
          //While calculating the delta wrt to the forward rate we assume the forward rate to be an independent variable 
          //Since Di = Di-1/(1+fi*taui) . And Caplet Pv = Di*taui*BS(fi,K,sigma,T) . The Delta will be computed using the chain rule
          var prevDf = ReferenceCurve.DiscountFactor(caplet.RateFixing);

          var forwardRate = CheckResetInForwardRate ? ForwardRate(caplet) : ProjectRate(caplet);
          var T = CalculateTime(AsOf, caplet.Expiry, Cap.DayCount);
          var vol = GetValidVolatility(VolatilityCube, caplet, forwardRate, Cap.ReferenceRateIndex, ref T);

          forwardRate *= indexMultiplier;
          if (VolatilityType == VolatilityType.Normal)
            vol *= Math.Abs(indexMultiplier);

          double d = 0, v = 0, g = 0, t = 0;
          var optionPrice = (Cap.OptionDigitalType != OptionDigitalType.None)
                              ? DigitalOption.BlackP(OptionStyle.European, Cap.OptionType, Cap.OptionDigitalType, T,
                                                     forwardRate, caplet.Strike, vol, caplet.DigitalFixedPayout, ref d,
                                                     ref g, ref t, ref v)
                              : Black.P(Cap.OptionType, T, forwardRate, caplet.Strike, vol, ref d, ref g, ref t, ref v);

          var deltaDfdeltaF = -(optionPrice*prevDf*dt*dt*EffectiveNotional*caplet.Notional)/
                              Math.Pow(1 + forwardRate*dt, 2);
          delta += (deltaDfdeltaF + CapletDeltaBlack(caplet)*discountFactor*dt)*0.0001;
        }
      }
      return delta;
    }

    private static double GetValidVolatility(
      IVolatilityObject volatility, CapletPayment caplet,
      double fwdRate, ReferenceIndex index,
      ref double T)
    {
      if (T < 0.0)
      {
        T = 0;
        return 0;
      }
      return volatility.CapletVolatility(caplet.Expiry, fwdRate, caplet.Strike, index);
    }

    /// <summary>
    ///   Calculate theta
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The theta is calculated as the difference between the current
    ///   full price, and the full price at the specified future pricing date
    ///   <paramref name="toAsOf"/> and future settlement date
    ///   <paramref name="toSettle"/>.</para>
    ///
    ///   <para>All term structures are held constant while moving the
    ///   the pricing and settlement dates (ie the 30 day discount factor remain
    ///   unchanged relative to the pricing dates.</para>
    /// </remarks>
    ///
    /// <param name="toAsOf">Forward pricing date</param>
    /// <param name="toSettle">Forward settlement date</param>
    ///
    /// <returns>MTM impact of moving pricing and settlement dates forward</returns>
    ///
    public double Theta(Dt toAsOf, Dt toSettle)
    {
      return Sensitivities.Theta(this, "Pv", toAsOf, toSettle, ThetaFlags.None, SensitivityRescaleStrikes.No);
    }

    /// <summary>
    /// Change in value due to a 1% increase in cap/floor market volatilities.
    /// </summary>
    /// 
    /// <returns>double</returns>
    /// 
    public double Vega()
    {
      var method = CapVegaMeasure.Vega;

      // Determine if we want SABR vegas
      if (GetRateVolatilitySabrCalibrator() != null)
      {  
        method = CapVegaMeasure.VegaSabr;
      }

      // Calc vega
      var v = VolatilityCube;
      var expiries = v.GetRateExpiryTenors();
      var vegaTbl = Sensitivities.VegaCapFloor(new[] { this }, v.GetRateStrikes(),
        expiries, method, VegaAllocationMethod.Flat,
        false, false, false, expiries.Length);
      // Done
      return Results.Sum(vegaTbl, "Vega");
    }

    /// <summary>
    /// Total dollar hedge notional of market cap/floors to be vega neutral.
    /// </summary>
    /// 
    /// <returns>double</returns>
    /// 
    public double VegaHedge()
    {
      var method = CapVegaMeasure.Vega;

      // Determine if we want SABR vegas
      if (GetRateVolatilitySabrCalibrator() != null)
      {
        method = CapVegaMeasure.VegaSabr;
      }

      // Calc vega
      var v = VolatilityCube;
      var vegaTbl = Sensitivities.VegaCapFloor(new[] { this }, v.GetRateStrikes(),
        v.GetRateExpiryTenors(), method,VegaAllocationMethod.Weighted,
        true, false, false, v.GetRateExpiryTenors().Length - 1);
      
      // Done
      return Results.Sum(vegaTbl, "HedgeNotional");
    }

    #endregion // Methods

    #region old backward compatible methods, to be removed after 10.0.0
    /// <summary>
    /// Delta (old)
    /// </summary>
    /// <returns></returns>
    public double Delta_Old()
    {
      var delta = 0.0;
      int count = Caplets.Count;
      foreach (CapletPayment caplet in Caplets)
      {
        if ( Settle<caplet.PayDt)
        {
          delta += CapletDeltaBlack(caplet) / (caplet.Notional *EffectiveNotional);
        }
      }

      return (count != 0) ? delta / count : delta;
    }

    /// <summary>
    /// Gamma (old)
    /// </summary>
    /// <returns></returns>
    public double Gamma_Old()
    {
      var origDelta = Delta_Old();
      double[] bumps = {10};
      Curve[] curves = {ReferenceCurve};
      BumpCurves(curves,bumps,true);
      double bumpedDelta = Delta_Old();
      BumpCurves(curves,bumps,false);
      return (bumpedDelta - origDelta);
    }

    /// <summary>
    ///   Bump curves (in basis points)
    /// </summary>
    static private void BumpCurves(Curve[] curves,
      double[] bumps,
      bool add)
    {
      if (null == bumps || bumps.Length == 0)
        return;

      int N = curves.Length;
      if (add)
      {
        for (int i = 0; i < N; ++i)
        {
          if (null != curves[i])
            curves[i].Spread += bumps[i] / 10000;
        }
      }
      else
      {
        for (int i = 0; i < N; ++i)
        {
          if (null != curves[i])
            curves[i].Spread -= bumps[i] / 10000;
        }
      }
      return;
    }

    #endregion 

    #region Pseudo Model

    private static Tuple<double, IList<double>> CapPv(Dt asOf, Dt settle, CapBase cap, PaymentSchedule caplets,
      DiscountCurve discountCurve, Func<CapletPayment, double> projectRate, VolatilityType volatilityType,
      IVolatilityObject volatilityCube)
    {
      double pv = 0;
      var capletVols = new List<double>();
      // Go through schedule and price caplets
      foreach (CapletPayment caplet in caplets)
      {
        if (caplet.PayDt > settle)
        {
          // Value caplet
          var capletValues = CapletPv(asOf, settle, cap, caplet, discountCurve, projectRate, volatilityType, volatilityCube);
          pv += capletValues.Item1;
          if (capletValues.Item2 > 0.0)
          {
            capletVols.Add(capletValues.Item2);
          }
        }
      }
      pv /= discountCurve.DiscountFactor(asOf);

      // Done
      return new Tuple<double, IList<double>>(pv, capletVols);
    }

    /// <summary>
    /// Static valuation method for caplets.
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="settle">The settle.</param>
    /// <param name="cap">The cap.</param>
    /// <param name="caplet">The caplet.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="projectRate">Function to project the reference rate</param>
    /// <param name="volatilityType">Type of the volatility.</param>
    /// <param name="volatilityCube">The volatility cube.</param>
    /// <returns></returns>
    private static Tuple<double, double> CapletPv(Dt asOf, Dt settle, CapBase cap, CapletPayment caplet,
      DiscountCurve discountCurve, Func<CapletPayment, double> projectRate, VolatilityType volatilityType,
      IVolatilityObject volatilityCube)
    {
      double pv = 0, vol, rate, T;
      rate = projectRate(caplet);
      if (caplet.Expiry <= settle)
      {
        vol = 0;
        T = 0;
      }
      else
      {
        vol = volatilityCube.CapletVolatility(caplet.Expiry, rate, caplet.Strike, cap.ReferenceRateIndex);
        T = CalculateTime(asOf, caplet.Expiry, cap.DayCount);
      }
      // Time
      var dt = caplet.PeriodFraction;
      var discountFactor = discountCurve.DiscountFactor(caplet.PayDt);
      var optionType = cap.OptionType;
      rate *= caplet.IndexMultiplier;
      if (volatilityType == VolatilityType.Normal)
        vol *= Math.Abs(caplet.IndexMultiplier);

      // Add
      if (caplet.OptionDigitalType != OptionDigitalType.None)
      {
        if (volatilityType == VolatilityType.LogNormal)
          pv += dt* discountFactor*
                DigitalOption.BlackP(OptionStyle.European, optionType, cap.OptionDigitalType, T,
                                     rate, caplet.Strike, vol, caplet.DigitalFixedPayout);
        else
          pv += dt* discountFactor*
                DigitalOption.NormalBlackP(OptionStyle.European, optionType, cap.OptionDigitalType, T,
                                           rate, caplet.Strike, vol, caplet.DigitalFixedPayout);
      }
      else
      {
        if (volatilityType == VolatilityType.LogNormal)
        {
          var pvCaplet = 0.0;
          if (rate <= 0.0 || caplet.Strike <= 0)
          {
            pvCaplet = discountFactor * dt * Math.Max((optionType == OptionType.Call ? 1.0 : -1.0) * (rate - caplet.Strike), 0.0);
          }
          else
          {
            pvCaplet = discountFactor * dt * Black.P(optionType, T, rate, caplet.Strike, vol);
          }
          pv += pvCaplet;
        }
        else
          pv += discountFactor * dt *
                BlackNormal.P(optionType, T, 0, rate, caplet.Strike, vol);
      } // Done
      return new Tuple<double, double>(caplet.Notional*pv, vol);
    }


    /// <summary>
    /// Static valuation method for caplets.
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="settle">The settle.</param>
    /// <param name="cap">The cap.</param>
    /// <param name="caplet">The caplet.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="projectRate">Function to project the reference rate</param>
    /// <param name="volatilityType">Type of the volatility.</param>
    /// <param name="volatilityCube">The volatility cube.</param>
    /// <param name="delta">The delta.</param>
    /// <param name="gamma">The gamma.</param>
    /// <param name="theta">The theta.</param>
    /// <param name="vega">The vega.</param>
    /// <returns></returns>
    private static double CapletPv(Dt asOf, Dt settle, CapBase cap, CapletPayment caplet,
      DiscountCurve discountCurve, Func<CapletPayment, double> projectRate, VolatilityType volatilityType,
      IVolatilityObject volatilityCube, out double delta, out double gamma, out double theta, out double vega)
    {
      delta = gamma = theta = vega = 0;
      double pv = 0, vol, rate, T;
      rate = projectRate(caplet);
      if (caplet.Expiry <= settle)
      {
        vol = 0;
        T = 0;
      }
      else
      {
        vol = volatilityCube.CapletVolatility(caplet.Expiry, rate, caplet.Strike, cap.ReferenceRateIndex);
        T = CalculateTime(asOf, caplet.Expiry, cap.DayCount);
      }
      // Time
      var dt = caplet.PeriodFraction;
      var discountFactor = discountCurve.DiscountFactor(caplet.PayDt);

      var optionType = cap.OptionType;
      rate *= caplet.IndexMultiplier;
      if (volatilityType == VolatilityType.Normal)
        vol *= Math.Abs(caplet.IndexMultiplier);

      // Add
      if (caplet.OptionDigitalType != OptionDigitalType.None)
      {
        if (volatilityType == VolatilityType.LogNormal)
        {
          pv += dt*discountFactor*
                DigitalOption.BlackP(OptionStyle.European, optionType, cap.OptionDigitalType, T,
                  rate, caplet.Strike, vol, caplet.DigitalFixedPayout, ref delta, ref gamma,
                  ref theta, ref vega);
          DiscountGreeks(discountFactor, ref delta, ref gamma, ref theta, ref vega);
        }
        else
        {
          pv += dt* discountFactor*
                DigitalOption.NormalBlackP(OptionStyle.European, optionType, cap.OptionDigitalType, T,
                  rate, caplet.Strike, vol, caplet.DigitalFixedPayout, ref delta, ref gamma,
                  ref theta, ref vega);
          DiscountGreeks(discountFactor, ref delta, ref gamma, ref theta, ref vega);
        }
      }
      else
      {
        if (volatilityType == VolatilityType.LogNormal)
        {
          var pvCaplet = 0.0;
          if (rate <=0.0 || caplet.Strike <=0)
          {
            pvCaplet = discountFactor * dt * Math.Max((optionType == OptionType.Call ? 1.0 : -1.0) * (rate - caplet.Strike) , 0.0);
          }
          else
          {
            pvCaplet = discountFactor * dt * Black.P(optionType, T, rate, caplet.Strike, vol, ref delta, ref gamma, ref theta, ref vega);
          }
          pv += pvCaplet;
        }
        else
          pv += discountFactor * dt * 
                BlackNormal.P(optionType, T, 0, rate, caplet.Strike, vol, ref delta, ref gamma, ref theta, ref vega);
      }
      // Done
      delta = delta*caplet.Notional;
      vega = vega*caplet.Notional;
      theta = (discountCurve.R(caplet.PayDt)*discountFactor*pv + theta)*caplet.Notional;
      gamma = gamma*caplet.Notional;
      return caplet.Notional*pv;
    }


    private static void DiscountGreeks(double df, ref double delta, ref double gamma, 
      ref double theta, ref double vega)
    {
      delta *= df;
      gamma *= df;
      theta *= df;
      vega *= df;
    }

    private RateVolatilitySabrCalibrator GetRateVolatilitySabrCalibrator()
    {
      var cube = VolatilityCube as RateVolatilityCube;
      return cube == null ? null
        : cube.RateVolatilityCalibrator as RateVolatilitySabrCalibrator;
    }

    public static double CalculateTime(Dt asOf, Dt expiry, DayCount dayCount)
    {
      return ToolkitConfigurator.Settings.CapFloorPricer.TimeToExpiryInActual365
        ? ((expiry - asOf)/365.0) : Dt.Fraction(asOf, expiry, dayCount);
    }

    #endregion
    
    #region Properties

    /// <summary>
    ///   Cap Product
    /// </summary>
    public CapBase Cap
    {
      get { return (CapBase)Product; }
    }

    /// <summary>
    ///   Current reset rate
    /// </summary>
    public IList<RateReset> Resets
    {
      get { return resets_; }
    }


    /// <summary>
    ///   Reference Curve used for pricing of floating-rate cashflows
    /// </summary>
    public DiscountCurve ReferenceCurve
    {
      get { return referenceCurve_; }
    }


    /// <summary>
    ///   Discount Curve used for pricing
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }

    }

    /// <summary>
    ///   Volatility 
    /// </summary>
    public IVolatilityObject VolatilityCube
    {
      get { return volatilityCube_; }
    }

    /// <summary>
    /// Caplets
    /// </summary>
    public PaymentSchedule Caplets
    {
      get
      {
        if (caplets_ == null)
          caplets_ = Cap.GetPaymentSchedule(AsOf, new RateResets(Resets));
        return caplets_;
      }
    }

    /// <summary>
    /// The dynamics of the interest rate process.
    /// </summary>
    public VolatilityType VolatilityType
    {
      get { return volatilityType_; }
    }
    
    /// <summary>
    /// The Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment != null)
        {
          if (paymentPricer_ == null)
            paymentPricer_ = BuildPaymentPricer(Payment, discountCurve_);
        }
        return paymentPricer_;
      }
    }

    /// <summary>
    /// Current reset rate.
    /// </summary>
    public double CurrentRate
    {
     get
     {
       if (currentRate_ == null)
         currentRate_ = RateResetUtil.ResetAt(Resets, AsOf);
       return currentRate_.Value;
     }
    }

    /// <summary>
    /// The previous expiry on or before the Settle date.
    /// </summary>
    public Dt LastExpiry
    {
      get
      {
        if (lastExpiry_ == null)
          lastExpiry_ = CalcLastExpiry();
        return lastExpiry_.Value;
      }
    }

    /// <summary>
    /// Calculate the previous expiry on or before the Settle date
    /// </summary>
    /// <returns></returns>
    private Dt CalcLastExpiry()
    {
      var caplets = Cap.GetPaymentSchedule(AsOf, new RateResets(Resets));
      // Find a caplet with expiry <= Settle and pay date > Settle
      CapletPayment currentCaplet = null;
      foreach(var caplet in caplets.GetPaymentsByType<CapletPayment>())
      {
        if(caplet.Expiry <= Settle && Settle < caplet.PayDt)
        {
          currentCaplet = caplet;
          break;
        }
      }

      return (currentCaplet == null ? Dt.Empty : currentCaplet.Expiry);
    }

    /// <summary>
    /// Gets or sets the convexity adjustment parameters.
    /// </summary>
    /// <value>The convexity parameters</value>
    public RateModelParameters ConvexityParameters { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to check rate reset in forward rate calculation.
    /// </summary>
    /// <remarks>This fixes the issue that the caplet payments use inconsistent reset
    ///  than the corresponding swap pricers</remarks>
    /// <value><c>true</c> if check rate reset in forward calculation; otherwise, <c>false</c>.</value>
    public bool CheckResetInForwardRate { get; set; }

    public  double ToleranceX { get; set; }
    public double ToleranceF { get; set; }
    #endregion // Properties

    #region ResetAction

    /// <summary>A caplet's characteristics changed.</summary>
    public static readonly ResetAction CapletChanged = new ResetAction();

    /// <summary>Cap/Floor characteristics changed.</summary>
    public static readonly ResetAction ProductChanged = new ResetAction();

    #endregion ResetAction

    #region Data
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CapFloorPricer));
    private IList<RateReset> resets_;
    private readonly DiscountCurve referenceCurve_;
    private readonly DiscountCurve discountCurve_;
    private readonly VolatilityType volatilityType_;
    private readonly IVolatilityObject volatilityCube_;
    
    private PaymentSchedule caplets_;
    private double? currentRate_;
    private Dt? lastExpiry_;
    #endregion // Data

    #region IRateResetsUpdater Members

    RateResets IRatesLockable.LockedRates
    {
      get
      {
        return new RateResets(resets_);
      }
      set
      {
        resets_ = value.ToList();
      }
    }

    IEnumerable<RateReset> IRatesLockable.ProjectedRates
    {
      get
      {
        var ps = GetPaymentSchedule(null, AsOf);
        foreach (var d in ps.GetPaymentDates())
        {
          foreach (var p in ps.GetPaymentsOnDate(d))
          {
            if (!(p is CapletPayment cp)) continue;
            if (cp.IsProjected)
            {
              yield return new RateReset(cp.Expiry, ForwardRate(cp));
            }
          }
        }
      }
    }

    #endregion
  } // class CapBlackPricer

  /// <summary>
  /// CMS cap/floor pricer.
  /// </summary>
  [Serializable]
  public class CmsCapFloorPricer : CapFloorPricerBase, IPricer<CmsCap>
  {
    /// <summary>
    ///   Construct a Cap pricer
    /// </summary>
    ///
    /// <param name="product">CMS cap/floor to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="referenceCurve">Floating rate reference curve (if required)</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="volatility">Volatility (eg 0.2 = 20pc)</param>
    /// <param name="convexityParameters">The convexity adjustment parameters</param>
    ///
    public CmsCapFloorPricer(CmsCap product,
      Dt asOf,
      Dt settle,
      DiscountCurve referenceCurve,
      DiscountCurve discountCurve,
      IVolatilityObject volatility,
      RateModelParameters convexityParameters)
      : base(product, asOf, settle, referenceCurve, discountCurve, volatility)
    {
      ConvexityParameters = convexityParameters;
      CheckResetInForwardRate = true;
    }

    /// <summary>
    /// Gets the cap/floor to price.
    /// </summary>
    /// <value>The cap/floor</value>
    public new CmsCap Cap
    {
      get { return (CmsCap)Product; }
    }

    /// <summary>
    /// Gets the product to price
    /// </summary>
    /// <value>The product</value>
    CmsCap IPricer<CmsCap>.Product
    {
      get { return Cap; }
    }

    /// <summary>
    /// Projects the swap rate.
    /// </summary>
    /// <param name="swaplet">The swaplet</param>
    /// <returns>System.Double.</returns>
    public override double ProjectRate(CapletPayment swaplet)
    {
      var index = Cap.SwapRateIndex;
      var projector = new SwapRateCalculator(AsOf, index, ReferenceCurve);
      var fixingSchdule = projector.GetFixingSchedule(Dt.Empty,
        swaplet.RateFixing, swaplet.TenorDate, swaplet.PayDt);
      var fixing = projector.Fixing(fixingSchdule);
      if (ConvexityParameters == null)
        return fixing.Forward;

      var volatilityStart = swaplet.VolatilityStartDt;
      if (volatilityStart.IsEmpty()) volatilityStart = AsOf;
      var forwardAdjustment = new SwapRateAdjustment(
        volatilityStart, DiscountCurve, ConvexityParameters);
      var ca = forwardAdjustment.ConvexityAdjustment(
        swaplet.PayDt, fixingSchdule, fixing);
      return ca + fixing.Forward;
    }
  }
}
