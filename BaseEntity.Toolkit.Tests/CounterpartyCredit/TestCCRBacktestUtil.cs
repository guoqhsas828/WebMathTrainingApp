//
// Copyright (c)    2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Ccr;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.CounterpartyCredit
{
  [TestFixture]
  public class TestCCRBacktestUtil
  {
   [Test]
   public void ProportionOfFailuresTest()
   {
    // System.Diagnostics.Debugger.Break();
     var rng = new Random();
     int sampleSize = 1000;
     var confidenceIntervals = new[]{0.75, 0.90, 0.95, 0.975, 0.99}; 
     for (int i = 0; i < confidenceIntervals.Length; ++i)
     {
       int exceptions = 0;
       double ci = confidenceIntervals[i];
       for (int j = 0; j < sampleSize; ++j)
       {
         var sample = rng.NextDouble();
         if (sample > ci)
           exceptions++;
       }
       bool pass = BacktestUtil.ProportionOfFailuresTest(ci, sampleSize, exceptions);
       Assert.IsTrue(pass,
         String.Format("POF({0:P2}), {1:P3} exceptions", ci, (double)exceptions / (double)sampleSize)) ; 
     }
   }

   [Test]
   public void ChristoffersenIntervalForecastTest()
   {
   //  System.Diagnostics.Debugger.Break();
     var rng = new Random();
     int sampleSize = 1000; 
     var confidenceIntervals = new[] { 0.1, 0.25, 0.5, 0.75, 0.90, 0.95, 0.975, 0.99 };
     var exceptions = new bool[sampleSize];
     for (int i = 0; i < confidenceIntervals.Length; ++i)
     {
       double ci = confidenceIntervals[i];
       int count = 0; 
       for (int j = 0; j < sampleSize; ++j)
       {
         var sample = rng.NextDouble();
         exceptions[j] = (sample > ci);
         if (sample > ci)
           count++;
       }
       bool pass = BacktestUtil.ChristoffersenIntervalForecastTest(ci, exceptions);
       Assert.IsTrue(pass,
         String.Format("Interval Forecast({0:P2}), {1:P3} exceptions", ci, (double)count / (double)sampleSize));
     }
   }
  }
}
