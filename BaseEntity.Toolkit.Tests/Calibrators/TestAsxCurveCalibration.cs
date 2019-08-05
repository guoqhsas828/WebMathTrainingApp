// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  [TestFixture]
  public class AsxCurveCalibrationTests
  {
    public AsxCurveCalibrationTests()
    {
      _curveTerms = RateCurveTermsUtil.CreateDefaultCurveTerms("AUDLIBOR_3M");
      var asx = new RateFuturesCurveTerm(
      BDConvention.Modified, DayCount.Actual365Fixed, Calendar.SYB,
      Tenor.Parse("90D"), Currency.AUD, RateFutureType.ASXBankBill,
      ProjectionType.SimpleProjection) { AssetKey = "ASX" };
      _curveTerms.AssetTerms[asx.AssetKey] = asx;

      var settings = RateCurveBuilder.CreateFitSettings(_pricingDate, _curveTerms);
      settings.OverlapTreatmentOrder = new[]{
        InstrumentType.FUT,InstrumentType.Swap,InstrumentType.MM};
      settings.InterpScheme = InterpScheme.FromString(
        "Weighted", ExtrapMethod.Const, ExtrapMethod.Const);
      _settings = new CalibratorSettings(settings);

      // Add big convexty adjustment
      _settings.FwdModelParameters = new RateModelParameters(
        RateModelParameters.Model.Hull,
        new[] {RateModelParameters.Param.Sigma},
        new IModelParameter[] {new VolatilityCurve(_pricingDate, 0.5)},
        Tenor.Empty, Currency.None);
    }

    [Test]
    public void TestAsxCurveConsistency()
    {
      Dt asOf = _pricingDate;
      var quotes = _quotes.Column(2).Select(s => s.ParseDouble()).ToArray();
      var instrs = _quotes.Column(0).ToArray();
      var tenors = _quotes.Column(1).ToArray();
      var dc = DiscountCurveFitCalibrator.DiscountCurveFit(asOf,
        _curveTerms, "AsxDiscount", quotes, instrs, tenors, _settings);

      var pricers = dc.Tenors
        .Select(t => new { P = t.Product as StirFuture, Q = t.MarketPv })
        .Where(o => o.P != null)
        .Select(o => CreatePricer(o.P, o.Q, dc, dc))
        .ToArray();
      for (int i = 0; i < pricers.Length; ++i)
      {
        var expect = pricers[i].QuotedPrice;
        var actual = pricers[i].ModelPrice();
        Assert.AreEqual(expect, actual, 1E-12);
      }
      return;
    }

    StirFuturePricer CreatePricer(StirFuture future, double quote,
      DiscountCurve dc, DiscountCurve rc)
    {
      return new StirFuturePricer(future, _pricingDate, _settleDate, 1.0/future.ContractSize, dc, rc) { QuotedPrice = quote };
    }

    private static Dt _D(string s)
    {
      return Dt.FromStr(s);
    }

    Dt _pricingDate = _D("7-Nov-11"), _settleDate = _D("7-Nov-11");

    private readonly string[,] _trade = {
      {"Pricing Date", "7-Nov-11"},
      {"Settle Date", "7-Nov-11"},
      {"Calculation Env", "Official"},
      {"Traded/Closing Level", "95.000%"},
      {"Futures Price", "95.500%"},
      {"Name", "SFXM4.2014"},
      {"Effective Date", ""},
      {"Maturity Date", "20-Jun-14"},
      {"Futures Type", "ASX90DBillFuture"},
      {"Currency", "AUD"},
      {"DayCount", "Actual365Fixed"},
      {"Calendar", "SYB"},
      {"Roll", "Modified"},
      {"Index", "AUDLIBOR"},
      {"Tenor", "3 Months"},
      {"Contract Size", "1000000"},
      {"Futures Quote Type", "ASXBankBill"},
      {"Quote Unit Value", "0"},
      {"Underlying Maturity Tenor", "90 Days"},
    };

    private readonly CurveTerms _curveTerms;
    private readonly CalibratorSettings _settings;

#if NotYet
    private readonly string[,] _fitsettings = {
      {"CurveFitMethod", "Bootstrap"},
      {"InterpMethod", "Weighted"},
      {"ExtrapMethod", "Const"},
      {"Overlap Treatment Order", "MM+FUT+Swap"},
      {"Interp Basis Swap Quote", "TRUE"},
      {"Fit to Market", "1"},
      {"Futures Weighting", "1"},
      {"CA Interp Method", "Linear"},
      {"CA Extrap Method", "Const"},
      {"Fast Rate Projection", "TRUE"},
      {"Create as Overlay", "FALSE"},
    };
#endif

    private readonly string[,] _quotes = {
      {"MM", "1 Days", "2.900%", "9-Nov-11", "10-Nov-11"},
      {"MM", "1 Months", "2.790%", "9-Nov-11", "9-Dec-11"},
      {"MM", "2 Months", "2.760%", "9-Nov-11", "9-Jan-12"},
      {"MM", "3 Months", "2.725%", "9-Nov-11", "9-Feb-12"},
      {"ASX", "IRU3", "97.230%", "", "18-Sep-13"},
      {"ASX", "IRZ3", "97.410%", "", "18-Dec-13"},
      {"ASX", "IRH4", "97.490%", "", "19-Mar-14"},
      {"ASX", "IRM4", "97.490%", "", "18-Jun-14"},
      {"ASX", "IRU4", "97.320%", "", "17-Sep-14"},
      {"Swap", "2 Years", "2.694%", "9-Nov-11", "11-Nov-13"},
      {"Swap", "3 Years", "2.885%", "9-Nov-11", "10-Nov-14"},
      {"Swap", "4 Years", "3.049%", "9-Nov-11", "9-Nov-15"},
      {"Swap", "5 Years", "3.215%", "9-Nov-11", "9-Nov-16"},
      {"Swap", "6 Years", "3.403%", "9-Nov-11", "9-Nov-17"},
      {"Swap", "7 Years", "3.543%", "9-Nov-11", "9-Nov-18"},
      {"Swap", "8 Years", "3.678%", "9-Nov-11", "11-Nov-19"},
      {"Swap", "9 Years", "3.794%", "9-Nov-11", "9-Nov-20"},
      {"Swap", "10 Years", "3.886%", "9-Nov-11", "9-Nov-21"},
      {"Swap", "15 Years", "4.180%", "9-Nov-11", "9-Nov-26"},
      {"Swap", "20 Years", "4.298%", "9-Nov-11", "10-Nov-31"},
      {"Swap", "25 Years", "4.316%", "9-Nov-11", "10-Nov-36"},
      {"Swap", "30 Years", "4.324%", "9-Nov-11", "11-Nov-41"},
    };
  }
}
