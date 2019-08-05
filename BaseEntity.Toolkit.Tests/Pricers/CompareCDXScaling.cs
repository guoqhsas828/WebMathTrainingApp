//
// Copyright (c)    2015. All rights reserved.
//
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Shared;


using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture("CompareCDXScaling HY7")]
  [TestFixture("CompareCDXScaling HY7-V1")]
  [TestFixture("CompareCDXScaling IG7")]
  public class CompareCDXScalingOldVsNew : ToolkitTestBase
  {

    public CompareCDXScalingOldVsNew(string name) : base(name)
    {}

    #region Data

    private Dt asOf_;
    private Dt settle_;
    private Dt effective_;
    
    private DiscountCurve discountCurve_ = null;
    private SurvivalCurve[] survivalCurves_ = null;

    private List<string> curveTenors_ = null;
    private List<string> indexTenors_ = null;
    private CDX[] cdx_ = null;
    private double[] quotes_ = null;
    private bool quotesArePrices_ = false;
    private double[] scalingWeights_ = null;
    private bool relativeScaling_ = false;

    #endregion // Data

    #region SetUp

    /// <summary>
    ///    Initializer
    /// </summary>
    /// 
    /// <remarks>
    /// This function is called once after a class object is constructed
    /// and before all the tests in this fixture.
    /// </remarks>
    /// 
    [OneTimeSetUp]
    public void Initialize()
    {
      ReInit();
     }

    private void ReInit()
    {
      string indexDataFile = GetTestFilePath(IndexDataFile);
      string discountDataFile = GetTestFilePath(DiscountDataFile);
      string creditDataFile = GetTestFilePath(CreditDataFile);

      BasketData.Index indexData = (BasketData.Index)XmlLoadData(indexDataFile, typeof(BasketData.Index));
      DiscountData discountData = (DiscountData)XmlLoadData(discountDataFile, typeof(DiscountData));
      CreditData creditData = (CreditData)XmlLoadData(creditDataFile, typeof(CreditData));

      curveTenors_ = new List<string>(creditData.TenorNames);
      settle_ = Dt.AddDays(asOf_, 1, Calendar.NYB);
      relativeScaling_ = true;
      adjustDiscountDataDates(asOf_, discountData);
      discountCurve_ = discountData.GetDiscountCurve();
      adjustCreditDataDates(asOf_, creditData);
      survivalCurves_ = creditData.GetSurvivalCurves(discountCurve_);
      Initialize(indexData);
     
    }

    /// <summary>
    /// We want to be able to play around with a set of CDS quotes and just vary the pricing date/maturities
    /// in memory rather than needing a new xml file for each set of dates. Need to call this method before
    /// calling creditData.GetSurvivalCurves
    /// </summary>
    /// <param name="newAsOf"></param>
    /// <param name="creditData"></param>
    private void adjustCreditDataDates(Dt newAsOf,CreditData creditData)
    {
      creditData.AsOf = newAsOf.ToStr("%D"); 
      creditData.Tenors = new string[creditData.TenorNames.Length];
      for(int t=0; t< creditData.TenorNames.Length; ++t)
      {
        Dt maturity = Dt.CDSMaturity(newAsOf, creditData.TenorNames[t]);
        creditData.Tenors[t] = maturity.ToStr("%D");

      }
    }
    /// <summary>
    /// We want to be able to play around with a IR quotes and just vary the pricing date/maturities
    /// in memory rather than needing a new xml file for each set of dates. Need to call this method before
    /// calling discountData.GetDiscountCurve
    /// </summary>
    /// <param name="newAsOf"></param>
    /// <param name="discountData"></param>
    private void adjustDiscountDataDates(Dt newAsOf, DiscountData discountData)
    {
      discountData.AsOf = newAsOf.ToStr("%D");
      discountData.Bootst.MmMaturities = new string[discountData.Bootst.MmTenors.Length];
      for (int t = 0; t < discountData.Bootst.MmTenors.Length; ++t)
      {
        Dt maturity = Dt.Add(newAsOf, discountData.Bootst.MmTenors[t]);
        discountData.Bootst.MmMaturities[t] = maturity.ToStr("%D");
      }

      discountData.Bootst.SwapMaturities = new string[discountData.Bootst.SwapTenors.Length];
      for (int t = 0; t < discountData.Bootst.SwapTenors.Length; ++t)
      {
        Dt maturity = Dt.Add(newAsOf, discountData.Bootst.SwapTenors[t]);
        discountData.Bootst.SwapMaturities[t] = maturity.ToStr("%D");
      }
    }
   
    /// <summary>
    ///   Initialize index data
    /// </summary>
    /// <param name="id">index data</param>
    private void Initialize(BasketData.Index id)
    {
    
      // Check survivalcurves
      if (id.CreditNames != null && id.CreditNames.Length != survivalCurves_.Length)
      {
        SurvivalCurve[] sc = survivalCurves_;
        survivalCurves_ = new SurvivalCurve[id.CreditNames.Length];
        int idx = 0;
        foreach (string name in id.CreditNames)
          survivalCurves_[idx++] = (SurvivalCurve)FindCurve(name, sc);
      }
      // Set up the index data for scaling
      indexTenors_ = new List<string>(id.TenorNames);

      int nTenors = indexTenors_.Count;

      // Create indices for scaling
      Dt firstPremiumDate = new Dt();
      double[] dealPremiums = id.DealPremia;
      double[] indexWeights = id.CreditWeights;

      cdx_ = new CDX[nTenors];
      for (int i = 0; i < nTenors; i++)
      {

        Dt maturity = Dt.CDSMaturity(effective_, indexTenors_[i]);
        cdx_[i] = new CDX(effective_, maturity, id.Currency,
                          dealPremiums[i]/10000.0, id.DayCount,
                          id.Frequency, id.Roll, id.Calendar, indexWeights);
        if (!firstPremiumDate.IsEmpty())
          cdx_[i].FirstPrem = firstPremiumDate;
        cdx_[i].Funded = false;
      }

      // Setup quotes and methods
      quotesArePrices_ = id.QuotesArePrices;
      quotes_ = new double[nTenors];
      for (int i = 0; i < nTenors; ++i)
      {
        quotes_[i] = id.Quotes[i] / 10000.0;
      }
        
      scalingWeights_ = id.ScalingWeights;

      return;
    }

    // Helper
    private static Curve FindCurve(string name, Curve[] curves)
    {
      foreach (Curve c in curves)
        if (String.Compare(name, c.Name) == 0)
          return c;
      throw new System.Exception(String.Format("Curve name '{0}' not found", name));
    }

    #endregion // SetUp

    #region Tests
    [Test]
    public void TestScalingDuration()
    {
      ResultData rd = new ResultData();
      rd.TestName = "Duration";
      rd.Accuracy = 1.0E-1; //for compare old/new accuracy to 2 dp seems reasonable
      rd.Results = new ResultData.ResultSet[5];//
      Timer timer = new Timer();
      Tenor t = new Tenor(6, TimeUnit.Months);
      Dt originalAsOf = asOf_; 
      for (int i = 0; i < 5; ++i)
      {
        asOf_ = Dt.Add(asOf_, t);
        ReInit();
        rd.Results[i] = new ResultData.ResultSet();
        rd.Results[i].Name = i == 0 ? "On-The-Run" : String.Format("{0} Months Off-The-Run", i * 6);
        CalcScalingFactors(CDXScalingMethod.Duration, relativeScaling_, rd.Results[i], timer);
      }
      rd.TimeUsed = timer.Elapsed;
      asOf_ = originalAsOf; 
      MatchExpects(rd);
    }

    [Test]
    public void TestScalingModel()
    {
      ResultData rd = new ResultData();
      rd.TestName = "Model";
      rd.Accuracy = 1.0E-2; //for compare old/new accuracy to 2 dp seems reasonable
      rd.Results = new ResultData.ResultSet[5];//
      Timer timer = new Timer();
      Tenor t = new Tenor(6, TimeUnit.Months);
      Dt originalAsOf = asOf_;
      for (int i = 0; i < 5; ++i)
      {
        asOf_ = Dt.Add(asOf_, t);
        ReInit();
        rd.Results[i] = new ResultData.ResultSet();
        rd.Results[i].Name = i == 0 ? "On-The-Run" : String.Format("{0} Months Off-The-Run", i*6);
        CalcScalingFactors(CDXScalingMethod.Model, relativeScaling_, rd.Results[i], timer);
      }
      rd.TimeUsed = timer.Elapsed;
      asOf_ = originalAsOf;
      MatchExpects(rd);
    }

    /// <summary>
    ///   Calculate scaling factors
    /// </summary>
    /// <param name="method">Scaling method</param>
    /// <param name="rs">Result dataset</param>
    /// <param name="timer">Timer</param>
    /// <returns>Array of scaling factors</returns>
    private double[] CalcScalingFactors( 
      CDXScalingMethod method,
      bool relativeScaling,
      ResultData.ResultSet rs,
      Timer timer)
    {
      
      /***************************calc factors new method****************************************************/
      bool[] includes =
        ArrayUtil.Generate<bool>(survivalCurves_.Length,
                                 delegate(int i)
                                   {
                                     //default to include all
                                     if(scalingWeights_ == null) return true; 
                                     else return scalingWeights_[i] == 0 ? false : true;
                                   });

      CDX[] cleanCDX = ArrayUtil.GenerateIf<CDX>(quotes_.Length, delegate(int i) { return quotes_[i] > 0.0; }, delegate(int i) { return cdx_[i]; });
      double[] cleanQuotes = ArrayUtil.GenerateIf<double>(quotes_.Length, delegate(int i) { return quotes_[i] > 0.0; }, delegate(int i) { return quotes_[i]; });
      string[] cleanTenors = ArrayUtil.GenerateIf<string>(quotes_.Length, delegate(int i) { return quotes_[i] > 0.0; }, delegate(int i) { return indexTenors_[i]; });
      IndexScalingCalibrator calibrator = new IndexScalingCalibrator(asOf_, settle_, cleanCDX, cleanTenors, cleanQuotes, quotesArePrices_, new CDXScalingMethod[] { method }, true, false, discountCurve_, survivalCurves_, includes, 0.4);
      
      if (timer != null)
        timer.Resume();

      Dt[] indexMaturities = ArrayUtil.Generate<Dt>(cleanCDX.Length, delegate(int i) { return cleanCDX[i].Maturity; });
      Dt[] curveMaturities;
      double[] curveFactors;
      IndexScalingCalibrator.MatchScalingFactors(survivalCurves_[0], indexMaturities, calibrator.GetScalingFactorsWithZeros(), true,
                                                   out curveMaturities, out curveFactors);

      if (rs != null)
      {
        rs.Labels = curveTenors_.ToArray();
        rs.Actuals = curveFactors;
      }

      if (timer != null)
        timer.Stop();


      /***************************calc factors old method****************************************************/
      // work out how to setup scaling methods equivalent to automatic off the run matching done above

      CDX[] adjustedCDX = new CDX[curveTenors_.Count];
      double[] adjustedQuotes = new double[curveTenors_.Count];
      CDXScalingMethod[] scalingMethods = new CDXScalingMethod[curveTenors_.Count];
    
      for(int i=0, j=0; i<curveTenors_.Count;i++)
      {   
         if(j>=cleanTenors.Length)
         {
           adjustedCDX[i] = cleanCDX[j-1];
           adjustedQuotes[i] = cleanQuotes[j-1];
           scalingMethods[i] = CDXScalingMethod.Previous;
         } 
         else if(curveMaturities[i]<cleanCDX[j].Maturity)
         {
           adjustedCDX[i] = cleanCDX[j];
           adjustedQuotes[i] = cleanQuotes[j];
           scalingMethods[i] = CDXScalingMethod.Next;
         }
         else if(j<cleanTenors.Length)
         {
           adjustedCDX[i] = cleanCDX[j];
           adjustedQuotes[i] = cleanQuotes[j];
           scalingMethods[i] = method;
           j++;
         }
         
      }


     if (timer != null)
        timer.Resume();

     
      // not interested in override functionality
      double[] overrideFactors = null;

      // Call scaling routine
      double[] factors = new double[adjustedCDX.Length];
      try
      {
        factors = CDXPricer.Scaling(asOf_, settle_, adjustedCDX, curveTenors_.ToArray(), adjustedQuotes, quotesArePrices_,
          scalingMethods, relativeScaling, overrideFactors, discountCurve_, survivalCurves_, scalingWeights_);
      }
      catch (Exception e)
      {
        for (int i = 0; i < adjustedCDX.Length; ++i)
          factors[i] = Double.NaN;
        Console.WriteLine(e.Message);
      }
      finally
      {
        ;
      }
      
      if (timer != null)
        timer.Stop();

      if (rs != null)
      {
        rs.Expects = factors; 
      }

      return factors;
    }


    
    #endregion // Tests

    #region Properties
   

    /// <summary>
    ///  Array of scaling methods
    /// </summary>
    public string ScalingMethods { get; set; } = null;

    public string IndexDataFile { get; set; }

    public string DiscountDataFile { get; set; }

    public string CreditDataFile { get; set; }

    public string AsOf
    {
      set { asOf_ = Dt.FromStr(value, "%Y%m%d"); }
    }


    public string Effective
    {
      set { effective_ = Dt.FromStr(value, "%Y%m%d"); }
    }


   
    #endregion // Properties
  }
}
