using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Tests.Numerics
{
  [TestFixture]
  public class TestIntegral
  {
    [Test]
    public void AdaptiveRegular()
    {
      double t0 = 0, t1 = 7;
      double y0 = 0.0135, y1 = 0.0136;
      Func_Double_Double f = (t)=> y0 + (y1 - y0)/(t1 - t0)*(t - t0);
      double result = 0, abserr = 0;
      int numsub = 0;
      Quadrature.AdaptiveRegular(f, t0, t1, 1E-10, 1E-10, 100, 0,
        ref result, ref abserr, ref numsub);
      double expect = 0.5*(y0 + y1)*(t1 - t0);
      Assert.AreEqual(expect, result, 1E-10, "Linear");
      return;
    }

  }
}
