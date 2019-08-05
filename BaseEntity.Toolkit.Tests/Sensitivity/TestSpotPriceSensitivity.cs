using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Sensitivity;

using NUnit.Framework;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Sensitivity
{

  [TestFixture]
  public class TestSpotPriceSensitivity
  {
    #region Bump

    [Test]
    public void StockCurveBumpOverlay()
    {
      StockCurveBump(BumpFlags.None);
    }

    [Test]
    public void StockCurveBumpInplace()
    {
      StockCurveBump(BumpFlags.BumpInPlace);
    }

    private void StockCurveBump(BumpFlags flag)
    {
      Dt asOf = Dt.Today();
      var dc = new DiscountCurve(asOf, 0.04);
      var stockCurve = new StockCurve(asOf, 100, dc, 0.04, null)
      {
        Name = "Stock_Curve"
      };
      var date1Y = Dt.Add(asOf, Tenor.OneYear);
      var price1Y = stockCurve.Interpolate(date1Y);
      Assert.AreEqual(stockCurve.SpotPrice, price1Y, "Price 1Y");

      foreach (var date in new[] { Dt.Empty, date1Y })
      {
        var pricer = new StockSpotPricer(stockCurve, date);
        var table = Sensitivities2.Calculate(
          new[] { pricer }, "Pv", null,
          BumpTarget.StockPrice | BumpTarget.IncludeSpot,
          1, 0, BumpType.ByTenor, flag,
          null, false, false, null, false, true, null);
        Assert.IsTrue(table.Rows.Count > 0);
        var actual = (double)table.Rows[0]["Delta"];
        Assert.AreEqual(1.0, actual, 1E-15,
          date.IsEmpty() ? "Spot" : date.ToString());
      }
    }

    #endregion

    #region Xml Serialization Consistency

    [Test]
    public void StockCurveXmlSerialization()
    {
      Dt asOf = Dt.Today();
      var dc = new DiscountCurve(asOf, 0.04) {Name = "Discount"};
      var stockCurve = new StockCurve(asOf, 100, dc, 0.04, null)
      {
        Name = "Stock_Curve"
      };
      var path = System.IO.Path.GetTempFileName();
      BaseEntity.Toolkit.Util.XmlSerialization.WriteXmlFile(stockCurve, path);
      var loaded = BaseEntity.Toolkit.Util.XmlSerialization.ReadXmlFile<StockCurve>(path);
      AssertMatch("StockCurve", stockCurve, loaded);
    }

    #endregion

    #region Nested type: StockSpotPricer

    private class StockSpotPricer : PricerBase, IPricer
    {
      public StockCurve StockCurve { get; }

      private readonly Dt _date;
      public DiscountCurve DiscountCurve => StockCurve.DiscountCurve;

      public StockSpotPricer(StockCurve curve, Dt date)
        : base(curve.Stock, curve.AsOf, curve.AsOf)
      {
        StockCurve = curve;
        _date = date;
      }

      public override double ProductPv()
      {
        return _date.IsEmpty() ? StockCurve.SpotPrice
          : StockCurve.Interpolate(_date);
      }
    }

    #endregion
  }
}
