//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.IO;
using NUnit.Framework;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Tests;
using xml = BaseEntity.Toolkit.Util.XmlSerialization;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestLoanPaymentSchedule 
  {

    [TestCase("loanPastMaturityPricer")]
    [TestCase("LoanWithAmortizationPricer")]
    [TestCase("LoanWithAmortizationTermPricer")]
    [TestCase("LoanWithGridPartialDrawnPricer")]
    [TestCase("LoanWithGridPartialDrawnTermPricer")]
    [TestCase("LoanWithGridPricer")]
    [TestCase("LoanWithGridTermPricer")]
    [TestCase("LoanWithMIPAndAmortizationPricer")]
    [TestCase("LoanWithMIPAndAmortizationTerm2Pricer")]
    [TestCase("LoanWithMIPAndAmortizationTerm3Pricer")]
    [TestCase("LoanWithMIPAndAmortizationTerm4Pricer")]
    [TestCase("LoanWithMIPAndAmortizationTerm5Pricer")]
    [TestCase("LoanWithMIPAndAmortizationTermPricer")]
    [TestCase("loanWithPrepaymentCurvePricer")]
    [TestCase("LoanWithPrepaymentCurveTermPricer")]
    [TestCase("SimpleloanRegressionPricer")]
    [TestCase("SimpleLoanTermPricer")]
    public void TestLoanPs(string fileName)
    {
      var filePath = String.Format(@"toolkit\test\data\LoanPricers\{0}.xml", fileName);
      var file = Path.Combine(BaseEntityContext.InstallDir, filePath);
      var pricer = xml.ReadXmlFile(file) as LoanPricer;
      if (pricer == null)
        throw new ArgumentException("The pricer is not exist");
      TestPsCfConsistent(pricer);
    }

    [TestCase("0589")]
    [TestCase("1298")]
    [TestCase("1961")]
    [TestCase("1965")]
    [TestCase("1970")]
    [TestCase("1973")]
    [TestCase("1976")]
    [TestCase("1980")]
    [TestCase("2953")]
    [TestCase("2954")]
    [TestCase("4176")]
    [TestCase("4177")]
    [TestCase("4535")]
    [TestCase("5002")]
    [TestCase("5003")]
    [TestCase("5004")]
    [TestCase("13814")]
    //  [TestCase("4175")]  this test may indicate a bug in the legacy cash flow method
    public void TestLoanPsMore(string number)
    {
      var filePath = String.Format(@"toolkit\test\data\LoanPricers\LoanPricer{0}.xml", number);
      var file = Path.Combine(SystemContext.InstallDir, filePath);
      var pricer = xml.ReadXmlFile(file) as LoanPricer;
      if (pricer == null)
        throw new ArgumentException("The pricer is not exist");

      TestPsCfConsistent(pricer);
      TestYtc(pricer);
    }


    private static void TestYtc(LoanPricer pricer)
    {
      var loan = pricer.Loan;
      var isTerm = loan.LoanType == LoanType.Term;
      double yt1ycallCf = 0.0, yt1ycallPs = 0.0;
      double yt2ycallCf = 0.0, yt2ycallPs = 0.0;
      double yt3ycallCf = 0.0, yt3ycallPs = 0.0;
      double ytwcf = 0.0, ytwps = 0.0;
      SetUsePaymentScheduleForLoan(false);
      if (isTerm)
      {
        yt1ycallCf = pricer.YieldToOneYearCall();
        yt2ycallCf = pricer.YieldToTwoYearCall();
        yt3ycallCf = pricer.YieldToThreeYearCall();
        ytwcf = pricer.YieldToWorst();
      }
      SetUsePaymentScheduleForLoan(true);
      if (isTerm)
      {
        yt1ycallPs = pricer.YieldToOneYearCall();
        yt2ycallPs = pricer.YieldToTwoYearCall();
        yt3ycallPs = pricer.YieldToThreeYearCall();
        ytwps = pricer.YieldToWorst();
      }
      Assert.AreEqual(yt1ycallCf, yt1ycallPs, 1E-14, "Yield to 1 year");
      Assert.AreEqual(yt2ycallCf, yt2ycallPs, 1E-14, "Yield to 2 year");
      Assert.AreEqual(yt3ycallCf, yt3ycallPs, 1E-14, "Yield to 3 year");
      Assert.AreEqual(ytwcf, ytwps, 1E-14, "Yield to worst");
    }


    private static void TestPsCfConsistent(LoanPricer pricer)
    {
      SetUsePaymentScheduleForLoan(false);
      var cf = pricer.GenerateCashflow(null, pricer.Settle);
      SetUsePaymentScheduleForLoan(true);
      var ps = pricer.GetPaymentSchedule(null, pricer.Settle, Dt.Empty);
      var cfFromPs = PaymentScheduleUtils.FillCashflow(null, ps,
        pricer.Settle, 1.0, 0.0);
      if (cf.Count > 0 && cfFromPs.Count > 0)
        TestPaymentCashflowUtil.AssertEqualCashflows(cf, cfFromPs);
    }


    internal static void SetUsePaymentScheduleForLoan(bool enable)
    {
      var field = typeof(LoanPricer).GetField(
        "_usePaymentSchedule",
        System.Reflection.BindingFlags.Static |
        System.Reflection.BindingFlags.NonPublic);
      field.SetValue(null, enable);
    }
  }
}
