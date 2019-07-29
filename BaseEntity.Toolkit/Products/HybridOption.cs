//
//   2015. All rights reserved.
//
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Hybrid Option Product based on two underlying assets.
  /// </summary>
  /// <remarks>
  ///   <para>Generalised spread/hybrid option product based on two underlying assets.</para>
  ///   <inheritdoc cref="HybridPayOffFn.CreateHybridPayOff(double, double, BaseEntity.Toolkit.Base.HybridOptionType, BaseEntity.Toolkit.Base.HybridBarrierType)"/>
  /// </remarks>
  /// <seealso cref="HybridPayOffFn"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.HybridOptionPricer"/>
  public class HybridOption : Product
  {
    /// <summary>
    /// Constructor for the Hybrid Option product, the payoff is function of swap rate level and stock price
    /// </summary>
    /// <param name="effective">Effective date </param>
    /// <param name="maturity">Maturity Date </param>
    /// <param name="ccy">Currency </param>
    /// <param name="strike">Strike</param>
    /// <param name="barrier">Barrier</param>
    /// <param name="optionType">Option type</param>
    /// <param name="barrierType">Barrier type</param>
    public HybridOption(Dt effective, Dt maturity, Currency ccy, double strike, double barrier, HybridOptionType optionType, HybridBarrierType barrierType)
      : base(effective, maturity, ccy)
    {
      PayOffFn = HybridPayOffFn.CreateHybridPayOff(strike, barrier, optionType, barrierType);
      Strike = strike;
      Barrier = barrier;
      HybridOptionType = optionType;
      HybridBarrierType = barrierType;
    }

    /// <summary>
    /// Returns the pay off fn 
    /// </summary>
    public Payoff2DFn PayOffFn
    {
      get; protected set;
    }

    /// <summary>
    /// Strike 
    /// </summary>
    public double Strike
    {
      get; private set;
    }

    /// <summary>
    /// Option type
    /// </summary>
    public HybridOptionType HybridOptionType
    {
      get; private set;
    }
   
    /// <summary>
    /// Barrier type enum 
    /// </summary>
    public HybridBarrierType HybridBarrierType
    {
      get; private set;
    }

    /// <summary>
    /// Barrier 
    /// </summary>
    public double Barrier
    {
      get; private set;
    }
  }
}
