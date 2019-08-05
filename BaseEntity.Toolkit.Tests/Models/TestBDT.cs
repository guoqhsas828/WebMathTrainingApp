//
// Compare BDT with existing numbers
// Copyright (c)    2002-2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
	[TestFixture]
  public class TestBDT : ToolkitTestBase
	{
		const double epsilon = 0.000001;

		//
		// Classic Black-Derman-Toy example, done and re-done
		// many times in the literature.
		//
		[Test, Smoke]
		public void example1()
		{
			int lastSlice = 5;
			double [] df = new double[lastSlice]; 
			double [] sigma = new double[lastSlice]; 
			double delta = 1.0;
			double [] rates = new double [lastSlice*(lastSlice-1)/2 + lastSlice];

			df[0] = RateCalc.PriceFromRate(0.10, delta, Frequency.Annual);
			df[1] = RateCalc.PriceFromRate(0.11, delta*2, Frequency.Annual);
			df[2] = RateCalc.PriceFromRate(0.12, delta*3, Frequency.Annual);
			df[3] = RateCalc.PriceFromRate(0.125, delta*4, Frequency.Annual);
			df[4] = RateCalc.PriceFromRate(0.13, delta*5, Frequency.Annual);

			sigma[0] = 0.20;
			sigma[1] = 0.19;
			sigma[2] = 0.18;
			sigma[3] = 0.17;
			sigma[4] = 0.16;

			BDT.Tree(df, sigma, delta, rates );

		  double[] expects = new double [15] {
				0.1,
				0.0979156, 0.14318,
				0.0976, 0.137669, 0.194187,
				0.0871723, 0.118303, 0.160552, 0.217888,
				0.0865344, 0.113405, 0.148619, 0.194767, 0.255246
			};

			int treeIndex = 0;
			for (int slice=0; slice<lastSlice; slice++) {
			  for (int node=0; node<=slice; node++) {
			    Assert.AreEqual( expects[treeIndex],
													 rates[treeIndex],
													 epsilon,
													 String.Format("Rate at [slice: {0}, node: {1}]", slice, node)
													 );
					++treeIndex;
				}
			}
		}// example1()

		//
		// Klose/Yuan "Seminar Financial Engineering" example
		// University of Vienna, Summer Term 2003
		//
		// They use least squares and Excel; we get the same
		// state prices but slightly different rates -- I suspect
		// it's a rounding issue.  State prices are more important
		// in any event.
		//
		[Test, Smoke]
		public void example2()
		{
		  int lastSlice = 10;
			double [] df = new double[lastSlice]; 
			double [] sigma = new double[lastSlice]; 
			double delta = 1.0;
			double [] rates = new double [lastSlice*(lastSlice-1)/2 + lastSlice];

			df[0] = RateCalc.PriceFromRate(0.0283, delta, Frequency.Annual);
			df[1] = RateCalc.PriceFromRate(0.0290, delta*2, Frequency.Annual);
			df[2] = RateCalc.PriceFromRate(0.0322, delta*3, Frequency.Annual);
			df[3] = RateCalc.PriceFromRate(0.0362, delta*4, Frequency.Annual);
			df[4] = RateCalc.PriceFromRate(0.0401, delta*5, Frequency.Annual);
			df[5] = RateCalc.PriceFromRate(0.0435, delta*6, Frequency.Annual);
			df[6] = RateCalc.PriceFromRate(0.0464, delta*7, Frequency.Annual);
			df[7] = RateCalc.PriceFromRate(0.0488, delta*8, Frequency.Annual);
			df[8] = RateCalc.PriceFromRate(0.0508, delta*9, Frequency.Annual);
			df[9] = RateCalc.PriceFromRate(0.0512, delta*10, Frequency.Annual);

			sigma[1] = 0.0020;
			sigma[2] = 0.0020;
			sigma[3] = 0.0020;
			sigma[4] = 0.0020;
			sigma[5] = 0.0020;
			sigma[6] = 0.0020;
			sigma[7] = 0.0020;
			sigma[8] = 0.0020;
			sigma[9] = 0.0020;

			BDT.Tree(df, sigma, delta, rates, false );

			double[] expects = new double[55] {
			  0.028300,
				0.0296411, 0.0297599,
				0.0384755, 0.0386297, 0.0387846,
				0.0480041, 0.0481965, 0.0483897, 0.0485836,
				0.055402, 0.0556241, 0.055847, 0.0560708, 0.0562956,
				0.0600634, 0.0603041, 0.0605458, 0.0607885, 0.0610321, 0.0612767,
				0.0632065, 0.0634598, 0.0637142, 0.0639695, 0.0642259, 0.0644834, 0.0647418,
				0.0648402, 0.0651001, 0.065361, 0.0656229, 0.065886, 0.06615, 0.0664152, 0.0666814,
				0.0658749, 0.0661389, 0.066404, 0.0666702, 0.0669374, 0.0672057, 0.067475, 0.0677455, 0.068017,
				0.0538287, 0.0540445, 0.0542611, 0.0544786, 0.0546969, 0.0549161, 0.0551362, 0.0553572, 0.0555791, 0.0558019
			};

			int treeIndex = 0;
			for (int slice=0; slice<lastSlice; slice++) {
			  for (int node=0; node<=slice; node++) {
			    Assert.AreEqual( expects[treeIndex],
													 rates[treeIndex],
													 epsilon,
													 String.Format("Rate at [slice: {0}, node: {1}]", slice, node)
													 );
					++treeIndex;
				}
			}

		} // example2()

	} // TestBDT
} 
