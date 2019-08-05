//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;
using DividendType = BaseEntity.Toolkit.Base.DividendSchedule.DividendType;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestDividendSchedule : ToolkitTestBase
  {
    private Dt asOf_ = new Dt(26,2,2013);
    private double divYield_ = 0.01;
    private double price_ = 15.0;
    private double expiry_ = 11.0;
    private double sigma_ = 0.5;
    private double strike_ = 15.0;
    private double rfr_ = 0.03;
    private string[] time_ = new[] {"1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y"};
    private double[] div_ = new[] {0.1,0.2,0.3,0.4,0.5,0.6,0.7,0.8,0.9,1.0};
    private double[] mixedDiv_ = new[] { 0.01, 0.015, 0.3, 0.01, 0.5, 0.02, 0.02, 0.3, 0.01, 0.01 };
    private DividendType[] mixedType_ = new[]
                                   {
                                     DividendType.Proportional, DividendType.Proportional, DividendType.Fixed,
                                     DividendType.Proportional, DividendType.Fixed, DividendType.Proportional, DividendType.Proportional, DividendType.Fixed,
                                     DividendType.Proportional, DividendType.Proportional
                                   };
    [Test]
    public void Test()
    {
      var schedule = new DividendSchedule(asOf_);
      for(int i=0;i<time_.Length;++i)
      {
        schedule.Add(Dt.Add(asOf_, time_[i]),div_[i]);
      }
      for(int i=0;i<time_.Length;++i)
      {
        AssertEqual("Time", Dt.Diff(asOf_, Dt.Add(asOf_,time_[i]))/365.25, schedule.GetTime(i),1e-10);
        AssertEqual("Div", div_[i], schedule.GetAmount(i),1e-10);
      }

      // Check serialization
      var clonedSchedule = schedule.CloneObjectGraph(CloneMethod.Serialization);
      for(int i=0;i<time_.Length;++i)
      {
        AssertEqual("Cloned Time",  clonedSchedule.GetTime(i), schedule.GetTime(i),1e-10);
        AssertEqual("Cloned Div", clonedSchedule.GetAmount(i), schedule.GetAmount(i),1e-10);
      }

      // Check Fast Clone
      clonedSchedule = schedule.CloneObjectGraph(CloneMethod.FastClone);
      for (int i = 0; i < time_.Length; ++i)
      {
        AssertEqual("Cloned Time", clonedSchedule.GetTime(i), schedule.GetTime(i), 1e-10);
        AssertEqual("Cloned Div", clonedSchedule.GetAmount(i), schedule.GetAmount(i), 1e-10);
      }
      schedule.Dispose();
    }
    
    [Test]
    public void TestEnumeration()
    {
      var schedule = new DividendSchedule(asOf_, time_.Select((t,i) => new Tuple<Dt, DividendType, double>(Dt.Add(asOf_,t), DividendType.Fixed, div_[i])));
      int idx = 0;
      foreach (var tuple in schedule)
      {
        Assert.AreEqual(tuple.Item1, schedule.GetDt(idx));
        Assert.AreEqual(tuple.Item2, schedule.GetDividendType(idx));
        Assert.AreEqual(tuple.Item3, schedule.GetAmount(idx), 1e-10);
        ++idx;
      }
      schedule.Dispose();
    }


    [Test]
    public void TestPv()
    {
      var labels = new List<string>();
      var values = new List<double>();
      var timer = new Timer();
      timer.Start();
      var schedule = new DividendSchedule(asOf_, time_.Select((t, i) => new Tuple<Dt, DividendType, double>(Dt.Add(asOf_, t), mixedType_[i], mixedDiv_[i])));
      double divPv = schedule.Pv(asOf_, Dt.Add(asOf_, "7Y"), 15.0, new DiscountCurve(asOf_, 0.03));
      labels.Add("divPv");
      values.Add(divPv);
      double divYield = schedule.EquivalentYield(asOf_, 15.0, new DiscountCurve(asOf_, 0.03), Dt.Add(asOf_, "2Y"), Dt.Add(asOf_, "10Y"));
      labels.Add("divYield");
      values.Add(divYield);
      double pv = BlackScholes.P(OptionStyle.European, OptionType.Call, expiry_, price_, strike_, rfr_, divYield_, schedule, sigma_);
      labels.Add("pv");
      values.Add(pv);
      double delta = BlackScholes.Delta(OptionStyle.European, OptionType.Call, expiry_, price_, strike_, rfr_, divYield_, schedule, sigma_);
      labels.Add("delta");
      values.Add(delta);
      double gamma = BlackScholes.Gamma(OptionStyle.European, OptionType.Call, expiry_, price_, strike_, rfr_, divYield_, schedule, sigma_);
      labels.Add("gamma");
      values.Add(gamma);
      double rho = BlackScholes.Rho(OptionStyle.European, OptionType.Call, expiry_, price_, strike_, rfr_, divYield_, schedule, sigma_);
      labels.Add("rho");
      values.Add(rho);
      double theta = BlackScholes.Theta(OptionStyle.European, OptionType.Call, expiry_, price_, strike_, rfr_, divYield_, schedule, sigma_);
      labels.Add("theta");
      values.Add(theta);
      double vega = BlackScholes.Vega(OptionStyle.European, OptionType.Call, expiry_, price_, strike_, rfr_, divYield_, schedule, sigma_);
      labels.Add("vega");
      values.Add(vega);
      double speed = BlackScholes.Speed(OptionStyle.European, OptionType.Call, expiry_, price_, strike_, rfr_, divYield_, schedule, sigma_);
      labels.Add("speed");
      values.Add(speed);
      double vomma = BlackScholes.Vomma(OptionStyle.European, OptionType.Call, expiry_, price_, strike_, rfr_, divYield_, schedule, sigma_);
      labels.Add("vomma");
      values.Add(vomma);
      double vanna = BlackScholes.Vanna(OptionStyle.European, OptionType.Call, expiry_, price_, strike_, rfr_, divYield_, schedule, sigma_);
      labels.Add("vanna");
      values.Add(vanna);
      double zomma = BlackScholes.Zomma(OptionStyle.European, OptionType.Call, expiry_, price_, strike_, rfr_, divYield_, schedule, sigma_);
      labels.Add("zomma");
      values.Add(zomma);
      double color = BlackScholes.Color(OptionStyle.European, OptionType.Call, expiry_, price_, strike_, rfr_, divYield_, schedule, sigma_);
      labels.Add("color");
      values.Add(color);
      double charm = BlackScholes.Charm(OptionStyle.European, OptionType.Call, expiry_, price_, strike_, rfr_, divYield_, schedule, sigma_);
      labels.Add("charm");
      values.Add(charm);
      schedule.Dispose();
      timer.Stop();
      MatchExpects(values, labels, timer.Elapsed);
    }
  }
}
