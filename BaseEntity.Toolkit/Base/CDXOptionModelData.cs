using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Config = BaseEntity.Toolkit.Util.Configuration.ToolkitConfigurator;

namespace BaseEntity.Toolkit.Base
{
  /// <exclude/>
  [Serializable]
  public class CDXOptionModelData
  {
    /// <exclude/>
    public CDXOptionModelParam Choice = CDXOptionModelParam.AdjustSpread
      | CDXOptionModelParam.ForceFlatMarketCurve
        | CDXOptionModelParam.AdjustSpreadByMarketMethod
          | (Config.Settings.CDXPricer.MarketPayoffsForIndexOptions
            ? (CDXOptionModelParam.MarketPayoffConsistent |
              CDXOptionModelParam.UseProtectionPvForFrontEnd)
            : CDXOptionModelParam.None);

    /// <exclude/>
    public Correlation Correlation = null;

    /// <exclude/>
    public Copula Copula = new Copula();

    /// <exclude/>
    public double Accuracy = 0;

    /// <exclude/>
    public double ModifiedBlackCenter = Double.NaN;
  }

  /// <summary>
  ///   CDX option model parameters
  /// </summary>
  [Flags]
  public enum CDXOptionModelParam : uint
  {
    /// <summary>None of the flags is set</summary>
    None = 0,

    /// <summary>Use protection pv instead expected losses for front end protection </summary>
    UseProtectionPvForFrontEnd = 1,

    /// <summary>Force the forward value be clean</summary>
    ForceCleanValue = 2,

    /// <summary>Force intrinsic survival probability in spread adjustment</summary>
    ForceIntrinsicSurvival = 4,

    /// <summary>Force to ignore the term structure of the user market curve</summary>
    ForceFlatMarketCurve = 8,

    /// <summary>If true, adjust forward spread by front end protection</summary>
    AdjustSpread = 16,

    /// <summary>Always adjust spread by market method</summary>
    AdjustSpreadByMarketMethod = 32,

    /// <summary>Always calculate index value as sum of component CDS values</summary>
    FullReplicatingMethod = 64,

    /// <summary>Use market payoff convention</summary>
    MarketPayoff = 128,

    /// <summary>Use market payoff convention</summary>
    MarketPayoffConsistent = 256,

    /// <summary>Handle index factors automatically (don't assume the effective factor at option struck is 1)</summary>
    HandleIndexFactors = 512,
  }

}
