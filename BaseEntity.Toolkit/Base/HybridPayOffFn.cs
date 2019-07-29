using System;
using BaseEntity.Toolkit.Numerics;


namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Hybrid option type
  /// </summary>
  public enum HybridOptionType
  {
    /// <summary>
    /// <m>(f - K)^+ I_{l \% B}</m>
    /// </summary>
    CallDigital,
    /// <summary>
    /// <m>(K - f)^+ I_{l \% B}</m>
    /// </summary>
    PutDigital,
    /// <summary>
    /// <formula inline="true">(f - l - K)^+</formula>
    /// </summary>
    Call,
    /// <summary>
    /// <formula inline="true">(f - l - K)^+</formula>
    /// </summary>
    Put
  }

  /// <summary>
  /// Hybrid Barrier type
  /// </summary>
  public enum HybridBarrierType
  {
    /// <summary>None</summary>
    None,
    /// <summary>Knock In</summary>
    KnockIn,
    /// <summary>Knock out</summary>
    KnockOut
  }
 
	///<summary>
	/// class used for creating hybrid option payoffs
	///</summary>
  public class HybridPayOffFn
  {
    /// <summary>
    /// static factory method for a generaalised spread/hybrid option payoff
    /// </summary>
    /// <remarks>
    ///   <para>A range of option payoffs are supported. These are:</para>
    ///   <list type="table">
    ///     <listheader><term>Type</term><description>Payoff</description></listheader>
    ///     <item>
    ///       <term>Call</term><description><m>(R - P - K)^+</m></description>
    ///     </item><item>
    ///       <term>Put</term><description><m>(K - R + P)^+</m></description>
    ///     </item><item>
    ///       <term>DigitalCall</term><description><m>(R - K)^+</m></description>
    ///     </item><item>
    ///       <term>DigitalPut</term><description><m>(K - R)^+</m></description>
    ///     </item><item>
    ///       <term>DigitalCall KnockIn</term><description><m>(R - K)^+I_{\{P &gt; B\}}</m></description>
    ///     </item><item>
    ///       <term>DigitalCall KnockOut</term><description><m>(R - K)^+I_{\{P &lt; B\}}</m></description>
    ///     </item><item>
    ///       <term>DigitalPut KnockIn</term><description><m>(K - R)^+I_{\{P &gt; B\}}</m></description>
    ///     </item><item>
    ///       <term>DigitalPut KnockOut</term><description><m>(K - R)^+I_{\{P &lt; B\}}</m></description>
    ///     </item>
    ///   </list>
    /// </remarks>
    /// <param name="strike">Strike for stock process</param>
    /// <param name="barrier">Barrier for rate process</param>
    /// <param name="optionType">Option type</param>
    /// <param name="barrierType">Barrier type</param>
    /// <returns>A payoff2DFn that pays an option on stock if the rate hits a certain barrier at maturity</returns>
    public static Payoff2DFn CreateHybridPayOff(double strike, double barrier, HybridOptionType optionType, HybridBarrierType barrierType)
    {

      //regular call option
      Payoff2DFn payOffFn = null;
      switch (optionType)
      {
        case HybridOptionType.Call:
          payOffFn = new Payoff2DFn((f, l) => Math.Max(f - l - strike, 0.0));
          break;
        case HybridOptionType.Put:
          payOffFn = new Payoff2DFn((f, l) => Math.Max(strike - f + l, 0.0));
          break;
        case HybridOptionType.CallDigital:
          if (barrierType == HybridBarrierType.None)
            payOffFn = new Payoff2DFn((f, l) => Math.Max(f - strike, 0))
                         {
                           IntegrationRegionF = new[] { strike, double.MaxValue }
                         };
          if (barrierType == HybridBarrierType.KnockIn)
            payOffFn = new Payoff2DFn((f, l) => Math.Max(f - strike, 0) * (l > barrier ? 1.0 : 0.0))
                         {
                           IntegrationRegionF = new[] { strike, double.MaxValue },
                           IntegrationRegionL = new[] { barrier, double.MaxValue }
                         };
          if (barrierType == HybridBarrierType.KnockOut)
            payOffFn = new Payoff2DFn((f, l) => Math.Max(f - strike, 0) * (l > barrier ? 0.0 : 1.0))
                         {
                           IntegrationRegionF = new[] { strike, double.MaxValue },
                           IntegrationRegionL = new[] { double.MinValue, barrier }
                         };
          break;
        case HybridOptionType.PutDigital:
          if (barrierType == HybridBarrierType.None)
            payOffFn = new Payoff2DFn((f, l) => Math.Max(strike - f, 0))
                         {
                           IntegrationRegionF = new[] { double.MinValue, strike }
                         };
          if (barrierType == HybridBarrierType.KnockIn)
            payOffFn = new Payoff2DFn((f, l) => Math.Max(strike - f, 0) * (l > barrier ? 1.0 : 0.0))
                         {
                           IntegrationRegionF = new[] { double.MinValue, strike },
                           IntegrationRegionL = new[] { barrier, double.MaxValue }
                         };
          if (barrierType == HybridBarrierType.KnockOut)
            payOffFn = new Payoff2DFn((f, l) => Math.Max(strike - f, 0) * (l > barrier ? 0.0 : 1.0))
                         {
                           IntegrationRegionF = new[] { double.MinValue, strike },
                           IntegrationRegionL = new[] { double.MinValue, barrier }
                         };
          break;
      }
      //regular put option
      return payOffFn;
    }
  }
}
