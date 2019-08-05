//
// Copyright (c)    2002-2018. All rights reserved.
//

using System;
using System.Globalization;
using System.Linq;
using BaseEntity.Shared;

using NUnit.Framework;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture("CAC")]
  [TestFixture("DAX")]
  [TestFixture("FTSE")]
  [TestFixture("HSI")]
  [TestFixture("N225")]
  [TestFixture("SPX")]
  [TestFixture("STOXX50E")]
  //[TestFixture]
  [Ignore("FB47570: Failing on Haswell")]
  public class TestSabrCalibration : ToolkitTestBase
  {
    public TestSabrCalibration(string name) : base(name) {}

    public double[,] Data { get; set; }

    [SetUp]
    public void Setup()
    {
      if (Data == null || Data.Length == 0)
        throw new ApplicationException("Empty data");
      if(Data.GetLength(0) < 1 || Data.GetLength(1) < 1)
        throw new ApplicationException("Invalid data");
    }

    [Test]
    public void CalibrateSabr()
    {
      CalibrateParameters(new[] { 0.0, 0.0, 0.0, -1.0 },
        new[] { Double.MaxValue, 1.0, Double.MaxValue, 1.0 });
    }

    [Test]
    public void CalibrateCev()
    {
      CalibrateParameters(new[] { 0.0, Double.MinValue, 0.0, 0.0 },
        new[] { Double.MaxValue, 1.0, Double.MaxValue, 1.0 });
    }

    public void CalibrateParameters(double[] lowerBounds, double[] upperBounds)
    {
      var modelFlag = SabrModelFlags.None;
      var data = Data;
      int rows = data.GetLength(0) - 1, cols = data.GetLength(1) - 1;
      var moneyness = data.Row(0).Skip(1).Select(m=>m+1).ToArray();
      var colnames = new[] {"Zeta", "Beta", "Nu", "Rho"}.Concat(data.Row(0).Skip(1)
        .Select(m => ((int)(m * 100.1)).ToString(CultureInfo.InvariantCulture)))
        .ToArray();
      double[][] results = Enumerable.Range(0,4+cols).Select(i=>new double[rows]).ToArray();
      for (int i = 0; i < rows; ++i)
      {
        var timeToExpiry = data[i + 1, 0];
        var pars = SabrVolatilitySmile.CalibrateParameters(
          timeToExpiry, moneyness, data.Row(i+1).Skip(1).ToArray(),
          lowerBounds, upperBounds, modelFlag);
        for (int k = 0; k < 4; ++k)
          results[k][i] = pars[k];
        for (int k = 0; k < moneyness.Length; ++k)
          results[k + 4][i] = SabrVolatilitySmile.CalculateVolatility(
            pars[0], pars[1], pars[2], pars[3], moneyness[k], timeToExpiry);
      }
      MatchExpects(ToResultData(results,colnames,0.0));
    }
  }
}
