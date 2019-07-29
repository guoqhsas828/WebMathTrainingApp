using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Calibrates Factorized Vols for a SurvivalCurveProcess
  /// </summary>
  [Serializable]
  public class SurvivalProcessCalibration : IProcessCalibration
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(SurvivalProcessCalibration));
    
    #region Constructors

    /// <summary>
    /// 
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="discountCurve"></param>
    /// <param name="survivalCurve"></param>
    /// <param name="volCurve"></param>
    /// <param name="assets"></param>
    /// <param name="factorization"></param>
    public SurvivalProcessCalibration(Dt asOf, DiscountCurve discountCurve, SurvivalCurve survivalCurve, VolatilityCurve volCurve,  CalibrationAsset[] assets, MatrixFactorization factorization){
      AsOf = asOf;
      DiscountCurve = discountCurve;
      SurvivalCurve = survivalCurve; 
      CalibrationAssets = assets;
      Factorization = factorization;
      InputVolatility = volCurve;
    }

    #endregion

    #region Properties

    /// <summary>
    /// AsOf date
    /// </summary>
    public Dt AsOf { get; private set; }
    
    /// <summary>
    /// Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get; private set;
    }
    
    /// <summary>
    /// Survival curve
    /// </summary>
    public SurvivalCurve SurvivalCurve { get; private set; }

    /// <summary>
    /// Factorization
    /// </summary>
    public MatrixFactorization Factorization { get; private set; }

    /// <summary>
    /// Input volatility
    /// </summary>
    public VolatilityCurve InputVolatility { get; private set; }

    /// <summary>
    /// Calibration assets
    /// </summary>
    public CalibrationAsset[] CalibrationAssets { get; private set; }

    /// <summary>
    /// Factor loadings
    /// </summary>
    public double[,] FactorLoadings { get; internal set; }

    /// <summary>
    /// Volatility curves
    /// </summary>
    public VolatilityCurve[] Volatilities { get; internal set; }

    #endregion


    #region Calibration
    /// <summary>
    /// Calibrate factorized vols
    /// </summary>
    public void Calibrate()
    {
      var modules = LoadModules();
      if (modules.Any())
      {
        var factorizedVols = CalibrateSurvivalCurve(modules);
        FactorLoadings = factorizedVols.FactorLoadings;
        Volatilities = factorizedVols.Volatilities;
      }
      else
      {
        // if curve is not actively simulated, construct 0 vols and fls 
        FactorLoadings = new double[1,Factorization.MarketFactorNames.Length];
        Volatilities = new VolatilityCurve[1];
      }
    }

    private IList<CCRCalibrationUtils.FactorData> LoadModules()
    {
      var modules = new List<CCRCalibrationUtils.FactorData>();
      for (int i = 0; i < CalibrationAssets.Length; i++)
      {
        var calibrationAsset = CalibrationAssets[i];
        if (Factorization.PrimaryAssets.Contains(calibrationAsset))
        {
          var module = LoadPrimaryModule(calibrationAsset);
          modules.Add(module);
        }

        if (Factorization.SecondaryAssets.Contains(calibrationAsset))
        {
          var module = LoadSecondaryModule(calibrationAsset);
          modules.Add(module);
        }
      }
      return modules; 
    }

    private CCRCalibrationUtils.FactorData LoadPrimaryModule(CalibrationAsset asset)
    {
      var idx = Array.IndexOf(Factorization.PrimaryAssets, asset);
      return new CCRCalibrationUtils.FactorData(asset.Type, SurvivalCurve, asset.Tenor, InputVolatility, Row(Factorization.FactorizedCorrelationMatrix, idx), null);
    }
    private CCRCalibrationUtils.FactorData LoadSecondaryModule(CalibrationAsset asset)
    {
      var idx = Array.IndexOf(Factorization.SecondaryAssets, asset);
      var fl = Multiply(Row(Factorization.FactorizedBetas, idx), Factorization.FactorizedCorrelationMatrix);
      var norm = Norm(fl);
      Tuple<int, double> idiosyncratic = null;
      if (asset.Type == CCRCalibrationUtils.MarketVariableType.CounterpartyCreditSpread)
      {
        //not part of environment, i.e. counterparty                                                     
        if (norm > 1.0)
        {
          fl = Scale(fl, norm);
        }
        var factorData = new CCRCalibrationUtils.FactorData(asset.Type, SurvivalCurve, asset.Tenor, InputVolatility, fl, null);
        Logger.VerboseFormat("Adding new Factor Data Element, Object Id: {0}, {1}", ObjectId(factorData.Obj, factorData.Tenor), factorData);
        return factorData;
      }

      if (norm < 0.99)
      {
        idiosyncratic = new Tuple<int, double>(Factorization.FactorNameRef[idx], Math.Sqrt(1.0 - norm * norm));
      }
      else
      {
        fl = Scale(fl, norm);
      }

      var factorDataWithIdio = new CCRCalibrationUtils.FactorData(asset.Type, SurvivalCurve, asset.Tenor, InputVolatility, fl, idiosyncratic);

      Logger.VerboseFormat("Adding new Factor Data Element, Object Id: {0}, {1}", ObjectId(factorDataWithIdio.Obj, factorDataWithIdio.Tenor), factorDataWithIdio);
      if (null != idiosyncratic)
      {
        Logger.VerboseFormat("Adding Idiosyncratic Factor of {0} for {1}", idiosyncratic, ObjectId(factorDataWithIdio.Obj, factorDataWithIdio.Tenor));
      }

      return factorDataWithIdio;
    }

    private ProcessCalibrationData  CalibrateSurvivalCurve(IEnumerable<CCRCalibrationUtils.FactorData> modules)
    {
      var retVal = new CalibrationUtils.CalibrationLogCollection();
      var data = modules.OrderBy(d => d.Tenor);

      Logger.VerboseFormat("Calibrating for Curve {0}", SurvivalCurve.Name);

      if (data.Any(d => d.Volatility != null))
      {
        var fl = ToMatrix(data.Select(d =>
        {
          var f = Scale(Resize(d.Factors, Factorization.MarketFactorNames.Length), -1.0);
          var idiosyncratic = d.IdiosyncraticFactor;
          if (idiosyncratic != null)
            f[idiosyncratic.Item1] = idiosyncratic.Item2;
          return f;
        }).ToArray());

        
        var volCurve = data.First(d => d.Volatility != null).Volatility as VolatilityCurve;
        var tenor = data.First().Tenor;
        if (volCurve == null)
        {
          Logger.ErrorFormat("Volatility of type VolatilityCurve expected for CreditSpread MarketVariableType");
          throw new ToolkitException($"No volatilities found for {SurvivalCurve.Name}");
        }

        try
        {
          var volCollection = new VolatilityCollection(new []{tenor});
          CCRCalibrationUtils.CalibrateCreditVolatility(AsOf, volCurve, tenor, SurvivalCurve, DiscountCurve, volCollection, retVal, 40);
          var volatilities = volCollection.GetVolsAt(SurvivalCurve);
          return new ProcessCalibrationData() {FactorLoadings = fl, Volatilities = volatilities, Name = SurvivalCurve.Name};
        }
        catch (ToolkitException e)
        {
          Logger.ErrorFormat("Process Calibration failed for Curve {0}. Returned error {1}", SurvivalCurve.Name, e.Message);
          throw;
        }
        catch (Exception e)
        {
          Logger.ErrorFormat("Process Calibration failed for for Credit {0}. {1}", SurvivalCurve.Name, e.Message);
          throw new ToolkitException($"Exception in SurvivalCurveProcessCalibration for {SurvivalCurve.Name}", e);
        }
        
      }
      else
      {
        Logger.ErrorFormat("Must provide vol object for {0}", SurvivalCurve.Name);
        throw new ToolkitException($"No volatilities found for {SurvivalCurve.Name}");
      }
    }





    #endregion

    #region Helper Methods

    private static double[] Resize(double[] array, int dim)
    {
      if (array.Length == dim)
      {
        return array;
      }
      var retVal = new double[dim];
      dim = Math.Min(array.GetLength(0), dim);
      for (var i = 0; i < dim; ++i)
      {
        retVal[i] = array[i];
      }
      return retVal;
    }

    private static double[,] Resize(double[,] array, int dim0, int dim1)
    {
      var retVal = new double[dim0, dim1];
      dim0 = Math.Min(array.GetLength(0), dim0);
      dim1 = Math.Min(array.GetLength(1), dim1);
      for (var i = 0; i < dim0; ++i)
      {
        for (var j = 0; j < dim1; ++j)
        {
          retVal[i, j] = array[i, j];
        }
      }
      return retVal;
    }

    private static double[] Multiply(double[] v, double[,] m)
    {
      var retVal = new double[m.GetLength(1)];
      for (var i = 0; i < v.Length; ++i)
      {
        for (var j = 0; j < retVal.Length; ++j)
        {
          retVal[j] += v[i] * m[i, j];
        }
      }
      return retVal;
    }

    private static T[] Row<T>(T[,] array, int idx)
    {
      var retVal = new T[array.GetLength(1)];
      for (var i = 0; i < retVal.Length; ++i)
      {
        retVal[i] = array[idx, i];
      }
      return retVal;
    }

    private static double Norm(double[] v)
    {
      double retVal = 0.0;
      for (var i = 0; i < v.Length; ++i)
      {
        retVal += v[i] * v[i];
      }
      return Math.Sqrt(retVal);
    }

    private static string ObjectId(object obj, Tenor t)
    {
      var cc = obj as CalibratedCurve;
      if (cc != null)
      {
        return t.IsEmpty ? cc.Name : string.Format("{0}.{1:S}", cc.Name, t);
      }
      return string.Empty;
    }

    private static double[] Scale(double[] v, double factor)
    {
      for (var i = 0; i < v.Length; ++i)
      {
        v[i] /= factor;
      }
      return v;
    }

    private static double[,] ToMatrix(double[][] jaggedArray)
    {
      if (jaggedArray == null)
      {
        return null;
      }
      var rows = jaggedArray.Length;
      var cols = jaggedArray.Max(v => v.Length);
      var retVal = new double[rows, cols];
      for (var i = 0; i < rows; ++i)
      {
        for (var j = 0; j < jaggedArray[i].Length; ++j)
        {
          retVal[i, j] = jaggedArray[i][j];
        }
      }
      return retVal;
    }


    #endregion


  }
}
