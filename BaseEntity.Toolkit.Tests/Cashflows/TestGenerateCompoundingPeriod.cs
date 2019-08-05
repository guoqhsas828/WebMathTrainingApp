//
// Copyright (c)    2002-2015. All rights reserved.
//

using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Cashflows;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Cashflows
{

  /// <summary>
  /// Consider about the short stub and long stub.
  /// The very short periods which should be ignored.
  /// </summary>
  [TestFixture]
  public class TestGenerateCompoundingPeriod : ToolkitTestBase
  {
    [TestCase(Frequency.Weekly, true, -3)]
    [TestCase(Frequency.Weekly, true, -2)]
    [TestCase(Frequency.Weekly, true, -1)]
    [TestCase(Frequency.Weekly, true, 0)]
    [TestCase(Frequency.Weekly, true, 1)]
    [TestCase(Frequency.Weekly, true, 2)]
    [TestCase(Frequency.Weekly, true, 3)]
    [TestCase(Frequency.Weekly, false, 0)]
    [TestCase(Frequency.Weekly, false, -1)]
    [TestCase(Frequency.Weekly, false, -2)]
    [TestCase(Frequency.Weekly, false, -3)]
    [TestCase(Frequency.BiWeekly, true, 0)]
    [TestCase(Frequency.BiWeekly, true, -1)]
    [TestCase(Frequency.BiWeekly, true, -2)]
    [TestCase(Frequency.BiWeekly, true, -3)]
    [TestCase(Frequency.BiWeekly, true, 1)]
    [TestCase(Frequency.BiWeekly, true, 2)]
    [TestCase(Frequency.BiWeekly, true, 3)]
    [TestCase(Frequency.BiWeekly, false, 0)]
    [TestCase(Frequency.BiWeekly, false, 1)]
    [TestCase(Frequency.BiWeekly, false, 3)]
    [TestCase(Frequency.BiWeekly, false, 2)]
    [TestCase(Frequency.BiWeekly, false, -1)]
    [TestCase(Frequency.BiWeekly, false, -2)]
    [TestCase(Frequency.BiWeekly, false, -3)]
    [TestCase(Frequency.Monthly, true, -5)]
    [TestCase(Frequency.Monthly, true, -4)]
    [TestCase(Frequency.Monthly, true, -3)]
    [TestCase(Frequency.Monthly, true, -2)]
    [TestCase(Frequency.Monthly, true, -1)]
    [TestCase(Frequency.Monthly, true, 0)]
    [TestCase(Frequency.Monthly, true, 1)]
    [TestCase(Frequency.Monthly, true, 2)]
    [TestCase(Frequency.Monthly, true, 3)]
    [TestCase(Frequency.Monthly, true, 4)]
    [TestCase(Frequency.Monthly, true, 5)]
    [TestCase(Frequency.Monthly, true, 10)]
    [TestCase(Frequency.Monthly, false, 5)]
    [TestCase(Frequency.Monthly, false, 4)]
    [TestCase(Frequency.Monthly, false, 3)]
    [TestCase(Frequency.Monthly, false, 2)]
    [TestCase(Frequency.Monthly, false, 1)]
    [TestCase(Frequency.Monthly, false, 0)]
    [TestCase(Frequency.Monthly, false, -1)]
    [TestCase(Frequency.Monthly, false, -2)]
    [TestCase(Frequency.Monthly, false, -3)]
    [TestCase(Frequency.Monthly, false, -4)]
    [TestCase(Frequency.Monthly, false, -5)]
    [TestCase(Frequency.Monthly, false, -10)]
    [TestCase(Frequency.Quarterly, true, -5)]
    [TestCase(Frequency.Quarterly, true, -4)]
    [TestCase(Frequency.Quarterly, true, -3)]
    [TestCase(Frequency.Quarterly, true, -2)]
    [TestCase(Frequency.Quarterly, true, -1)]
    [TestCase(Frequency.Quarterly, true, 0)]
    [TestCase(Frequency.Quarterly, true, 1)]
    [TestCase(Frequency.Quarterly, true, 2)]
    [TestCase(Frequency.Quarterly, true, 3)]
    [TestCase(Frequency.Quarterly, true, 4)]
    [TestCase(Frequency.Quarterly, true, 5)]
    [TestCase(Frequency.Quarterly, true, 20)]
    [TestCase(Frequency.Quarterly, false, 5)]
    [TestCase(Frequency.Quarterly, false, 4)]
    [TestCase(Frequency.Quarterly, false, 3)]
    [TestCase(Frequency.Quarterly, false, 2)]
    [TestCase(Frequency.Quarterly, false, 1)]
    [TestCase(Frequency.Quarterly, false, 0)]
    [TestCase(Frequency.Quarterly, false, -2)]
    [TestCase(Frequency.Quarterly, false, -3)]
    [TestCase(Frequency.Quarterly, false, -4)]
    [TestCase(Frequency.Quarterly, false, -5)]
    [TestCase(Frequency.Quarterly, false, -20)]
    [TestCase(Frequency.SemiAnnual, true, -5)]
    [TestCase(Frequency.SemiAnnual, true, -4)]
    [TestCase(Frequency.SemiAnnual, true, -3)]
    [TestCase(Frequency.SemiAnnual, true, -2)]
    [TestCase(Frequency.SemiAnnual, true, -1)]
    [TestCase(Frequency.SemiAnnual, true, 0)]
    [TestCase(Frequency.SemiAnnual, true, 1)]
    [TestCase(Frequency.SemiAnnual, true, 2)]
    [TestCase(Frequency.SemiAnnual, true, 3)]
    [TestCase(Frequency.SemiAnnual, true, 4)]
    [TestCase(Frequency.SemiAnnual, true, 5)]
    [TestCase(Frequency.SemiAnnual, true, 20)]
    [TestCase(Frequency.SemiAnnual, false, 5)]
    [TestCase(Frequency.SemiAnnual, false, 4)]
    [TestCase(Frequency.SemiAnnual, false, 3)]
    [TestCase(Frequency.SemiAnnual, false, 2)]
    [TestCase(Frequency.SemiAnnual, false, 1)]
    [TestCase(Frequency.SemiAnnual, false, 0)]
    [TestCase(Frequency.SemiAnnual, false, -1)]
    [TestCase(Frequency.SemiAnnual, false, -2)]
    [TestCase(Frequency.SemiAnnual, false, -3)]
    [TestCase(Frequency.SemiAnnual, false, -4)]
    [TestCase(Frequency.SemiAnnual, false, -5)]
    [TestCase(Frequency.SemiAnnual, false, -20)]
    [TestCase(Frequency.Annual, true, -5)]
    [TestCase(Frequency.Annual, true, -4)]
    [TestCase(Frequency.Annual, true, -3)]
    [TestCase(Frequency.Annual, true, -2)]
    [TestCase(Frequency.Annual, true, -1)]
    [TestCase(Frequency.Annual, true, 0)]
    [TestCase(Frequency.Annual, true, 1)]
    [TestCase(Frequency.Annual, true, 2)]
    [TestCase(Frequency.Annual, true, 3)]
    [TestCase(Frequency.Annual, true, 4)]
    [TestCase(Frequency.Annual, true, 5)]
    [TestCase(Frequency.Annual, true, 20)]
    [TestCase(Frequency.Annual, false, 5)]
    [TestCase(Frequency.Annual, false, 4)]
    [TestCase(Frequency.Annual, false, 3)]
    [TestCase(Frequency.Annual, false, 2)]
    [TestCase(Frequency.Annual, false, 1)]
    [TestCase(Frequency.Annual, false, 0)]
    [TestCase(Frequency.Annual, false, -1)]
    [TestCase(Frequency.Annual, false, -2)]
    [TestCase(Frequency.Annual, false, -3)]
    [TestCase(Frequency.Annual, false, -4)]
    [TestCase(Frequency.Annual, false, -5)]
    [TestCase(Frequency.Annual, false, -20)]
    [TestCase(Frequency.Daily, true, -1)]
    [TestCase(Frequency.Daily, true, -2)]
    [TestCase(Frequency.Daily, true, -3)]
    [TestCase(Frequency.Daily, true, -4)]
    [TestCase(Frequency.Daily, true, 0)]
    [TestCase(Frequency.Daily, true, 1)]
    [TestCase(Frequency.Daily, true, 2)]
    [TestCase(Frequency.Daily, true, 3)]
    [TestCase(Frequency.Daily, true, 4)]
    [TestCase(Frequency.Daily, false, -1)]
    [TestCase(Frequency.Daily, false, -2)]
    [TestCase(Frequency.Daily, false, -3)]
    [TestCase(Frequency.Daily, false, -4)]
    [TestCase(Frequency.Daily, false, 0)]
    [TestCase(Frequency.Daily, false, 1)]
    [TestCase(Frequency.Daily, false, 2)]
    [TestCase(Frequency.Daily, false, 3)]
    [TestCase(Frequency.Daily, false, 4)]
    [TestCase(Frequency.None, true, -1)]
    [TestCase(Frequency.None, true, -2)]
    [TestCase(Frequency.None, true, 0)]
    [TestCase(Frequency.None, true, 3)]
    [TestCase(Frequency.None, true, 4)]
    [TestCase(Frequency.None, false, -1)]
    [TestCase(Frequency.None, false, -2)]
    [TestCase(Frequency.None, false, 0)]
    [TestCase(Frequency.None, false, 3)]
    [TestCase(Frequency.None, false, 4)]
    public void TestCompoundingPeriod(Frequency cmpdFreq, bool isBeginShift, int shift)
    {
      foreach (var cal in _cal)
      {
        foreach (var bd in _bd)
        {
          TestCompoundingPeriod(cmpdFreq, isBeginShift, shift, cal, bd);
        }
      }
    }


    private void TestCompoundingPeriod(Frequency cmpdFreq, bool isBeginShift, int shift, Calendar cal,
      BDConvention bd)
    {
      var asOf = new Dt(20140331);
      var irIndex = new InterestRateIndex("Libor", new Tenor(Frequency.Quarterly), Currency.USD,
        DayCount.Actual360, cal, bd, 2);

      var discountCurve = new DiscountCurve(asOf, 0.04);

      var rateCalculator = new ForwardRateCalculator(asOf, irIndex, discountCurve);

      var projectionParams = new ProjectionParams()
      {
        ProjectionType = irIndex.ProjectionTypes[0],
        CompoundingConvention = CompoundingConvention.ISDA,
        CompoundingFrequency = cmpdFreq
      };

      var cycleStart = asOf;
      //extend some days to test Frequency.Daily and Frequency.None
      var cycleEnd = (cmpdFreq == Frequency.Daily || cmpdFreq==Frequency.None)
        ? Dt.Roll(Dt.Add(cycleStart, Frequency.Daily, 14, CycleRule.None), bd, cal)
        : Dt.Roll(Dt.Add(cycleStart, cmpdFreq, 6, CycleRule.None), bd, cal);

      var middlePoint = (cmpdFreq == Frequency.Daily||cmpdFreq==Frequency.None)
        ? Dt.Roll(Dt.Add(cycleStart, Frequency.Daily, 7, CycleRule.None), bd, cal)
        : Dt.Roll(Dt.Add(cycleStart, cmpdFreq, 3, CycleRule.None), bd, cal);

      
      var periodBegin = (isBeginShift)
        ? Dt.Roll(Dt.AddDays(middlePoint, shift, cal), bd, cal)
        : cycleStart;
      var periodEnd = (isBeginShift)
        ? cycleEnd
        : Dt.Roll(Dt.AddDays(middlePoint, shift, cal), bd, cal);

      var fip = new FloatingInterestPayment(asOf, cycleEnd, irIndex.Currency, cycleStart, cycleEnd, periodBegin,
        periodEnd, Dt.Empty, 1.0, 0.01, irIndex.DayCount, projectionParams.CompoundingFrequency,
        projectionParams.CompoundingConvention, rateCalculator, null);

      Dt expectedBegin = periodBegin;
      int diff;
      foreach (var cmpPeriod in fip.CompoundingPeriods)
      {
        Assert.AreEqual(expectedBegin, cmpPeriod.Item1, "Compounding period begin");

        diff = Dt.BusinessDays(cmpPeriod.Item1, cmpPeriod.Item2, cal);
        switch (cmpdFreq)
        {
          //For the Frequency.None, the program just simply produces a period from periodBegin to periodEnd
          case Frequency.None:
            Assert.AreEqual(Dt.Diff(periodBegin, periodEnd), Dt.Diff(cmpPeriod.Item1,cmpPeriod.Item2));
            break;
          //For the Frequency.Daily, we just test if the period range is 1 or not
          case Frequency.Daily:
            Assert.AreEqual(1, diff);
            break;
          case Frequency.Weekly:
          case Frequency.BiWeekly:
            //for the weekly and biweekly compounding frequency, check if the 
            //number of business day in all periods is larger than 2 days. 
            Assert.LessOrEqual(2, diff);
            break;
          default:
            //for the other compounding frequency, check if the number of 
            //business day in all periods is larger than 3 days
            Assert.LessOrEqual(3, diff);
            break;
        }

        expectedBegin = cmpPeriod.Item2;
      }

      const string msg = "Compounding period end";
      diff = Dt.BusinessDays(fip.CompoundingPeriods.Last().Item2, periodEnd, cal);
      switch (cmpdFreq)
      {
        //For the Frequency.None, the end should be equal
        case Frequency.None:
          Assert.AreEqual(periodEnd, fip.CompoundingPeriods.Last().Item2, msg);
          break;
        //For the Frequency.Daily, the end difference should be less than 1.
        case Frequency.Daily:
          Assert.LessOrEqual(diff,1, msg);
          break;
        case Frequency.Weekly:
        case Frequency.BiWeekly:
          //for the weekly and biweekly compounding frequency, 
          //the end difference should be less than 2.
          Assert.LessOrEqual(diff, 2, msg);
          break;
        default:
          //for the other compounding frequency, the end 
          //difference should be less than 3.
          Assert.LessOrEqual(diff, 3, msg);
          break;
      }
      return;
    }

    #region Data

    readonly Calendar[] _cal= {Calendar.NYB, Calendar.TGT, Calendar.LNB};
    readonly  BDConvention[] _bd={BDConvention.Modified, BDConvention.Following, BDConvention.FRN, BDConvention.Preceding};

    #endregion Data


  } // class TestGenerateCompoundingPeriod
}


