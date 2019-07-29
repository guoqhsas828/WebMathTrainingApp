/*
 * ToolkitConfigSettings.cs
 *
 *
 */
using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketForNtdPricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Products.StandardProductTerms;
using BaseEntity.Toolkit.Concurrency;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Util.Configuration
{
  /// <summary>
  ///   Container of all the configuration settings
  /// </summary>
  /// <exclude />
  [Serializable]
  public class ToolkitConfigSettings : ToolkitBaseConfigSettings
  {
    #region List of config groups

    /// <exclude />
    public readonly BasketPricerConfig BasketPricer = new BasketPricerConfig();

    /// <exclude />
    public readonly CashflowPricerConfig CashflowPricer = new CashflowPricerConfig();

    /// <exclude />
    public readonly CcrPricerConfig CcrPricer = new CcrPricerConfig();

    /// <exclude />
    public readonly CDSCashflowPricerConfig CDSCashflowPricer = new CDSCashflowPricerConfig();

    /// <exclude />
    public readonly CloneConfig Cloning = new CloneConfig();

    /// <exclude />
    public readonly FxCalibratorConfig FxCalibrator = new FxCalibratorConfig();

    /// <exclude />
    public readonly FxOptionConfig FxOption = new FxOptionConfig();

    /// <exclude />
    public readonly SwapLegConfig SwapLeg = new SwapLegConfig();

    /// <exclude />
    public readonly SwapLegPricerConfig SwapLegPricer = new SwapLegPricerConfig();

    /// <exclude />
    public readonly InterestCouponCalculatorConfig InterestCouponCalculator = new InterestCouponCalculatorConfig();

    /// <exclude />
    public readonly BondPricerConfig BondPricer = new BondPricerConfig();

    /// <exclude />
    public readonly CapFloorPricerConfig CapFloorPricer = new CapFloorPricerConfig();

    /// <exclude />
    public readonly DiscountBootstrapCalibratorConfig DiscountBootstrapCalibrator = new DiscountBootstrapCalibratorConfig();
    
    /// <exclude />
    public readonly ConcurrencyConfig Concurrency = new ConcurrencyConfig();

    /// <exclude />
    public readonly SemiAnalyticBasketPricerConfig SemiAnalyticBasketPricer = new SemiAnalyticBasketPricerConfig();

    /// <exclude />
    public readonly SurvivalCalibratorConfig SurvivalCalibrator = new SurvivalCalibratorConfig();

    /// <exclude />
    public readonly SyntheticCDOPricerConfig SyntheticCDOPricer = new SyntheticCDOPricerConfig();

    /// <exclude />
    public readonly NtdPricerConfig NtdPricer = new NtdPricerConfig();

    /// <exclude />
    public readonly CDXPricerConfig CDXPricer = new CDXPricerConfig();

    /// <exclude />
    public readonly LCDXPricerConfig LCDXPricer = new LCDXPricerConfig();

    /// <exclude />
    public readonly LoanModelConfig LoanModel = new LoanModelConfig();

    /// <exclude />
    public readonly SwaptionVolatilityFactoryConfig SwaptionVolatilityFactory = new SwaptionVolatilityFactoryConfig();

    /// <exclude />
    public readonly ThetaSensitivityConfig ThetaSensitivity = new ThetaSensitivityConfig();

    /// <exclude />
    public readonly ReferenceRateConfig ReferenceRate = new ReferenceRateConfig();

    /// <exclude />
    public readonly StandardProductTermsConfig StandardProductTerms = new StandardProductTermsConfig();

    /// <exclude />
    public readonly CpuExtensionsConfig CpuExtensions = new CpuExtensionsConfig();

    /// <exclude />
    public readonly BermudanSwaption BermudanSwaption = new BermudanSwaption();

    /// <exclude />
    public readonly SimulationConfig Simulations = new SimulationConfig();

    #endregion List of config groups

    #region XML representation

    /// <summary>
    ///  Get an xml string representation of this instance.
    /// </summary>
    public string XmlString => ToolkitConfigUtil.WriteSettingsXml(
      this, "ToolkitConfig");

    #endregion
  }

}
