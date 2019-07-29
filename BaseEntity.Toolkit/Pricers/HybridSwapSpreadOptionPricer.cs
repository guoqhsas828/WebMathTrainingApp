//
//   2015. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  #region HybridSwapSpreadOptionPricer

  /// <summary>
  /// Hybrid swap spread option pricing class, extending the generic interface to price CMS spread option
  /// </summary>
  /// <remarks>
  ///   <para>Pricer for a <see cref="HybridSwapSpreadOption">Generalised spread/hybrid option product</see> based on two
  ///   underlying CMS spreads with normal price distributions.</para>
  ///   <remarks>Since the CMS rates are not martingales under the forward measure associated to option maturity
  ///   <m>T</m>, the underlying process is <m>E^T(\mathrm{CMS}_T(T,U)|F_t)</m> and the initial condition is thus
  ///   adjusted by the convexity adjustment <m>E^T(\mathrm{CMS}_T(T,U)) - \mathrm{CMS}_0(T,U)</m></remarks>
  ///   <para>Numerical integration is used to solve for the option price.</para>
  /// </remarks>
  /// <seealso cref="HybridSwapSpreadOption"/>
  /// <seealso cref="HybridOptionPricer" />
  public class HybridSwapSpreadOptionPricer : HybridOptionPricer
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">Pricing date</param>
    /// <param name="discountCurve">Discount Curve</param>
    /// <param name="hybridSwapSpreadOption">The swap spread option</param>
    /// <param name="transitionKernel">Calibration kernal</param>
    /// <param name="initialValues">Initial values</param>
    /// <param name="addConvexity">Since CMS spread is not a martingale under the T forward measure, we model
    /// <m>E^T(CMS|F_t)</m> so that initial condition is forward spread plus convexity adjustment. If true add internally</param>
    public HybridSwapSpreadOptionPricer(Dt asOf, DiscountCurve discountCurve, HybridSwapSpreadOption hybridSwapSpreadOption,
      TransitionKernel2D transitionKernel, double[] initialValues, bool addConvexity)
      : base(asOf, discountCurve, hybridSwapSpreadOption, transitionKernel, initialValues)
    {
      if (addConvexity)
      {
        //add CMS to convexity adjustment
        InitialValues[0] += CmsConvexityAdjustment(asOf, hybridSwapSpreadOption.Maturity, InitialValues[0],
                                                   hybridSwapSpreadOption.ReceiveIndexTenor,
                                                   hybridSwapSpreadOption.IndexFrequency,
                                                   hybridSwapSpreadOption.SwapFrequency,
                                                   TransitionKernel.RateModelParameters(0, null));
        InitialValues[1] += CmsConvexityAdjustment(asOf, hybridSwapSpreadOption.Maturity, InitialValues[1],
                                                   hybridSwapSpreadOption.PayIndexTenor,
                                                   hybridSwapSpreadOption.IndexFrequency,
                                                   hybridSwapSpreadOption.SwapFrequency,
                                                   TransitionKernel.RateModelParameters(1, null));
      }
    }

    #endregion //Constructor

    #region Methods

    internal static double CmsConvexityAdjustment(Dt asOf, Dt maturity, double frw0, Tenor indexTenor, Frequency indexFrequency, Frequency swapFrequency, RateModelParameters parameters)
    {
      int ifreq = (int) indexFrequency;
      int sfreq = (int) swapFrequency;
      double delta = 1.0/sfreq;
      double tau = 1.0/ifreq;
      double rateTen = indexTenor.Years*ifreq;
      double taufrw0 = tau*frw0;
      double constant = 1 - taufrw0/(1 + taufrw0)*(delta/tau + rateTen/(Math.Pow(1 + taufrw0, rateTen) - 1));
      double var = parameters.SecondMoment(RateModelParameters.Process.Projection, asOf, frw0, maturity, maturity);
      return frw0*constant*(var/(frw0*frw0) - 1);
    }

    /// <summary>
    /// Compute the option sensitivity to the swap spread
    /// </summary>
    /// <param name="bumpSize">Bump size, the swap spread change amount</param>
    /// <returns>Delta sensitivity</returns>
    public double DeltaSpread(double bumpSize)
    {
      //save first 
      var savedValues = (double[]) InitialValues.Clone();
      //bump
      InitialValues[0] = savedValues[0] + bumpSize/2.0;
      InitialValues[1] = savedValues[1] - bumpSize/2.0;
      double pu = Price(InitialValues);
      InitialValues[0] = savedValues[0] - bumpSize/2.0;
      InitialValues[1] = savedValues[1] + bumpSize/2.0;
      double pd = Price(InitialValues);
      double delta = 0.5*(pu - pd);
      //restore
      for (int i = 0; i < savedValues.Length; i++)
        InitialValues[i] = savedValues[i];
      return delta;
    }

    /// <summary>
    /// Compute the option sensitivity to the swap spread
    /// </summary>
    /// <param name="bumpSize">Bump size, the swap spread change amount</param>
    /// <returns>Delta sensitivity</returns>
    public double GammaSpread(double bumpSize)
    {
      //save first 
      var savedValues = (double[])InitialValues.Clone();
      double p = Price(InitialValues);
      //bump
      InitialValues[0] = savedValues[0] + bumpSize / 2.0;
      InitialValues[1] = savedValues[1] - bumpSize / 2.0;
      double pu = Price(InitialValues);
      InitialValues[0] = savedValues[0] - bumpSize / 2.0;
      InitialValues[1] = savedValues[1] + bumpSize / 2.0;
      double pd = Price(InitialValues);
      double gamma = (pu -2*p + pd);
      //restore
      for (int i = 0; i < savedValues.Length; i++)
        InitialValues[i] = savedValues[i];
      return gamma;
    }

    /// <summary>
    /// Compute the option sensitivity to rate level change uniformly
    /// </summary>
    /// <param name="bumpSize">Bump size, the rate level uniform move amount</param>
    /// <returns>Delta sensitivity</returns>
    public double DeltaRate(double bumpSize)
    {
      //save first 
      var savedValues = (double[]) InitialValues.Clone();
      //bump
      InitialValues[0] = savedValues[0] + bumpSize;
      InitialValues[1] = savedValues[1] + bumpSize;
      double pu = Price(InitialValues);
      InitialValues[0] = savedValues[0] - bumpSize;
      InitialValues[1] = savedValues[1] - bumpSize;
      double pd = Price(InitialValues);
      double delta = 0.5*(pu - pd);
      //restore
      for (int i = 0; i < savedValues.Length; i++)
        InitialValues[i] = savedValues[i];
      return delta;
    }

    /// <summary>
    /// Compute the option sensitivity to rate level change uniformly
    /// </summary>
    /// <param name="bumpSize">Bump size, the rate level uniform move amount</param>
    /// <returns>Delta sensitivity</returns>
    public double GammaRate(double bumpSize)
    {
      //save first 
      var savedValues = (double[]) InitialValues.Clone();
      double p = Price(InitialValues);
      //bump
      InitialValues[0] = savedValues[0] + bumpSize;
      InitialValues[1] = savedValues[1] + bumpSize;
      double pu = Price(InitialValues);
      InitialValues[0] = savedValues[0] - bumpSize;
      InitialValues[1] = savedValues[1] - bumpSize;
      double pd = Price(InitialValues);
      double gamma = (pu - 2*p + pd);
      //restore
      for (int i = 0; i < savedValues.Length; i++)
        InitialValues[i] = savedValues[i];
      return gamma;
    }


    /// <summary>
    /// uniform vega bump 
    /// </summary>
    /// <param name="bumpSize">Bump size on rate parameters</param>
    /// <returns>Vega</returns>
    public double VegaRate(double bumpSize)
    {
      double retVal = 0.0;
      for (int i = 0; i < 2; ++i)
        retVal += Vega(i, bumpSize);
      return retVal;
    }

    #endregion //Methods
  }
  #endregion
}
