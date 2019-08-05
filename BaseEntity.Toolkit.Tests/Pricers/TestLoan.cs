//
// Copyright (c)    2018. All rights reserved.
//

using System.Data;
using System.IO;
using BaseEntity.Configuration;
using log4net;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;
using Double_Object_Fn = System.Func<object,double>;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// TestLoan class.
  /// </summary>
  [TestFixture("Loan With Amortizations")]
  [TestFixture("LoanWithGrid Regression")]
  [TestFixture("LoanWithGridPartialDrawn Regression")]
  [TestFixture("LoanWithMultipleInterestPeriodsAndAmortizations Regression")]
  [TestFixture("LoanWithPrepaymentCurve Regression")]
  [TestFixture("Matured Loan")]
  [TestFixture("SimpleLoan Regression")]
  [TestFixture("Loan With Amortizations Term",
    Ignore = "Fixture not found in XML configuration file")]
  [TestFixture("LoanWithGrid Regression Term",
    Ignore = "Fixture not found in XML configuration file")]
  [TestFixture("LoanWithGridPartialDrawn Regression Term",
    Ignore = "Fixture not found in XML configuration file")]
  [TestFixture("LoanWithMultipleInterestPeriodsAndAmortizations Regression Term",
    Ignore = "Fixture not found in XML configuration file")]
  [TestFixture("LoanWithMultipleInterestPeriodsAndAmortizations Regression Term2",
    Ignore = "Fixture not found in XML configuration file")]
  [TestFixture("LoanWithMultipleInterestPeriodsAndAmortizations Regression Term3",
    Ignore = "Fixture not found in XML configuration file")]
  [TestFixture("LoanWithMultipleInterestPeriodsAndAmortizations Regression Term4",
    Ignore = "Fixture not found in XML configuration file")]
  [TestFixture("LoanWithMultipleInterestPeriodsAndAmortizations Regression Term5",
    Ignore = "Fixture not found in XML configuration file")]
  [TestFixture("LoanWithPrepaymentCurve Regression Term",
    Ignore = "Fixture not found in XML configuration file")]
  [TestFixture("Matured Loan Term",
    Ignore = "Fixture not found in XML configuration file")]
  [TestFixture("SimpleLoan Regression Term",
    Ignore = "Fixture not found in XML configuration file")]
  public class TestLoan : SensitivityTest
  {
    public TestLoan (string name) : base(name)
    {}

    #region Data
    //logger
    private static ILog Log = LogManager.GetLogger(typeof(TestLoan));

    private LoanTestCase testCase_;
    private LoanPricer pricer_;
    private IPricer[] pricers_;
    private string[] pricerNames_;
    #endregion

    #region Constructors
    /// <summary>
    /// Default Constructor
    /// </summary>
    public TestLoan()
    {
    }

    [OneTimeSetUp]
    public void Initialize()
    {
      // Load test case
      testCase_ = TestCase.LoadFromFile<LoanTestCase>(GetTestFilePath(this.TestCaseFile));

      // Load pricers
      pricer_ = testCase_.ToPricer();

      pricers_ = new IPricer[] {pricer_};
      pricerNames_ = new string[] {"Loan"};
    }

    #endregion

    #region Tests
    [Test, Smoke, Category("Distribution")]
    public void Pv()
    {
      TestNumeric(
        pricer_,
        "", 
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).Pv(); }));
    }

    [Test, Smoke, Category("Distribution")]
    public void ModelFullPrice()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).ModelFullPrice(); }));
    }

    [Test, Smoke, Category("Distribution")]
    public void ModelFlatPrice()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).ModelFlatPrice(); }));
    }

    [Test, Smoke, Category("Distribution")]
    public void MarketFullPrice()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).MarketFullPrice(); }));
    }

    [Test, Smoke, Category("Distribution")]
    public void MarketFlatPrice()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).MarketFlatPrice(); }));
    }

    [Test, Smoke]
    public void Accrued()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).Accrued(); }));
    }

    [Test, Smoke]
    public void AccruedInterest()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).AccruedInterest(); }));
    }

    [Test, Smoke]
    public void OptionPrice()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).OptionPrice(); }));
    }

    [Test, Smoke]
    public void OptionPv()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).OptionPv(); }));
    }


    [Test, Smoke]
    public void OptionSpreadValue()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).OptionSpreadValue(); }));
    }

    [Test, Smoke]
    public void OAS()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).OAS(); }));
    }

    [Test, Smoke]
    public void CDSLevel()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).CDSLevel(); }));
    }

    [Test, Smoke]
    public void CDSBasis()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).CDSBasis(); }));
    }

    [Test, Smoke]
    public void RSpread()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).RSpread(); }));
    }

    [Test, Smoke]
    public void HSpread()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).HSpread(); }));
    }

    [Test, Smoke]
    public void IRR()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).Irr(); }));
    }

    [Test, Smoke]
    public void DiscountMargin()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).DiscountMargin(); }));
    }

    [Test, Smoke]
    public void ZSpread()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).ZSpread(); }));
    }

    [Test, Smoke]
    public void SpreadToTwoYearCall()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).SpreadToTwoYearCall(); }));
    }

    [Test, Smoke]
    public void SpreadToThreeYearCall()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).SpreadToThreeYearCall(); }));
    }

    [Test, Category("Sensitivities")]
    public void Rate01()
    {
      IR01(pricers_, pricerNames_);
    }

    [Test, Category("Sensitivities")]
    public void RateDuration()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).RateDuration(); }));
    }

    [Test, Category("Sensitivities")]
    public void RateConvexity()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).RateConvexity(); }));
    }

    [Test, Category("Sensitivities")]
    public void Theta()
    {
      Theta(pricers_, pricerNames_);
    }

    [Test, Category("Sensitivities")]
    public void Spread01()
    {
      Spread01(pricers_, pricerNames_);
    }

    [Test, Category("Sensitivities")]
    public void SpreadDuration()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).SpreadDuration(); }));
    }

    [Test, Category("Sensitivities")]
    public void SpreadConvexity()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).SpreadConvexity(); }));
    }

    [Test, Category("Sensitivities")]
    public void VOD()
    {
      VOD(pricers_, pricerNames_);
    }

    [Test, Category("Sensitivities")]
    public void Recovery01()
    {
      Recovery01(pricers_, pricerNames_);
    }

    [Test, Smoke]
    public void WAL()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).WAL(); }));
    }

    [Test, Smoke]
    public void ExpectedWAL()
    {
      TestNumeric(
        pricer_,
        "",
        new Double_Object_Fn(delegate(object pricer) { return ((LoanPricer)pricer).ExpectedWAL(); }));
    }
    
    /// <summary>
    /// Tests the cashflow generation.
    /// </summary>
    /// <returns></returns>
    [Test, Smoke]
    public void GenerateCashflow()
    {
      DataTable tbl = pricer_.GenerateCashflow(null, pricer_.AsOf).ToDataTable();
      ResultData expects = this.LoadExpects();

      // Clean up problematic columns
      tbl.Columns.Remove("Date");

      //get results
      ResultData results = new ResultData(tbl, 0);

      //make sure we have the right size results
      if (expects.Results[0].Expects == null)
      {
        expects.Results = new ResultData.ResultSet[results.Results.Length];
        for (int i = 0; i < results.Results.Length; i++)
          expects.Results[i] = new ResultData.ResultSet();
      }

      //setup results
      for (int i = 0; i < expects.Results.Length; i++)
      {
        expects.Results[i].Labels = results.Results[i].Labels;
        expects.Results[i].Actuals = results.Results[i].Actuals;
        expects.Results[i].Name = results.Results[i].Name;
      }

      //done
      MatchExpects(expects);
    }

    /// <summary>
    /// Tests the Distribution function.
    /// </summary>
    /// <returns></returns>
    [Test, Smoke]
    public void PerformanceDistribution()
    {
      DataTable tbl = this.ConvertPerformanceDistributionToDataTable(pricer_);
      ResultData expects = this.LoadExpects();

      //get results
      ResultData results = new ResultData(tbl, 0);

      //make sure we have the right size results
      if (expects.Results[0].Expects == null)
      {
        expects.Results = new ResultData.ResultSet[results.Results.Length];
        for (int i = 0; i < results.Results.Length; i++)
          expects.Results[i] = new ResultData.ResultSet();
      }

      //setup results
      for (int i = 0; i < expects.Results.Length; i++)
      {
        expects.Results[i].Labels = results.Results[i].Labels;
        expects.Results[i].Actuals = results.Results[i].Actuals;
        expects.Results[i].Name = results.Results[i].Name;
      }

      //done
      MatchExpects(expects);
    }

    /// <summary>
    /// Tests the Distribution function.
    /// </summary>
    /// <returns></returns>
    [Test, Smoke]
    public void TransitionMatrix()
    {
      DataTable tbl = this.ConvertTransitionMatrixToDataTable(pricer_, Date);
      ResultData expects = this.LoadExpects();

      //get results
      ResultData results = new ResultData(tbl, 0);

      //make sure we have the right size results
      if (expects.Results[0].Expects == null)
      {
        expects.Results = new ResultData.ResultSet[results.Results.Length];
        for (int i = 0; i < results.Results.Length; i++)
          expects.Results[i] = new ResultData.ResultSet();
      }

      //setup results
      for (int i = 0; i < expects.Results.Length; i++)
      {
        expects.Results[i].Labels = results.Results[i].Labels;
        expects.Results[i].Actuals = results.Results[i].Actuals;
        expects.Results[i].Name = results.Results[i].Name;
      }

      //done
      MatchExpects(expects);
    }

    public DataTable ConvertTransitionMatrixToDataTable(LoanPricer pricer, Dt date)
    {
      DataTable tbl = new DataTable(pricer.Loan.Description + "_TransitionMatrix");

      // Setup columns
      tbl.Columns.Add("Level", typeof (string));
      tbl.Columns.Add("Prepay", typeof(double));
      for (int i = pricer.Loan.PerformanceLevels.Length - 1; i >= 0; i--)
        tbl.Columns.Add(pricer.Loan.PerformanceLevels[i], typeof(double));
      tbl.Columns.Add("Default", typeof(double));

      // Move matrix into table
      double[,] matrix = pricer.TransitionMatrix(date);
      int N = matrix.GetLength(0);
      for (int i = N - 1; i >= 0; i--)
      {
        DataRow row = tbl.NewRow();
        string col = tbl.Columns[N - i].Caption;

        // Get probabilities
        row[0] = col;
        for (int j = N - 1; j >= 0; j--)
          row[N - j] = matrix[i, j];

        // Add to tbl
        tbl.Rows.Add(row);
      }

      //Done
      return tbl;
    }

    public DataTable ConvertPerformanceDistributionToDataTable(LoanPricer pricer)
    {
      DataTable tbl = new DataTable(pricer.Loan.Description + "_Distribution");

      // Setup cols
      tbl.Columns.Add("Date", typeof(string));
      tbl.Columns.Add("Prepay", typeof(double));
      for (int i = pricer.Loan.PerformanceLevels.Length - 1; i >= 0; i--)
        tbl.Columns.Add(pricer.Loan.PerformanceLevels[i], typeof(double));
      tbl.Columns.Add("Default", typeof(double));

      // Setup table
      double[,] dist = pricer.PerformanceDistribution();
      for (int t = 0; t < dist.GetLength(0); t++)
      {
        DataRow row = tbl.NewRow();
        int N = dist.GetLength(1);
        // Set date
        //row[0] = pricer.Schedule.GetPaymentDate(t).ToString("MM-dd-yyyy");
        row[0] = pricer.Schedule.GetPaymentDate(t).ToString("dd-MMM-yyyy");
        row[1] = dist[t, N - 1];
        for (int i = 0; i < pricer.Loan.PerformanceLevels.Length; i++)
          row[i + 2] = dist[t, N - 2 - i];
        row[N] = dist[t, 0];
        tbl.Rows.Add(row);
      }

      // Done
      return tbl;
    }

    #endregion

    #region Properties
    /// <summary>
    /// The file the test case is stored in.
    /// </summary>
    public string TestCaseFile { get; set; }

    /// <summary>
    /// The date to use for calculating the Transition Matrix.
    /// </summary>
    public Dt Date => Dt.Add(pricer_.Settle, 1, TimeUnit.Years);

    #endregion
  }


  [TestFixture]
  public class TestLoanEx
  {
    //this is to test the backward compatible, the expect value
    // is got from the risk.
    [Test]
    public void TestLoanWAL()
    {
      const double expect = 3.2002777777777771;
      var path = Path.Combine(SystemContext.InstallDir,
        @"toolkit\test\data\LoanPricers\LoanPricer4175a.xml");
      var pricer = XmlSerialization.ReadXmlFile(path) as LoanPricer;
      if (pricer != null)
      {
        var wal = pricer.WAL();
        NUnit.Framework.Assert.AreEqual(expect, wal, 1e-14);
      }
    }



    [Test]
    public void TestLoanDiscountMarginQuoting()
    {
      //the pricer with flat price quoting convention
      var path1 = Path.Combine(SystemContext.InstallDir,
        @"toolkit\test\data\LoanPricers\LoanPricerFlatPrice.xml");

      //the pricer with discount margin quoting convention
      var path2 = Path.Combine(SystemContext.InstallDir,
        @"toolkit\test\data\LoanPricers\LoanPricerDiscountMargin.xml");
      var p1 = XmlSerialization.ReadXmlFile(path1) as LoanPricer;
      var p2 = XmlSerialization.ReadXmlFile(path2) as LoanPricer;
      if (p1 != null && p2 != null)
      {
        var pv1 = p1.ModelFullPrice();
        var pv2 = p2.ModelFullPrice();
        Assert.AreEqual(pv1, pv2, 3E-13*p1.Notional);
      }
    }
  }
}
