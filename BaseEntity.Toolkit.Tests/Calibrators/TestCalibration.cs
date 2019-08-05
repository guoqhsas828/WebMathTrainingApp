// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Collections.Generic;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Calibrators;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  [TestFixture, Smoke]
  public class TestCalibration : ToolkitTestBase
  {
    const double epsilon = 0.00000000000001;

    public TestCalibration()
    {
      ExpectsFileName = "data/expects/TestCalibration.expects";
    }

    [Test, Smoke]
    public void TestIRRate()
    {
      Dt today = new Dt(29, 4, 2004);
      int[] years = new int[] { 1, 2, 3, 5, 7, 10 };
      Dt[] mat = new Dt[years.Length];
      int N = years.Length;

      int expectedCount = years.Length;

      Dt[] expectedDate = new Dt[]
      { 
        new Dt(29, 4, 2005),
        new Dt(29, 4, 2006),
        new Dt(29, 4, 2007),
        new Dt(29, 4, 2009),
        new Dt(29, 4, 2011),
        new Dt(29, 4, 2014),
      };

      double[] expectedValue = new double[]
      { 
        0.989937344894297, 
        0.960449265042844,
        0.913408207448221,
        0.818005145170132,
        0.704240685258398,
        0.548967930061516,
      };

      // Create discount Rate Calibrator
      DiscountRateCalibrator calibrator = new DiscountRateCalibrator(today, today);

      // Create discount curve
      DiscountCurve discountCurve = new DiscountCurve(calibrator);

      // Add cd rates
      for( int i=0; i < N; i++ )
      {
        mat[i] = Dt.Add(today, years[i], TimeUnit.Years);
        discountCurve.AddZeroYield(mat[i], (i+1)/100.0, DayCount.Actual360, Frequency.SemiAnnual);
      }

      discountCurve.Fit();

      int numRates = discountCurve.Count;
      Assert.AreEqual(expectedCount, numRates, String.Format("Expected {0} rates, got {1}", expectedCount, numRates));

      for(int i = 0; i < discountCurve.Count; i++ )
      {
        Assert.AreEqual(expectedDate[i], discountCurve.GetDt(i), String.Format("Expected {0}, got {1}", expectedDate[i], discountCurve.GetDt(i)));
      }

      for(int i = 0; i < discountCurve.Count; i++ )
      {
        Assert.AreEqual(expectedValue[i], discountCurve.GetVal(i), epsilon, String.Format("Expected {0}, got {1}", expectedDate[i], discountCurve.GetDt(i)));
      }
    }

    public struct SurvivalCurveNode
    {
      public SurvivalCurveNode(Dt dt, double df, double sp, double dp, double hr)
      {
        this.dt = dt;
        this.df = df;
        this.sp = sp;
        this.dp = dp;
        this.hr = hr;
      }

      public Dt dt;
      public double df;
      public double sp;
      public double dp;
      public double hr;
    }


    [Test, Smoke, Ignore("Unknown not work")]
    public void TestSurvivalBootstrap()
    {
      Dt today = new Dt(29, 4, 2004);
      int[] years = new int[] { 1, 2, 3, 5, 7, 10 };
      Dt[] mat = new Dt[ years.Length ];
      int N = years.Length;

      SurvivalCurveNode[] nodes = {
        new SurvivalCurveNode(new Dt(26, 10, 2004), 1,	0.975308641975309, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(24,  4, 2005), 1,	0.951226947111721, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(21, 10, 2005), 1,	0.927739861997851, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(19,  4, 2006), 1,	0.904832704911484, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(16, 10, 2006), 1,	0.882491156642065, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(14,  4, 2007), 1,	0.860701251539792, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(11, 10, 2007), 1,	0.839449368785723, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt( 8,  4, 2008), 1,	0.818722223877433, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt( 5, 10, 2008), 1,	0.798506860324904, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt( 3,  4, 2009), 1,	0.778790641551450, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(30,  9, 2009), 1,	0.759561242994624, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(29,  3, 2010), 1,	0.740806644402164, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(25,  9, 2010), 1,	0.722515122318160, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(24,  3, 2011), 1,	0.704675242754749, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(20,  9, 2011), 1,	0.687275854044755, 0.0246913580246912, 0.0506970850276515),
        new SurvivalCurveNode(new Dt(18,  3, 2012), 1,	0.670306079870810, 0.0246913580246912, 0.0506970850276515),
        new SurvivalCurveNode(new Dt(14,  9, 2012), 1,	0.653755312466593, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(13,  3, 2013), 1,	0.637613205985936, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt( 9,  9, 2013), 1,	0.621869670035666, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt( 8,  3, 2014), 1,	0.606514863368119, 0.0246913580246915, 0.0506970850276519),
        new SurvivalCurveNode(new Dt( 4,  9, 2014), 1,	0.591539187729400, 0.0246913580246912, 0.0506970850276515),
        new SurvivalCurveNode(new Dt( 3,  3, 2015), 1,	0.576933281859538, 0.0246913580246915, 0.0506970850276519),
        new SurvivalCurveNode(new Dt(30,  8, 2015), 1,	0.562688015640784, 0.0246913580246915, 0.0506970850276519),
        new SurvivalCurveNode(new Dt(26,  2, 2016), 1,	0.548794484390394, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(24,  8, 2016), 1,	0.535244003294335, 0.0246913580246915, 0.0506970850276519),
        new SurvivalCurveNode(new Dt(20,  2, 2017), 1,	0.522028101978426, 0.0246913580246908, 0.0506970850276505),
        new SurvivalCurveNode(new Dt(19,  8, 2017), 1,	0.509138519213526, 0.0246913580246917, 0.0506970850276524),
        new SurvivalCurveNode(new Dt(15,  2, 2018), 1,	0.496567197751464, 0.0246913580246909, 0.0506970850276508),
        new SurvivalCurveNode(new Dt(14,  8, 2018), 1,	0.484306279288465, 0.0246913580246917, 0.0506970850276524),
        new SurvivalCurveNode(new Dt(10,  2, 2019), 1,	0.472348099552947, 0.0246913580246908, 0.0506970850276505),
        new SurvivalCurveNode(new Dt( 9,  8, 2019), 1,	0.460685183514603, 0.0246913580246917, 0.0506970850276524),
        new SurvivalCurveNode(new Dt( 5,  2, 2020), 1,	0.449310240711773, 0.0246913580246910, 0.050697085027651),
        new SurvivalCurveNode(new Dt( 3,  8, 2020), 1,	0.438216160694198, 0.0246913580246921, 0.0506970850276533),
        new SurvivalCurveNode(new Dt(30,  1, 2021), 1,	0.427396008578292, 0.0246913580246912, 0.0506970850276515),
        new SurvivalCurveNode(new Dt(29,  7, 2021), 1,	0.416843020712162, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(25,  1, 2022), 1,	0.406550600447664, 0.0246913580246910, 0.050697085027651),
        new SurvivalCurveNode(new Dt(24,  7, 2022), 1,	0.396512314016857, 0.0246913580246914, 0.0506970850276517),
        new SurvivalCurveNode(new Dt(20,  1, 2023), 1,	0.386721886510268, 0.0246913580246912, 0.0506970850276515),
        new SurvivalCurveNode(new Dt(19,  7, 2023), 1,	0.377173197954459, 0.0246913580246916, 0.0506970850276521),
        new SurvivalCurveNode(new Dt(15,  1, 2024), 1,	0.367860279486448, 0.0246913580246911, 0.0506970850276512),
        new SurvivalCurveNode(new Dt(13,  7, 2024), 1,	0.358777309622586, 0.0246913580246901, 0.0506970850276491),
        new SurvivalCurveNode(new Dt( 9,  1, 2025), 1,	0.349918610619558, 0.0246913580246935, 0.0506970850276561),
        new SurvivalCurveNode(new Dt( 8,  7, 2025), 1,	0.341278644925249, 0.0246913580246888, 0.0506970850276464),
        new SurvivalCurveNode(new Dt( 4,  1, 2026), 1,	0.332852011717217, 0.0246913580246928, 0.0506970850276547),
        new SurvivalCurveNode(new Dt( 3,  7, 2026), 1,	0.324633443526669, 0.0246913580246906, 0.0506970850276501),
        new SurvivalCurveNode(new Dt(30, 12, 2026), 1,	0.316617802945763, 0.0246913580246922, 0.0506970850276535),
        new SurvivalCurveNode(new Dt(28,  6, 2027), 1,	0.308800079416238, 0.0246913580246912, 0.0506970850276515),
        new SurvivalCurveNode(new Dt(25, 12, 2027), 1,	0.301175386097319, 0.0246913580246908, 0.0506970850276505),
        new SurvivalCurveNode(new Dt(22,  6, 2028), 1,	0.293738956810965, 0.0246913580246919, 0.0506970850276528),
        new SurvivalCurveNode(new Dt(19, 12, 2028), 1,	0.286486143062546, 0.0246913580246918, 0.0506970850276526),
        new SurvivalCurveNode(new Dt(17,  6, 2029), 1,	0.279412411135076, 0.0246913580246912, 0.0506970850276515),
        new SurvivalCurveNode(new Dt(14, 12, 2029), 1,	0.272513339255198, 0.0246913580246916, 0.0506970850276521),
        new SurvivalCurveNode(new Dt(12,  6, 2030), 1,	0.265784614829143, 0.0246913580246911, 0.0506970850276512),
        new SurvivalCurveNode(new Dt( 9, 12, 2030), 1,	0.259222031746942, 0.0246913580246915, 0.0506970850276519),
        new SurvivalCurveNode(new Dt( 7,  6, 2031), 1,	0.252821487753191, 0.0246913580246916, 0.0506970850276521),
        new SurvivalCurveNode(new Dt( 4, 12, 2031), 1,	0.246578981882742, 0.0246913580246886, 0.0506970850276459),
        new SurvivalCurveNode(new Dt( 1,  6, 2032), 1,	0.240490611959711, 0.0246913580246937, 0.0506970850276565),
        new SurvivalCurveNode(new Dt(28, 11, 2032), 1,	0.234552572158237, 0.0246913580246916, 0.0506970850276521),
        new SurvivalCurveNode(new Dt(27,  5, 2033), 1,	0.228761150623465, 0.0246913580246915, 0.0506970850276519),
        new SurvivalCurveNode(new Dt(23, 11, 2033), 1,	0.223112727151281, 0.0246913580246916, 0.0506970850276521)
      };

      // Create simple discount curve
      DiscountCurve dc = new DiscountCurve(today, 0.0);

      // Create survival bootstrap
      SurvivalBootstrapCalibrator calibrator =
        new SurvivalBootstrapCalibrator(today,
                                        today,
                                        DayCount.Actual360, //DayCount.None,
                                        Frequency.SemiAnnual,
                                        Calendar.None,
                                        BDConvention.Modified,
                                        0.40,
                                        dc);

      // Create survival curve
      SurvivalCurve survivalCurve = new SurvivalCurve(calibrator);

      // Add cds rates
      for( int i=0; i < N; i++ )
      {
        mat[i] = Dt.Add(today, years[i], TimeUnit.Years);
        survivalCurve.AddCDS(mat[i], 0.03, DayCount.Actual360/*DayCount.None*/, Frequency.SemiAnnual, BDConvention.Modified, Calendar.None);
      }

      survivalCurve.Fit();

      int numNodes = survivalCurve.Count;
      int expectedCount = nodes.Length;
      Assert.AreEqual(expectedCount, numNodes, String.Format("Expected {0} nodes, got {1}", expectedCount, numNodes));

      Dt pdt = survivalCurve.AsOf;
      for(int i = 0; i < nodes.Length; i++ )
      {
        SurvivalCurveNode node = nodes[i];

        Dt dt = survivalCurve.GetDt(i);
        double df = dc.DiscountFactor(dt);
        double sp = survivalCurve.SurvivalProb(dt);
        double dp = survivalCurve.DefaultProb(pdt, dt);
        double hr = survivalCurve.HazardRate(pdt, dt);
        Assert.AreEqual(dt, node.dt, String.Format("dt {0} != {1}", dt, node.dt));
        Assert.AreEqual(df, node.df, epsilon, String.Format("df {0} != {1}", df, node.df));
        Assert.AreEqual(sp, node.sp, epsilon, String.Format("sp {0} != {1}", sp, node.sp));
        Assert.AreEqual(dp, node.dp, epsilon, String.Format("dp {0} != {1}", dp, node.dp));
        Assert.AreEqual(hr, node.hr, epsilon, String.Format("hr {0} != {1}", hr, node.hr));
        pdt = dt;
      }

    }


    [Test, Smoke]
    public void TestSurvivalFit()
    {
      Dt today = new Dt(29, 4, 2004);
      int[] years = new int[] { 1, 2, 3, 5, 7, 10 };
      Dt[] mat = new Dt[years.Length];
      int N = years.Length;

#if Old
      SurvivalCurveNode[] nodes = {
        new SurvivalCurveNode(new Dt(29, 4, 2005), 1, 0.9504336118651200, 0.0495663881348796, 0.0508369649794221),
        new SurvivalCurveNode(new Dt(29, 4, 2006), 1, 0.9034529450481020, 0.0494307716294081, 0.0506942860703420),
        new SurvivalCurveNode(new Dt(29, 4, 2007), 1, 0.8587946178097980, 0.0494307174303652, 0.0506942290528836),
        new SurvivalCurveNode(new Dt(29, 4, 2009), 1, 0.7758842483195860, 0.0965427213571274, 0.0506937836402779),
        new SurvivalCurveNode(new Dt(29, 4, 2011), 1, 0.7010750098895350, 0.0964180399229315, 0.0506942295441959),
        new SurvivalCurveNode(new Dt(29, 4, 2014), 1, 0.6020824104832470, 0.1412011525298640, 0.0506938892114349)
      };
#endif

      // Create simple discount curve
      DiscountCurve dc = new DiscountCurve(today, 0.0);

      // Create survival fit
      Dt settle = Settings.SurvivalCalibrator.UseNaturalSettlement
        ? Dt.Add(today, 1) : today;
      SurvivalFitCalibrator calibrator =
        new SurvivalFitCalibrator(today, settle, 0.40, dc);

      // Create survival curve
      SurvivalCurve survivalCurve = new SurvivalCurve(calibrator);

      // Add cds
      for (int i = 0; i < N; i++)
      {
        mat[i] = Dt.Add(today, years[i], TimeUnit.Years);
        survivalCurve.AddCDS(mat[i], 0.03, DayCount.Actual360, Frequency.Quarterly, BDConvention.None, Calendar.None);
        CDS cds = ((CDS)survivalCurve.Tenors[i].Product);
        cds.LastPrem = new Dt(20, 3, cds.Maturity.Year);
      }

      survivalCurve.Fit();

      int numNodes = survivalCurve.Count;
      int expectedCount = N;
      Assert.AreEqual(expectedCount, numNodes, "count");

      List<double> actuals = new List<double>();
      List<string> labels = new List<string>();
      Dt pdt = survivalCurve.AsOf;
      for (int i = 0; i < N; i++)
      {
        Dt dt = survivalCurve.GetDt(i);
        Dt mt = mat[i];
        if (Settings.CDSCashflowPricer.IncludeMaturityProtection)
          mt = Dt.Add(mt, 1);
        Assert.AreEqual(mt.ToInt(), dt.ToInt(), "dt[" + i + "]");

        double df = dc.DiscountFactor(dt);
        actuals.Add(df); labels.Add("df[" + i + "]");
        double sp = survivalCurve.SurvivalProb(dt);
        actuals.Add(sp); labels.Add("sp[" + i + "]");
        double dp = survivalCurve.DefaultProb(pdt, dt);
        actuals.Add(dp); labels.Add("dp[" + i + "]");
        double hr = survivalCurve.HazardRate(pdt, dt);
        actuals.Add(hr); labels.Add("hr[" + i + "]");
        pdt = dt;
      }

      MatchExpects(actuals, labels, 0);
    }
  }
}
