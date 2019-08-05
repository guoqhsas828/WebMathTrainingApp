
using System.Linq;
using NUnit.Framework;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Sensitivity 
{
  public class TestCdxOptionDeltaGamma : SensitivityTest
  {
    [TestCase(OptionType.Put, CDXOptionModelType.FullSpread)]
    [TestCase(OptionType.Put, CDXOptionModelType.Black)]
    [TestCase(OptionType.Put, CDXOptionModelType.ModifiedBlack)]
    [TestCase(OptionType.Put, CDXOptionModelType.BlackArbitrageFree)]
    [TestCase(OptionType.Call, CDXOptionModelType.FullSpread)]
    [TestCase(OptionType.Call, CDXOptionModelType.Black)]
    [TestCase(OptionType.Call, CDXOptionModelType.ModifiedBlack)]
    [TestCase(OptionType.Call, CDXOptionModelType.BlackArbitrageFree)]
    public void TestCdxOptionDeltaAndGamma(OptionType optionType, CDXOptionModelType modelType)
    {
      //manually bump
      var cdxPricer = CreateCdxPricer();
      var baseCdxPv = CalcIndexMarketPrice(cdxPricer);
      var ubumpedCdxPricer = CreateCdxPricer(_uBump);
      var uBumpedCdxPv = CalcIndexMarketPrice(ubumpedCdxPricer);
      var dBumpedCdxPricer = CreateCdxPricer(_dBump);
      var dBumpedCdxPv = CalcIndexMarketPrice(dBumpedCdxPricer);

      var optionPricer = GetCdxOptionPricer(optionType, modelType, false, true, _marketQuote);
      var baseOptionPv = CalcOptionMarketValue(optionPricer);
      var uBumpedOptionPricer = GetCdxOptionPricer(optionType, modelType, false, true,_marketQuote, _uBump);
      var uBumpedOptionPv = CalcOptionMarketValue(uBumpedOptionPricer);
      var dBumpedOptionPricer = GetCdxOptionPricer(optionType, modelType, false, true, _marketQuote, _dBump);
      var dBumpedOptionPv = CalcOptionMarketValue(dBumpedOptionPricer);
      var manualDelta1 = (uBumpedOptionPv - baseOptionPv)/(uBumpedCdxPv - baseCdxPv);
      var manualDelta2 = (dBumpedOptionPv - baseOptionPv)/(dBumpedCdxPv - baseCdxPv);
      var manualGamma = manualDelta1 - manualDelta2;

      //sensitivity calcs
      var expectD = optionPricer.MarketDelta(_uBump[0]/10000.0);
      var expectG = optionPricer.MarketGamma(_uBump[0]/10000.0, -_dBump[0]/10000.0, false);
      Assert.AreEqual(manualDelta1, expectD, 1e-12);
      Assert.AreEqual(manualGamma, expectG, 1e-12);
    }

    [TestCase(OptionType.Put, CDXOptionModelType.FullSpread)]
    [TestCase(OptionType.Put, CDXOptionModelType.Black)]
    [TestCase(OptionType.Put, CDXOptionModelType.ModifiedBlack)]
    [TestCase(OptionType.Put, CDXOptionModelType.BlackArbitrageFree)]
    [TestCase(OptionType.Call, CDXOptionModelType.FullSpread)]
    [TestCase(OptionType.Call, CDXOptionModelType.Black)]
    [TestCase(OptionType.Call, CDXOptionModelType.ModifiedBlack)]
    [TestCase(OptionType.Call, CDXOptionModelType.BlackArbitrageFree)]
    public void TestUniformBumpingAndScaling(OptionType optionType, CDXOptionModelType modelType)
    {
      var oPricer = GetCdxOptionPricer(optionType, modelType, true, true, _marketQuote);

      var delta = oPricer.MarketDelta(_uBump[0]/10000.0);
      var gamma = oPricer.MarketGamma(_uBump[0] / 10000.0, -_dBump[0] / 10000.0, false);

      var sopB = GetCdxOptionPricer(optionType, modelType, true, false, _marketQuote);
      var sopU = GetCdxOptionPricer(optionType, modelType, true, false, _marketQuote + _uBump[0]);
      var sopD = GetCdxOptionPricer(optionType, modelType, true, false, _marketQuote - _uBump[0]);
      var opvB = sopB.CalculateFairPrice(sopB.Volatility);
      var opvU = sopU.CalculateFairPrice(sopU.Volatility);
      var opvD = sopD.CalculateFairPrice(sopD.Volatility);

      var sipB = sopB.GetPricerForUnderlying();
      var sipU = sopU.GetPricerForUnderlying();
      var sipD = sopD.GetPricerForUnderlying();
      var ipvB = 1 - sipB.MarketPrice();
      var ipvU = 1 - sipU.MarketPrice();
      var ipvD = 1 - sipD.MarketPrice();

      var delta1 = (opvU - opvB)/(ipvU - ipvB);
      var delta2 = (opvD - opvB)/(ipvD - ipvB);

      var expectD = (opvU - opvB)/(ipvU - ipvB);
      var diffD = (expectD - delta)/expectD*100.0;
      var expectG = delta1 - delta2;
      var diffG = (expectG - gamma)/expectG*100.0;
      if (modelType == CDXOptionModelType.ModifiedBlack)
      {
        Assert.Less(diffD, 2.5);
        Assert.Less(diffG, 2.5);
      }
      else
      {
        Assert.Less(diffD, 1.0);
        Assert.Less(diffG, 1.0);
      }
    }

    // Create CDX Pricer
    private CDXPricer CreateCdxPricer(double[] sBumps = null)
    {
      var cdx = new CDX(_settle, Dt.Add(_settle, "5 year"), Currency.USD, _dealPremium / 10000, Calendar.NYB);
      var sc = SensitivityTestUtil.CreateSurvialCurvesArray(_asOf, _settle, sBumps);
      return new CDXPricer(cdx, _asOf, _settle, sc[0].SurvivalCalibrator.DiscountCurve, sc, _marketQuote / 10000);
    }

    private ICreditIndexOptionPricer GetCdxOptionPricer( OptionType type, CDXOptionModelType modelType, 
      bool scale, bool full, double marketQuote,  double[] sBumps = null)
    {
      var optionExpiry = Dt.CDSMaturity(_settle, "6M");
      var discountCurve = SensitivityTestUtil.CreateIRCurve(_asOf);
      var survivalCurves = SensitivityTestUtil.CreateSurvialCurvesArray(_asOf, _settle, sBumps);
      var cdx = new CDX(_settle, Dt.Add(_settle, "5 year"), Currency.USD, _dealPremium / 10000, Calendar.NYB);
      var cdxo= new CDXOption(_settle, Currency.USD, cdx, optionExpiry,
        type == OptionType.Put? PayerReceiver.Payer : PayerReceiver.Receiver,
        OptionStyle.European, _strike/10000.0, false);

       var quote = new MarketQuote(marketQuote/10000, QuotingConvention.CreditSpread);
      var modelData = new CDXOptionModelData();
      if(full) modelData.Choice |= CDXOptionModelParam.FullReplicatingMethod;
      var volSurface = CalibratedVolatilitySurface.FromFlatVolatility(_asOf, 0.35);
      var scaledCurves =scale? GetScalingCalibrator(marketQuote, discountCurve, survivalCurves).Scale(survivalCurves):null;

      return cdxo.CreatePricer(_asOf, _settle, discountCurve, quote, Dt.Empty, 0.4, survivalCurves.Length,
        scaledCurves??survivalCurves, modelType, modelData, volSurface, _notional, null);
    }


    private IndexScalingCalibrator GetScalingCalibrator(double marketQuote, DiscountCurve dCurve,
      SurvivalCurve[] sCurves)
    {
      var cdx = new CDX(_settle, Dt.Add(_settle, "5 year"), Currency.USD, _dealPremium / 10000, Calendar.NYB);
      var tenors = Toolkit.Base.Utils.ToTenorName(cdx.Effective, cdx.Maturity, true);
      var includes = ArrayUtil.NewArray(sCurves.Length, true);
      var indexScaleCalibrator = new IndexScalingCalibrator(_asOf, _settle, new[] { cdx }, new[] { tenors },
        new[] { marketQuote / 10000.0 }, false, new[] { CDXScalingMethod.Model }, true, false, dCurve,
        sCurves, includes, 0.4)
      {
        ScaleHazardRates = true,
        ScalingType = BumpMethod.Relative,
        ActionOnInadequateTenors = ActionOnInadequateTenors.DropFirstIndexTenor
      };
      if (indexScaleCalibrator.ActionOnInadequateTenors == ActionOnInadequateTenors.DropFirstIndexTenor)
        indexScaleCalibrator.CheckUseTenors();

      return indexScaleCalibrator;

    }


    private double CalcIndexMarketPrice(CDXPricer pricer)
    {
      return -(pricer.IntrinsicValue() - pricer.Accrued()) / pricer.CurrentNotional;
    }

    private double CalcOptionMarketValue(ICreditIndexOptionPricer pricer)
    {
      return pricer.CalculateFairPrice(pricer.Volatility);
    }

    private static Dt _asOf = new Dt(20160121);
    private static Dt _settle = new Dt(20160122);
    private double _dealPremium = 46.5;
    private double _marketQuote = 50.0;
    private double _strike = 48.0;
    private double _notional = 1000000;
    private double[] _uBump = Enumerable.Repeat(5.0, 125).ToArray();
    private double[] _dBump = Enumerable.Repeat(-5.0, 125).ToArray();
  }
}
