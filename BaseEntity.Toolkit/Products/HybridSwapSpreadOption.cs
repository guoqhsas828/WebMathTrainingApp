//
//   2015. All rights reserved.
//
using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Swap Spread Option Product, having the swap rate spread between two indexes as underlying, priced using the hybrid model
  /// </summary>
  /// <remarks>
  ///   <para>Generalised spread/hybrid option product based on two underlying swap rates.</para>
  ///   <inheritdoc cref="HybridPayOffFn.CreateHybridPayOff(double, double, BaseEntity.Toolkit.Base.HybridOptionType, BaseEntity.Toolkit.Base.HybridBarrierType)"/>
  /// </remarks>
  /// <seealso cref="HybridPayOffFn"/>
  /// <seealso cref="BaseEntity.Toolkit.Pricers.HybridSwapSpreadOptionPricer"/>
  public class HybridSwapSpreadOption : HybridOption
  {
    
    /// <summary>
    /// Constructor for the Hybrid Swap Spread Option product
    /// </summary>
    /// <param name="effective">Effective date </param>
    /// <param name="maturity">Maturity Date </param>
    /// <param name="ccy">Currency </param>
    /// <param name="strike">Strike on the swap rate spread between two indexes</param>
    /// <param name="barrier">Barrier term, if any</param>
    /// <param name="optionType">Call/Put type</param>
    /// <param name="barrierType">Barrier type feature</param>
    public HybridSwapSpreadOption(Dt effective, Dt maturity, Currency ccy, double strike, double barrier,
      HybridOptionType optionType, HybridBarrierType barrierType)
      : base(effective, maturity, ccy, strike, barrier, optionType, barrierType)
    {
      PayOffFn = HybridPayOffFn.CreateHybridPayOff(strike, barrier, optionType, barrierType);
    }

    /// <summary>
    ///   Validate product
    /// </summary>
    ///
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);
      if (HybridBarrierType != HybridBarrierType.None)
        InvalidValue.AddError(errors, this, "HybridBarrierType",
          string.Format("Barrier type [{0}] not supported for swap spread option currently", HybridBarrierType));
    }
    
    #region Properties

    /// <summary>
    /// Receive index tenor
    /// </summary>
    public Tenor ReceiveIndexTenor { get; set; }

    /// <summary>
    /// Index frequency (frequency of floating leg)
    /// </summary>
    public Frequency IndexFrequency { get; set; }

    /// <summary>
    /// Frequency of the fixed leg
    /// </summary>
    public Frequency SwapFrequency { get; set; }
    
    /// <summary>
    /// Pay index tenor
    /// </summary>
    public Tenor PayIndexTenor { get; set; }
   
    #endregion //Properties
  }
}
