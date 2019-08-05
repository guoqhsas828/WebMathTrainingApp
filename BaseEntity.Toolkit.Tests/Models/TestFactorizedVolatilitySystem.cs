//
// Copyright (c)    2002-2018. All rights reserved.
//
using System.Linq;
using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestFactorizedVolatilitySystem : ToolkitTestBase
  {
    protected string[] _tenors = { "1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y" };

    [Test]
    public void FactorLoadingCollectionMatchingReferentialIntegrity()
    {
      var ten = _tenors.Select(Tenor.Parse).ToArray();
      var factors = new FactorLoadingCollection(new[] { "F1", "F2" }, ten);
      var curve = new SurvivalCurve(Dt.Parse("20160606"));
      curve.Name = "Test";
      var baseArray = new double[,] { { 0, 1}, { 2, 3 }, { 4, 5 }, { 6, 7 }, { 8, 9 }, { 10, 11 }, { 12, 13 }, { 14, 15 }, { 16, 17 }, { 18, 19 } };
      factors.AddFactors(curve, baseArray);
      var factorResults = factors.GetFactors(curve);
      Assert.IsNotNull(factorResults);
      for (int i = 0; i < 10; ++i)
      {
        for (int j = 0; j < 2; ++j)
        {
          Assert.AreEqual(baseArray[i, j], factorResults[i, j], 1e-05);
        }
      }
    }

    [Test]
    public void FactorLoadingCollectionNoMatchingReferentialIntegrity()
    {
      var ten = _tenors.Select(Tenor.Parse).ToArray();
      var factors = new FactorLoadingCollection(new[] { "F1", "F2" }, ten);
      var curve1 = new SurvivalCurve(Dt.Parse("20160606"));
      curve1.Name = "Test";
      var curve2 = new SurvivalCurve(Dt.Parse("20160606"));
      curve2.Name = "Test";
      var baseArray = new double[,] { { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 }, { 8, 9 }, { 10, 11 }, { 12, 13 }, { 14, 15 }, { 16, 17 }, { 18, 19 } };
      factors.AddFactors(curve1, baseArray);
      var factorResults = factors.GetFactors(curve2);
      Assert.IsNotNull(factorResults);
      for (int i = 0; i < 10; ++i)
      {
        for (int j = 0; j < 2; ++j)
        {
          Assert.AreEqual(baseArray[i, j], factorResults[i, j], 1e-05);
        }
      }
    }

    [Test]
    public void VolatilityCollectionMatchingReferentialIntegrity()
    {
      var ten = _tenors.Select(Tenor.Parse).ToArray();
      var vc = new VolatilityCollection(ten);
      var curve = new SurvivalCurve(Dt.Parse("20160606"));
      curve.Name = "Test";
      var baseArray = new VolatilityCurve[10];
      baseArray[0] = new VolatilityCurve(Dt.Parse("20160610"));
      baseArray[1] = new VolatilityCurve(Dt.Parse("20160609"));
      baseArray[2] = new VolatilityCurve(Dt.Parse("20160608"));
      baseArray[3] = new VolatilityCurve(Dt.Parse("20160607"));
      baseArray[4] = new VolatilityCurve(Dt.Parse("20160606"));
      baseArray[5] = new VolatilityCurve(Dt.Parse("20160605"));
      baseArray[6] = new VolatilityCurve(Dt.Parse("20160604"));
      baseArray[7] = new VolatilityCurve(Dt.Parse("20160603"));
      baseArray[8] = new VolatilityCurve(Dt.Parse("20160602"));
      baseArray[9] = new VolatilityCurve(Dt.Parse("20160601"));
      vc.Add(curve, baseArray);
      var vcResults = vc.GetVols(curve);
      Assert.IsNotNull(vcResults);
      Assert.AreEqual(vcResults, baseArray);
    }

    [Test]
    public void VolatilityCollectionNoMatchingReferentialIntegrity()
    {
      var ten = _tenors.Select(Tenor.Parse).ToArray();
      var vc = new VolatilityCollection(ten);
      var curve1 = new SurvivalCurve(Dt.Parse("20160606"));
      curve1.Name = "Test";
      var curve2 = new SurvivalCurve(Dt.Parse("20160606"));
      curve2.Name = "Test";
      var baseArray = new VolatilityCurve[10];
      baseArray[0] = new VolatilityCurve(Dt.Parse("20160610"));
      baseArray[1] = new VolatilityCurve(Dt.Parse("20160609"));
      baseArray[2] = new VolatilityCurve(Dt.Parse("20160608"));
      baseArray[3] = new VolatilityCurve(Dt.Parse("20160607"));
      baseArray[4] = new VolatilityCurve(Dt.Parse("20160606"));
      baseArray[5] = new VolatilityCurve(Dt.Parse("20160605"));
      baseArray[6] = new VolatilityCurve(Dt.Parse("20160604"));
      baseArray[7] = new VolatilityCurve(Dt.Parse("20160603"));
      baseArray[8] = new VolatilityCurve(Dt.Parse("20160602"));
      baseArray[9] = new VolatilityCurve(Dt.Parse("20160601"));
      vc.Add(curve1, baseArray);
      var vcResults = vc.GetVols(curve2);
      Assert.IsNotNull(vcResults);
      Assert.AreEqual(vcResults, baseArray);
    }
  }
}