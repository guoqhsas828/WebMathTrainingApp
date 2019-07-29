//
//   2015. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Hybrid option pricing class. This is a generic interface which could be used for any european pay off function  
  /// </summary>
  /// <remarks>
  ///   <para>Pricer for a <see cref="HybridOption">Generalised spread/hybrid option product</see> based on two
  ///   underlying assets with lognormal price distributions.</para>
  ///   <para>Numerical integration is used to solve for the option price.</para>
  /// </remarks>
  public class HybridOptionPricer : PricerBase, IPricer
  {
    #region Constructors

    /// <summary>
    /// Constructor 
    /// </summary>
    /// <param name="asOf">as of date</param>
    /// <param name="discountCurve">Discount Curve</param>
    /// <param name="hybridOption">Option product</param>
    /// <param name="transitionKernel">Transition kernel</param>
    /// <param name="initialValues">Initial values</param>
    public HybridOptionPricer(Dt asOf, DiscountCurve discountCurve, HybridOption hybridOption, TransitionKernel2D transitionKernel, double[] initialValues)
      : base(hybridOption, asOf, asOf)
    {
      DiscountCurve = discountCurve;
      TransitionKernel = transitionKernel;
      if (hybridOption.HybridOptionType == HybridOptionType.CallDigital ||
          hybridOption.HybridOptionType == HybridOptionType.PutDigital)
      {
        TransitionKernel.Barrier = HybridOption.Barrier;
        TransitionKernel.Strike = HybridOption.Strike;
      }
      InitialValues = initialValues;
    }

    #endregion

    #region Methods

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      var copy = (HybridOptionPricer)base.Clone();
      copy.DiscountCurve = (DiscountCurve)DiscountCurve.Clone();
      copy.InitialValues = CloneUtil.Clone(InitialValues);
      copy.TransitionKernel = (TransitionKernel2D)TransitionKernel.Clone();
      return copy;
    }

    /// <summary>
    /// Compute the price of the option
    /// </summary>
    /// <returns></returns>
    public double Price(double[] initialValues)
    {
      var t = Dt.Fraction(AsOf, HybridOption.Maturity, DayCount.Actual365Fixed);
      double price = DiscountFactor() * TransitionKernel.Integrate(HybridOption.PayOffFn, initialValues, t);
      return price;
    }

    /// <summary>
    /// Compute the sensitivity to stock price change
    /// </summary>
    /// <param name="index">Process index</param>
    /// <param name="bumpSize">Bump size of the stock price change</param>
    /// <returns>Delta of stock price</returns>
    public double Delta(int index, double bumpSize)
    {
      //save first 
      var savedValues = (double[])InitialValues.Clone();
      //bump
      InitialValues[index] = savedValues[index] + bumpSize;
      double pu = Price(InitialValues);
      InitialValues[index] = savedValues[index] - bumpSize;
      double pd = Price(InitialValues);
      double delta = 0.5 * (pu - pd);
      //restore back 
      savedValues.CopyTo(InitialValues, 0);
      return delta;
    }

    /// <summary>
    /// Compute the sensitivity to stock price change
    /// </summary>
    /// <param name="index">process index</param>
    /// <param name="bumpSize">Bump size of the stock price change</param>
    /// <returns>Delta of stock price</returns>
    public double Gamma(int index, double bumpSize)
    {
      //base case
      double p = Price(InitialValues);
      //save first 
      var savedValues = (double[])InitialValues.Clone();
      //bump
      InitialValues[index] = savedValues[index] + bumpSize;
      double pu = Price(InitialValues);
      InitialValues[index] = savedValues[index] - bumpSize;
      double pd = Price(InitialValues);
      double gamma = (pu - 2 * p + pd);
      //restore back 
      savedValues.CopyTo(InitialValues, 0);
      return gamma;
    }

    /// <summary>
    /// Compute the sensitivity to stock price change
    /// </summary>
    /// <param name="bumpSize">Bump size of the stock price change</param>
    /// <returns>Delta of stock price</returns>
    public double CrossGamma(double bumpSize)
    {
      //save first 
      var savedValues = (double[])InitialValues.Clone();
      //bump
      InitialValues[0] = savedValues[0] + bumpSize;
      InitialValues[1] = savedValues[1] + bumpSize;
      double puu = Price(InitialValues);
      InitialValues[0] = savedValues[0] + bumpSize;
      InitialValues[1] = savedValues[1] - bumpSize;
      double pud = Price(InitialValues);
      InitialValues[0] = savedValues[0] - bumpSize;
      InitialValues[1] = savedValues[1] + bumpSize;
      double pdu = Price(InitialValues);
      InitialValues[0] = savedValues[0] - bumpSize;
      InitialValues[1] = savedValues[1] - bumpSize;
      double pdd = Price(InitialValues);
      double gamma = 0.25 * (puu - pud - pdu + pdd);
      //restore back 
      savedValues.CopyTo(InitialValues, 0);
      return gamma;
    }

    /// <summary>
    /// compute the vega w.r.t stock volatility 
    /// </summary>
    /// <param name="index">Process index</param>
    /// <param name="bumpSize">Bump size on the stock volatility</param>
    /// <returns>Vega w.r.t stock volatility</returns>
    public double Vega(int index, double bumpSize)
    {
      double f = InitialValues[index];
      var tau = Dt.Fraction(AsOf, HybridOption.Maturity, DayCount.Actual365Fixed); //compute the initial price
      double p0 = Price(InitialValues);
      var pos = TransitionKernel.MarginalParametersIndex(index);
      var savedParameters = (double[])TransitionKernel.Parameters.Clone();
      try
      {
        double retVal = 0.0;
        double sigAtm0 = TransitionKernel.ImpliedVol(index, f, tau);
        for (int i = 0; i < pos.Length; ++i)
        {
          TransitionKernel.Parameters[pos[i]] += 1e-3; //bump
          double p1 = Price(InitialValues);
          double dPi = p1 - p0;
          double sigAtm1 = TransitionKernel.ImpliedVol(index, f, tau);
          TransitionKernel.Parameters[pos[i]] -= 1e-3; //restore
          double dSigi = sigAtm1 - sigAtm0;
          retVal += (Math.Abs(dSigi) > 1e-8) ? dPi / dSigi : 0.0;
        }
        return retVal * bumpSize;
      }
      catch (Exception)
      {
        savedParameters.CopyTo(TransitionKernel.Parameters, 0);
        return 0.0;
      }
    }


    private double DiscountFactor()
    {
      return DiscountCurve.Interpolate(AsOf, Product.Maturity);
    }

    #endregion //Methods

    #region IPricer,PricerBase

    ///<summary>
    /// Option contract value is single contract price times the factor
    ///</summary>
    ///<returns>Product pv</returns>
    public override double ProductPv()
    {
      return Price(InitialValues) * Notional;
    }

    ///<summary>
    /// Pricer that will be used to price any additional (e.g. upfront) payment
    /// associated with the pricer.
    ///</summary>
    /// <remarks>
    /// It is the responsibility of derived classes to build this
    /// pricer with the appropriate discount curve for the payment's currency
    /// and to decide whether it can cache the pricer between uses.
    /// </remarks>
    ///<exception cref="NotImplementedException"></exception>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment != null)
        {
          if (paymentPricer_ == null)
            paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve);
        }
        return paymentPricer_;
      }
    }

    #endregion //IPrier,PricerBase

    #region Properties

    ///<summary>
    /// Hybrid option 
    ///</summary>
    public HybridOption HybridOption
    {
      get { return Product as HybridOption; }

    }

    /// <summary>
    /// The Initial Values 
    /// </summary>
    public double[] InitialValues { get; set; }

    /// <summary>
    /// The Discount curve used by the pricer
    /// </summary>
    public DiscountCurve DiscountCurve { get; set; }

    /// <summary>
    /// Transition kernel
    /// </summary>
    public TransitionKernel2D TransitionKernel { get; private set; }

    #endregion
  }
}
