//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// NUnit tests of CDO pricers, for quick tests
  /// Just wrapped calls to the Main() of the stand-alone program BasketPricerTest
  /// </summary>
  [TestFixture]
  public class Test_IO_PO_CDO_funded : ToolkitTestBase
  {
    const string basketDataFile = "Index Tranche Pricing using Base Correlation Basket.xml";
    const double eps = 0.00001;


    // Assert equal with relative eps
    private static void AssertEqual(double d0, double d1, double eps, string msg)
    {
      Assert.AreEqual(d0, d1, Math.Max(0.001, Math.Max(Math.Abs(d0), Math.Abs(d1))) * eps, msg);
    }

    /// <summary>
    ///   Create an array of pricers from a basket data file
    /// </summary>
    /// <param name="basketDataFile">basket data filename</param>
    /// <returns>Array of synthetic CDO pricers</returns>
    private static SyntheticCDOPricer[] CreatePricers(string basketDataFile)
    {
      string filename = GetTestFilePath(basketDataFile);
      BasketData bd = (BasketData)XmlLoadData(filename, typeof(BasketData));
      return bd.GetSyntheticCDOPricers();
    }

    [Test]
    public void Pv()
    {

      double resultIO_PO = 0;
      double resultFunded = 0;
      double ap;
      double dp;

      SyntheticCDOPricer[] pricers = CreatePricers(basketDataFile);

      for (int i = 0; i < pricers.Length; i++)
      {
        ap = pricers[i].CDO.Attachment;
        dp = pricers[i].CDO.Detachment;

        // create standard funded tranche
        SyntheticCDOPricer cdoFunded = (SyntheticCDOPricer)pricers[i].Clone();
        cdoFunded.CDO.CdoType = CdoType.FundedFixed;

        // create IO funded fixed tranche
        SyntheticCDOPricer ioUnfunded = (SyntheticCDOPricer)pricers[i].Clone();
        ioUnfunded.CDO.CdoType = CdoType.IoFundedFixed;

        // create PO tranche
        SyntheticCDOPricer po = (SyntheticCDOPricer)pricers[i].Clone();
        po.CDO.CdoType = CdoType.Po;

        resultIO_PO = ioUnfunded.Pv() + po.Pv();
        resultFunded = cdoFunded.Pv();

        AssertEqual(resultIO_PO, resultFunded, eps, "IO+PO Pv's for tranche [" + ap.ToString() + "-" + dp.ToString() + "] does not match PV of Funded CDO");
      }
    }

    [Test]
    public void Accrued()
    {
      double resultIO_PO = 0;
      double resultFunded = 0;
      double ap;
      double dp;

      SyntheticCDOPricer[] pricers = CreatePricers(basketDataFile);

      for (int i = 0; i < pricers.Length; i++)
      {
        ap = pricers[i].CDO.Attachment;
        dp = pricers[i].CDO.Detachment;

        // create standard funded tranche
        SyntheticCDOPricer cdoFunded = (SyntheticCDOPricer)pricers[i].Clone();
        cdoFunded.CDO.CdoType = CdoType.FundedFixed;

        // create IO funded fixed tranche
        SyntheticCDOPricer ioUnfunded = (SyntheticCDOPricer)pricers[i].Clone();
        ioUnfunded.CDO.CdoType = CdoType.IoFundedFixed;

        // create PO tranche
        SyntheticCDOPricer po = (SyntheticCDOPricer)pricers[i].Clone();
        po.CDO.CdoType = CdoType.Po;

        resultIO_PO = ioUnfunded.Accrued() + po.Accrued();
        resultFunded = cdoFunded.Accrued();

        AssertEqual(resultIO_PO, resultFunded, eps, "IO+PO Accrued for tranche [" + ap.ToString() + "-" + dp.ToString() + "] does not match Accrued of Funded CDO");
      }
    }

    [Test]
    public void ExpectedLoss()
    {
      double resultIO_PO = 0;
      double resultFunded = 0;
      double ap;
      double dp;

      SyntheticCDOPricer[] pricers = CreatePricers(basketDataFile);

      for (int i = 0; i < pricers.Length; i++)
      {
        ap = pricers[i].CDO.Attachment;
        dp = pricers[i].CDO.Detachment;

        // create standard funded tranche
        SyntheticCDOPricer cdoFunded = (SyntheticCDOPricer)pricers[i].Clone();
        cdoFunded.CDO.CdoType = CdoType.FundedFixed;

        // create IO funded fixed tranche
        SyntheticCDOPricer ioUnfunded = (SyntheticCDOPricer)pricers[i].Clone();
        ioUnfunded.CDO.CdoType = CdoType.IoFundedFixed;

        // create PO tranche
        SyntheticCDOPricer po = (SyntheticCDOPricer)pricers[i].Clone();
        po.CDO.CdoType = CdoType.Po;

        resultIO_PO = ioUnfunded.ExpectedLoss() + po.ExpectedLoss();
        resultFunded = 2*cdoFunded.ExpectedLoss();

        AssertEqual(resultIO_PO, resultFunded, eps, "IO+PO ExpectedLoss for tranche [" + ap.ToString() + "-" + dp.ToString() + "] does not match twixe the ExpectedLoss of Funded CDO");


      }
    }

    [Test]
    public void ExpectedSurvival()
    {
      double resultIO_PO = 0;
      double resultFunded = 0;
      double ap;
      double dp;

      SyntheticCDOPricer[] pricers = CreatePricers(basketDataFile);

      for (int i = 0; i < pricers.Length; i++)
      {
        ap = pricers[i].CDO.Attachment;
        dp = pricers[i].CDO.Detachment;

        // create standard funded tranche
        SyntheticCDOPricer cdoFunded = (SyntheticCDOPricer)pricers[i].Clone();
        cdoFunded.CDO.CdoType = CdoType.FundedFixed;

        // create IO funded fixed tranche
        SyntheticCDOPricer ioUnfunded = (SyntheticCDOPricer)pricers[i].Clone();
        ioUnfunded.CDO.CdoType = CdoType.IoFundedFixed;

        // create PO tranche
        SyntheticCDOPricer po = (SyntheticCDOPricer)pricers[i].Clone();
        po.CDO.CdoType = CdoType.Po;

        resultIO_PO = ioUnfunded.ExpectedSurvival() + po.ExpectedSurvival();
        resultFunded = 2*cdoFunded.ExpectedSurvival();

        AssertEqual(resultIO_PO, resultFunded, eps, "IO+PO ExpectedSurvival for tranche [" + ap.ToString() + "-" + dp.ToString() + "] does not match twice the ExpectedSurvival of Funded CDO");
      }
    }
  } // class Test_IO_PO_CDO_funded

}  
