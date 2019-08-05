//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestHybrids : ToolkitTestBase
  {
    [Test]
    public void Bgm2DCdf()
    {
      var pars = new[] {0.3, 0.0, 0.2, 0.0, 0.4};
      var bgm2DPdf = new ShiftedBgm2DPdf(pars, false, 50);
      var fn = new Payoff2DFn((f, l) => 1.0);
      fn.PayoffKinksF = new[] {1e-5, 2000};
      fn.PayoffKinksL = new[] {1e-5, 16};
      double pdf = bgm2DPdf.Integrate(fn, new[] {98, 0.04}, 10);
      Assert.AreEqual(1.0, pdf, 3e-4);
    }

    [Test]
    public void ShiftedBgm2DCdf()
    {
      var pars = new[] {0.3, 20, 0.2, 0.03, 0.4};
      var bgm2DPdf = new ShiftedBgm2DPdf(pars, false, 50);
      var fn = new Payoff2DFn((f, l) => 1.0);
      fn.PayoffKinksF = new[] {1e-5, 2000};
      fn.PayoffKinksL = new[] {1e-5, 16};
      double pdf = bgm2DPdf.Integrate(fn, new[] {98, 0.8}, 10);
      Assert.AreEqual(1.0, pdf, 3e-4);
    }

    [Test]
    public void Bgm2DCallPrice()
    {
      var pars = new[] {0.3, 0.0, 0.2, 0.0, 0.4};
      var bgm2DPdf = new ShiftedBgm2DPdf(pars, false, 50);
      const double strike = 98;
      var fn1 = new Payoff2DFn((f, l) => Math.Max(f - strike, 0));
      fn1.PayoffKinksF = new[] {strike, 400};
      fn1.PayoffKinksL = new[] {0.0, 10};
      double price = bgm2DPdf.Integrate(fn1, new[] {98, 0.04}, 1);
      double target = Black.P(OptionType.Call, 1, 98, 98, pars[0]);
      Assert.AreEqual(target, price, 5e-4);
    }


    [Test]
    public void ShiftedBgm2DCallPrice()
    {
      var pars = new[] {0.3, 20, 0.2, 0.03, 0.4};
      var bgm2DPdf = new ShiftedBgm2DPdf(pars, false, 50);
      const double strike = 98;
      var fn1 = new Payoff2DFn((f, l) => Math.Max(f - strike, 0));
      fn1.PayoffKinksF = new[] {strike, 200};
      fn1.PayoffKinksL = new[] {0.0, 10.0};
      double price = bgm2DPdf.Integrate(fn1, new[] {98, 0.04}, 0.2);
      double target = Black.P(OptionType.Call, 0.2, 98 - pars[1], 98 - pars[1], pars[0]);
      Assert.AreEqual(target, price, 5e-4);
    }

    [Test]
    public void BgmOutPerformanceOptions()
    {
      var pars = new[] {0.3, 0.0, 0.2, 0.0, 0.4};
      var bgm2DPdf = new ShiftedBgm2DPdf(pars, true, 1);
      double A = 100;
      double B = 90;
      double t = 5;
      double vol = Math.Sqrt(pars[0]*pars[0] + pars[2]*pars[2] - 2*pars[4]*pars[0]*pars[2]);
      double analyticPrice = 90*Black.P(OptionType.Call, t, A/B, 1, vol);
      var fn = new Payoff2DFn((f, l) => Math.Max(f - l, 0.0));
      fn.PayoffKinksF = new[] {1e-5, 3000};
      fn.PayoffKinksL = new[] {1e-5, 3000};
      double modelPrice = bgm2DPdf.Integrate(fn, new double[] {100, 90}, t);
      Assert.AreEqual(0.0, modelPrice - analyticPrice, 6e-4);
    }
  }
}