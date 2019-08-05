//
// Copyright (c)    2018. All rights reserved.
//

using System.Data;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  public class TestMmDRateLock : ToolkitTestBase
  {
    [TestCase(ScenarioShiftType.None, 0.0)]
    [TestCase(ScenarioShiftType.Absolute, 0.0005)]
    [TestCase(ScenarioShiftType.Relative, 0.01)]
    [TestCase(ScenarioShiftType.Specified, 0.0004)]
    public void TestMmdRateLockScenario(ScenarioShiftType type, double bumpSize)
    {
      var settle = new Dt(20140331);
      var determineDate = new Dt(20151222);
      var fwdDetermineDate = new Dt(20160204);
      const double fixedRate = 0.0147;
      const double dv01 = 4.86;
      const double mid = 0.0134;
      const double bondYield = 0.0117;
      const string contractName = "3mx5y";

      var product = new MmdRateLock(settle, determineDate, contractName,
        fixedRate, dv01, Currency.USD);

      var pricer = new MmdRateLockPricer(product, settle, fwdDetermineDate,
        bondYield, mid, 1.0);
      bumpSize = type == ScenarioShiftType.Specified ? bumpSize + bondYield : bumpSize;

      Test(pricer, type, bumpSize);

      //Test the full coverage for another product constructor.
      var cTenors = ((MmdRateLock) pricer.Product).ContractName;
      
      Tenor contractTenor, bondTenor;
      if (Tenor.TryParseComposite(cTenors, out contractTenor, out bondTenor))
      {
        product = new MmdRateLock(settle, determineDate, contractTenor, bondTenor,
          fixedRate, dv01, Currency.USD);
        pricer.Product = product;
        pricer.Reset();
        Test(pricer, type, bumpSize);
      }
    }

    private static void Test(MmdRateLockPricer pricer, ScenarioShiftType type, double bumpSize)
    {
      var scenarioValueShift = new ScenarioValueShift(type, bumpSize);
      var yieldShiftedScenario = new ScenarioShiftPricerTerms(
        new[] { "BondYield" }, new[] { scenarioValueShift });
      var dt = Scenarios.CalcScenario(new IPricer[] { pricer }, new[] { "Pv" },
        new[] { yieldShiftedScenario }, false, true, null);

      DataColumn labelCol = null;
      foreach (DataColumn column in dt.Columns)
      {
        if (!column.ColumnName.Equals("Delta")) continue;
        labelCol = column;
      }
      var delta1 = 0.0;
      if (labelCol != null)
      {
        delta1 = (double)dt.Rows[0][labelCol.ColumnName];
      }
      var delta2 = CalcBondYieldDelta(pricer, bumpSize, type);
      Assert.AreEqual(delta1, delta2, 1E-14);
    }

    private static double CalcBondYieldDelta(MmdRateLockPricer pricer,
      double bumpSize, ScenarioShiftType shiftType)
    {
      double originalYield = pricer.BondYield;
      var bValue = pricer.ProductPv();
      pricer.BondYield = Scenarios.Bump(originalYield, shiftType, bumpSize);
      var uValue = pricer.ProductPv();
      pricer.BondYield = originalYield;
      return uValue- bValue;
    }
  }
}
