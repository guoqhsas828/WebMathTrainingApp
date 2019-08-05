//
// Copyright (c)    2002-2015. All rights reserved.
//

using System;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Cashflows
{
  [TestFixture("TestSwapScheduleEOMEffMat")]
  [TestFixture("TestSwapScheduleEOMFirst")]
  [TestFixture("TestSwapScheduleEOMLast")]
  [TestFixture("TestSwapScheduleNoEOMEffMat")]
  [TestFixture("TestSwapScheduleNoEOMFirst")]
  [TestFixture("TestSwapScheduleNoEOMLast")]
  public class SwapCashflowsTest : ToolkitTestBase
  {

    public SwapCashflowsTest(string name) : base(name)
    {}

    // public properties, to be set via <fixture/>
    public DayCount DayCount { get; set; }
    public int AsOf { get; set; }
    public int Effective { get; set; }
    public int FirstCoupon { get; set; }
    public int LastCoupon { get; set; }
    public int Maturity { get; set; }
    public Frequency Freq { get; set; }
    public bool EOMRule { get; set; }
    public bool PeriodAdjust { get; set; }
    public BDConvention Roll { get; set; }


    /// <summary>
    ///  Generate a swap payment schedule and compare just its schedule dates
    ///  against expected results.
    /// </summary>
    /// <returns></returns>
    [Test]
    public void TestSwapSchedule()
    {
      const double notional = 1000*1000;
      const double coupon = .1;

      var product = new SwapLeg(ToDt(Effective), ToDt(Maturity), Currency.USD, coupon,
                                DayCount, Freq, Roll, Calendar.NYB, !PeriodAdjust);
      if (EOMRule)
        product.CycleRule = CycleRule.EOM;
      product.FirstCoupon = ToDt(FirstCoupon);
      product.LastCoupon = ToDt(LastCoupon);

      var dc = new DiscountCurve(ToDt(AsOf), 0.05);

      var bp = new SwapLegPricer(product, ToDt(AsOf), ToDt(AsOf),
                                 notional, dc,
//        new InterestRateIndex("LIBOR", Tenor.Parse("3M"), 
                                 //Frequency.Quarterly, Currency.USD, DayCount.Thirty360, Calendar.NYB, BDConvention.Modified, 2, null, Frequency.Daily), 
                                 null, dc, null, null, null);

      var timer = new Timer();
      timer.Start();
      PaymentSchedule ps = bp.GetPaymentSchedule(null, ToDt(Effective));
      DataTable dt = ps.ToDataTable();
      timer.Stop();

      Dt[] payDts = ps.GetPaymentDates().ToArray();

      CycleRule rule = EOMRule ? CycleRule.EOM : CycleRule.None;
      CashflowFlag flags = (PeriodAdjust ? 0 : CashflowFlag.AccrueOnCycle) | CashflowFlag.RollLastPaymentDate |
                           CashflowFlag.RespectLastCoupon;
      var sched = new Schedule(product.Effective, product.Effective,
                               ToDt(FirstCoupon), ToDt(LastCoupon), product.Maturity, product.Freq,
                               product.BDConvention, product.Calendar, rule, flags);
      int count = payDts.Length;
      var pay_Dts = new Dt[sched.Count];
      for (int i = 0; i < pay_Dts.Length; i++)
        pay_Dts[i] = sched.GetPaymentDate(i);
      Assert.AreEqual(count, sched.Count, "Count");

      if (count < sched.Count) count = sched.Count;
      for (int i = 0; i < count; ++i)
        Assert.AreEqual(payDts[i].ToInt(), sched.GetPaymentDate(i).ToInt(), "PayDt[" + i + ']');

      //Dt defaultLastCoupon = Schedule.DefaultLastCouponDate(product.FirstCoupon,
      //  product.Maturity, product.Freq, product.BDConvention, product.Calendar, rule);
      //Assert.AreEqual("LastCoupon", product.LastCoupon.ToInt(), defaultLastCoupon.ToInt());
      //Dt defaultFirstCoupon = Schedule.DefaultFirstCouponDate(product.Effective,
      //  product.LastCoupon, product.Maturity, product.Freq, rule);
      //Assert.AreEqual("FirstCoupon", product.FirstCoupon.ToInt(), defaultFirstCoupon.ToInt());

      ToResultData(dt, timer.Elapsed);
    }

    private static double FromStr(object o)
    {
      var s = (string) o;
      Dt d = Dt.FromStr(s);
      int i = d.ToInt();
      return (double) i;
    }

    private static Dt ToDt(int date)
    {
      return date <= 0 ? new Dt() : new Dt(date);
    }

    public void ToResultData(DataTable table, double timeUsed)
    {
      int rows = table.Rows.Count;
      int cols = 5;

      var accrualStartDate = new double[rows];
      var accrualEndDate = new double[rows];
      var periodStartDate = new double[rows];
      var periodEndDate = new double[rows];
      var payDt = new double[rows];
      var labels = new string[rows];

      for (int i = 0; i < rows; i++)
      {
        DataRow row = table.Rows[i];
        accrualStartDate[i] = FromStr(row["Accrual Start"]);
        accrualEndDate[i] = FromStr(row["Accrual End"]);
        periodStartDate[i] = FromStr(row["Period Start"]);
        periodEndDate[i] = FromStr(row["Period End"]);
        payDt[i] = FromStr(row["Payment Date"]);
        labels[i] = String.Format("Payment #{0}", i);
      }

      ResultData rd = LoadExpects();
      if (rd.Results.Length == 1 && rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[cols];
        for (int j = 0; j < cols; j++)
        {
          rd.Results[j] = new ResultData.ResultSet();
        }
      }
      else if (rd.Results[0].Expects.Length != rows)
      {
        throw new Exception(String.Format("Number of generated cashflows[{0}] doesn't match expected[{1}]",
                                          rows, rd.Results[0].Expects.Length));
      }

      rd.Results[0].Name = "Accrual Start";
      rd.Results[0].Labels = labels;
      rd.Results[0].Actuals = accrualStartDate;

      rd.Results[1].Name = "Accrual End";
      rd.Results[1].Labels = labels;
      rd.Results[1].Actuals = accrualEndDate;

      rd.Results[2].Name = "Period Start";
      rd.Results[2].Labels = labels;
      rd.Results[2].Actuals = periodStartDate;

      rd.Results[3].Name = "Period End";
      rd.Results[3].Labels = labels;
      rd.Results[3].Actuals = periodEndDate;

      rd.Results[4].Name = "Pay Dt";
      rd.Results[4].Labels = labels;
      rd.Results[4].Actuals = payDt;

      rd.TimeUsed = timeUsed;

      MatchExpects(rd);
    }
  }
}