//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// For CDX option when the settlement date = expiry the intrinsic value should 
  /// be returned.Old code pieces of MarketValue() in CDXOptionPricerBlack.cs and
  /// CDXOptionPricerModifiedBlack.cs return intrinsic* notional and was corrected
  /// </summary>
  [TestFixture]
  public class TestCDXOptionAtExpiry : ToolkitTestBase
  {
    public enum CDXOptionModelType
    {
      /// <summary>
      ///   Spread vol black model
      /// </summary>
      Black,
      /// <summary>
      ///   Price vol black model
      /// </summary>
      BlackPrice,
      /// <summary>
      ///   Spread vol modified black model
      /// </summary>
      ModifiedBlack,
    }

    #region SetUP
    /// <summary>
    ///   Create an array of CDX option Pricers
    /// </summary>
    /// <returns>CDX option Pricers</returns>
    [OneTimeSetUp]
    public void Initialize()
    {
      string filename = GetTestFilePath(discountDataFile_);
      DiscountData discountData = (DiscountData)XmlLoadData(filename, typeof(DiscountData));
      if (discountData == null)
        throw new Exception("No discount curve.");
      discountCurve = discountData.GetDiscountCurve();

      double prob = discountCurve.DiscountFactor(new Dt(20, 9, 2015));

      indexEffectiveDate = new Dt(25, 3, 2008);
      indexSchedTerminationDate = new Dt(20, 6, 2013);
      indexDayCount = DayCount.Actual360;
      indexFrequency = Frequency.Quarterly;
      indexRoll = BDConvention.Modified;
      indexCalendar = Calendar.NYB;
      indexDealPremium = 155.0;
      indexMarketSpread = 115;
      indexSpreadVolatility = 0.84;

      //optinoPricingDate = new Dt(19, 6, 2008);
      optionSettleDate = Dt.FromStr(discountData.AsOf, "%D");
      optionSettleDate = Dt.Add(optionSettleDate, 1, TimeUnit.Days);

      return;
    }
    #endregion // SetUp

    #region helpers
    private CDXOption CreateCDXOption(Dt expiry, PayerReceiver payOrReceive, double strike)
    {
      CDXOption cdxo = new CDXOption(indexEffectiveDate, expiry, indexEffectiveDate, indexSchedTerminationDate, Currency.USD,
                                     indexDealPremium / 10000.0, indexDayCount, indexFrequency, indexRoll, indexCalendar, payOrReceive,
                                     OptionStyle.European, strike / 10000.0, false);
      cdxo.Description = payOrReceive.ToString() + strike.ToString() + expiry.ToString();
      cdxo.Validate();

      return cdxo;
    }

    private CDXOptionPricer CreateCDXOptionPricer(CDXOption option, CDXOptionModelType model)
    {
      SurvivalCurve[] survivalCurves = null;
      double quote = indexMarketSpread / 10000.0;
      double center = Double.NaN;
      bool adjustSpread = true;
      CDXOptionPricer cdxOptionPricer;
      switch (model)
      {
        case CDXOptionModelType.Black:
          cdxOptionPricer = new CDXOptionPricerBlack(option, optionPricingDate, optionSettleDate, discountCurve,
            survivalCurves, quote, indexSpreadVolatility);
          break;
        case CDXOptionModelType.BlackPrice:
          cdxOptionPricer = new CDXOptionPricerBlack(option, optionPricingDate, optionSettleDate, discountCurve,
            survivalCurves, quote, indexSpreadVolatility);
          ((CDXOptionPricerBlack)cdxOptionPricer).PriceVolatilityApproach = true;
          break;
        case CDXOptionModelType.ModifiedBlack:
        default:
          cdxOptionPricer = new CDXOptionPricerModifiedBlack(option, optionPricingDate, optionSettleDate, discountCurve,
           survivalCurves, quote, indexSpreadVolatility);
          if (!Double.IsNaN(center))
            ((CDXOptionPricerModifiedBlack)cdxOptionPricer).Center = center;
          break;
      }
      cdxOptionPricer.Notional = notional;
      if(adjustSpread)
        cdxOptionPricer.ModelParam |= CDXOptionModelParam.AdjustSpread; //- for debug only
      else
        cdxOptionPricer.ModelParam &= ~CDXOptionModelParam.AdjustSpread; //- for debug only
      cdxOptionPricer.MarketRecoveryRate = marketRecoveryRate;

      return cdxOptionPricer;
    }

    #endregion helpers

    #region tests
    [Test, Smoke]
    public void TestBlackPayerExpiryStrike80()
    {
      Timer timer = new Timer();
      timer.Start();

      optionPricingDate = discountCurve.AsOf;
      optionSettleDate = Dt.Add(optionPricingDate, 1, TimeUnit.Days);
      Dt expiry = optionSettleDate;

      CDXOption option = CreateCDXOption(expiry, PayerReceiver.Payer, 80);
      CDXOptionPricer pricer = CreateCDXOptionPricer(option, CDXOptionModelType.Black);
      double marketValue = pricer.MarketValue(indexSpreadVolatility);
      double intrinsic = pricer.Intrinsic();
      double diff = Math.Abs(intrinsic - marketValue);

      Assert.AreEqual(0, diff,
        1e-6, "FairPrice at settle should be less than FairPrice 1 day later");

      timer.Stop();

      return;
    }
    [Test, Smoke]
    public void TestModifiedBlackPayerExpiryStrike80()
    {
      Timer timer = new Timer();
      timer.Start();

      optionPricingDate = discountCurve.AsOf;
      optionSettleDate = Dt.Add(optionPricingDate, 1, TimeUnit.Days);
      Dt expiry = optionSettleDate;

      CDXOption option = CreateCDXOption(expiry, PayerReceiver.Payer, 80);
      CDXOptionPricer pricer = CreateCDXOptionPricer(option, CDXOptionModelType.ModifiedBlack);
      double marketValue = pricer.MarketValue(indexSpreadVolatility);
      double intrinsic = pricer.Intrinsic();
      double diff = Math.Abs(intrinsic - marketValue);

      Assert.AreEqual( 0, diff,
        1e-6, "FairPrice at settle should be less than FairPrice 1 day later");

      timer.Stop();

      return;
    }

    [Test, Smoke]
    public void TestBlackPayerExpiryStrike85()
    {
      Timer timer = new Timer();
      timer.Start();

      optionPricingDate = discountCurve.AsOf;
      optionSettleDate = Dt.Add(optionPricingDate, 1, TimeUnit.Days);
      Dt expiry = optionSettleDate;

      CDXOption option = CreateCDXOption(expiry, PayerReceiver.Payer, 85);
      CDXOptionPricer pricer = CreateCDXOptionPricer(option, CDXOptionModelType.Black);
      double marketValue = pricer.MarketValue(indexSpreadVolatility);
      double intrinsic = pricer.Intrinsic();
      double diff = Math.Abs(intrinsic - marketValue);

      Assert.AreEqual(0, diff,
        1e-6, "FairPrice at settle should be less than FairPrice 1 day later");

      timer.Stop();

      return;
    }

    [Test, Smoke]
    public void TestModifiedBlackPayerExpiryStrike85()
    {
      Timer timer = new Timer();
      timer.Start();

      optionPricingDate = discountCurve.AsOf;
      optionSettleDate = Dt.Add(optionPricingDate, 1, TimeUnit.Days);
      Dt expiry = optionSettleDate;

      CDXOption option = CreateCDXOption(expiry, PayerReceiver.Payer, 85);
      CDXOptionPricer pricer = CreateCDXOptionPricer(option, CDXOptionModelType.ModifiedBlack);
      double marketValue = pricer.MarketValue(indexSpreadVolatility);
      double intrinsic = pricer.Intrinsic();
      double diff = Math.Abs(intrinsic - marketValue);

      Assert.AreEqual(0, diff,
        1e-6, "FairPrice at settle should be less than FairPrice 1 day later");

      timer.Stop();

      return;
    }

    [Test, Smoke]
    public void TestBlackPayerExpiryStrike90()
    {
      Timer timer = new Timer();
      timer.Start();

      optionPricingDate = discountCurve.AsOf;
      optionSettleDate = Dt.Add(optionPricingDate, 1, TimeUnit.Days);
      Dt expiry = optionSettleDate;

      CDXOption option = CreateCDXOption(expiry, PayerReceiver.Payer, 90);
      CDXOptionPricer pricer = CreateCDXOptionPricer(option, CDXOptionModelType.Black);
      double marketValue = pricer.MarketValue(indexSpreadVolatility);
      double intrinsic = pricer.Intrinsic();
      double diff = Math.Abs(intrinsic - marketValue);

      Assert.AreEqual(0, diff,
        1e-6, "FairPrice at settle should be less than FairPrice 1 day later");

      timer.Stop();

      return;
    }

    [Test, Smoke]
    public void TestModifiedBlackPayerExpiryStrike90()
    {
      Timer timer = new Timer();
      timer.Start();

      optionPricingDate = discountCurve.AsOf;
      optionSettleDate = Dt.Add(optionPricingDate, 1, TimeUnit.Days);
      Dt expiry = optionSettleDate;

      CDXOption option = CreateCDXOption(expiry, PayerReceiver.Payer, 90);
      CDXOptionPricer pricer = CreateCDXOptionPricer(option, CDXOptionModelType.ModifiedBlack);
      double marketValue = pricer.MarketValue(indexSpreadVolatility);
      double intrinsic = pricer.Intrinsic();
      double diff = Math.Abs(intrinsic - marketValue);

      Assert.AreEqual(0, diff,
        1e-6, "FairPrice at settle should be less than FairPrice 1 day later");

      timer.Stop();

      return;
    }

    [Test, Smoke]
    public void TestBlackPayerExpiryStrike95()
    {
      Timer timer = new Timer();
      timer.Start();

      optionPricingDate = discountCurve.AsOf;
      optionSettleDate = Dt.Add(optionPricingDate, 1, TimeUnit.Days);
      Dt expiry = optionSettleDate;

      CDXOption option = CreateCDXOption(expiry, PayerReceiver.Payer, 95);
      CDXOptionPricer pricer = CreateCDXOptionPricer(option, CDXOptionModelType.Black);
      double marketValue = pricer.MarketValue(indexSpreadVolatility);
      double intrinsic = pricer.Intrinsic();
      double diff = Math.Abs(intrinsic - marketValue);

      Assert.AreEqual(0, diff,
        1e-6, "FairPrice at settle should be less than FairPrice 1 day later");

      timer.Stop();

      return;
    }

    [Test, Smoke]
    public void TestModifiedBlackPayerExpiryStrike95()
    {
      Timer timer = new Timer();
      timer.Start();

      optionPricingDate = discountCurve.AsOf;
      optionSettleDate = Dt.Add(optionPricingDate, 1, TimeUnit.Days);
      Dt expiry = optionSettleDate;

      CDXOption option = CreateCDXOption(expiry, PayerReceiver.Payer, 95);
      CDXOptionPricer pricer = CreateCDXOptionPricer(option, CDXOptionModelType.ModifiedBlack);
      double marketValue = pricer.MarketValue(indexSpreadVolatility);
      double intrinsic = pricer.Intrinsic();
      double diff = Math.Abs(intrinsic - marketValue);

      Assert.AreEqual(0, diff,
        1e-6, "FairPrice at settle should be less than FairPrice 1 day later");

      timer.Stop();

      return;
    }

    [Test, Smoke]
    public void TestBlackPayerExpiryStrike110()
    {
      Timer timer = new Timer();
      timer.Start();

      optionPricingDate = discountCurve.AsOf;
      optionSettleDate = Dt.Add(optionPricingDate, 1, TimeUnit.Days);
      Dt expiry = optionSettleDate;

      CDXOption option = CreateCDXOption(expiry, PayerReceiver.Payer, 110);
      CDXOptionPricer pricer = CreateCDXOptionPricer(option, CDXOptionModelType.Black);
      double marketValue = pricer.MarketValue(indexSpreadVolatility);
      double intrinsic = pricer.Intrinsic();
      double diff = Math.Abs(intrinsic - marketValue);

      Assert.AreEqual(0, diff,
        1e-6, "FairPrice at settle should be less than FairPrice 1 day later");

      timer.Stop();

      return;
    }

    [Test, Smoke]
    public void TestModifiedBlackPayerExpiryStrike110()
    {
      Timer timer = new Timer();
      timer.Start();

      optionPricingDate = discountCurve.AsOf;
      optionSettleDate = Dt.Add(optionPricingDate, 1, TimeUnit.Days);
      Dt expiry = optionSettleDate;

      CDXOption option = CreateCDXOption(expiry, PayerReceiver.Payer, 110);
      CDXOptionPricer pricer = CreateCDXOptionPricer(option, CDXOptionModelType.ModifiedBlack);
      double marketValue = pricer.MarketValue(indexSpreadVolatility);
      double intrinsic = pricer.Intrinsic();
      double diff = Math.Abs(intrinsic - marketValue);

      Assert.AreEqual(0, diff,
        1e-6, "FairPrice at settle should be less than FairPrice 1 day later");

      timer.Stop();

      return;
    }

    #endregion test

    #region data
    const double epsilon = 1.0E-7;
    private string discountDataFile_ = "DiscData_TesingCDXOptionAtExpiry.xml";
    private DiscountCurve discountCurve = null;
    private Dt indexEffectiveDate = new Dt(25, 3, 2008);
    private Dt indexSchedTerminationDate = new Dt(20, 6, 2013);
    private DayCount indexDayCount;
    private Frequency indexFrequency;
    private BDConvention indexRoll;
    private Calendar indexCalendar;
    private double indexDealPremium;
    private double indexMarketSpread;
    private double indexSpreadVolatility;
    private double notional = 100000000;
    private double marketRecoveryRate = 0.4;
    //private PayerReceiver payOrReceive = PayerReceiver.Payer;

    private Dt optionPricingDate = new Dt(19, 6, 2008);
    private Dt optionSettleDate = new Dt();
    private double[] optionStrike = new double[] { 80, 85, 90, 95, 110 };

    #endregion data
  }
}