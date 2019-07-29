using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Models.Simulations
{

  /// <summary>
  /// that provides calibrated factor loadings and vols for a single curve
  /// </summary>
  public struct ProcessCalibrationData
  {
    /// <summary>
    /// Curve or Spot Asset name
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Calibrated Factor Loadings
    /// </summary>
    public double[,] FactorLoadings { get; set; }

    /// <summary>
    /// Calibrated Volatilities
    /// </summary>
    public VolatilityCurve[] Volatilities { get; set; }
    
  }

  /// <summary>
  /// Interface for a class that provides calibrated factor
  /// </summary>
  public interface IProcessCalibration
  {

    /// <summary>
    /// Calibrated Factor Loadings
    /// </summary>
    double[,] FactorLoadings { get;  }

    /// <summary>
    /// Calibrated Volatilities
    /// </summary>

    VolatilityCurve[] Volatilities { get; }

    /// <summary>
    /// Perform calibration. 
    /// </summary>
    /// <exception cref="ToolkitException">Thrown if calibration fails</exception>
    void Calibrate(); 

  }

  //public interface IDependencyGraphNode
  //{
  //  IList<IDependencyGraphNode> Dependencies { get; }
  //  void OnUpdate(); 

  //}

  /// <summary>
  /// Forward curve process calibration
  /// </summary>
  [Serializable]
  public class ForwardCurveProcessCalibration : IProcessCalibration
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(ForwardCurveProcessCalibration));
    
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">asOf</param>
    /// <param name="fwdCurve">fwdcurve</param>
    /// <param name="fwdTenors">forward tenors</param>
    /// <param name="inputVols">input vols</param>
    /// <param name="assets">assets</param>
    /// <param name="factorization">factorization</param>
    public ForwardCurveProcessCalibration(Dt asOf, CalibratedCurve fwdCurve, Tenor[] fwdTenors, object[] inputVols,  CalibrationAsset[] assets, MatrixFactorization factorization)
    {
      AsOf = asOf;
      ForwardCurve = fwdCurve;
      ForwardTenors = fwdTenors; 
      InputVolatilities = inputVols;
      CalibrationAssets = assets.Where(a => a.Name == fwdCurve.Name).ToArray();
      Factorization = factorization;
    }

    #endregion

    #region Properties

    /// <summary>
    /// AsOf date
    /// </summary>
    public Dt AsOf { get; private set; }
    
    /// <summary>
    /// Forward curve
    /// </summary>
    public CalibratedCurve ForwardCurve
    {
      get; private set;
    }

    /// <summary>
    /// Forward tenors
    /// </summary>
    public Tenor[] ForwardTenors { get; private set; }
    
    /// <summary>
    /// Factorization
    /// </summary>
    public MatrixFactorization Factorization { get; private set; }

    /// <summary>
    /// Array of input volatilities
    /// </summary>
    public object[] InputVolatilities { get; private set; }

    /// <summary>
    /// Calibration assets
    /// </summary>
    public CalibrationAsset[] CalibrationAssets { get; private set; }

    /// <summary>
    /// Factor loadings
    /// </summary>
    public double[,] FactorLoadings { get; private set; }

    /// <summary>
    /// Volatility curves
    /// </summary>
    public VolatilityCurve[] Volatilities { get; private set; }

    #endregion


    #region Calibration
    /// <summary>
    /// Calibrate factorized vols
    /// </summary>
    public void Calibrate()
    {
      var modules = LoadModules();
      if(modules.IsNullOrEmpty())
        throw new ToolkitException($"Failed to find matching assets for Calibration of IProcess {ForwardCurve.Name}");
      var factorizedVols = CalibrateFwdCurve(modules, modules.First().Type);
      FactorLoadings = factorizedVols.FactorLoadings;
      Volatilities = factorizedVols.Volatilities; 
    }

    private List<CCRCalibrationUtils.FactorData>  LoadModules()
    {
      var modules = new List<CCRCalibrationUtils.FactorData>();
      for (int i = 0; i < CalibrationAssets.Length; i++)
      {
        var calibrationAsset = CalibrationAssets[i];
        var vol = InputVolatilities.Length == 1 ? InputVolatilities[0] : InputVolatilities[i];
        if (Factorization.PrimaryAssets.Contains(calibrationAsset))
        {
          var module = LoadPrimaryModule(calibrationAsset, vol);
          modules.Add(module);
        }

        if (Factorization.SecondaryAssets.Contains(calibrationAsset))
        {
          var module = LoadSecondaryModule(calibrationAsset, vol);
          modules.Add(module);
        }
      }
      return modules; 
    }

    private CCRCalibrationUtils.FactorData LoadPrimaryModule(CalibrationAsset asset, object vol)
    {
      var idx = Array.IndexOf(Factorization.PrimaryAssets, asset);
      return new CCRCalibrationUtils.FactorData(asset.Type, ForwardCurve, asset.Tenor, vol, Row(Factorization.FactorizedCorrelationMatrix, idx), null);
    }
    private CCRCalibrationUtils.FactorData LoadSecondaryModule(CalibrationAsset asset, object vol)
    {
      var idx = Array.IndexOf(Factorization.SecondaryAssets, asset);
      var fl = Multiply(Row(Factorization.FactorizedBetas, idx), Factorization.FactorizedCorrelationMatrix);
      var norm = Norm(fl);
      Tuple<int, double> idiosyncratic = null;
      if (norm < 0.99)
      {
        idiosyncratic = new Tuple<int, double>(Factorization.FactorNameRef[idx], Math.Sqrt(1.0 - norm * norm));
      }
      else
      {
        fl = Scale(fl, norm);
      }

      var factorDataWithIdiosyncraticValue = new CCRCalibrationUtils.FactorData(asset.Type, ForwardCurve, asset.Tenor, vol, fl, idiosyncratic);

      return factorDataWithIdiosyncraticValue;
    }

    private ProcessCalibrationData CalibrateFwdCurve(IEnumerable<CCRCalibrationUtils.FactorData> modules, CCRCalibrationUtils.MarketVariableType type)
    {
      var data = modules.Where(v => v.Obj == ForwardCurve).OrderBy(d => d.Tenor);
      var forwardTenorDates = ForwardTenors.Select(t => Dt.Add(AsOf, t)).ToArray();
      var volCollection = new VolatilityCollection(ForwardTenors);
      
      Logger.VerboseFormat("Attempting Calibration for Curve {0}", ForwardCurve.Name);

      if (data.Any(d => d.Volatility != null))
      {
        var fl = ToMatrix(data.Select(d =>
        { 
          var f = Resize(d.Factors, Factorization.MarketFactorNames.Length);
          var idiosyncratic = d.IdiosyncraticFactor;
          if (idiosyncratic != null)
            f[idiosyncratic.Item1] = idiosyncratic.Item2;
          return f;
        }).ToArray());
        if (type == CCRCalibrationUtils.MarketVariableType.ForwardPrice || type == CCRCalibrationUtils.MarketVariableType.ForwardFx)
        {
          try
          {
            fl = CalibrationUtils.InterpolateFactorLoadings(AsOf, fl, data.Select(d => Dt.Add(AsOf, d.Tenor)).ToArray(), forwardTenorDates);
            CCRCalibrationUtils.FromVolatilityObject(ForwardCurve, data.First(d => d.Volatility != null).Volatility, volCollection);
            return new ProcessCalibrationData() {FactorLoadings = fl, Volatilities = volCollection.GetVolsAt(ForwardCurve), Name = ForwardCurve.Name};
          }
          catch (ToolkitException e)
          {
            Logger.ErrorFormat("Process Calibration failed for Curve {0}. Returned error {1}", ForwardCurve.Name, e.Message);
            throw;
          }
          catch (Exception e)
          {
            Logger.ErrorFormat("Process Calibration failed for Curve {0}. Returned error {1}", ForwardCurve.Name, e.Message);
            throw new ToolkitException($"Exception in ForwardCurveProcessCalibration for {ForwardCurve.Name}", e);
          }
        }
        else
        {
          Logger.ErrorFormat("Object of type {0} is incompatible to {1} MarketVariableType", ForwardCurve.GetType(), type);
          throw new ToolkitException($"Object of type {ForwardCurve.GetType()} is incompatible with MarketVariableType {type}");
        }
      }
      else
      {
        Logger.ErrorFormat("Must provide vol object for {0}", ForwardCurve.Name);
        throw new ToolkitException($"No volatilities found for {ForwardCurve.Name}");
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
