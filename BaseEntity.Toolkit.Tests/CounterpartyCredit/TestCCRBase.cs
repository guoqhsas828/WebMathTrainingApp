//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.CounterpartyCredit
{
 
  public abstract class TestCCRBase : ToolkitTestBase
  {
    #region Data
    /// <exclude></exclude>
    protected Dt asOf_ = new Dt(15, 12, 2010);
    /// <exclude></exclude>
    protected int[] cptyIndex_ = {0, 1};   // {0, 1, 2, 3}
    /// <exclude></exclude>
    protected double cptyDefaultTimeCorrelation_ = 0.65;
    /// <exclude></exclude>
    protected double[] cptyRec_ = {0.4, 0.5};
    /// <exclude></exclude>
    protected Currency[] currencies_ = {Currency.USD, Currency.EUR, Currency.JPY};
    /// <exclude></exclude>
    protected DayCount[] dayCounts_ = {DayCount.Actual360, DayCount.Actual360, DayCount.Actual360};
    /// <exclude></exclude>
    protected BDConvention[] roll_ = {BDConvention.Modified, BDConvention.Modified, BDConvention.Modified };
    /// <exclude></exclude>
    protected Frequency[] swapFrequency_ = {Frequency.Quarterly, Frequency.SemiAnnual, Frequency.Quarterly};
    /// <exclude></exclude>
    protected string[] indexTenor_ = {"3M", "6M", "3M"};
    /// <exclude></exclude>
    protected string[] basisSwapIndexTenor_ = { "6M", "1Y", "6M" };
    /// <exclude></exclude>
    protected Calendar[] calendar_ = {Calendar.NYB, Calendar.TGT, Calendar.TKB};
    /// <exclude></exclude>
    protected string[] tenors_ = {"1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y"};
    /// <exclude></exclude>
    protected string[] capTenors_ = new[] { "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "12Y", "15Y", "20Y" };
    /// <exclude></exclude>
    protected double[] usdCapVols_ = {0.9, 0.9, 0.7, 0.6, 0.5, 0.4, 0.3, 0.2, 0.2, 0.2};
    /// <exclude></exclude>
    protected double[] eurCapVols_ = { 1.2, 1.1, 0.8, 0.7, 0.6, 0.4, 0.4, 0.4, 0.3, 0.2 };
    /// <exclude></exclude>
    protected double[] jpyCapVols_ = { 1.1, 0.9, 0.7, 0.6, 0.5, 0.4, 0.3, 0.3, 0.3, 0.3 };
    /// <exclude></exclude>
    protected double[] rates_ = {0.03, 0.04, 0.05, 0.05, 0.05, 0.05, 0.05, 0.05, 0.05, 0.05};
    /// <exclude></exclude>
    protected double[] frates2_ = {0.02, 0.02, 0.02, 0.02, 0.02, 0.02, 0.025, 0.015, 0.035, 0.015};
    /// <exclude></exclude>
    protected double[] frates1_ = {0.04, 0.04, 0.04, 0.04, 0.04, 0.04, 0.045, 0.045, 0.045, 0.045};
    /// <exclude></exclude>
    protected double[] fxrates_ = {1.3972, 0.0123};
    /// <exclude></exclude>
    protected double stockPrice_ = 120;
    /// <exclude></exclude>
    protected double spotInflation_ = 95;
    /// <exclude></exclude>
    protected double[] projectionRates_ = {0.04, 0.04, 0.04, 0.04, 0.04, 0.04, 0.045, 0.045, 0.045, 0.045};
    /// <exclude></exclude>
    protected double[] inflationZeroRates_ = {
                                               0.010, 0.011, 0.0115, 0.0120, 0.0118, 0.0122, 0.0130, 0.0135, 0.0140,
                                               0.0142
                                             };

    /// <exclude></exclude>
    protected double[] cfvolas_ = {0.3, 0.35};

    /// <exclude></exclude>
    protected double[] lambda_ = {0.05, 0.14, 0.08, 0.10, 0.02, 0.06, 0.045, 0.07, 0.2, 0.145};

    /// <exclude></exclude>
    protected double[] volas_ = {0.5, 0.4, 0.3, 0.25, 0.2, 0.2, 0.2, 0.2, 0.2, 0.2};
    
    /// <exclude></exclude>
    protected double[] cvolas_ = {0.3, 0.35, 0.4, 0.2, 0.15, 0.45, 0.34, 0.25, 0.28, 0.333};

    /// <exclude></exclude>
    protected double[] fxvolas_ = {0.18, 0.20};

    /// <exclude></exclude>
    protected double[] inflationVol_ = {0.05, 0.04, 0.03, 0.05, 0.06, 0.08, 0.08, 0.10, 0.10, 0.10};

    protected double stockVol_ = 0.5;

    /// <exclude></exclude>
    protected double upBump_ = 1;

    /// <exclude></exclude>
    protected string[] bumpedTenors_;

    /// <exclude></exclude>
    protected double downBump_ = 1;

    /// <exclude></exclude>
    protected bool bumpRelative_;

    /// <exclude></exclude>
    protected bool calcGamma_ = true;

    /// <exclude></exclude>
    protected BumpType bumpType_ = BumpType.Parallel;

    /// <exclude></exclude>
    protected QuotingConvention quoteTarget_ = QuotingConvention.None;

    /// <exclude></exclude>
    protected Input input_;

    /// <exclude></exclude>
    protected Netting netting_;

    /// <exclude></exclude>
    protected Frequency simulFreq_ = Frequency.SemiAnnual;
   
    #endregion

    #region Input

    /// <exclude></exclude>
    protected class Input : BaseEntityObject
    {
      /// <exclude></exclude>
      public Dt AsOf;

      /// <exclude></exclude>
      public FactorLoadingCollection FactorLoadings;

      /// <exclude></exclude>
      public SurvivalCurve[] Cpty;

      /// <exclude></exclude>
      public double[] CptyRec;

      /// <exclude></exclude>
      public SurvivalCurve[] CreditCurves;

      /// <exclude></exclude>
      public VolatilityCollection Volatilities;

      /// <exclude></exclude>
      public DiscountCurve[] DiscountCurves;

      /// <exclude></exclude>
      public string[] FactorNames;

      /// <exclude></exclude>
      public CalibratedCurve[] FwdCurves;

      /// <exclude></exclude>
      public FxRate[] FxRates;

      /// <exclude></exclude>
      public string[] Names;

      /// <exclude></exclude>
      public IPricer[] Pricers;

      /// <exclude></exclude>
      public int Sample;

      /// <exclude></exclude>
      public Dt[] SimulDates;

      /// <exclude></exclude>
      public Dt[] TenorDates;

      /// <exclude></exclude>
      public Tenor GridSize;
    }

    #endregion

    #region InitializeParameters

    /// <exclude></exclude>
    protected static InflationIndex CreateInflationIndex(Currency ccy, Calendar calendar, DayCount dayCount, BDConvention roll)
    {
      return new InflationIndex(String.Concat("CPI", ccy), ccy, dayCount, calendar, roll, Frequency.Monthly, Tenor.Empty);
    }

    /// <exclude></exclude>
    protected static InflationCurve CreateInflationCurve(Dt asOf, double spotInfl, string[] tenors,  double[] fwd, DiscountCurve disc, DayCount dayCount, Calendar calendar, BDConvention roll)
    {
      var inflIndex = CreateInflationIndex(Currency.USD, calendar, dayCount, roll);
      inflIndex.HistoricalObservations = new RateResets(spotInfl, 0.0);
      var zeroSwaps = tenors.Select(t => Dt.Add(asOf, t)).Select(t =>
      {
        var fixedLeg = new SwapLeg(asOf, t, Currency.USD, 0.0, DayCount.Thirty360,
                                   Frequency.None, BDConvention.Following, Calendar.NYB, false)
        {
          IsZeroCoupon = true
        };
        var floatLeg = new SwapLeg(asOf, t, Frequency.None, 0, inflIndex,
                                   Currency.USD, DayCount.Thirty360, BDConvention.Following,
                                   Calendar.NYB) { ProjectionType = ProjectionType.InflationRate, IsZeroCoupon = true };
        return new Swap(floatLeg, fixedLeg);
      }).ToArray();
      var iCurve = InflationCurveFitCalibrator.FitInflationCurve(asOf, new CalibratorSettings(), zeroSwaps, tenors, fwd, null, disc, inflIndex, Tenor.Empty, null,
                                                                 false, null, false);
      return iCurve;
    }

    /// <exclude></exclude>
    protected static StockCurve CreateStockForwardCurve(double spot, DiscountCurve discount)
    {
      return new StockCurve(discount.AsOf, spot, discount, 0.0, null) {Ccy = discount.Ccy, Name = "IBM_Curve$%"};
    }

    /// <exclude></exclude>
    protected static FxCurve CreateFxCurve(FxRate fxRate, DiscountCurve disc1, DiscountCurve disc2)
    {
      var domestic = (fxRate.ToCcy == disc1.Ccy) ? disc1 : disc2;
      var foreign = (fxRate.FromCcy == disc1.Ccy) ? disc1 : disc2;
      return new FxCurve(fxRate, null, domestic, foreign, null);
    }
    
    /// <exclude></exclude>
    protected static string FxName(FxRate fxRate)
    {
      return fxRate.Name;
    }
    
    /// <exclude></exclude>
    protected static double[,] GenerateFactors(Random rand, int n, int m, double norm)
    {
      var fl = new double[n,m];
      for (int i = 0; i < fl.GetLength(0); ++i)
      {
        double rho = 0.0;
        for (int j = 0; j < fl.GetLength(1); ++j)
        {
          double rhoij = rand.NextDouble();
          fl[i, j] = rhoij;
          rho += rhoij * rhoij;
        }
        rho = Math.Sqrt(rho);
        for (int j = 0; j < fl.GetLength(1); ++j)
          fl[i, j] *= norm / rho;
      }
      return fl;
    }

    /// <exclude></exclude>
    protected static double[,] GenerateBetas(Random rand, int n, double[,] fl, double[,] corrMatrix, double norm)
    {
      var yFl = GenerateFactors(rand, n, fl.GetLength(1), norm);
      var betas = new double[n,fl.GetLength(0)];
      var x = new double[fl.GetLength(0)];
      var u = (double[,])corrMatrix.Clone();
      var w = new double[u.GetLength(1)];
      var v = new double[u.GetLength(1),u.GetLength(1)];
      LinearSolvers.FactorizeSVD(u, w, v);
      for (int k = 0; k < n; ++k)
      {
        var b = new double[fl.GetLength(0)];
        for (int i = 0; i < fl.GetLength(0); ++i)
          for (int j = 0; j < fl.GetLength(1); ++j)
            b[i] += fl[i, j] * yFl[k, j];
        LinearSolvers.SolveSVD(u, w, v, b, x);
        for (int j = 0; j < b.Length; ++j)
          betas[k, j] = x[j];
      }
      return betas;
    }
    
    /// <exclude></exclude>
    protected static double[,] GenerateCorrelationMatrix(double[,] fl)
    {
      var matrix = new MatrixOfDoubles(fl);
      var corr = LinearAlgebra.Multiply(matrix, LinearAlgebra.Transpose(matrix));
      var retVal = new double[corr.dim1(),corr.dim2()];
      for (int i = 0; i < corr.dim1(); ++i)
        for (int j = 0; j < corr.dim2(); ++j)
          retVal[i, j] = corr.at(i, j);
      return retVal;
    }

    /// <summary>
    /// Initialize factors and volatilities
    /// </summary>
    protected void InitializeParameters(Input input, params double[] cptyNorm)
    {
      int nFactors = input.FactorLoadings.FactorCount;
      var discountCurves = input.DiscountCurves;
      var creditCurves = input.CreditCurves;
      var fwdCurves = input.FwdCurves;
      var fxRates = input.FxRates;
      var rand = new Random(3);
      foreach (var dc in discountCurves)
        input.FactorLoadings.AddFactors(dc, GenerateFactors(rand, tenors_.Length, nFactors, 1.0));
      for (int i = 0; i < creditCurves.Length; ++i)
      {
        var cc = creditCurves[i];
        int cptyIdx = Array.IndexOf(cptyIndex_, i);
        double norm = (cptyNorm != null && cptyNorm.Length > 0 && cptyIdx >= 0) ? cptyNorm[cptyIdx] : 1.0;
        input.FactorLoadings.AddFactors(cc, GenerateFactors(rand, 1, nFactors, norm));
      }
      foreach (var rate in fxRates)
        input.FactorLoadings.AddFactors(rate, GenerateFactors(rand, 1, nFactors, 1.0));
      foreach (var fc in fwdCurves)
      {
        var spot = fc as StockCurve;
        if (spot != null)
        {
          input.FactorLoadings.AddFactors(spot.Spot, GenerateFactors(rand, 1, nFactors, 1.0));
        }
        else
          input.FactorLoadings.AddFactors(fc, GenerateFactors(rand, tenors_.Length, nFactors, 1.0));
      }
      for (int i = 0; i < discountCurves.Length; ++i)
      {
        var vols = Array.ConvertAll(volas_, v => new VolatilityCurve(asOf_, v));
        input.Volatilities.Add(discountCurves[i], vols);
      }
      for (int i = 0; i < creditCurves.Length; ++i)
      {
        var vol = new VolatilityCurve(asOf_, cvolas_[i]);
        input.Volatilities.Add(creditCurves[i], vol);
      }
      foreach (var fc in fwdCurves)
      {
        var stockCurve = fc as StockCurve;
        if (stockCurve != null)
        {
          CCRCalibrationUtils.CalibrateSpotVolatility(asOf_, stockCurve, new VolatilityCurve(asOf_, stockVol_), input.Volatilities, input.FactorLoadings, null);
        }
        var inflCurve = fc as InflationCurve;
        if (inflCurve != null)
        {
          var vols = Array.ConvertAll(inflationVol_, v => new VolatilityCurve(asOf_, v));
          input.Volatilities.Add(inflCurve, vols);
        }
        var projCurve = fc as DiscountCurve;
        if (projCurve != null)
        {
          var vols = Array.ConvertAll(volas_, v => new VolatilityCurve(asOf_, v));
          input.Volatilities.Add(projCurve, vols);
        }
      }
      for (int i = 0; i < discountCurves.Length - 1; ++i)
      {
        var vol = new VolatilityCurve(asOf_, fxvolas_[i]);
        input.Volatilities.Add(fxRates[i], vol);
      }
    }

    #endregion

    #region CreateInput
    protected static Dt[] GenerateSimulDates(Dt asOf, Frequency simulFreq)
    {
      var simDates = new List<Dt>();
      Dt terminal = Dt.Add(asOf, 365 * 12);
      Dt dt = asOf;
      for (;;)
      {
        simDates.Add(dt);
        dt = Dt.Add(dt, simulFreq, false);
        if (Dt.Cmp(dt, terminal) > 0)
          break;
      }
      return simDates.ToArray();
    }


    protected Input CreateInput(int nFactors, int sampleSize, Tenor gridSize, int rateCount, int creditCount, bool withCredit, bool withInflation, bool withStock, bool withDualCurve, params double[] cptyNorm)
    {
      DiscountCurve[] dc;
      SurvivalCurve[] sc;
      CalibratedCurve[] fwd;
      FxRate[] fx;
      CreateMarket(rateCount, creditCount, withInflation, withStock, withDualCurve, out dc, out fx, out sc, out fwd);
      var portfolio = CreatePricers(dc, fx, withCredit ? sc : new SurvivalCurve[0], fwd);
      var names = portfolio.Select((p, i) => (i % 2 == 0) ? "A" : "B").ToArray(); //2 netting groups
      string[] marketFactorNames = ArrayUtil.Generate(nFactors, i => string.Concat("factor", i));
      var ten = tenors_.Select(Tenor.Parse).ToArray();
      var input = new Input
                  {
                    AsOf = asOf_,
                    Pricers = portfolio,
                    Names = names,
                    Cpty = (sc.Length > 0) ? Array.ConvertAll(cptyIndex_, i => sc[i]) : new SurvivalCurve[0],
                    CptyRec = cptyRec_,
                    DiscountCurves = dc,
                    CreditCurves = sc,
                    FwdCurves = fwd,
                    FxRates = fx,
                    TenorDates = tenors_.Select(t => Dt.Add(asOf_, t)).ToArray(),
                    FactorLoadings = new FactorLoadingCollection(marketFactorNames, ten),
                    Volatilities = new VolatilityCollection(ten),
                    Sample = sampleSize,
                    SimulDates = GenerateSimulDates(asOf_, simulFreq_),
                    FactorNames = marketFactorNames,
                    GridSize = gridSize
                  };
      InitializeParameters(input, cptyNorm);
      return input;
    }


    protected void CreateMarket(int rateCount, int creditCount, bool withInflation, bool withStock, bool withDualCurve, out DiscountCurve[] discountCurves, out FxRate[] fxRates, out SurvivalCurve[] survivalCurves, out CalibratedCurve[] fwdCurves)
    {
      Dt[] tenorDates = Array.ConvertAll(tenors_, ten => Dt.Add(asOf_, ten));
      discountCurves = new DiscountCurve[rateCount];
      discountCurves[0] = CreateDiscountCurve(asOf_, indexTenor_[0], tenorDates, rates_, currencies_[0]);
      if (rateCount > 1)
        discountCurves[1] = CreateDiscountCurve(asOf_, indexTenor_[1], tenorDates, frates1_, currencies_[1]);
      if (rateCount > 2)
        discountCurves[2] = CreateDiscountCurve(asOf_, indexTenor_[2], tenorDates, frates2_, currencies_[2]);
      //set up fx rates
      fxRates = new FxRate[rateCount - 1];
      for (int i = 1; i < rateCount; i++)
        fxRates[i - 1] = new FxRate(asOf_, 2, discountCurves[i].Ccy, discountCurves[0].Ccy, fxrates_[i - 1], Calendar.None, Calendar.None);
      var fwd = new List<CalibratedCurve>();
      if (withDualCurve)
        fwd.Add(CreateDiscountCurve(asOf_, "6M", tenorDates, projectionRates_, currencies_[0]));
      if (withInflation)
        fwd.Add(CreateInflationCurve(asOf_, spotInflation_, tenors_, inflationZeroRates_, discountCurves[0], dayCounts_[0], calendar_[0], roll_[0]));
      if (withStock)
        fwd.Add(CreateStockForwardCurve(stockPrice_, discountCurves[0]));
      fwdCurves = fwd.ToArray();
      survivalCurves = new SurvivalCurve[creditCount];
      for (int i = 0; i < creditCount; ++i)
      {
        int ccy = i % rateCount;
        survivalCurves[i] = SurvivalCurve.FromProbabilitiesWithCDS(asOf_, currencies_[ccy], null, InterpMethod.Weighted,
                                                                   ExtrapMethod.Const, new[] {Dt.Add(asOf_, "10Y")},
                                                                   new[] {Math.Exp(-lambda_[i] * 5)}, new[] {"10Y"},
                                                                   new[] {DayCount.Actual360}, new[] {Frequency.Quarterly},
                                                                   new[] {BDConvention.Following}, new[] {Calendar.NYB},
                                                                   new[] {0.4}, 0.0);
        survivalCurves[i].Name = String.Concat("obligor.", i);
        survivalCurves[i].SurvivalCalibrator.DiscountCurve = discountCurves[ccy];
      }
    }

    protected static SwapPricer CreateSwapPricer(Dt asOf, double notional, DiscountCurve discountCurve, string indexTenor, DayCount indexDayCount, Calendar indexCalendar, BDConvention indexRoll, Frequency frequency, BDConvention roll, string maturity, DayCount dayCount, Calendar calendar, int settleDays)
    {
      var rateResets = new RateResets(0, 0);
      //vanilla domestic swap
      ReferenceIndex index = new InterestRateIndex(String.Concat("Libor", discountCurve.Ccy), Tenor.Parse(indexTenor), discountCurve.Ccy, indexDayCount,
                                                   indexCalendar, indexRoll, settleDays);
      var domesticLegFixed = new SwapLeg(Dt.AddDays(asOf, settleDays, calendar), Dt.Add(asOf, maturity), discountCurve.Ccy,
                                         0.035, dayCount, frequency, roll, calendar, false) {FinalExchange = false};
      var domesticLegFloat = new SwapLeg(Dt.AddDays(asOf, settleDays, calendar), Dt.Add(asOf, maturity),
                                         Tenor.Parse(indexTenor).ToFrequency(), 0, index) {FinalExchange = false};
      var fixedLeg = new SwapLegPricer(domesticLegFixed, asOf, Dt.AddDays(asOf, settleDays, calendar), notional, discountCurve,
                                       null, null, null, null, null);
      var floatLeg = new SwapLegPricer(domesticLegFloat, asOf, Dt.AddDays(asOf, settleDays, calendar), -notional, discountCurve,
                                       index, discountCurve, rateResets, null, null);
      var swapPricer = new SwapPricer(fixedLeg, floatLeg);
      fixedLeg.SwapLeg.Coupon = swapPricer.ParCoupon();
      return swapPricer;
    }


    protected static SwapPricer CreateXccySwapPricer(Dt asOf, double notional, FxRate fxRate, DiscountCurve ccy1DiscountCurve, DiscountCurve ccy2DiscountCurve, string indexTenor, DayCount indexDayCount, Calendar indexCalendar, BDConvention indexRoll, Frequency frequency, BDConvention roll, string maturity, DayCount dayCount, Calendar calendar, int settleDays)
    {
      var rateResets = new RateResets(0, 0);
      ReferenceIndex foreignIndex = new InterestRateIndex(String.Concat("Libor", ccy2DiscountCurve.Ccy), Tenor.Parse(indexTenor), ccy2DiscountCurve.Ccy, indexDayCount,
                                                          indexCalendar, indexRoll, settleDays);
      var ccy1LegFixed = new SwapLeg(Dt.AddDays(asOf, settleDays, calendar), Dt.Add(asOf, maturity), ccy1DiscountCurve.Ccy,
                                     0.035, dayCount, frequency, roll, calendar, false) {FinalExchange = true};
      var ccy2LegFloat = new SwapLeg(Dt.AddDays(asOf, settleDays, indexCalendar), Dt.Add(asOf, maturity), Tenor.Parse(indexTenor).ToFrequency(), 0, foreignIndex)
                         {FinalExchange = true};
      var ccy1LegPricer = new SwapLegPricer(ccy1LegFixed, asOf, Dt.AddDays(asOf, settleDays, calendar), notional,
                                            ccy1DiscountCurve, null, null, null, null, null);
      var ccy2LegPricer = new SwapLegPricer(ccy2LegFloat, asOf, Dt.AddDays(asOf, settleDays, indexCalendar),
                                            -notional * fxRate.GetRate(ccy1DiscountCurve.Ccy, ccy2DiscountCurve.Ccy),
                                            ccy1DiscountCurve, foreignIndex, ccy2DiscountCurve, rateResets, null,
                                            CreateFxCurve(fxRate, ccy1DiscountCurve, ccy2DiscountCurve));
      var swapPricerXccy = new SwapPricer(ccy1LegPricer, ccy2LegPricer);
      ccy1LegPricer.SwapLeg.Coupon = swapPricerXccy.ParCoupon();
      return swapPricerXccy;
    }

    protected SwapPricer CreateBasisSwapPricer(double notional, DiscountCurve discountCurve, DiscountCurve projectionCurve, string indexTenor1, string indexTenor2, DayCount indexDayCount, Calendar indexCalendar, BDConvention indexRoll, string maturity, int settleDays)
    {
      var rateResets = new RateResets(0, 0);
      ReferenceIndex index1 = new InterestRateIndex(String.Concat("Libor", discountCurve.Ccy, indexTenor1), Tenor.Parse(indexTenor1), discountCurve.Ccy, indexDayCount,
                                                    indexCalendar, indexRoll, settleDays);
      ReferenceIndex index2 = new InterestRateIndex(String.Concat("Libor", discountCurve.Ccy, indexTenor2), Tenor.Parse(indexTenor2), discountCurve.Ccy, indexDayCount,
                                                    indexCalendar, indexRoll, settleDays);
      var index1Leg = new SwapLeg(Dt.AddDays(asOf_, settleDays, index1.Calendar), Dt.Add(asOf_, maturity), index1.IndexTenor.ToFrequency(), 1e-3, index1);
      var index2Leg = new SwapLeg(Dt.AddDays(asOf_, settleDays, index2.Calendar), Dt.Add(asOf_, maturity), index2.IndexTenor.ToFrequency(), 0, index2);
      var index1LegPricer = new SwapLegPricer(index1Leg, asOf_, Dt.AddDays(asOf_, settleDays, indexCalendar), notional, discountCurve, index1, discountCurve, rateResets,
                                              null, null);
      var index2LegPricer = new SwapLegPricer(index2Leg, asOf_, Dt.AddDays(asOf_, settleDays, indexCalendar), -notional, discountCurve, index2, projectionCurve,
                                              rateResets, null, null);
      var basisSwap = new SwapPricer(index1LegPricer, index2LegPricer);
      
      index2LegPricer.SwapLeg.Coupon = basisSwap.ParCoupon();
      return basisSwap;
    }

    protected static InflationBondPricer CreateInflationBondPricer(Dt asOf, DiscountCurve discountCurve, InflationCurve inflationCurve, string maturity, double notional, double coupon, 
      DayCount indexDayCount, Calendar indexCalendar, BDConvention indexRoll, DayCount dayCount, Frequency frequency, BDConvention roll, Calendar calendar, double spotInflation)
    {
      ReferenceIndex inflIndex = CreateInflationIndex(discountCurve.Ccy, indexCalendar, indexDayCount, indexRoll);
      var inflationBond = new InflationBond(Dt.AddDays(asOf, 1, calendar), Dt.Add(asOf, maturity), discountCurve.Ccy, BondType.None, coupon, dayCount,
                                              CycleRule.None, frequency, roll, calendar, (InflationIndex)inflIndex,
                                              spotInflation, Tenor.Empty);
      var inflBondPricer = new InflationBondPricer(inflationBond, asOf, Dt.AddDays(asOf, 2, Calendar.NYB), notional, discountCurve, (InflationIndex)inflIndex, inflationCurve, null, null);
      return inflBondPricer;
    }

    protected static StockOptionPricer CreateStockOptionPricer(Dt asOf, StockCurve fwdCurve, double stockPrice, string maturity, OptionType optionType, VolatilityCurve vol, double notional)
    {
      var option = new StockOption(Dt.Add(asOf, maturity), optionType, OptionStyle.European, stockPrice){Ccy = fwdCurve.Spot.Ccy, Description = fwdCurve.Spot.Name};
      var stockOptionPricer = new StockOptionPricer(option, asOf, asOf, fwdCurve, null, vol) { Notional = notional };
      return stockOptionPricer;
    }

    protected IPricer[] CreatePricers(DiscountCurve[] discountCurves, FxRate[] fxRates, SurvivalCurve[] survivalCurves, CalibratedCurve[] fwdCurves)
    {
      return CreatePricers(1e7, 1e7, 1e6, 1e6, true, discountCurves, fxRates, survivalCurves, fwdCurves);
    }


    private IPricer[] CreatePricers(double swapNotional, double bondNotional, double optionNotional, double cdsNotional, bool scaleCDS, DiscountCurve[] discountCurves, FxRate[] fxRates, SurvivalCurve[] survivalCurves, CalibratedCurve[] fwdCurves)
    {
      var pricerList = new List<IPricer>();
      var usdSwapPricer = CreateSwapPricer(asOf_, swapNotional, discountCurves[0], indexTenor_[0], dayCounts_[0], calendar_[0], roll_[0], swapFrequency_[0],
                                           roll_[0], "10Y", dayCounts_[0], calendar_[0], 2);
      pricerList.Add(usdSwapPricer);
      if (discountCurves.Length > 1)
      {
        var eurSwapPricer = CreateSwapPricer(asOf_, swapNotional * fxRates[0].GetRate(discountCurves[0].Ccy, discountCurves[1].Ccy), discountCurves[1],
                                             indexTenor_[1],
                                             dayCounts_[1], calendar_[1], roll_[1], swapFrequency_[1], roll_[1],
                                             "10Y", dayCounts_[1], calendar_[1], 2);
        pricerList.Add(eurSwapPricer);
        var eurXccySwapPricer = CreateXccySwapPricer(asOf_, swapNotional, fxRates[0], discountCurves[0], discountCurves[1], indexTenor_[1], dayCounts_[1],
                                                     calendar_[1],
                                                     roll_[1], swapFrequency_[0], roll_[0], "10Y", dayCounts_[0], calendar_[0], 2);
        pricerList.Add(eurXccySwapPricer);
        //cross currency swap

      }
      if (discountCurves.Length > 2)
      {
        var jpySwapPricer = CreateSwapPricer(asOf_, swapNotional / fxRates[1].GetRate(discountCurves[2].Ccy, discountCurves[0].Ccy), discountCurves[2],
                                             indexTenor_[2],
                                             dayCounts_[2], calendar_[2], roll_[2], swapFrequency_[2], roll_[2],
                                             "10Y", dayCounts_[2], calendar_[2], 2);
        pricerList.Add(jpySwapPricer);
        var jpyXccySwapPricer = CreateXccySwapPricer(asOf_, swapNotional, fxRates[1], discountCurves[0], discountCurves[2], indexTenor_[2], dayCounts_[2],
                                                     calendar_[2], roll_[2], swapFrequency_[0], roll_[0], "10Y", dayCounts_[0], calendar_[0], 2);
        pricerList.Add(jpyXccySwapPricer);
      }
      //set up cds
      var cds = new CDS(Dt.AddDays(asOf_, 1, Calendar.NYB),
                        Dt.Add(asOf_, "10Y"), Currency.USD, 0.0,
                        DayCount.Actual360, Frequency.Quarterly,
                        BDConvention.Following, Calendar.NYB);
      pricerList.AddRange(survivalCurves.Select((sc, i) =>
                                                {
                                                  var product = CloneUtil.Clone(cds);
                                                  product.Ccy = sc.Ccy;
                                                  var ccyIdx = IndexOf(discountCurves, product.Ccy);
                                                  var dc = discountCurves[ccyIdx];
                                                  var retVal = new CDSCashflowPricer(product, asOf_, dc, null, sc);
                                                  retVal.CDS.Premium = retVal.BreakEvenPremium();
                                                  var fx = (ccyIdx == 0) ? 1.0 : fxRates[ccyIdx - 1].GetRate(discountCurves[0].Ccy, discountCurves[ccyIdx].Ccy);
                                                  retVal.Notional = scaleCDS
                                                                      ? Math.Pow(-1, i) * cdsNotional / retVal.CDS.Premium
                                                                      : Math.Pow(-1, i) * cdsNotional * fx;
                                                  return retVal;
                                                }));
      var projectionCurve = fwdCurves.FirstOrDefault(c => c is DiscountCurve);
      if (projectionCurve != null) //basis swap
      {
        var basisSwapPricer = CreateBasisSwapPricer(swapNotional, discountCurves[0], (DiscountCurve)projectionCurve, indexTenor_[0], basisSwapIndexTenor_[0],
                                                    dayCounts_[0], calendar_[0], roll_[0], "10Y", 2);
        pricerList.Add(basisSwapPricer);

      }
      var inflationCurve = fwdCurves.FirstOrDefault(c => c is InflationCurve);
      if (inflationCurve != null)
      {
        var inflBondPricer = CreateInflationBondPricer(asOf_, discountCurves[0], (InflationCurve)inflationCurve, "10Y", bondNotional, 0.05, dayCounts_[0], calendar_[0],
                                                       roll_[0], dayCounts_[0], Frequency.SemiAnnual, roll_[0], calendar_[0], spotInflation_);
        pricerList.Add(inflBondPricer);

      }
      var stockFwdCurve = fwdCurves.FirstOrDefault(c => c is StockCurve);
      if (stockFwdCurve != null)
      {
        var vol = new VolatilityCurve(asOf_, stockVol_);
        vol.Fit();
        var stockOptionPricer = CreateStockOptionPricer(asOf_, (StockCurve)stockFwdCurve, stockPrice_, "5Y", OptionType.Call, vol, optionNotional);
        pricerList.Add(stockOptionPricer);
      }
      return pricerList.ToArray();
    }

    #endregion

    #region CreateBase
    protected abstract ICounterpartyCreditRiskCalculations CreateEngine(Input input);

    protected abstract IPricer[] CreateBondPricers(out string[] id);
    
    #endregion
    
    #region Nested type: TestBond

    [Serializable]
    protected class TestBond : IProduct
    {
      private Currency ccy_;
      private Dt effective_;
      private Dt maturity_;

      public TestBond(Dt effective, Dt maturity, Currency ccy)
      {
        effective_ = effective;
        ccy_ = ccy;
        maturity_ = maturity;
      }

      #region IProduct Members

      public object Clone()
      {
        return new TestBond(effective_, maturity_, ccy_);
      }

      public void Validate(ArrayList errors)
      {
      }

      public string Description
      { 
        get;
        set;
      }

      public Currency Ccy
      {
        get { return ccy_; }
        set { ccy_ = value; }
      }

      public Dt Effective
      {
        get { return effective_; }
        set { effective_ = value; }
      }

      public Dt Maturity
      {
        get { return maturity_; }
        set { maturity_ = value; }
      }

      public Dt EffectiveMaturity => Maturity;

      public double Notional
      {
        get { return 1.0; }
        set { return; }
      }

      /// <summary>
      /// True if this product is active on the specified pricing date
      /// </summary>
      /// <remarks>
      ///   <para>A product is active there is any residual risk. Ie there are any
      ///   unsettled cashflows.</para>
      ///   <para>For most products this is if the pricing date is on or before
      ///   the maturity date.</para>
      /// </remarks>
      /// <param name="asOf">Pricing as-of date</param>
      /// <returns>true if product is active</returns>
      public bool IsActive(Dt asOf)
      {
        return !((asOf < Effective) || (asOf > Maturity));
      }

      #endregion
    }

    #endregion

    #region Nested type: TestBondPricer

    [Serializable]
    internal class TestBondPricer : IPricer
    {
      private Dt asOf_;
      protected readonly DiscountCurve discount_;
      private readonly Dt maturity_;
      private readonly TestBond product_;
      private Dt settle_;
      protected readonly CalibratedCurve projection_;

      internal TestBondPricer(Dt asOf, Dt settle, Dt maturity, CalibratedCurve projection, DiscountCurve discount)
      {
        asOf_ = asOf;
        settle_ = settle;
        maturity_ = maturity;
        projection_ = projection;
        discount_ = discount;
        product_ = new TestBond(asOf, maturity, discount.Ccy);
      }

      #region IPricer Members

      public virtual double Pv()
      {
        if (settle_ > maturity_)
          return 0.0;
        double retVal = discount_.Interpolate(settle_, maturity_);
        if (projection_ != null)
          retVal *= Projection(maturity_);
        return retVal;
      }

      private double Projection(Dt date)
      {
        if (projection_ is DiscountCurve)
          return projection_.F(date, Dt.Add(date, "1Y"));
        return projection_.Interpolate(date);
      }

      public object Clone()
      {
        return new TestBondPricer(asOf_, settle_, maturity_, projection_, discount_);
      }

      public double Accrued()
      {
        return 0.0;
      }

      public Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
      {
        return null;
      }

      public void Reset()
      {
        return;
      }

      public Dt AsOf
      {
        get { return asOf_; }
        set { asOf_ = value; }
      }

      public Dt Settle
      {
        get { return settle_; }
        set { settle_ = value; }
      }

      public IProduct Product => product_;

      public IPricer PaymentPricer => null;

      public Currency ValuationCurrency => product_.Ccy;

      #endregion
    }

    #endregion

    #region Nested type: TestBondPricer

    [Serializable]
    internal class TestStockPricer : TestBondPricer
    {
      internal TestStockPricer(Dt asOf, Dt settle, StockCurve projection) : base(asOf, settle, settle, projection, projection.DiscountCurve)
      {
        InitialPrice = projection.Spot.Value;
        InitialDate = projection.Spot.Spot;
        ClonedDiscount = (DiscountCurve)projection.DiscountCurve.Clone();
      }
      
      #region IPricer Members

      public override double Pv()
      {
        Dt spot = StockCurve.Spot.Spot;
        double spotPrice = StockCurve.Spot.Value; //Add E(dividends from 0 - T) + ex div price at T. Then we take expectations anyways.
        double dividendPv = StockCurve.Dividends.Pv(InitialDate, spot, InitialPrice, ClonedDiscount) / StockCurve.DiscountCurve.Interpolate(spot);
        return spotPrice + dividendPv;
      }

      #endregion
      #region Properties
      private StockCurve StockCurve => projection_ as StockCurve;
      private double InitialPrice { get; set; }
      private Dt InitialDate { get; set; }
      private DiscountCurve ClonedDiscount { get; set; }
      #endregion
    }

    #endregion

    #region Nested type: CollateralMap
    
    protected class CollateralMap : ICollateralMap
    {
      #region ICollateralMap Members

      Tenor ICollateralMap.MarginPeriodOfRisk => new Tenor(20, TimeUnit.Days);

      string ICollateralMap.NettingGroup => "A";

      double? ICollateralMap.LastPosting
      {
        get { return null; }
        set { return; }
      }

      bool ICollateralMap.ReusePermitted => true;
      bool ICollateralMap.IndependentAmountSegregated => true;

      double ICollateralMap.VariationMargin(double pv, double spread, Dt dt)
      {
        return 0.2*Math.Max(pv, 0.0);
      }

      public double IndependentAmount(double pv, double vm)
      {
        return 0; 
      }

      #endregion
    }

    #endregion

    #region FormatResults
    
    protected DataTable FormatResults(ICounterpartyCreditRiskCalculations engine, Netting netting)
    {
      var dataTable = new DataTable();
      dataTable.Columns.Add(new DataColumn("Tenor", typeof (string)));
      dataTable.Columns.Add(new DataColumn("Measure", typeof (string)));
      dataTable.Columns.Add(new DataColumn("Value", typeof (double)));
      DataRow dataRowCVA = dataTable.NewRow();
      DataRow dataRowDVA = dataTable.NewRow();
      dataRowCVA["Measure"] = "CVA";
      dataRowCVA["Tenor"] = "None";
      dataRowCVA["Value"] = engine.GetMeasure(CCRMeasure.CVA, netting, Dt.Empty, 0.0);
      dataRowDVA["Measure"] = "DVA";
      dataRowDVA["Tenor"] = "None";
      dataRowDVA["Value"] = engine.GetMeasure(CCRMeasure.DVA, netting, Dt.Empty, 0.0);
      dataTable.Rows.Add(dataRowCVA);
      dataTable.Rows.Add(dataRowDVA);
      for (int i = 0; i < engine.SimulatedValues.ExposureDates.Length; ++i)
      {
        Dt dt = engine.SimulatedValues.ExposureDates[i];
        string tenor = dt.ToString();
        DataRow dataRowEE = dataTable.NewRow();
        DataRow dataRowNEE = dataTable.NewRow();
        DataRow dataRowPFE = dataTable.NewRow();
        dataRowEE["Measure"] = "EE";
        dataRowEE["Tenor"] = tenor;
        dataRowEE["Value"] = engine.GetMeasure(CCRMeasure.EE, netting, dt, 0.0);
        dataRowNEE["Measure"] = "NEE";
        dataRowNEE["Tenor"] = tenor;
        dataRowNEE["Value"] = engine.GetMeasure(CCRMeasure.NEE, netting, dt, 0.0);
        dataRowPFE["Measure"] = "PFE";
        dataRowPFE["Tenor"] = tenor;
        dataRowPFE["Value"] = engine.GetMeasure(CCRMeasure.PFE, netting, dt, 0.99);
        dataTable.Rows.Add(dataRowEE);
        dataTable.Rows.Add(dataRowNEE);
        dataTable.Rows.Add(dataRowPFE);
      }
      return dataTable;
    }

    protected DataTable FormatResultsZero(ICounterpartyCreditRiskCalculations engine, Netting netting)
    {
      var dataTable = new DataTable();
      dataTable.Columns.Add(new DataColumn("Tenor", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Measure", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Value", typeof(double)));
      DataRow dataRowCVA = dataTable.NewRow();
      DataRow dataRowDVA = dataTable.NewRow();
      dataRowCVA["Measure"] = "CVA0";
      dataRowCVA["Tenor"] = "None";
      dataRowCVA["Value"] = engine.GetMeasure(CCRMeasure.CVA0, netting, Dt.Empty, 0.0);
      dataRowDVA["Measure"] = "DVA0";
      dataRowDVA["Tenor"] = "None";
      dataRowDVA["Value"] = engine.GetMeasure(CCRMeasure.DVA0, netting, Dt.Empty, 0.0);
      dataTable.Rows.Add(dataRowCVA);
      dataTable.Rows.Add(dataRowDVA);
      for (int i = 0; i < engine.SimulatedValues.ExposureDates.Length; ++i)
      {
        Dt dt = engine.SimulatedValues.ExposureDates[i];
        string tenor = dt.ToString();
        DataRow dataRowEE = dataTable.NewRow();
        DataRow dataRowNEE = dataTable.NewRow();
        DataRow dataRowPFE = dataTable.NewRow();
        dataRowEE["Measure"] = "EE0";
        dataRowEE["Tenor"] = tenor;
        dataRowEE["Value"] = engine.GetMeasure(CCRMeasure.DiscountedEE0, netting, dt, 0.0);
        dataRowNEE["Measure"] = "NEE0";
        dataRowNEE["Tenor"] = tenor;
        dataRowNEE["Value"] = engine.GetMeasure(CCRMeasure.DiscountedNEE0, netting, dt, 0.0);
        dataRowPFE["Measure"] = "PFE0";
        dataRowPFE["Tenor"] = tenor;
        dataRowPFE["Value"] = engine.GetMeasure(CCRMeasure.PFE0, netting, dt, 0.99);
        dataTable.Rows.Add(dataRowEE);
        dataTable.Rows.Add(dataRowNEE);
        dataTable.Rows.Add(dataRowPFE);
      }
      return dataTable;
    }

    protected DataTable FormatFundingResults(ICounterpartyCreditRiskCalculations engine, Netting netting)
    {
      var dataTable = new DataTable();
      dataTable.Columns.Add(new DataColumn("Measure", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Value", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Tenor", typeof(string)));
      DataRow dataRowFCA = dataTable.NewRow();
      DataRow dataRowFBA = dataTable.NewRow();
      DataRow dataRowFCA0 = dataTable.NewRow();
      DataRow dataRowFBA0 = dataTable.NewRow();
      DataRow dataRowFCAND = dataTable.NewRow();
      DataRow dataRowFBAND = dataTable.NewRow();

      dataRowFCA["Measure"] = "FCA";
      dataRowFCA["Value"] = engine.GetMeasure(CCRMeasure.FCA, netting, Dt.Empty, 0.0);
      dataRowFCA["Tenor"] = "None";
      dataRowFBA["Measure"] = "FBA";
      dataRowFBA["Value"] = engine.GetMeasure(CCRMeasure.FBA, netting, Dt.Empty, 0.0);
      dataRowFBA["Tenor"] = "None";
      dataRowFCA0["Measure"] = "FCA0";
      dataRowFCA0["Value"] = engine.GetMeasure(CCRMeasure.FCA0, netting, Dt.Empty, 0.0);
      dataRowFCA0["Tenor"] = "None";
      dataRowFBA0["Measure"] = "FBA0";
      dataRowFBA0["Value"] = engine.GetMeasure(CCRMeasure.FBA0, netting, Dt.Empty, 0.0);
      dataRowFBA0["Tenor"] = "None";

      dataRowFCAND["Measure"] = "FCANoDefault";
      dataRowFCAND["Value"] = engine.GetMeasure(CCRMeasure.FCANoDefault, netting, Dt.Empty, 0.0);
      dataRowFCAND["Tenor"] = "None";
      dataRowFBAND["Measure"] = "FBANoDefault";
      dataRowFBAND["Value"] = engine.GetMeasure(CCRMeasure.FBANoDefault, netting, Dt.Empty, 0.0);
      dataRowFBAND["Tenor"] = "None";

      dataTable.Rows.Add(dataRowFCA);
      dataTable.Rows.Add(dataRowFBA);
      dataTable.Rows.Add(dataRowFCA0);
      dataTable.Rows.Add(dataRowFBA0);
      dataTable.Rows.Add(dataRowFCAND);
      dataTable.Rows.Add(dataRowFBAND);
      return dataTable;
    }


    protected DataTable FormatResults(IRunSimulationPath engine)
    {
      var dataTable = new DataTable();
      dataTable.Columns.Add(new DataColumn("Tenor", typeof (string)));
      dataTable.Columns.Add(new DataColumn("Measure", typeof (string)));
      dataTable.Columns.Add(new DataColumn("Value", typeof (double)));
      double cva = engine.GetMeasureAllocatedByTrade(CCRMeasure.CVA,0, 1.0).Sum();
      double dva = engine.GetMeasureAllocatedByTrade(CCRMeasure.DVA, 0, 1.0).Sum();
      DataRow dataRowCVA = dataTable.NewRow();
      DataRow dataRowDVA = dataTable.NewRow();
      dataRowCVA["Measure"] = "CVA";
      dataRowCVA["Tenor"] = "None";
      dataRowCVA["Value"] = cva;
      dataRowDVA["Measure"] = "DVA";
      dataRowDVA["Tenor"] = "None";
      dataRowDVA["Value"] = dva;
      dataTable.Rows.Add(dataRowCVA);
      dataTable.Rows.Add(dataRowDVA);

      for (int i = 0; i < engine.ExposureDates.Length; ++i)
      {
        Dt dt = engine.ExposureDates[i];
        string tenor = dt.ToString();
        DataRow dataRowEE = dataTable.NewRow();
        DataRow dataRowNEE = dataTable.NewRow();
        DataRow dataRowPFE = dataTable.NewRow();
        dataRowEE["Measure"] = "EE";
        dataRowEE["Tenor"] = tenor;
        dataRowEE["Value"] = engine.GetMeasureAllocatedByTrade(CCRMeasure.EE, i, 1.0).Sum();
        dataRowNEE["Measure"] = "NEE";
        dataRowNEE["Tenor"] = tenor;
        dataRowNEE["Value"] = engine.GetMeasureAllocatedByTrade(CCRMeasure.NEE, i, 1.0).Sum();
        dataRowPFE["Measure"] = "PFE";
        dataRowPFE["Tenor"] = tenor;
        dataRowPFE["Value"] = engine.GetMeasureAllocatedByTrade(CCRMeasure.PFE, i, 0.99).Sum();
        dataTable.Rows.Add(dataRowEE);
        dataTable.Rows.Add(dataRowNEE);
        dataTable.Rows.Add(dataRowPFE);
      }
      return dataTable;
    }

    protected DataTable FormatFundingResults(IRunSimulationPath engine)
    {
      var dataTable = new DataTable();
      dataTable.Columns.Add(new DataColumn("Measure", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Value", typeof(double)));
      dataTable.Columns.Add(new DataColumn("Tenor", typeof(string)));
      DataRow dataRowFCA = dataTable.NewRow();
      DataRow dataRowFBA = dataTable.NewRow();
      DataRow dataRowFCA0 = dataTable.NewRow();
      DataRow dataRowFBA0 = dataTable.NewRow();
      DataRow dataRowFCAND = dataTable.NewRow();
      DataRow dataRowFBAND = dataTable.NewRow();

      dataRowFCA["Measure"] = "FCA";
      dataRowFCA["Value"] = engine.GetMeasure(CCRMeasure.FCA, Dt.Empty, 0.0);
      dataRowFCA["Tenor"] = "None";
      dataRowFBA["Measure"] = "FBA";
      dataRowFBA["Value"] = engine.GetMeasure(CCRMeasure.FBA, Dt.Empty, 0.0);
      dataRowFBA["Tenor"] = "None";
      dataRowFCA0["Measure"] = "FCA0";
      dataRowFCA0["Value"] = engine.GetMeasure(CCRMeasure.FCA0, Dt.Empty, 0.0);
      dataRowFCA0["Tenor"] = "None";
      dataRowFBA0["Measure"] = "FBA0";
      dataRowFBA0["Value"] = engine.GetMeasure(CCRMeasure.FBA0, Dt.Empty, 0.0);
      dataRowFBA0["Tenor"] = "None";

      dataRowFCAND["Measure"] = "FCANoDefault";
      dataRowFCAND["Value"] = engine.GetMeasure(CCRMeasure.FCANoDefault, Dt.Empty, 0.0);
      dataRowFCAND["Tenor"] = "None";
      dataRowFBAND["Measure"] = "FBANoDefault";
      dataRowFBAND["Value"] = engine.GetMeasure(CCRMeasure.FBANoDefault, Dt.Empty, 0.0);
      dataRowFBAND["Tenor"] = "None";


      dataTable.Rows.Add(dataRowFCA);
      dataTable.Rows.Add(dataRowFBA);
      dataTable.Rows.Add(dataRowFCA0);
      dataTable.Rows.Add(dataRowFBA0);
      dataTable.Rows.Add(dataRowFCAND);
      dataTable.Rows.Add(dataRowFBAND);
      return dataTable;
    }


    #endregion

    #region Tests
    protected void BaseCVACalculations()
    {
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      engine.Execute();
      DataTable dt = FormatResults(engine, netting_);
      timer.Stop();
      ResultData retVal = ToResultData(dt, timer.Elapsed);
      MatchExpects(retVal);
    }

    protected void BaseRnTest()
    {
      var engine = CreateEngine(input_);
      engine.Execute();
      double rn0 = 0.0, rn1 = 0.0, rn2 = 0.0, mass = 0.0;
      int idx = engine.SimulatedValues.ExposureDates.Length / 2;
      foreach (var path in engine.SimulatedValues.Paths)
      {
        var p = path as SimulatedPathValues;
        rn0 += p.Weight * p.GetRadonNikodymSurvival(idx);
        rn1 += p.Weight * p.GetRadonNikodymCpty(idx);
        rn2 += p.Weight * p.GetRadonNikodymOwn(idx);
        mass += p.Weight;
      }
      rn0 /= mass;
      rn1 /= mass;
      rn2 /= mass;
      Assert.AreEqual(1.0, rn0, 1e-2);
      Assert.AreEqual(1.0, rn1, 1e-2);
      Assert.AreEqual(1.0, rn2, 1e-2);
    }

    protected void BaseCreditSpreadSensitivities()
    {
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.CreditSpreadSensitivities(engine, netting_, upBump_, downBump_, bumpRelative_,
                                                                  bumpType_,
                                                                  quoteTarget_, bumpedTenors_, calcGamma_, null);
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }


    protected void BaseRateSensitivities()
    {
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.RateSensitivities(engine, netting_, upBump_, downBump_, bumpRelative_, bumpType_,
                                                          quoteTarget_, bumpedTenors_, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }


    protected void BaseFxSensitivities()
    {
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.FxSensitivities(engine, netting_, upBump_, downBump_, bumpRelative_,
                                                        BumpType.Parallel, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }

    protected void BaseSpotSensitivities()
    {
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.SpotSensitivities(engine, netting_, upBump_, downBump_, bumpRelative_,
                                                        BumpType.Parallel, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }


    protected void BaseCreditVolSensitivities()
    {
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.CreditVolatilitiesSensitivities(engine, netting_, 0.05, 0.05, true, bumpType_,
                                                                        bumpedTenors_, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }


    protected void BaseRateVolSensitivities()
    {
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.RateVolatilitiesSensitivities(engine, netting_, 0.05, 0.05, true, bumpType_,
                                                                      bumpedTenors_, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }


    protected void BaseFxVolSensitivities()
    {
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.FxVolatilitiesSensitivities(engine, netting_, 0.05, 0.05, true, bumpType_,
                                                                    bumpedTenors_, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }

    protected void BaseSpotVolSensitivities()
    {
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.SpotVolatilitiesSensitivities(engine, netting_, 0.05, 0.05, true, bumpType_,
                                                                    bumpedTenors_, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }


    protected void BaseCreditFactorSensitivities()
    {
      int nFactors = input_.FactorLoadings.FactorCount;
      var upBump = new double[nFactors];
      var downBump = new double[nFactors];
      for (int i = 0; i < nFactors; ++i)
      {
        double bump = (i%2 == 0) ? 5 : -5;
        upBump[i] = bump;
        downBump[i] = bump;
      }
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.CreditFactorsSensitivities(engine, netting_, upBump, downBump, false, bumpType_,
                                                                   calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }


    protected void BaseRateFactorSensitivities()
    {
      int nFactors = input_.FactorLoadings.FactorCount;
      var upBump = new double[nFactors];
      var downBump = new double[nFactors];
      for (int i = 0; i < nFactors; ++i)
      {
        double bump = (i%2 == 0) ? 5 : -5;
        upBump[i] = bump;
        downBump[i] = bump;
      }
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.RateFactorsSensitivities(engine, netting_, upBump, downBump, false, bumpType_,
                                                                 calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }


    protected void BaseFxFactorSensitivities()
    {
      int nFactors = input_.FactorLoadings.FactorCount;
      var upBump = new double[nFactors];
      var downBump = new double[nFactors];
      for (int i = 0; i < nFactors; ++i)
      {
        double bump = (i%2 == 0) ? 5 : -5;
        upBump[i] = bump;
        downBump[i] = bump;
      }
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.FxFactorsSensitivities(engine, netting_, upBump, downBump, false, bumpType_,
                                                               calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }

    protected void BaseSpotFactorSensitivities()
    {
      int nFactors = input_.FactorLoadings.FactorCount;
      var upBump = new double[nFactors];
      var downBump = new double[nFactors];
      for (int i = 0; i < nFactors; ++i)
      {
        double bump = (i % 2 == 0) ? 5 : -5;
        upBump[i] = bump;
        downBump[i] = bump;
      }
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.SpotFactorsSensitivities(engine, netting_, upBump, downBump, false, bumpType_,
                                                               calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }

    protected void BaseFwdSensitivities()
    {
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.ForwardPriceSensitivities(engine, netting_, upBump_, downBump_, bumpRelative_,
                                                                  bumpType_,
                                                                  quoteTarget_, bumpedTenors_, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }


    protected void BaseFwdVolSensitivities()
    {
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.ForwardPriceVolatilitiesSensitivities(engine, netting_, 0.05, 0.05, true,
                                                                              bumpType_, bumpedTenors_, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }


    protected void BaseFwdFactorSensitivities()
    {
      int nFactors = input_.FactorLoadings.FactorCount;
      var upBump = new double[nFactors];
      var downBump = new double[nFactors];
      for (int i = 0; i < nFactors; ++i)
      {
        double bump = (i%2 == 0) ? 5 : -5;
        upBump[i] = bump;
        downBump[i] = bump;
      }
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input_);
      DataTable dataTable = Simulations.ForwardPriceFactorsSensitivities(engine, netting_, upBump, downBump, false,
                                                                         bumpType_, calcGamma_, null);
      timer.Stop();
      ResultData rd = ToResultData(dataTable, calcGamma_, timer.Elapsed);
      MatchExpects(rd);
    }

    protected void BaseTestSensitivityVsBumping(Input input)
    {

      var timer = new Timer();
      timer.Start();
      var engine = CreateEngine(input);
      engine.Execute();
      var rt = Simulations.RateSensitivities(engine, netting_, 0.01, 0.0, true, BumpType.Parallel,
                                             QuotingConvention.None, null, false, null);
      var ct = Simulations.CreditSpreadSensitivities(engine, netting_, 0.01, 0.0, true,
                                                     BumpType.Parallel, QuotingConvention.None, null, false, null);
      var fxt = Simulations.FxSensitivities(engine, netting_, 0.01, 0.0, true,
                                            BumpType.Parallel, false, null);
      var ft = Simulations.ForwardPriceSensitivities(engine, netting_, 0.01, 0.0, true,
                                                     BumpType.Parallel, QuotingConvention.None, null, false, null);
      var st = Simulations.SpotSensitivities(engine, netting_, 0.01, 0.0, true, BumpType.Parallel, false, null);
      var cva = engine.GetMeasure(CCRMeasure.CVA, netting_, Dt.Empty, 0.0);
      if (rt.Rows.Count != 0)
      {
        var dc = input.DiscountCurves;
        var bumpedCva = dc.Select((d, i) =>
                                  {
                                    var original = CloneUtil.Clone(d);
                                    var flags = BumpFlags.BumpRelative;
                                    var dependentCurves = d.DependentCurves;
                                    d.DependentCurves = new Dictionary<long, CalibratedCurve>();
                                    //empty the dependent curve list
                                    new CalibratedCurve[] {d}.BumpQuotes(new string[0], QuotingConvention.None, 0.01,
                                                                         flags | BumpFlags.RefitCurve);
                                    d.DependentCurves = dependentCurves;
                                    var bumpedEng = CreateEngine(input);
                                    bumpedEng.Execute();
                                    var retVal = bumpedEng.GetMeasure(CCRMeasure.CVA, netting_, Dt.Empty, 0.0);
                                    new Curve[] {dc[i]}.CurveSet(new Curve[] {original});
                                    return retVal;
                                  }).ToArray();
        var deltas = Array.ConvertAll(bumpedCva, bumped => bumped - cva);
        var names = Array.ConvertAll(dc, d => d.Name);
        for (int i = 0; i < rt.Rows.Count; ++i)
        {
          var row = rt.Rows[i];
          if ((string)row["Measure"] == "CVA")
          {
            var name = (string)row["InputName"];
            var idx = Array.IndexOf(names, name);
            if (idx >= 0)
              Assert.AreEqual(deltas[idx], (double)row["Delta"], 1e-6);
          }
        }
      }
      if (ct.Rows.Count != 0)
      {
        var cc = input.CreditCurves;
        var bumpedCva = cc.Select((c, i) =>
                                  {
                                    var original = CloneUtil.Clone(c);
                                    var flags = BumpFlags.BumpRelative;
                                    var dependentCurves = c.DependentCurves;
                                    c.DependentCurves = new Dictionary<long, CalibratedCurve>();
                                    //empty the dependent curve list
                                    new CalibratedCurve[] {cc[i]}.BumpQuotes(new string[0], QuotingConvention.None, 0.01,
                                                                             flags | BumpFlags.RefitCurve);
                                    cc[i].DependentCurves = dependentCurves;
                                    var bumpedEng = CreateEngine(input);
                                    bumpedEng.Execute();
                                    var retVal = bumpedEng.GetMeasure(CCRMeasure.CVA, netting_, Dt.Empty, 0.0);
                                    new Curve[] {cc[i]}.CurveSet(new Curve[] {original});
                                    return retVal;
                                  }).ToArray();
        var deltas = Array.ConvertAll(bumpedCva, bumped => bumped - cva);
        var names = Array.ConvertAll(cc, c => c.Name);
        for (int i = 0; i < ct.Rows.Count; ++i)
        {
          var row = ct.Rows[i];
          if ((string)row["Measure"] == "CVA")
          {
            var name = (string)row["InputName"];
            var idx = Array.IndexOf(names, name);
            if (idx >= 0)
              Assert.AreEqual(deltas[idx], (double)row["Delta"], 1e-6);
          }
        }
      }
      if (fxt.Rows.Count != 0)
      {
        var fx = input.FxRates;
        var bumpedCva = fx.Select((f, i) =>
                                  {
                                    var original = CloneUtil.Clone(f);
                                    var c = new VolatilityCurve(f.Spot, f.GetRate(currencies_[i + 1], currencies_[0]));
                                    var flags = BumpFlags.BumpRelative;
                                    //empty the dependent curve list
                                    new CalibratedCurve[] {c}.BumpQuotes(new string[0], QuotingConvention.None, 0.01,
                                                                         flags | BumpFlags.RefitCurve);
                                    fx[i].SetRate(currencies_[i + 1], currencies_[0], c.GetVal(0));
                                    var bumpedEng = CreateEngine(input);
                                    bumpedEng.Execute();
                                    var retVal = bumpedEng.GetMeasure(CCRMeasure.CVA, netting_, Dt.Empty, 0.0);
                                    fx[i].SetRate(currencies_[i + 1], currencies_[0],
                                                  original.GetRate(currencies_[i + 1], currencies_[0]));
                                    return retVal;
                                  }).ToArray();
        var deltas = Array.ConvertAll(bumpedCva, bumped => bumped - cva);
        var names = Array.ConvertAll(fx, f => string.Concat(Enum.GetName(typeof(Currency), f.FromCcy), Enum.GetName(typeof(Currency), f.ToCcy)));
        for (int i = 0; i < fxt.Rows.Count; ++i)
        {
          var row = fxt.Rows[i];
          if ((string)row["Measure"] == "CVA")
          {
            var name = (string)row["InputName"];
            var idx = Array.IndexOf(names, name);
            if (idx >= 0)
              Assert.AreEqual(deltas[idx], (double)row["Delta"], 1e-6);
          }
        }
      }
      if (ft.Rows.Count != 0)
      {
        var fc = input.FwdCurves;
        var bumpedCva = fc.Select((f, i) =>
                                  {
                                    var original = CloneUtil.Clone(f);
                                    var flags = BumpFlags.BumpRelative;
                                    var dependentCurves = fc[i].DependentCurves;
                                    f.DependentCurves = new Dictionary<long, CalibratedCurve>();
                                    //empty the dependent curve list
                                    new[] {f}.BumpQuotes(new string[0], QuotingConvention.None, 0.01,
                                                         flags | BumpFlags.RefitCurve);
                                    f.DependentCurves = dependentCurves;
                                    var bumpedEng = CreateEngine(input);
                                    bumpedEng.Execute();
                                    var retVal = bumpedEng.GetMeasure(CCRMeasure.CVA, netting_, Dt.Empty, 0.0);
                                    new Curve[] {f}.CurveSet(new Curve[] {original});
                                    return retVal;
                                  }).ToArray();
        var deltas = Array.ConvertAll(bumpedCva, bumped => bumped - cva);
        var names = Array.ConvertAll(fc, c => c.Name);
        for (int i = 0; i < ft.Rows.Count; ++i)
        {
          var row = ft.Rows[i];
          if ((string)row["Measure"] == "CVA")
          {
            var name = (string)row["InputName"];
            var idx = Array.IndexOf(names, name);
            if (idx >= 0)
              Assert.AreEqual(deltas[idx], (double)row["Delta"], 1e-6);
          }
        }
      }
      if (st.Rows.Count != 0)
      {
        var spot =
          input.FwdCurves.OfType<IForwardPriceCurve>().Select(c => c.Spot).Where(
            sp => input.Volatilities.References.Contains(sp) && input.FactorLoadings.References.Contains(sp)).ToArray();
        var bumpedCva = spot.Select((f, i) =>
                                    {
                                      var original = f.CloneObjectGraph();
                                      var c = new VolatilityCurve(f.Spot, f.Value);
                                      var flags = BumpFlags.BumpRelative;
                                      //empty the dependent curve list
                                      new CalibratedCurve[] {c}.BumpQuotes(new string[0], QuotingConvention.None, 0.01,
                                                                           flags | BumpFlags.RefitCurve);
                                      spot[i].Value = c.GetVal(0);
                                      var bumpedEng = CreateEngine(input);
                                      bumpedEng.Execute();
                                      var retVal = bumpedEng.GetMeasure(CCRMeasure.CVA, netting_, Dt.Empty, 0.0);
                                      spot[i].Value = original.Value;
                                      return retVal;
                                    }).ToArray();
        var deltas = Array.ConvertAll(bumpedCva, bumped => bumped - cva);
        var names = Array.ConvertAll(spot, s => s.Name);
        for (int i = 0; i < st.Rows.Count; ++i)
        {
          var row = st.Rows[i];
          if ((string)row["Measure"] == "CVA")
          {
            var name = (string)row["InputName"];
            var idx = Array.IndexOf(names, name);
            if (idx >= 0)
              Assert.AreEqual(deltas[idx], (double)row["Delta"], 1e-6);
          }
        }
      }
    }

    protected void BaseTestHedgeNotionals(Input input)
    {
      var timer = new Timer();
      timer.Start();
      input.SimulDates = input.SimulDates.Union(new[] {Dt.AddWeeks(input.AsOf, 1, CycleRule.None)}).ToArray();
      input.Sample = 1000;
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input);
      var hedgePricers = CreatePricers(1.0, 1.0, 1.0, 1.0, false, input.DiscountCurves, input.FxRates, input.CreditCurves, input.FwdCurves);
      var dt = Simulations.CvaHedgeNotionals(engine, Dt.Empty, netting_,
                                             hedgePricers.Where(p => !(p is CDSCashflowPricer) || input.Cpty[0] == (((CDSCashflowPricer)p).SurvivalCurve) || input.Cpty[1] == (((CDSCashflowPricer)p).SurvivalCurve)).
                                               ToArray(), null, null, null);
      timer.Stop();
      ResultData retVal = HedgesToResultData(dt, timer.Elapsed);
      MatchExpects(retVal);
    }

    protected void MakeNormalVols(Input input)
    {
      if (input.DiscountCurves.Any(input.Volatilities.References.Contains))
      {
        var dc = input.DiscountCurves.First(input.Volatilities.References.Contains);
        var liborVols = input.Volatilities.GetVolsAt(dc);
        for (int i = 0; i < liborVols.Length; ++i)
        {
          var t0 = (i == 0) ? asOf_ : input.TenorDates[i - 1];
          var t1 = input.TenorDates[i];
          var f = dc.F(t0, t1);
          var volCurve = liborVols[i];
          var vols = volCurve.Tenors.Select(t => new Tuple<Dt, double>(t.CurveDate, t.OriginalQuote.Value));
          volCurve.Clear();
          volCurve.DistributionType = DistributionType.Normal;
          foreach (var tuple in vols)
            volCurve.Add(tuple.Item1, tuple.Item2 * f);
        }
      }
    }


    protected void BaseTestTowerPropertyNormalRates(Input input)
    {
      if (input == null)
      {
        string[] id;
        IPricer[] pricers = CreateBondPricers(out id);
        input = CloneUtil.Clone(input_);
        input.Pricers = pricers;
        input.Names = id;
        MakeNormalVols(input);
      }
      BaseTestTowerProperty(input);
    }


    protected void BaseTestTowerProperty(Input input)
    {

      if (input == null)
      {
        string[] id;
        IPricer[] pricers = CreateBondPricers(out id);
        input = CloneUtil.Clone(input_);
        input.Pricers = pricers;
        input.Names = id;
      }
      var netting = new Netting(input.Names, input.Names, null);
      var timer = new Timer();
      timer.Start();
      ICounterpartyCreditRiskCalculations engine = CreateEngine(input);
      engine.Execute();
      DataTable dt = FormatResultsZero(engine, netting);
      timer.Stop();
      ResultData retVal = ToResultData(dt, timer.Elapsed);
      MatchExpects(retVal);
    }

    #endregion
   
    #region Utils
    protected IRunSimulationPath CreatePathwiseEngine(Input input, IList<double[,]> precalculatedPvs)
    {
      return Simulations.CreatePathSimulator(MultiStreamRng.Type.MersenneTwister, input.AsOf, input.Pricers, input.Names, netting_, input.Cpty, input.CptyRec,
                                             input.TenorDates, input.DiscountCurves, input.FxRates, input.FwdCurves, input.CreditCurves, 
                                             input.Volatilities, input.FactorLoadings, -100.0, input.Sample,
                                             input.SimulDates, precalculatedPvs, input.GridSize, false);
    }

    protected ResultData ToCalibrationResultData(FactorLoadingCollection fl, VolatilityCollection volatilities, double timeUsed)
    {
      var flTable = fl.ToDataTable<object>(false);
      var volTable = volatilities.ToDataTable<object>();
      int fcols = flTable.Columns.Count;
      int frows = flTable.Rows.Count;
      int vcols = volTable.Columns.Count;
      int vrows = volTable.Rows.Count;
      var labels = new string[(fcols-1)*frows + (vcols-2)*vrows];
      var res = new double[labels.Length];
      int count = 0;
      for (int i = 0; i < frows; i++)
      {
        DataRow row = flTable.Rows[i];
        var r = row["References"];
        for(int j = 1; j < fcols; ++j)
        {
          labels[count] = string.Format("{0}.{1}", r, flTable.Columns[j].ColumnName);
          res[count] = (double)row[flTable.Columns[j].ColumnName];
          ++count;
        }
      }
      for (int i = 0; i < vrows; i++)
      {
        DataRow row = volTable.Rows[i];
        var r = row["References"];
        for(int j = 1; j < vcols - 1; ++j)
        {
          labels[count] = string.Format("{0}.{1}.{2}", r, volTable.Columns[j].ColumnName, volTable.Columns["Dt"]);
          var v = row[volTable.Columns[j].ColumnName];
          res[count] = v is double ? (double)v : 0.0;
          ++count;
        }
      }
      ResultData rd = LoadExpects();
      rd.Accuracy = 1e-6;
      if (rd.Results.Length == 0 || rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[1];
        rd.Results[0] = new ResultData.ResultSet();
      }
      {
        rd.Results[0].Name = "CalibrationResults";
        rd.Results[0].Labels = labels;
        rd.Results[0].Actuals = res;
      }
      return rd;
    }

    protected ResultData ToResultData(DataTable dataTable, double timeUsed)
    {
      int rows = dataTable.Rows.Count;
      var labels = new string[rows];
      var vals = new double[rows];
      for (int i = 0; i < rows; i++)
      {
        var row = dataTable.Rows[i];
        labels[i] = string.Format("{0}.{1}", (string)row["Measure"], (string)row["Tenor"]);
        vals[i] = (double)row["Value"];
      }
      var rd = LoadExpects();
      rd.Accuracy = 1e-6;
      if (rd.Results.Length == 0 || rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[1];
        rd.Results[0] = new ResultData.ResultSet();
      }
      {
        rd.Results[0].Name = "RiskMeasures";
        rd.Results[0].Labels = labels;
        rd.Results[0].Actuals = vals;
      }
      rd.TimeUsed = timeUsed;
      return rd;
    }

    protected ResultData ToResultData(DataTable expects, DataTable actual, double timeUsed)
    {
      int rows = expects.Rows.Count;
      var labels = new string[rows];
      var expectedVals = new double[rows];
      var actualVals = new double[rows];

      for (int i = 0; i < rows; i++)
      {
        var expectsRow = expects.Rows[i];
        var actualRow = actual.Rows[i];
        labels[i] = string.Format("{0}.{1}", (string)expectsRow["Measure"], (string)expectsRow["Tenor"]);
        expectedVals[i] = (double)expectsRow["Value"];
        actualVals[i] = (double)actualRow["Value"];
      }
      var rd = new ResultData(actualVals, labels, timeUsed);
      rd.Accuracy = 1e-6;
      if (rd.Results.Length == 0 || rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[1];
        rd.Results[0] = new ResultData.ResultSet();
      }
      rd.Results[0].Name = "RiskMeasures";
      rd.Results[0].Labels = labels;
      rd.Results[0].Expects = expectedVals;
      rd.Results[0].Actuals = actualVals;
      rd.TimeUsed = timeUsed;
      return rd;
    }


    protected ResultData ToResultData(DataTable expects, DataTable actual, bool calcGamma, double timeUsed)
    {
      int cols = calcGamma ? 2 : 1;
      int rows = expects.Rows.Count;
      var labels = new string[rows];
      var expectedDeltas = new double[rows];
      var expectedGammas = new double[rows];
      var actualDeltas = new double[rows];
      var actualGammas = new double[rows];

      for (int i = 0; i < rows; i++)
      {
        var expectsRow = expects.Rows[i];
        var actualsRow = actual.Rows[i];
        labels[i] = string.Format("{0}.{1}.{2}", (string)expectsRow["Measure"], (string)expectsRow["InputName"], (string)expectsRow["Tenor"]);
        expectedDeltas[i] = (double)expectsRow["Delta"];
        actualDeltas[i] = (double)actualsRow["Delta"];
        if (calcGamma)
        {
          expectedGammas[i] = (double)expectsRow["Gamma"];
          actualGammas[i] = (double)actualsRow["Gamma"];
        }
      }
      var rd = new ResultData(expects, timeUsed);
      rd.Accuracy = 1e-6;
      if (rd.Results.Length == 0 && rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[cols];
        for (int j = 0; j < cols; ++j)
          rd.Results[j] = new ResultData.ResultSet();
      }

      rd.Results[0].Name = "Delta";
      rd.Results[0].Labels = labels;
      rd.Results[0].Expects = expectedDeltas;
      rd.Results[0].Actuals = actualDeltas;

      if (calcGamma)
      {
        rd.Results[1].Name = "Gamma";
        rd.Results[1].Labels = labels;
        rd.Results[1].Expects = expectedGammas;
        rd.Results[1].Actuals = actualGammas;
      }
      rd.TimeUsed = timeUsed;
      return rd;
    }

    protected ResultData ToResultData(DataTable dataTable, bool calcGamma, double timeUsed)
    {
      int cols = calcGamma ? 2 : 1;
      int rows = dataTable.Rows.Count;
      var labels = new string[rows];
      var deltas = new double[rows];
      var gammas = new double[rows];
      for (int i = 0; i < rows; i++)
      {
        DataRow row = dataTable.Rows[i];
        labels[i] = string.Format("{0}.{1}.{2}", (string)row["Measure"], (string)row["InputName"], (string)row["Tenor"]);
        deltas[i] = (double)row["Delta"];
        if (calcGamma)
          gammas[i] = (double)row["Gamma"];
      }
      ResultData rd = LoadExpects();
      rd.Accuracy = 1e-6;
      if (rd.Results.Length == 0 || rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[cols];
        for (int j = 0; j < cols; ++j)
          rd.Results[j] = new ResultData.ResultSet();
      }
      {
        rd.Results[0].Name = "Delta";
        rd.Results[0].Labels = labels;
        rd.Results[0].Actuals = deltas;
      }
      if (calcGamma)
      {
        rd.Results[1].Name = "Gamma";
        rd.Results[1].Labels = labels;
        rd.Results[1].Actuals = gammas;
      }
      rd.TimeUsed = timeUsed;
      return rd;
    }

    private ResultData HedgesToResultData(DataTable dataTable, double timeUsed)
    {
      int rows = dataTable.Rows.Count;
      var labels = new string[rows];
      var values = new double[rows];
      for (int i = 0; i < rows; i++)
      {
        DataRow row = dataTable.Rows[i];
        labels[i] = (string)row["InstrumentName"];
        values[i] = (double)row["HedgeNotional"];
      }
      ResultData rd = LoadExpects();
      rd.Accuracy = 1e-6;
      if (rd.Results.Length == 0 || rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[1];
        rd.Results[0] = new ResultData.ResultSet();
      }
      {
        rd.Results[0].Name = "HedgeNotionals";
        rd.Results[0].Labels = labels;
        rd.Results[0].Actuals = values;
      }
      rd.TimeUsed = timeUsed;
      return rd;
    }


    private int IndexOf(IEnumerable<DiscountCurve> discountCurves, Currency ccy)
    {
      int i = 0;
      foreach (DiscountCurve curve in discountCurves)
      {
        if (curve.Ccy == ccy)
          return i;
        ++i;
      }
      return -1;
    }

    private static DiscountCurve CreateDiscountCurve(Dt asOf, string tenor,
                                                     Dt[] tenorDates, double[] rates,
                                                     Currency ccy)
    {
      int n = tenorDates.Length;
      var discounts = new double[n];
      var fractions = new double[n];
      // Create discount curve from the rates_
      var calibrator = new DiscountRateCalibrator(asOf, asOf);
      var dcurve = new DiscountCurve(calibrator);
      dcurve.Interp = InterpFactory.FromMethod(
        InterpMethod.Weighted, ExtrapMethod.Const);
      DayCount dc = DayCount.Actual365Fixed;
      dcurve.Ccy = ccy;
      dcurve.Name = String.Format("{0}Libor{1}", ccy, tenor);
      Dt reset = asOf;
      double df = 1;
      for (int i = 0; i < n; ++i)
      {
        Dt maturity = tenorDates[i];
        double frac = fractions[i] =
                      Dt.Fraction(reset, maturity, dc);
        discounts[i] = (df /= 1 + frac*rates[i]);
        dcurve.AddZeroYield(maturity, RateCalc.RateFromPrice(
                                        df, asOf, maturity, dc, Frequency.None), dc, Frequency.None);
        reset = maturity;
      }
      dcurve.Fit();
      return dcurve;
    }


    protected static RateVolatilityCube GetCapletVolCube(Dt asOf, InterestRateIndex index, DiscountCurve dc, string[] capTenors, double[] capVols, VolatilityType volatilityType)
    {

      var calibratedTenors = capTenors.Select(t => Dt.Add(asOf, t)).ToArray();
      var strikes = calibratedTenors.Select((dt, i) => RateVolatilityCalibrator.CalculateSwapRate(dc, asOf, dt, index)).ToArray();
      var calibrator = new RateVolatilityATMCapCalibrator(asOf, asOf, dc, dt => index, dt => dc, strikes, calibratedTenors, capVols, 100.0, volatilityType);
      var volCube = new RateVolatilityCube(calibrator);
      volCube.Fit();
      return volCube;
    }

    protected static VolatilityCurve GetVolatilityCurve(string name, Dt asOf, string[] tenors, double[] vols)
    {
      var dates = tenors.Select(t => Dt.Add(asOf, t)).ToArray();
      var retVal = new VolatilityCurve(asOf) {Name = name};
      for (int i = 0; i < dates.Length; ++i)
        retVal.AddVolatility(dates[i], vols[i]);
      retVal.Fit();
      return retVal;
    }

    protected static SwaptionVolatilityCube GetMarketSwaptionVolatility(Dt asOf, DiscountCurve discountCurve)
    {
      string[] swaptionExpiries = {"1M", "2M", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "15Y", "20Y", "25Y", "30Y"};
      string[] swaptionTenors = {"1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y", "15Y", "20Y", "25Y", "30Y"};
      double[,] swaptionMatrix =
        {
          {
            0.430708872, 0.455326866, 0.425945933, 0.384810189, 0.353923112, 0.326822749, 0.305934322,
            0.289860774, 0.276978215, 0.267430958, 0.232805641, 0.220272702, 0.215261865, 0.212760352
          },
          {
            0.439169866, 0.448798476, 0.413919093, 0.374039522, 0.344152407, 0.318057608, 0.297918355,
            0.281849479, 0.269965394, 0.260420518, 0.228052251, 0.216021487, 0.210762142, 0.208011553
          },
          {
            0.451864305, 0.447776664, 0.405398405, 0.3662746, 0.336387182, 0.310990168, 0.290406335,
            0.274840854, 0.263455162, 0.253912903, 0.222801017, 0.211771487, 0.205764049, 0.20276429
          },
          {
            0.523521085, 0.454468615, 0.410319227, 0.363935156, 0.327331448, 0.300939131, 0.280364399,
            0.265472369, 0.253422429, 0.244385291, 0.216288265, 0.205264261, 0.200258403, 0.197260184
          },
          {
            0.583342086, 0.461290897, 0.402919182, 0.349808239, 0.308423626, 0.282846658, 0.263952492,
            0.248900663, 0.236364965, 0.227337328, 0.204266333, 0.195249733, 0.19024876, 0.187752668
          },
          {
            0.475179571, 0.385274819, 0.335897171, 0.296499721, 0.265423109, 0.246865722, 0.232824841,
            0.221298382, 0.212778154, 0.205757998, 0.189726942, 0.183221897, 0.179726536, 0.177234988
          },
          {
            0.373087036, 0.308982317, 0.274898656, 0.24783934, 0.226298109, 0.212768659, 0.203250026,
            0.195237057, 0.188722571, 0.183719551, 0.173207491, 0.168211795, 0.166219789, 0.164230795
          },
          {
            0.295106304, 0.251820555, 0.228775268, 0.21074376, 0.196226296, 0.187715029, 0.180208217,
            0.175196622, 0.17019842, 0.166696239, 0.159698312, 0.156707764, 0.154220856, 0.153232456
          },
          {
            0.241723622, 0.212227595, 0.196704349, 0.184692394, 0.175190458, 0.169186933, 0.164179282,
            0.160183407, 0.157183287, 0.155179903, 0.150193647, 0.147209675, 0.145723979, 0.144238108
          },
          {
            0.174933532, 0.163160533, 0.15616798, 0.150672489, 0.147166427, 0.144674101, 0.142676109,
            0.141174342, 0.140183084, 0.139189444, 0.135708953, 0.133726974, 0.13174423, 0.129759396
          },
          {
            0.134531876, 0.132169004, 0.131185289, 0.129688969, 0.129683882, 0.129197192, 0.130202125,
            0.130206318, 0.130208467, 0.130209263, 0.127734364, 0.12525587, 0.123273902, 0.119790369
          },
          {
            0.128286136, 0.129261767, 0.128756955, 0.128751285, 0.128745934, 0.129252248, 0.12875797,
            0.128761141, 0.128763263, 0.128764698, 0.126784933, 0.123305425, 0.119820759, 0.116330322
          },
          {
            0.128864165, 0.129292501, 0.128791535, 0.12779142, 0.128288892, 0.128794026, 0.129297484,
            0.129799935, 0.129802477, 0.130303779, 0.12732441, 0.122840376, 0.118349159, 0.114356476
          },
          {
            0.130955098, 0.132323138, 0.132822597, 0.132323383, 0.132822831, 0.132828092, 0.132832172,
            0.132835475, 0.132838282, 0.132840731, 0.128355071, 0.123361154, 0.117868118, 0.11237305
          },
          {
            0.131560058, 0.13535636, 0.134858093, 0.134359806, 0.134360978, 0.133862647, 0.133364291,
            0.133365407, 0.133366505, 0.132368582, 0.126370513, 0.120375538, 0.114378797, 0.109382282
          }
        };
      var skewStrikes = new[] {-0.01, -0.005, 0.0, 0.005, 0.01};
      var logNormalVolCubeStrikeSkews = new double[swaptionExpiries.Length * swaptionTenors.Length,skewStrikes.Length];
      for (int idx = 0; idx < swaptionExpiries.Length * swaptionTenors.Length; idx++)
      {
        for (int jdx = 0; jdx < skewStrikes.Length; jdx++)
        {
          logNormalVolCubeStrikeSkews[idx, jdx] = skewStrikes[jdx] * (idx * skewStrikes.Length + jdx) / 100.0;
        }
      }
      return SwaptionVolatilityCube.CreateSwaptionMarketCube(asOf, discountCurve, swaptionExpiries, swaptionTenors, swaptionMatrix,
                                                             new InterestRateIndex("LIBOR3M", Frequency.Quarterly, Currency.USD, DayCount.Actual360,
                                                                                   Calendar.NYB, 2),
                                                             VolatilityType.LogNormal,
                                                             swaptionExpiries, skewStrikes, swaptionTenors, logNormalVolCubeStrikeSkews, null, null,
                                                             DayCount.Thirty360,
                                                             BDConvention.Modified, Frequency.SemiAnnual, Calendar.NYB, 2);
    }


    protected static BgmForwardVolatilitySurface GetBgmForwardVolatilitySurface(Dt asOf, DiscountCurve discountCurve)
    {
      string[] swaptionExpiries = {"1M", "2M", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "15Y", "20Y", "25Y", "30Y"};
      string[] swaptionTenors = {"1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y", "15Y", "20Y", "25Y", "30Y"};
      double[,] swaptionMatrix =
        {
          {
            0.430708872, 0.455326866, 0.425945933, 0.384810189, 0.353923112, 0.326822749, 0.305934322,
            0.289860774, 0.276978215, 0.267430958, 0.232805641, 0.220272702, 0.215261865, 0.212760352
          },
          {
            0.439169866, 0.448798476, 0.413919093, 0.374039522, 0.344152407, 0.318057608, 0.297918355,
            0.281849479, 0.269965394, 0.260420518, 0.228052251, 0.216021487, 0.210762142, 0.208011553
          },
          {
            0.451864305, 0.447776664, 0.405398405, 0.3662746, 0.336387182, 0.310990168, 0.290406335,
            0.274840854, 0.263455162, 0.253912903, 0.222801017, 0.211771487, 0.205764049, 0.20276429
          },
          {
            0.523521085, 0.454468615, 0.410319227, 0.363935156, 0.327331448, 0.300939131, 0.280364399,
            0.265472369, 0.253422429, 0.244385291, 0.216288265, 0.205264261, 0.200258403, 0.197260184
          },
          {
            0.583342086, 0.461290897, 0.402919182, 0.349808239, 0.308423626, 0.282846658, 0.263952492,
            0.248900663, 0.236364965, 0.227337328, 0.204266333, 0.195249733, 0.19024876, 0.187752668
          },
          {
            0.475179571, 0.385274819, 0.335897171, 0.296499721, 0.265423109, 0.246865722, 0.232824841,
            0.221298382, 0.212778154, 0.205757998, 0.189726942, 0.183221897, 0.179726536, 0.177234988
          },
          {
            0.373087036, 0.308982317, 0.274898656, 0.24783934, 0.226298109, 0.212768659, 0.203250026,
            0.195237057, 0.188722571, 0.183719551, 0.173207491, 0.168211795, 0.166219789, 0.164230795
          },
          {
            0.295106304, 0.251820555, 0.228775268, 0.21074376, 0.196226296, 0.187715029, 0.180208217,
            0.175196622, 0.17019842, 0.166696239, 0.159698312, 0.156707764, 0.154220856, 0.153232456
          },
          {
            0.241723622, 0.212227595, 0.196704349, 0.184692394, 0.175190458, 0.169186933, 0.164179282,
            0.160183407, 0.157183287, 0.155179903, 0.150193647, 0.147209675, 0.145723979, 0.144238108
          },
          {
            0.174933532, 0.163160533, 0.15616798, 0.150672489, 0.147166427, 0.144674101, 0.142676109,
            0.141174342, 0.140183084, 0.139189444, 0.135708953, 0.133726974, 0.13174423, 0.129759396
          },
          {
            0.134531876, 0.132169004, 0.131185289, 0.129688969, 0.129683882, 0.129197192, 0.130202125,
            0.130206318, 0.130208467, 0.130209263, 0.127734364, 0.12525587, 0.123273902, 0.119790369
          },
          {
            0.128286136, 0.129261767, 0.128756955, 0.128751285, 0.128745934, 0.129252248, 0.12875797,
            0.128761141, 0.128763263, 0.128764698, 0.126784933, 0.123305425, 0.119820759, 0.116330322
          },
          {
            0.128864165, 0.129292501, 0.128791535, 0.12779142, 0.128288892, 0.128794026, 0.129297484,
            0.129799935, 0.129802477, 0.130303779, 0.12732441, 0.122840376, 0.118349159, 0.114356476
          },
          {
            0.130955098, 0.132323138, 0.132822597, 0.132323383, 0.132822831, 0.132828092, 0.132832172,
            0.132835475, 0.132838282, 0.132840731, 0.128355071, 0.123361154, 0.117868118, 0.11237305
          },
          {
            0.131560058, 0.13535636, 0.134858093, 0.134359806, 0.134360978, 0.133862647, 0.133364291,
            0.133365407, 0.133366505, 0.132368582, 0.126370513, 0.120375538, 0.114378797, 0.109382282
          }
        };

      var par = new BgmCalibrationParameters
                {
                  CalibrationMethod = VolatilityBootstrapMethod.Cascading,
                  Tolerance = 0.0001,
                  PsiUpperBound = 1.0,
                  PsiLowerBound = 0.0,
                  PhiUpperBound = 1.002,
                  PhiLowerBound = 0.9
                };
      BgmCorrelation correlations = BgmCorrelation.CreateBgmCorrelation(BgmCorrelationType.PerfectCorrelation,
                                                                        swaptionExpiries.Length, new double[0,0]);
      return BgmForwardVolatilitySurface.Create(
        asOf, par, discountCurve, swaptionExpiries, swaptionTenors, CycleRule.None,
        BDConvention.None, Calendar.None, correlations,
        swaptionMatrix, DistributionType.LogNormal);
    }


    protected VolatilityCurve GetVolCurve(string id, string[] expiries, double initial, double slope)
    {
      double[] impliedVols = Array.ConvertAll(expiries, e =>
                                                        {
                                                          double t = Dt.FractDiff(asOf_, Dt.Add(asOf_,e)) / 365.0;
                                                          return initial * Math.Exp(slope * t);
                                                        });
      var retVal = new VolatilityCurve(asOf_){Name = String.Concat(id, "Vol")};
      for (int i = 0; i < expiries.Length; ++i)
        retVal.AddVolatility(Dt.Add(asOf_, expiries[i]), impliedVols[i]);
      retVal.Fit();
      return retVal;
    }
    #endregion
  }
}
