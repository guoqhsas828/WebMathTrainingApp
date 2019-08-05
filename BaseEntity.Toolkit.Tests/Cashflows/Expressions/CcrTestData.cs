//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Cashflows.Expressions
{
  internal class CcrTestData
  {
    #region Data

    /// <exclude></exclude>
    private Dt _asOf = new Dt(15, 12, 2010);

    /// <exclude></exclude>
    private int[] _cptyIndex = { 0, 1 }; // {0, 1, 2, 3}

    /// <exclude></exclude>
    private double _cptyDefaultTimeCorrelation = 0.65;

    /// <exclude></exclude>
    private double[] _cptyRec = { 0.4, 0.5 };

    /// <exclude></exclude>
    internal Currency[] Currencies = { Currency.USD, Currency.EUR, Currency.JPY };

    /// <exclude></exclude>
    private DayCount[] _dayCounts =
    {
      DayCount.Actual360, DayCount.Actual360,
      DayCount.Actual360
    };

    /// <exclude></exclude>
    private BDConvention[] _roll =
    {
      BDConvention.Modified, BDConvention.Modified,
      BDConvention.Modified
    };

    /// <exclude></exclude>
    private Frequency[] _swapFrequency =
    {
      Frequency.Quarterly, Frequency.SemiAnnual,
      Frequency.Quarterly
    };

    /// <exclude></exclude>
    private string[] _indexTenor = { "3M", "6M", "3M" };

    /// <exclude></exclude>
    private string[] _basisSwapIndexTenor = { "6M", "1Y", "6M" };

    /// <exclude></exclude>
    private Calendar[] _calendar = { Calendar.NYB, Calendar.TGT, Calendar.TKB };

    /// <exclude></exclude>
    private string[] _tenors =
    {
      "1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y",
      "10Y"
    };

    /// <exclude></exclude>
    private string[] _capTenors =
    {
      "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "12Y", "15Y", "20Y"
    };

    /// <exclude></exclude>
    private double[] _usdCapVols = { 0.9, 0.9, 0.7, 0.6, 0.5, 0.4, 0.3, 0.2, 0.2, 0.2 };

    /// <exclude></exclude>
    private double[] _eurCapVols = { 1.2, 1.1, 0.8, 0.7, 0.6, 0.4, 0.4, 0.4, 0.3, 0.2 };

    /// <exclude></exclude>
    private double[] _jpyCapVols = { 1.1, 0.9, 0.7, 0.6, 0.5, 0.4, 0.3, 0.3, 0.3, 0.3 };

    /// <exclude></exclude>
    private double[] _rates = { 0.03, 0.04, 0.05, 0.05, 0.05, 0.05, 0.05, 0.05, 0.05, 0.05 };

    /// <exclude></exclude>
    private double[] _frates2 =
    {
      0.02, 0.02, 0.02, 0.02, 0.02, 0.02, 0.025, 0.015, 0.035,
      0.015
    };

    /// <exclude></exclude>
    private double[] _frates1 =
    {
      0.04, 0.04, 0.04, 0.04, 0.04, 0.04, 0.045, 0.045, 0.045,
      0.045
    };

    /// <exclude></exclude>
    private double[] _fxrates = { 1.3972, 0.0123 };

    /// <exclude></exclude>
    private double _stockPrice = 120;

    /// <exclude></exclude>
    private double _spotInflation = 95;

    /// <exclude></exclude>
    private double[] _projectionRates =
    {
      0.04, 0.04, 0.04, 0.04, 0.04, 0.04, 0.045, 0.045,
      0.045, 0.045
    };

    /// <exclude></exclude>
    private double[] _inflationZeroRates =
    {
      0.010, 0.011, 0.0115, 0.0120, 0.0118, 0.0122, 0.0130, 0.0135, 0.0140,
      0.0142
    };

    /// <exclude></exclude>
    private double[] _cfvolas = { 0.3, 0.35 };

    /// <exclude></exclude>
    private double[] _lambda =
    {
      0.05, 0.14, 0.08, 0.10, 0.02, 0.06, 0.045, 0.07, 0.2,
      0.145
    };

    /// <exclude></exclude>
    private double[] _volas = { 0.5, 0.4, 0.3, 0.25, 0.2, 0.2, 0.2, 0.2, 0.2, 0.2 };

    /// <exclude></exclude>
    private double[] _cvolas = { 0.3, 0.35, 0.4, 0.2, 0.15, 0.45, 0.34, 0.25, 0.28, 0.333 };

    /// <exclude></exclude>
    private double[] _fxvolas = { 0.18, 0.20 };

    /// <exclude></exclude>
    private double[] _inflationVol =
    {
      0.05, 0.04, 0.03, 0.05, 0.06, 0.08, 0.08, 0.10, 0.10,
      0.10
    };

    private double _stockVol = 0.5;

    /// <exclude></exclude>
    private double _upBump = 1;

    /// <exclude></exclude>
    private string[] _bumpedTenors;

    /// <exclude></exclude>
    private double _downBump = 1;

    /// <exclude></exclude>
    private bool _bumpRelative;

    /// <exclude></exclude>
    private bool _calcGamma = true;

    /// <exclude></exclude>
    private BumpType _bumpType = BumpType.Parallel;

    /// <exclude></exclude>
    private QuotingConvention _quoteTarget = QuotingConvention.None;

    /// <exclude></exclude>
    private Input _input;

    /// <exclude></exclude>
    internal Netting Netting;

    /// <exclude></exclude>
    private Frequency _simulFreq = Frequency.SemiAnnual;

    #endregion

    #region Input

    /// <exclude></exclude>
    internal class Input : BaseEntityObject
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
    internal static InflationIndex CreateInflationIndex(Currency ccy, Calendar calendar,
      DayCount dayCount, BDConvention roll)
    {
      return new InflationIndex(String.Concat("CPI", ccy), ccy, dayCount, calendar, roll,
        Frequency.Monthly, Tenor.Empty);
    }

    /// <exclude></exclude>
    internal static InflationCurve CreateInflationCurve(Dt asOf, double spotInfl,
      string[] tenors, double[] fwd, DiscountCurve disc, DayCount dayCount,
      Calendar calendar, BDConvention roll)
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
          Calendar.NYB)
        {
          ProjectionType = ProjectionType.InflationRate,
          IsZeroCoupon = true
        };
        return new Swap(floatLeg, fixedLeg);
      }).ToArray();
      var iCurve = InflationCurveFitCalibrator.FitInflationCurve(asOf,
        new CalibratorSettings(), zeroSwaps, tenors, fwd, null, disc, inflIndex,
        Tenor.Empty, null,
        false, null, false);
      return iCurve;
    }

    /// <exclude></exclude>
    internal static StockCurve CreateStockForwardCurve(double spot, DiscountCurve discount)
    {
      return new StockCurve(discount.AsOf, spot, discount, 0.0, null)
      {
        Ccy = discount.Ccy,
        Name = "IBM_Curve$%"
      };
    }

    /// <exclude></exclude>
    internal static FxCurve CreateFxCurve(FxRate fxRate, DiscountCurve disc1,
      DiscountCurve disc2)
    {
      var domestic = (fxRate.ToCcy == disc1.Ccy) ? disc1 : disc2;
      var foreign = (fxRate.FromCcy == disc1.Ccy) ? disc1 : disc2;
      return new FxCurve(fxRate, null, domestic, foreign, null);
    }

    /// <exclude></exclude>
    internal static string FxName(FxRate fxRate)
    {
      return fxRate.Name;
    }

    /// <exclude></exclude>
    internal static double[,] GenerateFactors(Random rand, int n, int m, double norm)
    {
      var fl = new double[n, m];
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
    internal static double[,] GenerateBetas(Random rand, int n, double[,] fl,
      double[,] corrMatrix, double norm)
    {
      var yFl = GenerateFactors(rand, n, fl.GetLength(1), norm);
      var betas = new double[n, fl.GetLength(0)];
      var x = new double[fl.GetLength(0)];
      var u = (double[,])corrMatrix.Clone();
      var w = new double[u.GetLength(1)];
      var v = new double[u.GetLength(1), u.GetLength(1)];
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
    internal static double[,] GenerateCorrelationMatrix(double[,] fl)
    {
      var matrix = new MatrixOfDoubles(fl);
      var corr = LinearAlgebra.Multiply(matrix, LinearAlgebra.Transpose(matrix));
      var retVal = new double[corr.dim1(), corr.dim2()];
      for (int i = 0; i < corr.dim1(); ++i)
        for (int j = 0; j < corr.dim2(); ++j)
          retVal[i, j] = corr.at(i, j);
      return retVal;
    }

    /// <summary>
    /// Initialize factors and volatilities
    /// </summary>
    internal void InitializeParameters(Input input, params double[] cptyNorm)
    {
      int nFactors = input.FactorLoadings.FactorCount;
      var discountCurves = input.DiscountCurves;
      var creditCurves = input.CreditCurves;
      var fwdCurves = input.FwdCurves;
      var fxRates = input.FxRates;
      var rand = new Random(3);
      foreach (var dc in discountCurves)
        input.FactorLoadings.AddFactors(dc, GenerateFactors(rand, _tenors.Length, nFactors, 1.0));
      for (int i = 0; i < creditCurves.Length; ++i)
      {
        var cc = creditCurves[i];
        int cptyIdx = Array.IndexOf(_cptyIndex, i);
        double norm = (cptyNorm != null && cptyNorm.Length > 0 && cptyIdx >= 0)
          ? cptyNorm[cptyIdx] : 1.0;
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
          input.FactorLoadings.AddFactors(fc, GenerateFactors(rand, _tenors.Length, nFactors, 1.0));
      }
      for (int i = 0; i < discountCurves.Length; ++i)
      {
        var vols = Array.ConvertAll(_volas, v => new VolatilityCurve(_asOf, v));
        input.Volatilities.Add(discountCurves[i], vols);
      }
      for (int i = 0; i < creditCurves.Length; ++i)
      {
        var vol = new VolatilityCurve(_asOf, _cvolas[i]);
        input.Volatilities.Add(creditCurves[i], vol);
      }
      foreach (var fc in fwdCurves)
      {
        var stockCurve = fc as StockCurve;
        if (stockCurve != null)
        {
          CCRCalibrationUtils.CalibrateSpotVolatility(_asOf, stockCurve,
            new VolatilityCurve(_asOf, _stockVol), input.Volatilities,
            input.FactorLoadings, null);
        }
        var inflCurve = fc as InflationCurve;
        if (inflCurve != null)
        {
          var vols = Array.ConvertAll(_inflationVol, v => new VolatilityCurve(_asOf, v));
          input.Volatilities.Add(inflCurve, vols);
        }
        var projCurve = fc as DiscountCurve;
        if (projCurve != null)
        {
          var vols = Array.ConvertAll(_volas, v => new VolatilityCurve(_asOf, v));
          input.Volatilities.Add(projCurve, vols);
        }
      }
      for (int i = 0; i < discountCurves.Length - 1; ++i)
      {
        var vol = new VolatilityCurve(_asOf, _fxvolas[i]);
        input.Volatilities.Add(fxRates[i], vol);
      }
    }

    #endregion

    #region CreateInput

    internal static Dt[] GenerateSimulDates(Dt asOf, Frequency simulFreq)
    {
      var simDates = new List<Dt>();
      Dt terminal = Dt.Add(asOf, 365 * 12);
      Dt dt = asOf;
      for (; ; )
      {
        simDates.Add(dt);
        dt = Dt.Add(dt, simulFreq, false);
        if (Dt.Cmp(dt, terminal) > 0)
          break;
      }
      return simDates.ToArray();
    }


    internal Input CreateInput(int nFactors, int sampleSize, Tenor gridSize,
      int rateCount, int creditCount, bool withCredit, bool withInflation,
      bool withStock, bool withDualCurve, params double[] cptyNorm)
    {
      DiscountCurve[] dc;
      SurvivalCurve[] sc;
      CalibratedCurve[] fwd;
      FxRate[] fx;
      CreateMarket(rateCount, creditCount, withInflation, withStock, withDualCurve, out dc,
        out fx, out sc, out fwd);
      var portfolio = CreatePricers(dc, fx, withCredit ? sc : new SurvivalCurve[0], fwd);
      var names = portfolio.Select((p, i) => (i%2 == 0) ? "A" : "B").ToArray();
      //2 netting groups
      string[] marketFactorNames = ArrayUtil.Generate(
        nFactors, i => string.Concat("factor", i));
      var ten = _tenors.Select(Tenor.Parse).ToArray();
      var input = new Input
      {
        AsOf = _asOf,
        Pricers = portfolio,
        Names = names,
        Cpty = (sc.Length > 0) ? Array.ConvertAll(_cptyIndex, i => sc[i])
          : new SurvivalCurve[0],
        CptyRec = _cptyRec,
        DiscountCurves = dc,
        CreditCurves = sc,
        FwdCurves = fwd,
        FxRates = fx,
        TenorDates = _tenors.Select(t => Dt.Add(_asOf, t)).ToArray(),
        FactorLoadings = new FactorLoadingCollection(marketFactorNames, ten),
        Volatilities = new VolatilityCollection(ten),
        Sample = sampleSize,
        SimulDates = GenerateSimulDates(_asOf, _simulFreq),
        FactorNames = marketFactorNames,
        GridSize = gridSize
      };
      InitializeParameters(input, cptyNorm);
      return input;
    }


    internal void CreateMarket(int rateCount, int creditCount,
      bool withInflation, bool withStock, bool withDualCurve,
      out DiscountCurve[] discountCurves, out FxRate[] fxRates,
      out SurvivalCurve[] survivalCurves, out CalibratedCurve[] fwdCurves)
    {
      Dt[] tenorDates = Array.ConvertAll(_tenors, ten => Dt.Add(_asOf, ten));
      discountCurves = new DiscountCurve[rateCount];
      discountCurves[0] = CreateDiscountCurve(_asOf,
        _indexTenor[0], tenorDates, _rates,Currencies[0]);
      if (rateCount > 1)
        discountCurves[1] = CreateDiscountCurve(_asOf,
          _indexTenor[1], tenorDates, _frates1, Currencies[1]);
      if (rateCount > 2)
        discountCurves[2] = CreateDiscountCurve(_asOf,
          _indexTenor[2], tenorDates, _frates2, Currencies[2]);
      //set up fx rates
      fxRates = new FxRate[rateCount - 1];
      for (int i = 1; i < rateCount; i++)
        fxRates[i - 1] = new FxRate(_asOf, 2, discountCurves[i].Ccy,
          discountCurves[0].Ccy, _fxrates[i - 1], Calendar.None, Calendar.None);
      var fwd = new List<CalibratedCurve>();
      if (withDualCurve)
        fwd.Add(CreateDiscountCurve(_asOf, "6M", tenorDates, _projectionRates,
          Currencies[0]));
      if (withInflation)
        fwd.Add(CreateInflationCurve(_asOf, _spotInflation, _tenors,
          _inflationZeroRates, discountCurves[0], _dayCounts[0],
          _calendar[0], _roll[0]));
      if (withStock)
        fwd.Add(CreateStockForwardCurve(_stockPrice, discountCurves[0]));
      fwdCurves = fwd.ToArray();
      survivalCurves = new SurvivalCurve[creditCount];
      for (int i = 0; i < creditCount; ++i)
      {
        int ccy = i % rateCount;
        survivalCurves[i] = SurvivalCurve.FromProbabilitiesWithCDS(
          _asOf, Currencies[ccy], null, InterpMethod.Weighted,
          ExtrapMethod.Const, new[] { Dt.Add(_asOf, "10Y") },
          new[] { Math.Exp(-_lambda[i] * 5) }, new[] { "10Y" },
          new[] { DayCount.Actual360 }, new[] { Frequency.Quarterly },
          new[] { BDConvention.Following }, new[] { Calendar.NYB },
          new[] { 0.4 }, 0.0);
        survivalCurves[i].Name = String.Concat("obligor.", i);
        survivalCurves[i].SurvivalCalibrator.DiscountCurve = discountCurves[ccy];
      }
    }

    internal static SwapPricer CreateSwapPricer(Dt asOf, double notional,
      DiscountCurve discountCurve, string indexTenor, DayCount indexDayCount,
      Calendar indexCalendar, BDConvention indexRoll, Frequency frequency,
      BDConvention roll, string maturity, DayCount dayCount, Calendar calendar,
      int settleDays)
    {
      var rateResets = new RateResets(0, 0);
      //vanilla domestic swap
      ReferenceIndex index =
        new InterestRateIndex(String.Concat("Libor", discountCurve.Ccy),
          Tenor.Parse(indexTenor), discountCurve.Ccy, indexDayCount,
          indexCalendar, indexRoll, settleDays);
      var domesticLegFixed = new SwapLeg(Dt.AddDays(asOf, settleDays, calendar),
        Dt.Add(asOf, maturity), discountCurve.Ccy,
        0.035, dayCount, frequency, roll, calendar, false) { FinalExchange = false };
      var domesticLegFloat = new SwapLeg(Dt.AddDays(asOf, settleDays, calendar),
        Dt.Add(asOf, maturity),
        Tenor.Parse(indexTenor).ToFrequency(), 0, index) { FinalExchange = false };
      var fixedLeg = new SwapLegPricer(domesticLegFixed, asOf,
        Dt.AddDays(asOf, settleDays, calendar), notional, discountCurve,
        null, null, null, null, null);
      var floatLeg = new SwapLegPricer(domesticLegFloat, asOf,
        Dt.AddDays(asOf, settleDays, calendar), -notional, discountCurve,
        index, discountCurve, rateResets, null, null);
      var swapPricer = new SwapPricer(fixedLeg, floatLeg);
      fixedLeg.SwapLeg.Coupon = swapPricer.ParCoupon();
      return swapPricer;
    }


    internal static SwapPricer CreateXccySwapPricer(
      Dt asOf, double notional, FxRate fxRate,
      DiscountCurve ccy1DiscountCurve, DiscountCurve ccy2DiscountCurve,
      string indexTenor, DayCount indexDayCount, Calendar indexCalendar,
      BDConvention indexRoll, Frequency frequency, BDConvention roll,
      string maturity, DayCount dayCount, Calendar calendar, int settleDays)
    {
      var rateResets = new RateResets(0, 0);
      ReferenceIndex foreignIndex =
        new InterestRateIndex(String.Concat("Libor", ccy2DiscountCurve.Ccy),
          Tenor.Parse(indexTenor), ccy2DiscountCurve.Ccy, indexDayCount,
          indexCalendar, indexRoll, settleDays);
      var ccy1LegFixed = new SwapLeg(Dt.AddDays(asOf, settleDays, calendar),
        Dt.Add(asOf, maturity), ccy1DiscountCurve.Ccy,
        0.035, dayCount, frequency, roll, calendar, false) { FinalExchange = true };
      var ccy2LegFloat = new SwapLeg(Dt.AddDays(asOf, settleDays, indexCalendar),
        Dt.Add(asOf, maturity), Tenor.Parse(indexTenor).ToFrequency(), 0, foreignIndex)
      {
        FinalExchange = true
      };
      var ccy1LegPricer = new SwapLegPricer(ccy1LegFixed, asOf,
        Dt.AddDays(asOf, settleDays, calendar), notional,
        ccy1DiscountCurve, null, null, null, null, null);
      var ccy2LegPricer = new SwapLegPricer(ccy2LegFloat, asOf,
        Dt.AddDays(asOf, settleDays, indexCalendar),
        -notional * fxRate.GetRate(ccy1DiscountCurve.Ccy, ccy2DiscountCurve.Ccy),
        ccy1DiscountCurve, foreignIndex, ccy2DiscountCurve, rateResets, null,
        CreateFxCurve(fxRate, ccy1DiscountCurve, ccy2DiscountCurve));
      var swapPricerXccy = new SwapPricer(ccy1LegPricer, ccy2LegPricer);
      ccy1LegPricer.SwapLeg.Coupon = swapPricerXccy.ParCoupon();
      return swapPricerXccy;
    }

    internal SwapPricer CreateBasisSwapPricer(double notional,
      DiscountCurve discountCurve, DiscountCurve projectionCurve,
      string indexTenor1, string indexTenor2, DayCount indexDayCount,
      Calendar indexCalendar, BDConvention indexRoll, string maturity,
      int settleDays)
    {
      var rateResets = new RateResets(0, 0);
      ReferenceIndex index1 =
        new InterestRateIndex(String.Concat("Libor", discountCurve.Ccy, indexTenor1),
          Tenor.Parse(indexTenor1), discountCurve.Ccy, indexDayCount,
          indexCalendar, indexRoll, settleDays);
      ReferenceIndex index2 =
        new InterestRateIndex(String.Concat("Libor", discountCurve.Ccy, indexTenor2),
          Tenor.Parse(indexTenor2), discountCurve.Ccy, indexDayCount,
          indexCalendar, indexRoll, settleDays);
      var index1Leg = new SwapLeg(Dt.AddDays(_asOf, settleDays, index1.Calendar),
        Dt.Add(_asOf, maturity), index1.IndexTenor.ToFrequency(), 1e-3, index1);
      var index2Leg = new SwapLeg(Dt.AddDays(_asOf, settleDays, index2.Calendar),
        Dt.Add(_asOf, maturity), index2.IndexTenor.ToFrequency(), 0, index2);
      var index1LegPricer = new SwapLegPricer(index1Leg, _asOf,
        Dt.AddDays(_asOf, settleDays, indexCalendar), notional, discountCurve, index1,
        discountCurve, rateResets,
        null, null);
      var index2LegPricer = new SwapLegPricer(index2Leg, _asOf,
        Dt.AddDays(_asOf, settleDays, indexCalendar), -notional, discountCurve, index2,
        projectionCurve,
        rateResets, null, null);
      var basisSwap = new SwapPricer(index1LegPricer, index2LegPricer);

      index2LegPricer.SwapLeg.Coupon = basisSwap.ParCoupon();
      return basisSwap;
    }

    internal static InflationBondPricer CreateInflationBondPricer(
      Dt asOf, DiscountCurve discountCurve, InflationCurve inflationCurve,
      string maturity, double notional, double coupon,
      DayCount indexDayCount, Calendar indexCalendar, BDConvention indexRoll,
      DayCount dayCount, Frequency frequency, BDConvention roll,
      Calendar calendar, double spotInflation)
    {
      ReferenceIndex inflIndex = CreateInflationIndex(discountCurve.Ccy, indexCalendar,
        indexDayCount, indexRoll);
      var inflationBond = new InflationBond(Dt.AddDays(asOf, 1, calendar),
        Dt.Add(asOf, maturity), discountCurve.Ccy, BondType.None, coupon, dayCount,
        CycleRule.None, frequency, roll, calendar, (InflationIndex)inflIndex,
        spotInflation, Tenor.Empty);
      var inflBondPricer = new InflationBondPricer(inflationBond, asOf,
        Dt.AddDays(asOf, 2, Calendar.NYB), notional, discountCurve,
        (InflationIndex)inflIndex, inflationCurve, null, null);
      return inflBondPricer;
    }

    internal static StockOptionPricer CreateStockOptionPricer(
      Dt asOf, StockCurve fwdCurve, double stockPrice, string maturity,
      OptionType optionType, VolatilityCurve vol, double notional)
    {
      var option = new StockOption(Dt.Add(asOf, maturity), optionType,
        OptionStyle.European, stockPrice)
      {
        Ccy = fwdCurve.Spot.Ccy,
        Description = fwdCurve.Spot.Name
      };
      var stockOptionPricer = new StockOptionPricer(option, asOf, asOf, fwdCurve, null,
        vol) { Notional = notional };
      return stockOptionPricer;
    }

    internal IPricer[] CreatePricers(
      DiscountCurve[] discountCurves, FxRate[] fxRates,
      SurvivalCurve[] survivalCurves, CalibratedCurve[] fwdCurves)
    {
      return CreatePricers(1e7, 1e7, 1e6, 1e6, true, discountCurves, fxRates,
        survivalCurves, fwdCurves);
    }


    private IPricer[] CreatePricers(
      double swapNotional, double inflationBondNotional,
      double optionNotional, double cdsNotional, bool scaleCDS,
      DiscountCurve[] discountCurves, FxRate[] fxRates,
      SurvivalCurve[] survivalCurves, CalibratedCurve[] fwdCurves)
    {
      var pricerList = new List<IPricer>();
      var usdSwapPricer = CreateSwapPricer(_asOf, swapNotional, discountCurves[0],
        _indexTenor[0], _dayCounts[0], _calendar[0], _roll[0], _swapFrequency[0],
        _roll[0], "10Y", _dayCounts[0], _calendar[0], 2);
      pricerList.Add(usdSwapPricer);
      if (discountCurves.Length > 1)
      {
        var eurSwapPricer = CreateSwapPricer(_asOf,
          swapNotional * fxRates[0].GetRate(discountCurves[0].Ccy, discountCurves[1].Ccy),
          discountCurves[1],
          _indexTenor[1],
          _dayCounts[1], _calendar[1], _roll[1], _swapFrequency[1], _roll[1],
          "10Y", _dayCounts[1], _calendar[1], 2);
        pricerList.Add(eurSwapPricer);
        var eurXccySwapPricer = CreateXccySwapPricer(_asOf, swapNotional, fxRates[0],
          discountCurves[0], discountCurves[1], _indexTenor[1], _dayCounts[1],
          _calendar[1],
          _roll[1], _swapFrequency[0], _roll[0], "10Y", _dayCounts[0], _calendar[0], 2);
        pricerList.Add(eurXccySwapPricer);
        //cross currency swap

      }
      if (discountCurves.Length > 2)
      {
        var jpySwapPricer = CreateSwapPricer(_asOf,
          swapNotional / fxRates[1].GetRate(discountCurves[2].Ccy, discountCurves[0].Ccy),
          discountCurves[2],
          _indexTenor[2],
          _dayCounts[2], _calendar[2], _roll[2], _swapFrequency[2], _roll[2],
          "10Y", _dayCounts[2], _calendar[2], 2);
        pricerList.Add(jpySwapPricer);
        var jpyXccySwapPricer = CreateXccySwapPricer(_asOf, swapNotional, fxRates[1],
          discountCurves[0], discountCurves[2], _indexTenor[2], _dayCounts[2],
          _calendar[2], _roll[2], _swapFrequency[0], _roll[0], "10Y", _dayCounts[0],
          _calendar[0], 2);
        pricerList.Add(jpyXccySwapPricer);
      }
      //set up cds
      var cds = new CDS(Dt.AddDays(_asOf, 1, Calendar.NYB),
        Dt.Add(_asOf, "10Y"), Currency.USD, 0.0,
        DayCount.Actual360, Frequency.Quarterly,
        BDConvention.Following, Calendar.NYB);
      pricerList.AddRange(survivalCurves.Select((sc, i) =>
      {
        var product = CloneUtil.Clone(cds);
        product.Ccy = sc.Ccy;
        var ccyIdx = IndexOf(discountCurves, product.Ccy);
        var dc = discountCurves[ccyIdx];
        var retVal = new CDSCashflowPricer(product, _asOf, dc, null, sc);
        retVal.CDS.Premium = retVal.BreakEvenPremium();
        var fx = (ccyIdx == 0)
          ? 1.0
          : fxRates[ccyIdx - 1].GetRate(discountCurves[0].Ccy, discountCurves[ccyIdx].Ccy);
        retVal.Notional = scaleCDS
          ? Math.Pow(-1, i) * cdsNotional / retVal.CDS.Premium
          : Math.Pow(-1, i) * cdsNotional * fx;
        return retVal;
      }));
      var projectionCurve = fwdCurves.FirstOrDefault(c => c is DiscountCurve);
      if (projectionCurve != null) //basis swap
      {
        var basisSwapPricer = CreateBasisSwapPricer(swapNotional, discountCurves[0],
          (DiscountCurve)projectionCurve, _indexTenor[0], _basisSwapIndexTenor[0],
          _dayCounts[0], _calendar[0], _roll[0], "10Y", 2);
        pricerList.Add(basisSwapPricer);

      }
      var inflationCurve = fwdCurves.FirstOrDefault(c => c is InflationCurve);
      if (inflationCurve != null)
      {
        var inflBondPricer = CreateInflationBondPricer(_asOf,
          discountCurves[0], (InflationCurve)inflationCurve, "10Y",
          inflationBondNotional, 0.05, _dayCounts[0], _calendar[0], _roll[0],
          _dayCounts[0], Frequency.SemiAnnual, _roll[0], _calendar[0],
          _spotInflation);
        pricerList.Add(inflBondPricer);

      }
      var stockFwdCurve = fwdCurves.FirstOrDefault(c => c is StockCurve);
      if (stockFwdCurve != null)
      {
        var vol = new VolatilityCurve(_asOf, _stockVol);
        vol.Fit();
        var stockOptionPricer = CreateStockOptionPricer(_asOf, (StockCurve)stockFwdCurve,
          _stockPrice, "5Y", OptionType.Call, vol, optionNotional);
        pricerList.Add(stockOptionPricer);
      }
      return pricerList.ToArray();
    }

    #endregion

    #region Private helpers

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
      // Create discount curve from the _rates
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
        discounts[i] = (df /= 1 + frac * rates[i]);
        dcurve.AddZeroYield(maturity, RateCalc.RateFromPrice(
          df, asOf, maturity, dc, Frequency.None), dc, Frequency.None);
        reset = maturity;
      }
      dcurve.Fit();
      return dcurve;
    }

    #endregion

    #region Test helpers
    #endregion
  }
}
