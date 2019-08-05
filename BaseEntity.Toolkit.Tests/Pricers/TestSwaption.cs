//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BGMCorrelation = BaseEntity.Toolkit.Models.BGM;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Curves;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestSwaption : ToolkitTestBase
  {
    [SetUp]
    public void Setup()
    {
      AsOf = new Dt(29, Month.May, 2009);
      Settle = new Dt(30, Month.May, 2009);

      SwapEffective = new Dt(22, Month.June, 2009);

      Maturity = new Dt(9, Month.May, 2013);
      Effective = new Dt(29, Month.May, 2009);
      CCY = Currency.USD;
      LastReset = 0;
      FixedCoupon = 0.00000000000000001; //very near zero so strike is virtually 0;
      FixedDayCount = DayCount.Thirty360;
      FixedFrequency = Frequency.SemiAnnual;
      FixedRoll = BDConvention.Modified;
      Calendar = Calendar.NYB;

      FloatingDayCount = DayCount.Actual360;
      FloatingFrequency = Frequency.Quarterly;
      FloatingRoll = BDConvention.Following;

      Expiration = new Dt(20, Month.June, 2009);
      PayOrRec = PayerReceiver.Payer;
      OptionStyle = OptionStyle.European;
      Strike = FixedCoupon; 

      DiscountCurve = new DiscountCurve(AsOf, .05);
      ReferenceCurve = DiscountCurve;
      VolCurve = new VolatilityCurve(AsOf,0.01);
      VolCube = new RateVolatilityCube(AsOf, 0.01, VolatilityType.LogNormal);

      rateData_ = RateCurveData.LoadFromCsvFile(rateDataFile_);
      Expiries = new [] {"6M", "1Y", "5Y"};
      FwdTenors = new[] {"1Y", "5Y", "10Y", "15Y"};
      AtmLogNormalVols = new double[,] {{0.1, 0.2, 0.3, 0.4}, {0.15, 0.3, 0.45, 0.6}, {0.2, 0.4, 0.5, 0.6}};
      AtmNormalVols = new double[,] { { 0.0010, 0.0020, 0.0030, 0.0040 }, { 0.0015, 0.0030, 0.0045, 0.0060 }, { 0.0020, 0.0040, 0.0050, 0.0060 } };
      SkewStrikes = new double[] {-0.01, -0.005, 0.0, 0.005, 0.01};
      ReferenceIndex = "USDLIBOR_3M";
      LogNormalVolCubeStrikeSkews = new double[Expiries.Length*FwdTenors.Length,SkewStrikes.Length];
      for (int idx = 0; idx < Expiries.Length * FwdTenors.Length; idx++)
        for (int jdx = 0; jdx < SkewStrikes.Length; jdx++)
        {
          LogNormalVolCubeStrikeSkews[idx, jdx] = SkewStrikes[jdx] * (idx * SkewStrikes.Length + jdx)/100.0;
        }

      NormalVolCubeStrikeSkews = new double[Expiries.Length * FwdTenors.Length, SkewStrikes.Length];
      for (int idx = 0; idx < Expiries.Length * FwdTenors.Length; idx++)
        for (int jdx = 0; jdx < SkewStrikes.Length; jdx++)
        {
          NormalVolCubeStrikeSkews[idx, jdx] = SkewStrikes[jdx] * (idx * SkewStrikes.Length + jdx)/100.0;
        }

      CapExpiries = new string[] {"1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "15Y","20Y"};
      CapStrikes = new double[] {0.01, 0.015, 0.02, 0.03, 0.035, 0.04, 0.05, 0.06, 0.07, 0.08};
      CapAlphaNormalArray = new double[] {0.0795620933835237	,0.0913447519477809	,0.1000000000000000	,0.1000000000000000	,0.0445037308359751	,0.0343912771261003	,0.0418546627218118	,0.0441649040086046	,0.0372543482765010	,0.0353568736948916	,0.0346094598780450	,0.0343230543434805	,0.0335201061782101	,0.0330187044508781	,0.0322688688426997	,0.0322008505089826	,0.0316934206475766	,0.0308973177380138	,0.0301278028936331	,0.0300789115530723	,0.0292314294993891	,0.0278040417029035	,0.0269197444122156	,0.0262506692635023	,0.0255321964351286	,0.0249595559103519	,0.0245578552393458	,0.0242440278392517	,0.0239343371776526	,0.0235777680385243	,0.0234859951820871	,0.0234520837992214	,0.0234519629818190	,0.0234747805299125	,0.0235076083328784	,0.0235407656338528	,0.0235593251374747	,0.0235701089420518	,0.0235810890829180	,0.0235892445455066	,0.0235892445455066	,0.0235892445455066};
      CapBetaNormalArray = new double[] {0.5239726027397260	,0.5983561643835620	,0.5384404924760600	,0.4765389876880980	,0.4136114911080710	,0.3513679890560880	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000};
      CapRhoNormalArray = new double[] {0.5000000000000000	,0.5000000000000000	,0.5000000000000000	,0.1863535181272610	,0.5000000000000000	,0.5000000000000000	,0.0038563849102183	,0.1125787268213240	,0.2588530731223850	,0.3207347025525440	,0.1921794493353780	,0.1043928867504420	,0.0337907215957531	,0.0399455598378035	,0.0357388215661874	,0.1015989081225690	,0.1285226686710940	,0.1351081799955020	,0.1356904917733540	,0.1765000860967410	,0.1634951852709180	,0.1008605132323080	,0.0776189874990097	,0.0720059128361360	,0.0534215988626353	,0.0362052889665116	,0.0204158984387293	,0.0064495565326424	,-0.0062913787419977	,-0.0599306081436760	,-0.0683135432904994	,-0.0740756668777085	,-0.0807133689278271	,-0.0874068602448077	,-0.0946167755883820,	-0.1014620281618550,	-0.1083405504471990	,-0.1147553187385420	,-0.1214054884990700	,-0.1245585522108150	,-0.1245585522108150	,-0.1245585522108150};
      CapNuNormalArray = new double[] {0.3537817101945780	,0.2752640583580510	,0.5374758099838550	,0.6681047338855910	,0.2198168030901020	,0.2314845721721410	,0.4102887022211760	,0.4554810932026630	,0.3348403315412200	,0.2958617134035760	,0.3037400396014690	,0.3584123685799540	,0.3730041058245220	,0.3731468767102070	,0.3579195193001210	,0.3378808783379750	,0.3051537870060580	,0.2777680289595350	,0.2637196849849920	,0.2654686252265200	,0.2680009859506080	,0.2648397690332050	,0.2687876712748130	,0.2752551683319780	,0.2789707206138660	,0.2810813858644800	,0.2809017128793030	,0.2808689408688490	,0.2823474958971460	,0.2773128706415400	,0.2769981864827590	,0.2769342290851600	,0.2767317682916770	,0.2764832678549200	,0.2761575472441430	,0.2758883053425740	,0.2756097899854450	,0.2753949236715960	,0.2751914590321970	,0.2750920236169760	,0.2750920236169760	,0.2750920236169760};
      CapExpiryDates = new string[] {"29-Nov-09","29-May-10","29-Nov-10","29-May-11","29-Nov-11","29-May-12","29-Nov-12","29-May-13","29-Nov-13","29-May-14","29-Nov-14","29-May-15","29-Nov-15","29-May-16","29-Nov-16","29-May-17","29-Nov-17","29-May-18","29-Nov-18","29-May-19","29-Nov-19","29-May-20","29-Nov-20","29-May-21","29-Nov-21","29-May-22","29-Nov-22","29-May-23","29-Nov-23","29-May-24","29-Nov-24","29-May-25","29-Nov-25","29-May-26","29-Nov-26","29-May-27","29-Nov-27","29-May-28","29-Nov-28","29-May-29","29-Nov-29","29-May-30"};

      CapAlphaLogNormalArray = new double[] {0.0853919063318389	,0.1000000000000000	,0.0703431503666850	,0.0490682986682061	,0.0544748377125981	,0.0479667085128415	,0.0323198806540323	,0.0321262068559125	,0.0359998853742453	,0.0383319416579275	,0.0319061545131015	,0.0253160471766676	,0.0267242221606095	,0.0329008293777557	,0.0370076258575220	,0.0352306499116666	,0.0284840958657431	,0.0274465834521075	,0.0296092991563619	,0.0300417789620271	,0.0286071144595854	,0.0266230148816766	,0.0259796637466939	,0.0266251552764243	,0.0275123752659269	,0.0286911152847125	,0.0297928806526575	,0.0303627829386440	,0.0300259576076927	,0.0287872555183423	,0.0277254240956184	,0.0263913137244068	,0.0248822377026359	,0.0234191406167590	,0.0221254466074971	,0.0212220146256078	,0.0208499945818172	,0.0210771617229855	,0.0216598435696343	,0.0222794021577065	,0.0231540685170035	,0.0243676645910501};
      CapBetaLogNormalArray = new double[] {0.5239726027397260	,0.5983561643835620	,0.5384404924760600	,0.4765389876880980	,0.4136114911080710	,0.3513679890560880	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000	,0.3500000000000000};
      CapRhoLogNormalArray = new double[] {0.3964638097462560	,-0.0365562165518527	,-0.7641761831805360	,-0.2565346827498370	,0.4727012078640180	,0.5000000000000000	,0.1529223055699650	,0.2433359094033930	,0.0981464220502172	,0.1521633834554570	,0.1872642821322530	,0.1734957461071850	,0.0766189702585784	,0.0703425409322333	,0.1507587643454690	,0.1924759127219120	,0.1017252810898110	,0.0115753785720942	,-0.0684757264022035	,-0.0238116477098801	,0.0111563515606138	,0.0037564517283716	,0.0199635231914476	,0.0560444284477660	,0.0673502754347225	,0.0738877673136775	,0.0781379804401475	,0.0807931248638071	,0.0743954566109374	,0.0280722325874348	,0.0045440158927417	,-0.0226804228565418	,-0.0528405216435137	,-0.0810997586656145	,-0.1053538918581530	,-0.1217714632898090	,-0.1283463999920780	,-0.1265009001778210	,-0.1221371799670020	,-0.1345655203762640	,-0.1259053815654890	,-0.1113094805309590};
      CapNuLogNormalArray = new double[] { 0.7000000000000000	,0.7000000000000000	,0.7000000000000000	,0.7000000000000000	,0.1660492127803730	,0.1786538845396030	,0.4528413898707300	,0.4369353540305780	,0.3425946311670040	,0.3438960627076950	,0.3567364100268740	,0.3471872247969230	,0.3181237606199500	,0.3108452814090240	,0.3302008504204100	,0.3298951930345300	,0.3024395314067010	,0.2860490863628190	,0.2831642833668590	,0.2968135650996640	,0.3070013518601430	,0.3056295263277490	,0.3063214793381790	,0.3022382679573110	,0.2886901798263700	,0.2690803353741990	,0.2490984077073400	,0.2373076901585180	,0.2293286851542860	,0.2106964234329610	,0.2038108438344790	,0.2010549403012680	,0.2009149187016740	,0.2024054457047730	,0.2045282808221720	,0.2062894645852960	,0.2067594546072610	,0.2093704103457100	,0.2154795411542160	,0.2184827708983830	,0.2218440259040520	,0.2238779690780420};
    }

    [Test, Smoke]
    public void TestPVEqualsForwardSwap()
    {
      var refIndex = StandardReferenceIndices.Create(ReferenceIndex);
      FloatingLeg = new SwapLeg(SwapEffective, Maturity, FloatingFrequency, LastReset, refIndex);
      FloatingLeg.Validate();

      FixedLeg = new SwapLeg(SwapEffective, Maturity, refIndex.Currency, FixedCoupon,
        FixedDayCount, FixedFrequency, FixedRoll, Calendar, false);
      FixedLeg.Validate();

      var swaption = new Swaption(Effective, Expiration, refIndex.Currency, FixedLeg,
        FloatingLeg, 2, PayOrRec, OptionStyle, Strike);
      var pricer = new SwaptionBlackPricer(swaption, AsOf,
        Settle, ReferenceCurve, DiscountCurve, VolCurve);
      pricer.Validate();

      var fixedpricer = new SwapLegPricer(FixedLeg, Settle, Settle, 1.0,
        DiscountCurve, null, null, null, null, null);
      fixedpricer.Validate();
      var floatingpricer = new SwapLegPricer(FloatingLeg, Settle, Settle, 1.0,
        DiscountCurve, FloatingLeg.ReferenceIndex, ReferenceCurve, new RateResets(),  null, null);
      floatingpricer.Validate();

      double swapPV = Math.Abs(fixedpricer.Pv() - floatingpricer.Pv());
      double gamma = pricer.Gamma();

      Assert.AreEqual(1, pricer.DeltaHedge(), "DeltaHedge");
      Assert.AreEqual(0, Double.IsNaN(gamma) ? 0 : gamma, "Gamma");
      Assert.AreEqual(pricer.Pv(), swapPV, 1E-15, "Pv");
      Assert.AreEqual(0, pricer.Vega(), 1E-15, "Vega");
    }

    [Test, Smoke]
    public void TestPVZero()
    {
      PayOrRec = PayerReceiver.Receiver;
      var rateIndex = StandardReferenceIndices.Create(ReferenceIndex);
      FixedLeg = new SwapLeg(SwapEffective, Maturity, rateIndex.Currency, FixedCoupon,
        FixedDayCount, FixedFrequency, FixedRoll, Calendar, false);
      FloatingLeg = new SwapLeg(SwapEffective, Maturity, FloatingFrequency, LastReset,rateIndex);
      var swaption = new Swaption(Effective, Expiration, rateIndex.Currency, FixedLeg,
        FloatingLeg, 2, PayOrRec, OptionStyle, Strike);
      var pricer = new SwaptionBlackPricer(swaption, AsOf, Settle,
        ReferenceCurve, DiscountCurve, VolCurve);
      double gamma = pricer.Gamma();

      Assert.AreEqual(0, pricer.DeltaHedge());
      Assert.AreEqual(0, Double.IsNaN(gamma)?0:gamma);
      Assert.AreEqual(0, pricer.Pv());
      Assert.AreEqual(0, pricer.Vega());
    }

    private Swaption CreateStandardSwaption(Tenor tenorExpiry, Tenor tenorForward, Tenor tenorAmortize, bool accreting = false)
    {
      var refIndex = (InterestRateIndex)StandardReferenceIndices.Create(ReferenceIndex);
      if (rateCurveCache_.ContainsKey(refIndex.Currency))
        IRDiscountCurve = rateCurveCache_[refIndex.Currency];
      else
      {
        var interpScheme = InterpScheme.FromString(
        rateCurveInterp_, ExtrapMethod.Smooth, ExtrapMethod.Smooth);
        var discountCurveName = refIndex.IndexName + "_3M";
        IRDiscountCurve = rateData_.CalibrateDiscountCurve(discountCurveName, AsOf, discountCurveName, 
          discountCurveName, rateCurveFitMethod_, interpScheme, 
          new InstrumentType[] {InstrumentType.Swap, InstrumentType.MM});
        rateCurveCache_.Add(refIndex.Currency, IRDiscountCurve);
      }

      if (IRProjectionCurve == null)
        IRProjectionCurve = IRDiscountCurve;

      var standardExpiry = RateVolatilityUtil.SwaptionStandardExpiry(AsOf, refIndex,
                                                                     tenorExpiry);
      var swapEffective = RateVolatilityUtil.SwaptionStandardForwardSwapEffective(standardExpiry, refIndex.SettlementDays, refIndex.Calendar);
      var swapMaturity = Dt.Roll(Dt.Add(swapEffective, tenorForward), refIndex.Roll, refIndex.Calendar);
      FloatingLeg = new SwapLeg(swapEffective, swapMaturity, FloatingFrequency, LastReset, refIndex);
      FloatingLeg.Validate();

      var atmStrike = CurveUtil.DiscountForwardSwapRate(IRDiscountCurve, swapEffective, swapMaturity, FixedDayCount, FixedFrequency,
                                                        FixedRoll, Calendar);
      FixedLeg = new SwapLeg(swapEffective, swapMaturity, refIndex.Currency, atmStrike,
        FixedDayCount, FixedFrequency, FixedRoll, Calendar, false);
      FixedLeg.Validate();
      if (!tenorAmortize.IsEmpty)
      {
        if (accreting)
        {
          AmortizationUtil.ToSchedule(
            new[]
            {
              swapEffective,
              Dt.Roll(Dt.Add(swapEffective, tenorAmortize), refIndex.Roll, refIndex.Calendar)
            },
            new[] { 0.0000001, 15.5 },
            AmortizationType.RemainingNotionalLevels,
            FloatingLeg.AmortizationSchedule);

          AmortizationUtil.ToSchedule(
            new[]
            {
              swapEffective,
              Dt.Roll(Dt.Add(swapEffective, tenorAmortize), refIndex.Roll, refIndex.Calendar)
            },
            new[] { 0.0000001, 15.5 },
            AmortizationType.RemainingNotionalLevels,
            FixedLeg.AmortizationSchedule);
        }
        else
        {
          AmortizationUtil.ToSchedule(
            new[] { Dt.Roll(Dt.Add(swapEffective, tenorAmortize), refIndex.Roll, refIndex.Calendar) },
            new double[] { 0.00000000001 },
            AmortizationType.RemainingNotionalLevels,
            FloatingLeg.AmortizationSchedule);

          AmortizationUtil.ToSchedule(
            new[] { Dt.Roll(Dt.Add(swapEffective, tenorAmortize), refIndex.Roll, refIndex.Calendar) },
            new double[] { 0.00000000001 },
            AmortizationType.RemainingNotionalLevels,
            FixedLeg.AmortizationSchedule);
        }
      }

      var swaption = new Swaption(Effective, swapEffective, refIndex.Currency, FixedLeg,
        FloatingLeg, 2, PayOrRec, OptionStyle, atmStrike);

      return swaption;
    }

    [Test, Smoke]
    public void TestSwaptionExpiry()
    {
      var swaption = CreateStandardSwaption(Tenor.Parse("6M"), Tenor.Parse("5Y"), Tenor.Empty);
      var maturity = swaption.Maturity;

      // Check the consistency between the property and the method
      var expiry = swaption.Expiration;
      Assert.AreEqual(expiry, swaption.GetExpiration(maturity));
      Assert.AreEqual(expiry, Dt.AddDays(maturity,
        -swaption.NotificationDays, swaption.NotificationCalendar));

      // Move expiry back by 30 days,
      // then check the consistency of setter and getter
      expiry -= 30;
      swaption.Expiration = expiry;
      Assert.AreEqual(expiry, swaption.Expiration);
      Assert.AreEqual(expiry, swaption.GetExpiration(maturity));

      // For dates not on maturity...
      var forwardStart = maturity + 10;
      var expectExpiry = swaption.Expiration + 10;
      Assert.AreEqual(expectExpiry, swaption.GetExpiration(forwardStart));
    }

    [Test, Smoke]
    public void TestSwaptionATMMarketLogNormal()
    {
      var swaption = CreateStandardSwaption(Tenor.Parse("6M"), Tenor.Parse("1Y"), Tenor.Empty);
      var settle = RateVolatilityUtil.SwaptionStandardSettle(AsOf, (InterestRateIndex)swaption.UnderlyingFloatLeg.ReferenceIndex);

      var volCube = SwaptionVolatilityCube.CreateSwaptionMarketCube(AsOf, IRDiscountCurve, Expiries, FwdTenors,
                                                                  AtmLogNormalVols,
                                                                  (InterestRateIndex)swaption.UnderlyingFloatLeg.ReferenceIndex, VolatilityType.LogNormal,
                                                                  Expiries, SkewStrikes, FwdTenors, LogNormalVolCubeStrikeSkews, null,
                                                                  null, FixedLeg.DayCount, FixedLeg.BDConvention, FixedLeg.Freq,
                                                                  FixedLeg.Calendar, swaption.UnderlyingFloatLeg.ReferenceIndex.SettlementDays);

      var pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      pricer.Validate();

      AssertEqual("6M * 1Y ImpliedVol", 0.10, pricer.IVol(DistributionType.LogNormal), 1E-5);

      swaption = CreateStandardSwaption(Tenor.Parse("6M"), Tenor.Parse("5Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("6M * 5Y ImpliedVol", 0.20, pricer.IVol(DistributionType.LogNormal), 1E-5);

      swaption = CreateStandardSwaption(Tenor.Parse("6M"), Tenor.Parse("10Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("6M * 10Y ImpliedVol", 0.30, pricer.IVol(DistributionType.LogNormal), 1E-5);

      swaption = CreateStandardSwaption(Tenor.Parse("1Y"), Tenor.Parse("1Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("1Y * 1Y ImpliedVol", 0.15, pricer.IVol(DistributionType.LogNormal), 1E-5);

      swaption = CreateStandardSwaption(Tenor.Parse("1Y"), Tenor.Parse("5Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("1Y * 5Y ImpliedVol", 0.3, pricer.IVol(DistributionType.LogNormal), 1E-5);

      swaption = CreateStandardSwaption(Tenor.Parse("1Y"), Tenor.Parse("10Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("1Y * 10Y ImpliedVol", 0.45, pricer.IVol(DistributionType.LogNormal), 1E-5);

      swaption = CreateStandardSwaption(Tenor.Parse("1Y"), Tenor.Parse("15Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("1Y * 15Y ImpliedVol", 0.6, pricer.IVol(DistributionType.LogNormal), 1E-5);

      swaption = CreateStandardSwaption(Tenor.Parse("5Y"), Tenor.Parse("1Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("5Y * 1Y ImpliedVol", 0.2, pricer.IVol(DistributionType.LogNormal), 1E-5);

      swaption = CreateStandardSwaption(Tenor.Parse("5Y"), Tenor.Parse("5Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("5Y * 5Y ImpliedVol", 0.4, pricer.IVol(DistributionType.LogNormal), 1E-5);

      swaption = CreateStandardSwaption(Tenor.Parse("5Y"), Tenor.Parse("10Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("5Y * 10Y ImpliedVol", 0.5, pricer.IVol(DistributionType.LogNormal), 1.1E-5);

      swaption = CreateStandardSwaption(Tenor.Parse("5Y"), Tenor.Parse("15Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("5Y * 15Y ImpliedVol", 0.6, pricer.IVol(DistributionType.LogNormal), 1E-5);
    }

    [Test, Smoke]
    public void TestSwaptionAtmMarketNormal()
    {
      var swaption = CreateStandardSwaption(Tenor.Parse("6M"), Tenor.Parse("1Y"), Tenor.Empty);
      var settle = RateVolatilityUtil.SwaptionStandardSettle(AsOf, (InterestRateIndex)swaption.UnderlyingFloatLeg.ReferenceIndex);
      
      var volCube = SwaptionVolatilityCube.CreateSwaptionMarketCube(AsOf, IRDiscountCurve, Expiries, FwdTenors,
                                                                  AtmNormalVols,
                                                                  (InterestRateIndex)swaption.UnderlyingFloatLeg.ReferenceIndex, VolatilityType.Normal,
                                                                  Expiries, SkewStrikes, FwdTenors, NormalVolCubeStrikeSkews, null,
                                                                  null, FixedLeg.DayCount, FixedLeg.BDConvention, FixedLeg.Freq,
                                                                  FixedLeg.Calendar, swaption.UnderlyingFloatLeg.ReferenceIndex.SettlementDays);

      var pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      pricer.Validate();

      AssertEqual("6M * 1Y ImpliedVol", 10, 10000*pricer.IVol(DistributionType.Normal), 0.1);

      swaption = CreateStandardSwaption(Tenor.Parse("6M"), Tenor.Parse("5Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("6M * 5Y ImpliedVol", 20, 10000*pricer.IVol(DistributionType.Normal), 0.1);

      swaption = CreateStandardSwaption(Tenor.Parse("6M"), Tenor.Parse("10Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("6M * 10Y ImpliedVol", 30, 10000*pricer.IVol(DistributionType.Normal), 0.1);

      swaption = CreateStandardSwaption(Tenor.Parse("1Y"), Tenor.Parse("1Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("1Y * 1Y ImpliedVol", 15, 10000*pricer.IVol(DistributionType.Normal), 0.1);

      swaption = CreateStandardSwaption(Tenor.Parse("1Y"), Tenor.Parse("5Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("1Y * 5Y ImpliedVol", 30, 10000*pricer.IVol(DistributionType.Normal), 0.1);

      swaption = CreateStandardSwaption(Tenor.Parse("1Y"), Tenor.Parse("10Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("1Y * 10Y ImpliedVol", 45, 10000*pricer.IVol(DistributionType.Normal), 0.1);

      swaption = CreateStandardSwaption(Tenor.Parse("1Y"), Tenor.Parse("15Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("1Y * 15Y ImpliedVol", 60, 10000*pricer.IVol(DistributionType.Normal), 0.1);

      swaption = CreateStandardSwaption(Tenor.Parse("5Y"), Tenor.Parse("1Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("5Y * 1Y ImpliedVol", 20, 10000*pricer.IVol(DistributionType.Normal), 0.1);

      swaption = CreateStandardSwaption(Tenor.Parse("5Y"), Tenor.Parse("5Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("5Y * 5Y ImpliedVol", 40, 10000*pricer.IVol(DistributionType.Normal), 0.1);

      swaption = CreateStandardSwaption(Tenor.Parse("5Y"), Tenor.Parse("10Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("5Y * 10Y ImpliedVol", 50, 10000*pricer.IVol(DistributionType.Normal), 0.11);

      swaption = CreateStandardSwaption(Tenor.Parse("5Y"), Tenor.Parse("15Y"), Tenor.Empty);
      pricer = new SwaptionBlackPricer(swaption, AsOf, settle, IRProjectionCurve, IRDiscountCurve, volCube);
      AssertEqual("5Y * 15Y ImpliedVol", 60, 10000*pricer.IVol(DistributionType.Normal), 0.1);
    }

    public enum NotionalChange { Amortizaing, Accreting }

    [Smoke]
    [TestCase(NotionalChange.Amortizaing)]
    [TestCase(NotionalChange.Accreting)]
    public void TestSwaptionMarketNormal(NotionalChange nc)
    {
      bool isAccreting = (nc == NotionalChange.Accreting);
      var swaption = CreateStandardSwaption(Tenor.Parse("1Y"),
        Tenor.Parse("10Y"), Tenor.Parse("5y"), isAccreting);

      var amortSwaptionMaturity = Dt.Add(swaption.UnderlyingFixedLeg.Effective, Tenor.Parse("5y"));
      var atmStrike = CurveUtil.DiscountForwardSwapRate(IRDiscountCurve, swaption.UnderlyingFixedLeg.Effective, amortSwaptionMaturity,
        FixedDayCount, FixedFrequency, FixedRoll, Calendar);
      swaption.Strike = atmStrike;
      swaption.UnderlyingFixedLeg.Coupon = atmStrike;
      var volCube = SwaptionVolatilityCube.CreateSwaptionMarketCube(AsOf, IRDiscountCurve, Expiries, FwdTenors,
                                                                  AtmNormalVols,
                                                                  (InterestRateIndex)swaption.UnderlyingFloatLeg.ReferenceIndex, VolatilityType.Normal,
                                                                  Expiries, SkewStrikes, FwdTenors, NormalVolCubeStrikeSkews, null,
                                                                  null, FixedLeg.DayCount, FixedLeg.BDConvention, FixedLeg.Freq,
                                                                  FixedLeg.Calendar, swaption.UnderlyingFloatLeg.ReferenceIndex.SettlementDays);
      var pricer = new SwaptionBlackPricer(swaption, AsOf,
        Settle, IRProjectionCurve, IRDiscountCurve, volCube);
      pricer.Validate();

      var dd = RateVolatilityUtil.EffectiveSwaptionDuration(pricer);
      AssertEqual("EffectiveDuration", 60.0, dd.Value, 0.05);
      var expectFwdStart = isAccreting ? new Dt(20150602) : swaption.Maturity;
      AssertEqual("EffectiveStart", expectFwdStart, dd.Date);
      var expectVol = isAccreting ? 1E-6 : 30;
      AssertEqual("ImpliedVol", expectVol, 10000 * pricer.IVol(DistributionType.Normal), 0.01);
    }
 
    [Smoke]
    [TestCase(NotionalChange.Amortizaing)]
    [TestCase(NotionalChange.Accreting)]
    public void TestSwaptionMarketLogNormal(NotionalChange nc)
    {
      bool isAccreting = (nc == NotionalChange.Accreting);
      var swaption = CreateStandardSwaption(Tenor.Parse("1Y"),
        Tenor.Parse("10Y"), Tenor.Parse("5y"), isAccreting);

      var amortSwaptionMaturity = Dt.Add(swaption.UnderlyingFixedLeg.Effective, Tenor.Parse("5y"));
      var atmStrike = CurveUtil.DiscountForwardSwapRate(IRDiscountCurve, swaption.UnderlyingFixedLeg.Effective, amortSwaptionMaturity,
        FixedDayCount, FixedFrequency, FixedRoll, Calendar);
      swaption.Strike = atmStrike;
      swaption.UnderlyingFixedLeg.Coupon = atmStrike;

      var volCube = SwaptionVolatilityCube.CreateSwaptionMarketCube(AsOf, IRDiscountCurve, Expiries, FwdTenors,
                                                                  AtmLogNormalVols,
                                                                  (InterestRateIndex)swaption.UnderlyingFloatLeg.ReferenceIndex, VolatilityType.LogNormal,
                                                                  Expiries, SkewStrikes, FwdTenors, LogNormalVolCubeStrikeSkews, null,
                                                                  null, FixedLeg.DayCount, FixedLeg.BDConvention, FixedLeg.Freq,
                                                                  FixedLeg.Calendar, swaption.UnderlyingFloatLeg.ReferenceIndex.SettlementDays);
      var pricer = new SwaptionBlackPricer(swaption, AsOf,
        Settle, IRProjectionCurve, IRDiscountCurve, volCube);
      pricer.Validate();

      var dd = RateVolatilityUtil.EffectiveSwaptionDuration(pricer);
      AssertEqual("EffectiveDuration", 60.0, dd.Value, 0.05);
      var expectFwdStart = isAccreting ? new Dt(20150602) : swaption.Maturity;
      AssertEqual("EffectiveStart", expectFwdStart, dd.Date);
      var expectVol = isAccreting ? 0.4133113 : 0.3;
      AssertEqual("ImpliedVol", expectVol, pricer.IVol(DistributionType.LogNormal), 0.0001);
    }

    [Test, Smoke]
    public void TestSwaptionAmortizationBGMNormal()
    {
      var swaption = CreateStandardSwaption(Tenor.Parse("1Y"), Tenor.Parse("10Y"), Tenor.Parse("5y"));

      var amortSwaptionMaturity = Dt.Add(swaption.UnderlyingFixedLeg.Effective, Tenor.Parse("5y"));
      var atmStrike = CurveUtil.DiscountForwardSwapRate(IRDiscountCurve, swaption.UnderlyingFixedLeg.Effective, amortSwaptionMaturity,
        FixedDayCount, FixedFrequency, FixedRoll, Calendar);
      swaption.Strike = atmStrike;
      swaption.UnderlyingFixedLeg.Coupon = atmStrike;

      var volCube = BgmForwardVolatilitySurface.Create(AsOf, new BgmCalibrationParameters(), IRDiscountCurve, Expiries, FwdTenors, CycleRule.None,
                                                       FixedLeg.BDConvention, Calendar,
                                                       BGMCorrelation.BgmCorrelation.CreateBgmCorrelation(BGMCorrelation.BgmCorrelationType.PerfectCorrelation, Expiries.Length, new double[0, 0]),
                                                       AtmNormalVols, DistributionType.Normal);
      var pricer = new SwaptionBlackPricer(swaption, AsOf,
        Settle, IRProjectionCurve, IRDiscountCurve, volCube);
      pricer.Validate();

      AssertEqual("EffectiveDuration", 60.0, RateVolatilityUtil.EffectiveSwaptionDuration(pricer).Value, 0.05);
      AssertEqual("ImpliedVol", 30, 10000 * pricer.IVol(DistributionType.Normal), 0.1);
    }

    [Test, Smoke]
    public void TestSwaptionAmortizationBGMLogNormal()
    {
      var swaption = CreateStandardSwaption(Tenor.Parse("1Y"), Tenor.Parse("10Y"), Tenor.Parse("5y"));

      var amortSwaptionMaturity = Dt.Add(swaption.UnderlyingFixedLeg.Effective, Tenor.Parse("5y"));
      var atmStrike = CurveUtil.DiscountForwardSwapRate(IRDiscountCurve, swaption.UnderlyingFixedLeg.Effective, amortSwaptionMaturity,
        FixedDayCount, FixedFrequency, FixedRoll, Calendar);
      swaption.Strike = atmStrike;
      swaption.UnderlyingFixedLeg.Coupon = atmStrike;

      var volCube = BgmForwardVolatilitySurface.Create(AsOf, new BgmCalibrationParameters(), IRDiscountCurve, Expiries, FwdTenors, CycleRule.None,
                                                       FixedLeg.BDConvention, Calendar, 
                                                       BGMCorrelation.BgmCorrelation.CreateBgmCorrelation(BGMCorrelation.BgmCorrelationType.PerfectCorrelation, Expiries.Length, new double[0, 0]), 
                                                       AtmLogNormalVols, DistributionType.LogNormal);
      var pricer = new SwaptionBlackPricer(swaption, AsOf,
        Settle, IRProjectionCurve, IRDiscountCurve, volCube);
      pricer.Validate();

      AssertEqual("EffectiveDuration", 60.0, RateVolatilityUtil.EffectiveSwaptionDuration(pricer).Value, 0.05);
      AssertEqual("ImpliedVol", 0.3, pricer.IVol(DistributionType.LogNormal), 0.001);
    }

    [Test, Smoke]
    public void TestSwaptionAmortizationCapBasisNormal()
    {
      var swaption = CreateStandardSwaption(Tenor.Parse("1Y"), Tenor.Parse("10Y"), Tenor.Parse("5y"));

      var amortSwaptionMaturity = Dt.Add(swaption.UnderlyingFixedLeg.Effective, Tenor.Parse("5y"));
      var atmStrike = CurveUtil.DiscountForwardSwapRate(IRDiscountCurve, swaption.UnderlyingFixedLeg.Effective, amortSwaptionMaturity,
        FixedDayCount, FixedFrequency, FixedRoll, Calendar);
      swaption.Strike = atmStrike;
      swaption.UnderlyingFixedLeg.Coupon = atmStrike;

      var rateIndex = (InterestRateIndex) swaption.UnderlyingFloatLeg.ReferenceIndex;
      Dt settle = Cap.StandardSettle(AsOf, rateIndex);
      Dt[] capExpiryDates = ArrayUtil.Convert(CapExpiries, (expiry => Cap.StandardLastPayment(settle, expiry, rateIndex))).ToArray();

      var capParamDates = CollectionUtil.ConvertAll(CapExpiryDates, t => Dt.FromStr(t));
      var calibrator = new RateVolatilityParametricSabrCalibrator(
        AsOf, IRDiscountCurve, IRDiscountCurve,  rateIndex, VolatilityType.Normal,
        capExpiryDates, capExpiryDates, CapStrikes,
        capParamDates,
        CapAlphaNormalArray,
        capParamDates,
        CapBetaNormalArray,
        capParamDates,
        CapRhoNormalArray,
        capParamDates,
        CapNuNormalArray);

      var cube = new RateVolatilityCube(calibrator)
      {
        ExpiryTenors = Array.ConvertAll(CapExpiries, expiryTenor => Tenor.Parse(expiryTenor))
      };

      cube.Fit();

      var volCube = SwaptionVolatilityCube.CreateCapFloorBasisAdjustedCube(cube, AsOf, IRDiscountCurve, Expiries, SkewStrikes, FwdTenors,
                                                                           NormalVolCubeStrikeSkews, null, null,
                                                                           FixedDayCount, FixedRoll, FixedFrequency,
                                                                           Calendar,
                                                                           swaption.UnderlyingFloatLeg.ReferenceIndex.
                                                                             SettlementDays);
      var pricer = new SwaptionBlackPricer(swaption, AsOf,
        Settle, IRProjectionCurve, IRDiscountCurve, volCube);
      pricer.Validate();

      AssertEqual("EffectiveDuration", 60.0, RateVolatilityUtil.EffectiveSwaptionDuration(pricer).Value, 0.05);
      AssertEqual("ImpliedVol", 114.1, 10000 * pricer.IVol(DistributionType.Normal), 0.1);
    }

    [Test, Smoke]
    public void TestSwaptionAmortizationCapBasisLogNormal()
    {
      var swaption = CreateStandardSwaption(Tenor.Parse("1Y"), Tenor.Parse("10Y"), Tenor.Parse("5y"));

      var amortSwaptionMaturity = Dt.Add(swaption.UnderlyingFixedLeg.Effective, Tenor.Parse("5y"));
      var atmStrike = CurveUtil.DiscountForwardSwapRate(IRDiscountCurve, swaption.UnderlyingFixedLeg.Effective, amortSwaptionMaturity,
        FixedDayCount, FixedFrequency, FixedRoll, Calendar);
      swaption.Strike = atmStrike;
      swaption.UnderlyingFixedLeg.Coupon = atmStrike;

      var rateIndex = (InterestRateIndex)swaption.UnderlyingFloatLeg.ReferenceIndex;
      Dt settle = Cap.StandardSettle(AsOf, rateIndex);
      Dt[] capExpiryDates = ArrayUtil.Convert(CapExpiries, (expiry => Cap.StandardLastPayment(settle, expiry, rateIndex))).ToArray();

      var capParamDates = CollectionUtil.ConvertAll(CapExpiryDates, t => Dt.FromStr(t));
      var calibrator = new RateVolatilityParametricSabrCalibrator(
        AsOf, IRDiscountCurve, IRDiscountCurve, rateIndex,VolatilityType.LogNormal,
        capExpiryDates, capExpiryDates, CapStrikes,
        capParamDates,
        CapAlphaLogNormalArray,
        capParamDates,
        CapBetaLogNormalArray,
        capParamDates,
        CapRhoLogNormalArray,
        capParamDates,
        CapNuLogNormalArray);

      var cube = new RateVolatilityCube(calibrator)
      {
        ExpiryTenors = Array.ConvertAll(CapExpiries, expiryTenor => Tenor.Parse(expiryTenor))
      };

      cube.Fit();
      var volCube = SwaptionVolatilityCube.CreateCapFloorBasisAdjustedCube(cube, AsOf, IRDiscountCurve, Expiries, SkewStrikes, FwdTenors,
                                                                     LogNormalVolCubeStrikeSkews, null, null,
                                                                     FixedDayCount, FixedRoll, FixedFrequency,
                                                                     Calendar,
                                                                     swaption.UnderlyingFloatLeg.ReferenceIndex.
                                                                       SettlementDays);

      var pricer = new SwaptionBlackPricer(swaption, AsOf,
        Settle, IRProjectionCurve, IRDiscountCurve, volCube);
      pricer.Validate();

      AssertEqual("EffectiveDuration", 60.0, RateVolatilityUtil.EffectiveSwaptionDuration(pricer).Value, 0.05);
      AssertEqual("ImpliedVol", 0.41, pricer.IVol(DistributionType.LogNormal), 0.01);
    }

    public Dt AsOf { get; set; }
    public Dt Settle { get; set; }
    public DiscountCurve ReferenceCurve { get; set; }
    public DiscountCurve DiscountCurve { get; set; }
    public VolatilityCurve VolCurve { get; set; }
    public RateVolatilityCube VolCube { get; set; }
    public Dt Effective { get; set; }
    public Dt SwapEffective { get; set; }
    public Dt Maturity { get; set; }
    public Dt Expiration { get; set; }
    public Currency CCY { get; set; }
    public SwapLeg FixedLeg { get; set; }
    public SwapLeg FloatingLeg { get; set; }
    public OptionStyle OptionStyle { get; set; }
    public PayerReceiver PayOrRec { get; set; }
    public double Strike { get; set; }

    public Calendar Calendar { get; set; }

    public double FixedCoupon { get; set; }
    public DayCount FixedDayCount { get; set; }
    public Frequency FixedFrequency { get; set; }
    public BDConvention FixedRoll { get; set; }

    public double LastReset { get; set; }
    public DayCount FloatingDayCount { get; set; }
    public Frequency FloatingFrequency { get; set; }
    public BDConvention FloatingRoll { get; set; }
    public string ReferenceIndex { get; set; }

    public string[] Expiries { get; set; }
    public string[] FwdTenors { get; set; }
    public double[,] AtmLogNormalVols { get; set; }
    public double[,] AtmNormalVols { get; set; }
    public double[] SkewStrikes { get; set; }
    public double[,] LogNormalVolCubeStrikeSkews { get; set; }
    public double[,] NormalVolCubeStrikeSkews { get; set; }
    private string rateDataFile_ = "data/comac1_ir_data.csv";
    private RateCurveData rateData_;
    private Dictionary<Currency, DiscountCurve> rateCurveCache_ = new Dictionary<Currency,DiscountCurve>();
    private DiscountCurve IRDiscountCurve { get; set; }
    private DiscountCurve IRProjectionCurve { get; set; }
    private string rateCurveInterp_ = "Weighted";
    private CurveFitMethod rateCurveFitMethod_ = CurveFitMethod.Bootstrap;
    private string[] CapExpiries { get; set; }
    private double[] CapStrikes { get; set; }
    private string[] CapExpiryDates { get; set; }
    private double[] CapAlphaNormalArray { get; set; }
    private double[] CapBetaNormalArray { get; set; }
    private double[] CapRhoNormalArray { get; set; }
    private double[] CapNuNormalArray { get; set; }
    private double[] CapAlphaLogNormalArray { get; set; }
    private double[] CapBetaLogNormalArray { get; set; }
    private double[] CapRhoLogNormalArray { get; set; }
    private double[] CapNuLogNormalArray { get; set; }
  }
}
