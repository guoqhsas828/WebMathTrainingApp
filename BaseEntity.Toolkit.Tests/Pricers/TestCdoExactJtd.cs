//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Pricers;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture("CDO0000015")]
  public class TestCdoExactJtd : TestCdoBase
  {
    #region Set Up

    public TestCdoExactJtd(string name) : base(name)
    {}

    /// <summary>
    ///   Create an array of CDO Pricers
    /// </summary>
    /// <returns>CDO Pricers</returns>
    [OneTimeSetUp]
    public void SetUp()
    {
      _alternativeSettings = new ConfigItems
      {
        {"BasketPricer.ExactJumpToDefault", true}
      }.Update();

      CreatePricers();
    }

    /// <summary>
    /// Restore the original configuration settings
    /// </summary>
    [OneTimeTearDown]
    public void TearDown()
    {
      _alternativeSettings?.Dispose();
      _alternativeSettings = null;
    }

    private IDisposable _alternativeSettings;
    #endregion // SetUp

    #region Tests

    [Test, Category("PricingMethods")]
    public void BreakEvenPremium()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).BreakEvenPremium();
        });
    }

    [Test, Category("PricingMethods")]
    public void BreakEvenFee()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).BreakEvenFee();
        });
    }

    [Test, Category("RiskMethods")]
    public void DefaultSensitivity()
    {
      Default(cdoPricers_);
    }

    [Test, Category("RiskMethods")]
    public void DefaultSensitivity_RescaleStrikes()
    {
      Default(cdoPricers_, rescaleStrikesArray_);
    }

    [Test, Category("PricingMethods")]
    public void Pv()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).Pv();
        });
    }

    [Test, Category("PricingMethods")]
    public void FlatPrice()
    {
      TestNumeric(cdoPricers_, cdoNames_,
        delegate(object p)
        {
          return ((SyntheticCDOPricer)p).FlatPrice();
        });
    }

    #endregion
  }
}
