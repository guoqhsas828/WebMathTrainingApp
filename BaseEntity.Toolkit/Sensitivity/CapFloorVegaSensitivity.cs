/*
 * CapFloorVegaSensitivity.cs
 *
 *   2005-2010. All rights reserved.
 * 
 */

using System;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>The Cap Vega sensitivity method</summary>
  public enum CapVegaMeasure
  {
    /// <summary>The Black Vega</summary>
    Vega,
    /// <summary>The Sabr Vega</summary>
    VegaSabr
  }

  /// <summary>
  /// The vega hedge allocation method across intermediate strikes( Flat/Weighted)
  /// </summary>
  public enum VegaAllocationMethod
  {
    /// <summary>
    /// Allocates the Vegas in a flat manner
    /// </summary>
    Flat,
    /// <summary>
    /// Allocate the Vegas Linearly 
    /// </summary>
    Weighted
   }
  /// <summary>
  /// class that gets the cap vega hedges 
  /// </summary>
  public class CapVegaHedgeSolver
  {
    /// <summary>
    /// Constructors for the Cap Vega Hedge solver class 
    /// Note: Assumes that the array of maturities is sorted 
    /// </summary>
    public CapVegaHedgeSolver(double[] capletExposures,
                              double strike, 
                              Dt[]maturities,
                              CapFloorPricerBase pricer,
                              CapVegaMeasure measure)
    {
      capletExposures_ = capletExposures;
      strike_ = strike;
      maturities_ = maturities;
      measure_ = measure;
      pricer_ = pricer;
      mktPricerExposures_ = CalculateMarketPricerExposures(pricer);
      
    }


   

    private List<int> GetRowsToRemove(int numHedgers,out double thresHold)
    {
      List<int> rowsToRemove = new List<int>();
      var rows = mktPricerExposures_.GetUpperBound(0) + 1;
      var cols = mktPricerExposures_.GetUpperBound(1) + 1;

      var sortedList = new List<double>();
      var exposureArray = new double[rows];
      var eigenSum = 0.0;
      for(int i=0;i<rows;i++)
      {
        sortedList.Add(mktPricerExposures_[cols - 1, i]);
        eigenSum += mktPricerExposures_[cols - 1, i];
        exposureArray[i] = mktPricerExposures_[cols - 1, i];
      }
        
      sortedList.Sort();
      var numRowsToDelete = rows - numHedgers;

      var removedEigenSum = 0.0;
      for(int i=0;i<numRowsToDelete;i++)
      {
        rowsToRemove.Add(Array.IndexOf(exposureArray,sortedList[i]));
        removedEigenSum += sortedList[i];
      }
      thresHold = (eigenSum!=0.0)?(eigenSum - removedEigenSum)/(eigenSum):1.0;
      return rowsToRemove;
    }

    private List<int> GetRowsToRemove(double thresHold)
    {
      List<int> rowsToRemove = new List<int>();
      var rows = mktPricerExposures_.GetUpperBound(0) + 1;
      var cols = mktPricerExposures_.GetUpperBound(1) + 1;

      var sum = 0.0;

      for (int i = 0; i < rows; i++)
        sum += mktPricerExposures_[cols - 1, i];



      for (int i = 0; i < rows; i++)
      {
        if ((mktPricerExposures_[cols - 1, i] / sum) <= thresHold)
          rowsToRemove.Add(i);
      }
      return rowsToRemove;
    }

    private void ReduceRank(double thresHold)
    {
      List<int> rowsToRemove = GetRowsToRemove(thresHold);
      //First get the new maturities 
      Dt[] updatedMaturities = new Dt[maturities_.Length - rowsToRemove.Count];
      double[] updatedCapletExposures = new double[maturities_.Length - rowsToRemove.Count];
      int idx = 0;
      for (int i = 0; i < maturities_.Length; i++)
      {
        if (!rowsToRemove.Contains(i))
          updatedMaturities[idx++] = maturities_[i];
      }
      maturities_ = updatedMaturities;
      mktPricerExposures_ = CalculateMarketPricerExposures(pricer_);

      //update the caplet exposures as well 
      int index = 0;
      for (int i = 0; i < capletExposures_.Length; i++)
      {
        if (!rowsToRemove.Contains(i))
        {
          updatedCapletExposures[index++] = capletExposures_[i];
        }
        else
        {
          if (i != capletExposures_.Length - 1)
          {
            capletExposures_[i + 1] = capletExposures_[i] + capletExposures_[i + 1];
            continue;
          }
        }
      }
      capletExposures_ = updatedCapletExposures;


    }

    private void ReduceRank(int numHedgers,out double thresHold)
    {
      List<int> rowsToRemove = GetRowsToRemove(numHedgers,out thresHold);
      //First get the new maturities 
      Dt[] updatedMaturities = new Dt[maturities_.Length - rowsToRemove.Count];
      double[] updatedCapletExposures = new double[maturities_.Length - rowsToRemove.Count];
      int idx = 0;
      for (int i = 0; i < maturities_.Length; i++)
      {
        if (!rowsToRemove.Contains(i))
          updatedMaturities[idx++] = maturities_[i];
      }
      maturities_ = updatedMaturities;
      mktPricerExposures_ = CalculateMarketPricerExposures(pricer_);

      //update the caplet exposures as well 
      int index = 0;
      for (int i = 0; i < capletExposures_.Length; i++)
      {
        if (!rowsToRemove.Contains(i))
        {
          updatedCapletExposures[index++] = capletExposures_[i];
        }
        else
        {
          if (i != capletExposures_.Length - 1)
          {
            capletExposures_[i + 1] = capletExposures_[i] + capletExposures_[i + 1];
            continue;
          }
        }
      }
      capletExposures_ = updatedCapletExposures;
    }

    

   

  /// <summary>
  /// Overloaded method that calculates the hedge notionals based on the number of hedging securities 
  /// </summary>
  /// <param name="numHedgers"></param>
  /// <param name="thresHold"></param>
  /// <returns></returns>
   public Dictionary<Dt,double> CalculateHedgeNotionals(int numHedgers,out double thresHold)
   {
     Dictionary<Dt, double> hedgeNotionalDict = new Dictionary<Dt, double>();

     ReduceRank(numHedgers,out thresHold);

     //Convert the 
     double[,] capVegas = mktPricerExposures_;
     double[] capletVegas = capletExposures_;

     var cols = capVegas.GetUpperBound(1) + 1;
     double[] x = new double[capletVegas.Length];
     for (int i = capletVegas.Length - 1; i >= 0; i--)
     {
       var hedgeDelta = capVegas[cols - 1, i];
       var hedgeNotional = (hedgeDelta != 0) ? (capletVegas[i] / hedgeDelta) : 0.0;
       UpdateCapletVegas(capletVegas, hedgeNotional, capVegas, i);
       x[i] = hedgeNotional;
     }

     for (int i = 0; i < maturities_.Length; i++)
     {
       hedgeNotionalDict.Add(maturities_[i], x[i]);
     }
     return hedgeNotionalDict;
   }

   /// <summary>
   /// 
   /// </summary>
   /// <param name="thresHold"></param>
   /// <returns></returns>
   public Dictionary<Dt, double> CalculateHedgeNotionals(double thresHold)
   {
     Dictionary<Dt, double> hedgeNotionalDict = new Dictionary<Dt, double>();

     ReduceRank(thresHold);

     //Convert the 
     double[,] capVegas = mktPricerExposures_;
     double[] capletVegas = capletExposures_;

     var cols = capVegas.GetUpperBound(1) + 1;
     double[] x = new double[capletVegas.Length];
     for (int i = capletVegas.Length - 1; i >= 0; i--)
     {
       var hedgeDelta = capVegas[cols - 1, i];
       var hedgeNotional = (hedgeDelta != 0) ? (capletVegas[i] / hedgeDelta) : 0.0;
       UpdateCapletVegas(capletVegas, hedgeNotional, capVegas, i);
       x[i] = hedgeNotional;
     }

     for (int i = 0; i < maturities_.Length; i++)
     {
       hedgeNotionalDict.Add(maturities_[i], x[i]);
     }
     return hedgeNotionalDict;

   }

    

    private static void UpdateCapletVegas(double[] capletVegas, double value, double[,] hedgeValues, int idx)
    {
      var cols = hedgeValues.GetUpperBound(1) + 1;
      for (int i = 0; i < idx; i++)
      {
        capletVegas[i] -= value * hedgeValues[cols-1,i];
      }
    }

    
    private double[,] CalculateMarketPricerExposures(CapFloorPricerBase pricer)
    {
      //First create the set of market prices 
      //These are essentially caps struck at the same strike but different maturities 
      var mktPricers = new CapFloorPricerBase[maturities_.Length];
     
      for (int i = 0; i < mktPricers.Length; i++)
      {
        // Create market pricer
        var c = pricer.Cap;
        var cap = new Cap(pricer.Settle, maturities_[i], c.Ccy, c.Type, strike_, c.DayCount, c.Freq, c.BDConvention, c.Calendar);
        
        mktPricers[i] = CapFloorPricerBase.CreatePricer(cap, pricer.AsOf, pricer.Settle, new List<RateReset> {new RateReset(pricer.AsOf, pricer.CurrentRate)}, pricer.ReferenceCurve,
                                                      pricer.DiscountCurve, pricer.VolatilityCube);
      }

      //Now, we form a MaturityX Maturity matrix , which contains all the bucketed caplet exposures for the market caps 
      var marketExposures = new double[maturities_.Length,mktPricers.Length];
      
      for(int i=0;i<mktPricers.Length;i++)
      {
        var vegaCalcFn = CapFloorVegaSensitivity.GetVegaCalculatorDelegate(mktPricers[i],
                                                                           Enum.GetName(typeof (CapVegaMeasure),
                                                                                        measure_));
        var exposures = CapFloorVegaSensitivity.BucketCapletExposures(mktPricers[i], new double[]{strike_}, maturities_,
                                                                               vegaCalcFn, VegaAllocationMethod.Flat);

        int rows = exposures.GetUpperBound(0) + 1;
        for (int j = 0; j < rows; j++)
          marketExposures[i, j] = exposures[j, 0];
      }
      return marketExposures;

    }

    
    

    #region data 

    private double[] capletExposures_;
    private double strike_;
    private Dt[] maturities_;
    private double[,] mktPricerExposures_;
    private CapVegaMeasure measure_;
    private CapFloorPricerBase pricer_;

    #endregion 
  }

  /// <summary>
  /// 
  /// </summary>
  public static class CapFloorVegaSensitivity
  {

    #region public methods 

    /// <summary>
    /// Creates the Vega Sensitiviites Data Set
    /// </summary>
    /// <param name="strikes">Strike list of available hedge instruments</param>
    /// <param name="hedgeTenors">Tenor list of available hedge instruments</param>
    /// <param name="pricer">Cap/Floor pricer</param>
    /// <param name="measure">Vega measure</param>
    /// <param name="method">Vega allocation method</param>
    /// <param name="calcCapHedge"></param>
    /// <param name="numHedgers"></param>
    /// <param name="calcVanna"></param>
    /// <param name="calcVolga"></param>
    /// <returns></returns>
    public static DataTable VegaSensitivities(double[] strikes, Tenor[] hedgeTenors, CapFloorPricerBase pricer,
      CapVegaMeasure measure, VegaAllocationMethod method, bool calcCapHedge, int numHedgers, bool calcVanna, bool calcVolga)
    {
      var expiries = ArrayUtil.Convert(hedgeTenors, expiry => Dt.Add(pricer.Settle, expiry)).ToArray();

      var capletVegaExposures = BucketCapletExposures(pricer, strikes, expiries, GetVegaCalculatorDelegate(pricer, Enum.GetName(typeof(CapVegaMeasure), measure)), method);
      double[,] capletVannaExposures = null;
      double[,] capletVolgaExposures = null;
      double[,] hedgeNotionals = null;

      Dictionary<double, double> hedgeCoverage = null;
      if (calcCapHedge)
      {
        double[] varianceCoverage;
        hedgeNotionals = ReverseBootstrap(capletVegaExposures, strikes, expiries, pricer, measure, numHedgers,out varianceCoverage);
        hedgeCoverage = new Dictionary<double, double>();
        for (int i = 0; i < strikes.Length; i++)
          hedgeCoverage.Add(strikes[i], varianceCoverage[i]);

      }

      if (calcVanna)
      {
        capletVannaExposures = BucketCapletExposures(pricer, strikes, expiries,
                                                         GetVegaCalculatorDelegate(pricer, "VannaSabr"), method);
      }
      if (calcVolga)
      {
        capletVolgaExposures = BucketCapletExposures(pricer, strikes, expiries,
                                                     GetVegaCalculatorDelegate(pricer, "VolgaSabr"), method);
      }
      DataTable dt = ToDataTable(pricer, capletVegaExposures, hedgeNotionals, capletVannaExposures, capletVolgaExposures, hedgeTenors,
                         strikes);
      dt.ExtendedProperties.Add("VarianceCoverage", hedgeCoverage);

      return dt;
    }

    /// <summary>
    /// Creates the Vega Sensitiviites Data Set
    /// </summary>
    /// <param name="strikes"></param>
    /// <param name="hedgeTenors"></param>
    /// <param name="pricer"></param>
    /// <param name="measure"></param>
    /// <param name="method"></param>
    /// <param name="calcCapHedge"></param>
    /// <param name="thresHold"></param>
    /// <param name="calcVanna"></param>
    /// <param name="calcVolga"></param>
    /// <returns></returns>
    public static DataTable VegaSensitivities(double[] strikes, Tenor[] hedgeTenors, CapFloorPricerBase pricer,
      CapVegaMeasure measure, VegaAllocationMethod method, bool calcCapHedge, double thresHold, bool calcVanna, bool calcVolga)
    {
      var expiries = ArrayUtil.Convert(hedgeTenors, expiry => Dt.Add(pricer.Settle, expiry)).ToArray();

      var capletVegaExposures = BucketCapletExposures(pricer, strikes, expiries, GetVegaCalculatorDelegate(pricer, Enum.GetName(typeof(CapVegaMeasure), measure)), method);
      double[,] capletVannaExposures = null;
      double[,] capletVolgaExposures = null;
      double[,] hedgeNotionals = null;

      if (calcCapHedge)
      {
        hedgeNotionals = ReverseBootstrap(capletVegaExposures, strikes, expiries, pricer, measure, thresHold);
      }

      if (calcVanna)
      {
        capletVannaExposures = BucketCapletExposures(pricer, strikes, expiries,
                                                         GetVegaCalculatorDelegate(pricer, "VannaSabr"), method);
      }
      if (calcVolga)
      {
        capletVolgaExposures = BucketCapletExposures(pricer, strikes, expiries,
                                                     GetVegaCalculatorDelegate(pricer, "VolgaSabr"), method);
      }
      
      DataTable dt =  ToDataTable(pricer, capletVegaExposures, hedgeNotionals, capletVannaExposures, capletVolgaExposures, hedgeTenors,
                         strikes);
      return dt;
    }


    



    

    #endregion 

    #region internal methods 

    /// <summary>
    /// Buckets the Caplet Exposures 
    /// </summary>
    /// <param name="pricer"></param>
    /// <param name="strikes"></param>
    /// <param name="expiries"></param>
    /// <param name="vegaCalcFn"></param>
    /// <param name="vegaAllocationMethod"></param>
    /// <returns></returns>
    internal static double[,] BucketCapletExposures(CapFloorPricerBase pricer, double[] strikes, Dt[] expiries, Func<CapletPayment, double> vegaCalcFn, VegaAllocationMethod vegaAllocationMethod)
    {

      var capletExposures = new double[expiries.Length, strikes.Length];
      foreach (CapletPayment payment in pricer.Caplets)
      {
        if(pricer.Settle < payment.Expiry)
          InsertInBucket(strikes,expiries,capletExposures,vegaCalcFn,payment,pricer,vegaAllocationMethod);
      }
      return capletExposures;
    }

    internal static Func<CapletPayment, double> GetVegaCalculatorDelegate(CapFloorPricerBase pricer, string funcName)
    {
      switch (funcName)
      {
        case "Vega":
          {
            return pricer.CapletVegaBlack;
          }
        case "VegaSabr":
          {
            return pricer.CapletVegaSabr;
          }
        case "VannaSabr":
          {
            return pricer.CapletVannaSabr;
          }
        case "VolgaSabr":
          {
            return pricer.CapletVolgaSabr;
          }
        default:
          {
            throw new ArgumentException("Invalid cap vega sensitivity method");
          }
      }
    }
    #endregion 

    #region private methods


    private static void InsertInBucket(double[] strikes, Dt[] expiries, double[,] result, Func<CapletPayment, double> vegaFn, CapletPayment payment, CapFloorPricerBase pricer,VegaAllocationMethod method)
    {

      Dt payDate = payment.RateFixing;
      double strike = payment.Strike;
      var discountCurve = pricer.ReferenceCurve;

      var discountFactor = discountCurve.DiscountFactor(payment.PayDt);

      var dt = payment.PeriodFraction;
      var factor = (pricer.VolatilityType == VolatilityType.LogNormal) ? 0.01 : 0.0001;
      //First Compute the Vegas 
      var vega = vegaFn(payment)*discountFactor*dt*factor;
      var savedStrike = strike;

      int rowIdx = Array.FindIndex(expiries, i => (i > payDate ? true : false));
      rowIdx = (rowIdx == -1) ? ((expiries[expiries.Length - 1] < payDate) ? expiries.Length - 1 : 0) : rowIdx;

      //search for the first strike that is just greater than the given strike
      int colIdx = Array.FindIndex(strikes, i => (i >= strike ? true : false));
      colIdx = (colIdx == -1) ? ((strikes[strikes.Length - 1] < strike) ? strikes.Length - 1 : 0) : colIdx;

      //first check if we got an exactmatch or a bracket
      if (ExactMatchFound(strikes, strike) || IsExtrapolated(strikes, strike))
      {
        result[rowIdx, colIdx] += vega;
      }
      else
      {


        //update the strike 
        payment.Strike = strikes[colIdx - 1];
        //Get the vega on the lower bracket
        var vegaLb = vegaFn(payment);

        payment.Strike = strikes[colIdx];
        var vegaUb = vegaFn(payment);

        payment.Strike = savedStrike;
        double wt1, wt2;
        CalculateBracketWeights(vega, vegaLb, vegaUb, savedStrike, strikes[colIdx - 1], strikes[colIdx], method, out wt1, out wt2);

        //NOw the left bucket gets allocated w1*vega(K-h1) 
        result[rowIdx, colIdx - 1] += wt1 * vegaLb;
        //The right bucket gets allocated the remainder
        result[rowIdx, colIdx] += wt2 * vegaUb;
      }
    }

    private static DataTable ToDataTable(PricerBase pricer, double[,] vegaExposures, double[,] hedgeNotionals, double[,] vannaExposures, double[,] volgaExposures,
      Tenor[] hedgeTenors, double[] strikes)
    {
      // Setup table
      var result = new DataTable("VolatilitySensitivity");
      result.Columns.Add("Pricer", typeof(string));
      result.Columns.Add("Expiry", typeof(string));
      result.Columns.Add("Strike", typeof(double));
      result.Columns.Add("Vega", typeof(double));
      if (hedgeNotionals != null)
        result.Columns.Add("HedgeNotional", typeof(double));
      if (vannaExposures != null)
        result.Columns.Add("Vanna", typeof(double));
      if (volgaExposures != null)
        result.Columns.Add("Volga", typeof(double));
      // Add rows
      for (int i = 0; i < hedgeTenors.Length; i++)
      {
        for (int j = 0; j < strikes.Length; j++)
        {
          DataRow row = result.NewRow();
          row["Pricer"] = pricer.Product.Description;
          row["Expiry"] = hedgeTenors[i].ToString("S",null);
          row["Strike"] = strikes[j];
          row["Vega"] = vegaExposures[i, j];
          if (hedgeNotionals != null)
            row["HedgeNotional"] = hedgeNotionals[i, j];
          if (vannaExposures != null)
            row["Vanna"] = vannaExposures[i, j];
          if (volgaExposures != null)
            row["Volga"] = volgaExposures[i, j];
          result.Rows.Add(row);
        }
      }

      // Done
      return result;
    }

    private static double[,] ReverseBootstrap(double[,] capletExposures, double[] strikes, Dt[] expiries, CapFloorPricerBase pricer,
      CapVegaMeasure measure, int numHedgers,out double[] varianceCoverage )
    {
      var result = new double[expiries.Length, strikes.Length];

      int rows = capletExposures.GetUpperBound(0) + 1;
      int cols = capletExposures.GetUpperBound(1) + 1;
      int idx = Array.FindIndex(expiries, i => (i >= pricer.Cap.Maturity ? true : false));
      idx = (idx == -1) ? ((expiries[expiries.Length - 1] < pricer.Cap.Maturity) ? expiries.Length - 1 : 0) : idx;
      Dt maturity = expiries[idx];

      int numHedgingSecurities = idx + 1;
      Dt[] hedgeMaturities = new Dt[numHedgingSecurities];
      Array.Copy(expiries, hedgeMaturities, numHedgingSecurities);

      //Find the nearest Buckets on the strike dimension 
      varianceCoverage = new double[cols];
      for (int i = 0; i < cols; i++)
      {
        var capletVegas = new double[numHedgingSecurities];
        for (int j = 0; j < numHedgingSecurities; j++)
        {
          capletVegas[j] = capletExposures[j, i];
        }
        double thresHold;
        CapVegaHedgeSolver solver = new CapVegaHedgeSolver(capletVegas, strikes[i], hedgeMaturities, pricer, measure);
        Dictionary<Dt, double> hedgeNotionals = solver.CalculateHedgeNotionals(numHedgers,out thresHold);
        varianceCoverage[i] = thresHold;


        foreach (var kvp in hedgeNotionals)
        {
          Dt hedgeMaturity = kvp.Key;
          int index = Array.FindIndex(expiries, date => (date == hedgeMaturity) ? true : false);
          result[index, i] = kvp.Value;
        }


      }
      return result; 
    }

    private static double[,] ReverseBootstrap(double[,] capletExposures, double[] strikes, Dt[] expiries, CapFloorPricerBase pricer,
      CapVegaMeasure measure, double thresHold)
    {
      var result = new double[expiries.Length, strikes.Length];

      int rows = capletExposures.GetUpperBound(0) + 1;
      int cols = capletExposures.GetUpperBound(1) + 1;
      int idx = Array.FindIndex(expiries, i => (i >= pricer.Cap.Maturity ? true : false));
      idx = (idx == -1) ? ((expiries[expiries.Length - 1] < pricer.Cap.Maturity) ? expiries.Length - 1 : 0) : idx;
      Dt maturity = expiries[idx];

      int numHedgingSecurities = idx + 1;
      Dt[] hedgeMaturities = new Dt[numHedgingSecurities];
      Array.Copy(expiries, hedgeMaturities, numHedgingSecurities);

      //Find the nearest Buckets on the strike dimension 

      for (int i = 0; i < cols; i++)
      {
        var capletVegas = new double[numHedgingSecurities];
        for (int j = 0; j < numHedgingSecurities; j++)
        {
          capletVegas[j] = capletExposures[j, i];
        }

        CapVegaHedgeSolver solver = new CapVegaHedgeSolver(capletVegas, strikes[i], hedgeMaturities, pricer, measure);
        Dictionary<Dt, double> hedgeNotionals = solver.CalculateHedgeNotionals(thresHold);


        foreach (var kvp in hedgeNotionals)
        {
          Dt hedgeMaturity = kvp.Key;
          int index = Array.FindIndex(expiries, date => (date == hedgeMaturity) ? true : false);
          result[index, i] = kvp.Value;
        }


      }
      return result;
    }

    private static bool ExactMatchFound<T>(T[] array, T value)
    {
      return (Array.IndexOf(array, value) >= 0);
    }

    private static bool IsExtrapolated(double[] array, double value)
    {
      return (array[0] > value) || (array[array.Length - 1] < value);
    }


    /// <summary>
    /// Calculates the bracket weights.
    /// </summary>
    /// <param name="vega">The vega.</param>
    /// <param name="vegaL">The vega L.</param>
    /// <param name="vegaU">The vega U.</param>
    /// <param name="k">The k.</param>
    /// <param name="kl">The kl.</param>
    /// <param name="ku">The ku.</param>
    /// <param name="method">The method.</param>
    /// <param name="wt1">The WT1.</param>
    /// <param name="wt2">The WT2.</param>
    private static void CalculateBracketWeights(double vega, double vegaL, double vegaU, double k, double kl, double ku, VegaAllocationMethod method, out double wt1, out double wt2)
    {

      switch (method)
      {
        case VegaAllocationMethod.Weighted:
          {
            if (vegaU != 0.0)
            {
              double scaleRatio = (ku - k) / (ku - kl);
              wt1 = (scaleRatio * vega) / (vegaL * scaleRatio + vegaU * (1 - scaleRatio));
              wt2 = ((1 - scaleRatio) * vega) / (scaleRatio * vegaL + vegaU * (1 - scaleRatio));
            }
            else
            {
              wt1 = 1.0;
              wt2 = 0.0;
            }
            break;

          }
        case VegaAllocationMethod.Flat:
          {
            if (vegaU != 0.0)
            {
              wt1 = 0.0;
              wt2 = vega / vegaU;
            }
            else
            {
              wt1 = 0.0;
              wt2 = 1.0;
            }

            break;
          }
        default:
          {
            throw new ArgumentException("Invalid Vega allocation method ");
          }
      }

    }
   


    #endregion

    





  }
}