using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// class of calibration asset
  /// </summary>
  public class CalibrationAsset
  {
    /// <summary>
    /// constructor
    /// </summary>
    /// <param name="name">asset name</param>
    /// <param name="tenor">aseet tenor</param>
    /// <param name="type">Market variable type</param>
    public CalibrationAsset(string name, Tenor tenor, CCRCalibrationUtils.MarketVariableType type)
    {
      Name = name;
      Tenor = tenor;
      Type = type;
    }
    /// <summary>
    /// Asset or curve name
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// Tenor of maturity
    /// </summary>
    public Tenor Tenor { get; }
    /// <summary>
    /// Asset type
    /// </summary>
    public CCRCalibrationUtils.MarketVariableType Type { get; }

   /// <summary>
   /// Name.Tenor
   /// </summary>
   /// <returns></returns>
    public override string ToString()
    {
      return $"{Name}{(Tenor.IsEmpty ? "" : ".")}{(Tenor.IsEmpty ? "" : Tenor.ToString())}";
    }

    /// <summary>
    /// Implements by value equality
    /// </summary>
    public override bool Equals(object obj)
    {
      var other = obj as CalibrationAsset; 
      if(other == null)
        return false;
      return Name.Equals(other.Name) && Tenor.Equals(other.Tenor) && Type.Equals(other.Type);
    }

    /// <summary>
    /// Compute hashcode from member values
    /// </summary>
    public override int GetHashCode()
    {
      return Name.GetHashCode() ^ Tenor.GetHashCode() ^ Type.GetHashCode();
    }
  }

  /// <summary>
  /// Class of factorization
  /// </summary>
  public class MatrixFactorization 
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(MatrixFactorization));

    #region Constructors
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="systemicFactorCount">systemic factor count</param>
    /// <param name="primaryAssets">array of calibration asset</param>
    /// <param name="correlationMatrix">correlation matrix</param>
    /// <param name="secondaryAssets">secondary assets</param>
    /// <param name="betas">array of betas</param>
    public MatrixFactorization(int systemicFactorCount, CalibrationAsset[] primaryAssets, double[,] correlationMatrix, CalibrationAsset[] secondaryAssets, double[,] betas)
    {
      PrimaryAssets = primaryAssets; 
      CorrelationMatrix = correlationMatrix;
      SecondaryAssets = secondaryAssets; 
      Betas = betas;
      SystemicFactorCount = systemicFactorCount; 

      FactorNameRef = new Dictionary<int, int>();
      _factorizedCorrelationMatrixAttributes = new Lazy<FactorizedCorrelationMatrixAttributes>(Factorize);

    }

    #endregion

    #region Methods
    /// <summary>
    /// Boolean type. Contains reference or not
    /// </summary>
    /// <param name="reference">referene object</param>
    /// <returns></returns>
    public bool ContainsReference(object reference)
    {
      var id = VolatilityCollection.GetId(reference);
      return PrimaryAssets.Any(a => a.Name == id) || SecondaryAssets.Any(a => a.Name == id);
    }
    #endregion

    #region Public Properties
    /// <summary>
    /// Array of primary assets
    /// </summary>
    public CalibrationAsset[] PrimaryAssets { get; private set; }

    /// <summary>
    /// Correlation matrix
    /// </summary>
    public double[,] CorrelationMatrix { get; private set; }

    /// <summary>
    /// Array of secondary assets
    /// </summary>
    public CalibrationAsset[] SecondaryAssets { get; private set; }

    /// <summary>
    /// Betas
    /// </summary>
    public double[,] Betas { get; private set; }

    /// <summary>
    /// Beta asset names
    /// </summary>
    public string[] BetaAssetNames { get { return SecondaryAssets.Where(a => a.Type != CCRCalibrationUtils.MarketVariableType.CounterpartyCreditSpread).Select(a => a.ToString()).ToArray(); } }
    
    /// <summary>
    /// Systemic factor count
    /// </summary>
    public int SystemicFactorCount { get; private set; }

    /// <summary>
    /// The factorized Primary Correlation Matrix
    /// </summary>
    public double[,] FactorizedCorrelationMatrix => _factorizedCorrelationMatrixAttributes.Value.PrimaryCorrelationMatrix;

    /// <summary>
    /// The factorized Primary Correlation Matrix
    /// </summary>
    public double[,] FactorizedBetas => _factorizedCorrelationMatrixAttributes.Value.Betas;

    /// <summary>
    /// The Market Factor Names for the Universe 
    /// </summary>
    public string[] MarketFactorNames => _factorizedCorrelationMatrixAttributes.Value.MarketFactorNames;

    /// <summary>
    /// AsOf date for the MatrixFactorization
    /// </summary>
    public Dt AsOf { get; set; }

    /// <summary>
    /// A index lookup for the market factors
    /// </summary>
    public Dictionary<int, int> FactorNameRef { get; set; }
    
    #endregion
    
    #region Private Helper Methods

    private FactorizedCorrelationMatrixAttributes Factorize()
    {
      var factorizedMatrix = CorrelationMatrix.Clone() as double[,];
      try
      {
        CalibrationUtils.FactorizeCorrelationMatrix(factorizedMatrix, SystemicFactorCount, false);
      }
      catch (Exception)
      {
        throw new ToolkitException(string.Format("Factorization of the Primary Correlation Matrix failed."));
      }

      var numFactors = Math.Min(SystemicFactorCount, CorrelationMatrix.GetLength(1));
      var factorNames = CCRCalibrationUtils.GetFactorNames(numFactors);
      LoadFactorNames(SecondaryAssets, Betas, factorNames, factorizedMatrix);
      var marketFactorNames = factorNames.ToArray();
      
      Logger.DebugFormat("{0}", String.Join(",", marketFactorNames));
      Logger.DebugFormat(" factorizedMatrix [{0},{1}]", factorizedMatrix.GetLength(0), factorizedMatrix.GetLength(1));
      LogMatrix(factorizedMatrix);
      Logger.DebugFormat("Betas [{0},{1}]", Betas.GetLength(0), Betas.GetLength(1));
      LogMatrix(Betas);

      return new FactorizedCorrelationMatrixAttributes(factorizedMatrix, Betas, marketFactorNames);
    }

    private void LogMatrix(double[,] matrix)
    {
      for (int x = 0; x < matrix.GetLength(0); x++)
      {
        var row = new StringBuilder(); 
        for (int y = 0; y < matrix.GetLength(1); y++)
        {
          row.AppendFormat("{0},", matrix[x, y]);
        }
        Logger.Debug(row.ToString());
      }
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

    private static double Norm(double[] v)
    {
      double retVal = 0.0;
      for (var i = 0; i < v.Length; ++i)
      {
        retVal += v[i] * v[i];
      }
      return Math.Sqrt(retVal);
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

    private void LoadFactorNames(CalibrationAsset[] secondaryAssets, double[,] betas, List<string> factorNames, double[,] primaryFactorLoadings)
    {
      for (int i = 0; i < secondaryAssets.Length; ++i)
      {
        var fl = Multiply(Row(betas, i), primaryFactorLoadings);
        var norm = Norm(fl);
        Tuple<int, double> idiosyncratic = null;

        if (norm < 0.99)
        {
          if (!FactorNameRef.ContainsKey(i))
          {
            FactorNameRef.Add(i, factorNames.Count);
          }

          if(secondaryAssets[i].Type != CCRCalibrationUtils.MarketVariableType.CounterpartyCreditSpread)
            factorNames.Add(secondaryAssets[i].ToString());
        }
      }
    }

    #endregion

    #region Internal Class

    /// <summary>
    /// class of factorized correlation matrix attributes
    /// </summary>
    protected class FactorizedCorrelationMatrixAttributes
    {
      /// <summary>
      /// constructor
      /// </summary>
      /// <param name="primaryCorrelationMatrix">primary correlation matrix</param>
      /// <param name="betas">betas</param>
      /// <param name="marketFactorNames">market factor names</param>
      public FactorizedCorrelationMatrixAttributes(double[,] primaryCorrelationMatrix, double[,] betas, string[] marketFactorNames)
      {
        PrimaryCorrelationMatrix = primaryCorrelationMatrix;
        Betas = betas;
        MarketFactorNames = marketFactorNames;
      }

      /// <summary>
      /// primary correlation matrix
      /// </summary>
      public double[,] PrimaryCorrelationMatrix { get; }

      /// <summary>
      /// Betas
      /// </summary>
      public double[,] Betas { get; }

      /// <summary>
      /// Market Factor names
      /// </summary>
      public string[] MarketFactorNames { get; }
    } 

    #endregion

    #region Data

    private readonly Lazy<FactorizedCorrelationMatrixAttributes> _factorizedCorrelationMatrixAttributes;

    #endregion
  }
}
