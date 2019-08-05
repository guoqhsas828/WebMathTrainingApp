// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics.Rng;
using static BaseEntity.Toolkit.Calibrators.Volatilities.HullWhiteParameterCalibration;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Calibrators
{

  [TestFixture, Category("LongRunning")]
  public class HullWhiteShortRateModelTests3
  {
    #region Test

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void TestStabilityOfGlobalMinimumConverging(int index)
    {
      LocateMemory();

      var inputDatas = new HullWhiteTestData().InputDatas;
      var inputData = inputDatas[index];
      inputData.AsOf = _asOf;
      var dataContainer = HullWhiteTestUtil.GetInitialDataContainer(inputData, _rateCurve);
      var wv0 = dataContainer.Variables;
      var inputVols = dataContainer.Volatilities;
      var apartPoint = dataContainer.MeanVariables.Length;
      var rng = new RandomNumberGenerator();
      for (int i = -1; i < 3; i++)
      {
        Console.WriteLine(i);
        //skip the initial one
        if (i >= 0)
        {
          var randomVariables = GetRandomVariable(wv0, 0.5, rng);
          var means = randomVariables.Take(apartPoint).ToArray();
          var sigma = randomVariables.Skip(apartPoint).ToArray();
          while (!Satisfy(means, sigma, 10, -1, 10, 0))
          {
            randomVariables = GetRandomVariable(wv0, 0.5, rng);
            means = randomVariables.Take(apartPoint).ToArray();
            sigma = randomVariables.Skip(apartPoint).ToArray();
          }
          dataContainer.Variables = randomVariables;
        }

        //Add input variables
        var v = new Variable
        {
          InputWholeVariable = dataContainer.Variables,
          InputSigmaVariable = dataContainer.SigmaVariables,
          InputMeanVariable = dataContainer.MeanVariables,
          OptWholeVariable = null,
          OptSigmaVariable = null,
          OptMeanVariable = null
        };
        _variables.Add(v);

        //optimize and add vol information
        var optVols = CollectPerformInfo(dataContainer, ref _performs);
        var volInfo = GetVolInfo(optVols, inputVols);
        _volInfo.Add(volInfo);

        //add optimized variables
        v = new Variable
        {
          OptWholeVariable = dataContainer.Variables,
          OptSigmaVariable = dataContainer.SigmaVariables,
          OptMeanVariable = dataContainer.MeanVariables,
          InputSigmaVariable = null,
          InputMeanVariable = null,
          InputWholeVariable = null
        };
        _variables.Add(v);
        
        Assert.AreEqual(0.0, volInfo.AverageDiff, 2E-2);
      }

      var meanObject = _performs[0].MeanObjective;
      var sigmaObject = _performs[0].SigmaObjective;

      foreach (var perform in _performs)
      {
        _timeGridList.Add(perform.TimeGrid);
        _meanPoints.Add(perform.MeanPoints);
        _sigmaPoints.Add(perform.SigmaPoints);
        _timeList.Add(perform.Times);
        _meanObjecitveValues.Add(perform.MeanObjective);
        _sigmaObjectiveValues.Add(perform.SigmaObjective);
        _marktPvs.Add(perform.MarketPvs);
        _modelpvs.Add(perform.ModelPvs);
        Assert.AreEqual(meanObject, perform.MeanObjective, 5E-4);
        Assert.AreEqual(sigmaObject, perform.SigmaObjective, 5E-4);
      }

      Report();
      Dispose();
    }

    #endregion Test

    #region Collect Infomation

    private double[,] CollectPerformInfo(HullWhiteDataContainer d, ref List<Perform> perform)
    {
      int n = d.Expiries.Length, m = d.Tenors.Length;

      var meanTime = Fit(d, HwOptimizeFlag.OptimizeMean);
      var sigmaTime = Fit(d, HwOptimizeFlag.OptimizeSigma);
      var bothTime = Fit(d, HwOptimizeFlag.OptimizeAll);
      var calibratedVols = new double[n, m];

      var modelPvs = new double[n * m];
      var marketPvs = new double[n * m];
      var points = d.Points;
      var time = d.Times;
      var volType = d.VolType;
      var vols = d.Volatilities;

      var objectiveValues = new ObjectiveValue();

      for (int i = 0, k = 0; i < n; i++)
      {
        for (int j = 0; j < m; j++)
        {
          if(vols[i,j] <= 0.0) continue;
          var point = points[k];
          int eI = point.Begin, mI = point.End;
          var modelPv = SwaptionModelPv(eI, mI, point.SwapRate, d);
          var normalizedPrice = modelPv / point.Annuity;
          var impliedVol = volType == DistributionType.Normal
            ? BlackNormal.ImpliedVolatility(OptionType.Call,
              time[eI], 0, point.SwapRate, point.SwapRate, normalizedPrice)
            : Black.ImpliedVolatility(OptionType.Call, time[eI],
              point.SwapRate, point.SwapRate, normalizedPrice);
          calibratedVols[i, j] = impliedVol;

          objectiveValues = GetObjectiveValue(d);

          modelPvs[k] = modelPv;
          marketPvs[k] = point.MarketPv;
          k++;
        }
      }

      var performPara = new Perform
      {
        TimeGrid = d.TimeGrids.Length,
        MeanPoints = d.MeanVariables.Length,
        SigmaPoints = d.SigmaVariables.Length,
        Times = new[] { meanTime, sigmaTime, bothTime },
        SigmaObjective = objectiveValues.SigmaObjectiveValue,
        MeanObjective = objectiveValues.MeanObjectiveValue,
        MarketPvs = marketPvs,
        ModelPvs = modelPvs
      };
      perform.Add(performPara);

      return calibratedVols;
    }
    private double Fit(HullWhiteDataContainer data, HwOptimizeFlag flag)
    {
      var stopWatch = new Stopwatch();
      stopWatch.Start();
      var fn = new HwOptimizingFn(data, flag);
      var guess = GetGuess(data, flag);
      double[] uBounds, lBounds;
      GetBoundary(data, out uBounds, out lBounds);
      fn.Fit(new HullWhiteCalibrationSettings(), guess, uBounds, lBounds);
      stopWatch.Stop();
      return stopWatch.Elapsed.TotalSeconds;
    }

    private static VolInfo GetVolInfo(double[,] optimizedVol, double[,] inputVols)
    {
      var aDiff = 0.0;
      var n = optimizedVol.GetLength(0);
      var m = optimizedVol.GetLength(1);
      var diffs = new double[n, m];
      for (int i = 0; i < n; i++)
      {
        for (int j = 0; j < m; j++)
        {
          if (!inputVols[i, j].AlmostEquals(0.0))
          {
            diffs[i, j] = Math.Abs(optimizedVol[i, j] / inputVols[i, j] - 1);
            aDiff += Math.Abs(optimizedVol[i, j] / inputVols[i, j] - 1);
          }
        }
      }
      var averageDiff = aDiff / (n * m);

      return new VolInfo
      {
        InputVols = inputVols,
        OptVols = optimizedVol,
        VolDiffs = diffs,
        AverageDiff = averageDiff
      };
    }

    #endregion Collect Infomation

    #region Report
    private void Report()
    {
      foreach (var variable in _variables)
        ReportVariable("Input whole variable", variable.InputWholeVariable);
      Console.WriteLine();

      foreach (var variable in _variables)
        ReportVariable("Input sigma variable", variable.InputSigmaVariable);
      Console.WriteLine();

      foreach (var variable in _variables)
        ReportVariable("Input mean variable", variable.InputMeanVariable);
      Console.WriteLine();

      foreach (var variable in _variables)
        ReportVariable("Optimized whole variable", variable.OptWholeVariable);
      Console.WriteLine();

      foreach (var variable in _variables)
        ReportVariable("Optimized sigma variable", variable.OptSigmaVariable);
      Console.WriteLine();

      foreach (var variable in _variables)
        ReportVariable("Optimized mean variable", variable.OptMeanVariable);
      Console.WriteLine();

      //time grid, mean constraint points and sigma constraint points
      for (int i = 0; i < _timeGridList.Count; i++)
      {
        Console.WriteLine("TimeGrid:" + "\t" + _timeGridList[i] + "\t" + "\t"
                          + "MeanPoints:" + "\t" + _meanPoints[i] + "\t" + "\t "
                          + "SigmaPoints:" + "\t" + _sigmaPoints[i]);
      }
      Console.WriteLine();

      //mean, sigma and both fitting time
      foreach (var times in _timeList)
      {
        Console.WriteLine("Fit Mean Time:" + "\t" + times[0] + "\t" + "\t"
                          + "Fit Sigma Time:" + "\t" + times[1] + "\t" + "\t "
                          + "Fit Both Time:" + "\t" + times[2]);
      }
      Console.WriteLine();

      //objective values
      for (int i = 0; i < _meanObjecitveValues.Count; i++)
      {
        Console.WriteLine("MeanObjectiveValue:" + "\t" + _meanObjecitveValues[i] + "\t" + "\t"
                          + "SigmaObjetiveValue" + "\t" + _sigmaObjectiveValues[i]);
      }
      Console.WriteLine();

      for (int i = 0; i < _marktPvs.Count; i++)
      {
        Console.WriteLine("MarketPvs:" + "\t" + String.Join("\t", _marktPvs[i]));
        Console.WriteLine("ModelPvs:" + "\t" + String.Join("\t", _modelpvs[i]));
      }
      Console.WriteLine();

      //vols and diffs
      for (int i = 0; i < _volInfo.Count; i++)
      {
        if (i > 4) continue;
        var volInfo = _volInfo[i];
        ReportVol(volInfo.InputVols);
        ReportVol(volInfo.OptVols);
        ReportVol(volInfo.VolDiffs);
        Console.WriteLine("Average Diff" + "\t" + volInfo.AverageDiff + "\t");
        Console.WriteLine();
        Console.WriteLine();
      }
    }

    private void ReportVariable(string title, double[] variable)
    {
      if (variable != null)
        Console.WriteLine(title + "\t" + String.Join("\t", variable));
    }

    private void ReportVol(double[,] vols)
    {
      var n = vols.GetLength(0);
      var m = vols.GetLength(1);
      for (int j = 0; j < n; j++)
      {
        for (int k = 0; k < m; k++)
        {
          Console.Write(vols[j, k] + "\t");
        }
        Console.WriteLine();
      }
      Console.WriteLine();
    }

    #endregion

    #region Helpers

    internal static double[] GetRandomVariable(double[] x,
   double dev, RandomNumberGenerator rng)
    {
      var n = x.Length;
      var xnew = new double[n];

      for (int i = 0; i < n; ++i)
        xnew[i] = rng.Normal(x[i], dev);
      return xnew;
    }

    private static bool Satisfy(double[] means, double[] sigmas,
    double muBound, double mlBound, double suBound, double slBound)
    {
      bool satisfy = true;
      for (int i = 0; i < means.Length; i++)
        satisfy &= means[i] < muBound && means[i] > mlBound;
      for (int i = 0; i < sigmas.Length; i++)
        satisfy &= sigmas[i] < suBound && sigmas[i] > slBound;

      return satisfy;
    }

    private void LocateMemory()
    {
      _performs = new List<Perform>();
      _meanObjecitveValues = new List<double>();
      _sigmaObjectiveValues = new List<double>();
      _timeList = new List<double[]>();
      _timeGridList = new List<double>();
      _sigmaPoints = new List<double>();
      _meanPoints = new List<double>();
      _marktPvs = new List<double[]>();
      _modelpvs = new List<double[]>();
      _volInfo = new List<VolInfo>();
      _variables = new List<Variable>();
    }

    private void Dispose()
    {
      _performs = null;
      _meanObjecitveValues = null;
      _sigmaObjectiveValues = null;
      _timeList = null;
      _timeGridList = null;
      _sigmaPoints = null;
      _meanPoints = null;
      _marktPvs = null;
      _modelpvs = null;
      _volInfo = null;
      _variables = null;
    }


    private ObjectiveValue GetObjectiveValue(HullWhiteDataContainer data)
    {
      int n = data.Expiries.Length, m = data.Tenors.Length;
      var points = data.Points;
      var zeroPrices = data.DiscountFactors;
      var b = data.B;

      var sigmaObjective = 0.0;
      var meanObjective = 0.0;
      for (int i = 0, k = 0; i < n; i++)
      {
        for (int j = 0; j < m; j++)
        {
          var pk = points[k];
          var pk1 = points[k + 1];
          var modelPv = SwaptionModelPv(pk.Begin, pk.End,
            pk.SwapRate, data);
          sigmaObjective += pk.Weight*(Math.Pow(modelPv/pk.MarketPv - 1, 2));
          if (!pk.Weight.AlmostEquals(0.0) && j < m - 1)
          {
            var weightRatio = pk1.Weight/pk.Weight;
            var ivr = Math.Sqrt(ImpliedVariancesRatio(pk.Begin,
              pk1.End, pk.End, zeroPrices, b));
            var volRatio = pk1.Volatility/pk.Volatility;
            meanObjective += weightRatio*(Math.Pow(ivr/volRatio - 1, 2));
          }
        }
      }
      return new ObjectiveValue
      {
        MeanObjectiveValue = meanObjective,
        SigmaObjectiveValue = sigmaObjective
      };
    }

    private void GetBoundary(HullWhiteDataContainer data, 
      out double[] uBounds, out double[] lBounds)
    {
      var mcds = data.MeanCurveDates;
      var scds = data.SigmaCurveDates;
      var muBounds = mcds.Select(d => _muCurve.Interpolate(d)).ToArray();
      var mlBounds = mcds.Select(d => _mlCurve.Interpolate(d)).ToArray();
      var suBounds = scds.Select(d => _suCurve.Interpolate(d)).ToArray();
      var slBounds = scds.Select(d => _slCurve.Interpolate(d)).ToArray();
      uBounds = muBounds.Concat(suBounds).ToArray();
      lBounds = mlBounds.Concat(slBounds).ToArray();
    }

    #endregion Helpers

    #region Structs
    struct ObjectiveValue
    {
      public double MeanObjectiveValue;
      public double SigmaObjectiveValue;
    }
    struct Perform
    {
      public int TimeGrid;
      public int MeanPoints;
      public int SigmaPoints;
      public double[] Times;
      public double MeanObjective;
      public double SigmaObjective;
      public double[] MarketPvs;
      public double[] ModelPvs;
    }
    struct VolInfo
    {
      public double[,] InputVols;
      public double[,] OptVols;
      public double[,] VolDiffs;
      public double AverageDiff;
    }

    struct Variable
    {
      public double[] InputWholeVariable;
      public double[] OptWholeVariable;
      public double[] InputMeanVariable;
      public double[] OptMeanVariable;
      public double[] InputSigmaVariable;
      public double[] OptSigmaVariable;
    }
    #endregion

    #region Data
    
    private VolatilityCurve _muCurve = new VolatilityCurve(_asOf, 10);
    private VolatilityCurve _mlCurve = new VolatilityCurve(_asOf, -1);
    private VolatilityCurve _suCurve = new VolatilityCurve(_asOf, 10);
    private VolatilityCurve _slCurve = new VolatilityCurve(_asOf, 0);

    private List<double[]> _timeList,
      _marktPvs,
      _modelpvs;

    private List<VolInfo> _volInfo;

    private List<Variable> _variables;

    private List<Perform> _performs;

    private List<double> _meanObjecitveValues,
      _sigmaObjectiveValues,
      _timeGridList,
      _meanPoints,
      _sigmaPoints;

    private static Dt _asOf = new Dt(20110609);

    private static DiscountCurve _rateCurve =
      new RateCurveBuilder().CreateRateCurves(_asOf) as DiscountCurve;


    #endregion Data
  }
  
}
