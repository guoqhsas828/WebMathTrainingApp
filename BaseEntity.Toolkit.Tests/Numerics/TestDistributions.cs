//
// NUnit test of Distribution functions
// Copyright (c)    2002-2018. All rights reserved.
//

using System;
using System.IO;
using System.Text.RegularExpressions;

using BaseEntity.Toolkit.Numerics;

using NUnit.Framework;
using BaseEntity.Configuration;

namespace BaseEntity.Toolkit.Tests.Numerics
{
  [TestFixture]
  public class TestDistributions
	{
		const double epsilon = 1e-08;

		// We'll do inverse CDFs first
		[Test, Smoke]
		public void ExponentialInverseCDF()
		{
			// Locals
			double lambda, x, cdf, myX;
			// Read in good values, and test to see if we match
			string filename = GetTestFilePath("mathutil", "expinv.txt");
			Console.WriteLine("Trying to read " + filename);
			StreamReader reader = File.OpenText(filename);
			do
			{
				// Pull out the actual numbers
				Match match = Regex.Match(reader.ReadLine(),
																	@"^\s*(?<lambda>-{0,1}\d+(\.\d+){0,1})\s+(?<cdf>-{0,1}\d+(\.\d+){0,1})\s+(?<x>-{0,1}\d+(\.\d+){0,1})\s*$",
																	RegexOptions.ExplicitCapture);
				if (match.Success)
				{
					lambda = double.Parse(match.Groups["lambda"].Value);
					cdf = double.Parse(match.Groups["cdf"].Value);
					x = double.Parse(match.Groups["x"].Value);
					// Calculate our value
					myX = Exponential.inverseCumulative(cdf, lambda);
					// Compare
					Assert.AreEqual( x, myX, epsilon );
				}
				else
				{
					Console.Error.WriteLine("Badly formed input line.");
				}
			}   
			while(reader.Peek() != -1);
			reader.Close();
		} // ExponentialInverse

		[Test, Smoke]
		[Ignore("Only good to 4 decimal places at the moment.")]
		public void StudentTInverseCDF()
		{
			// Locals
			double df, x, cdf, myX;
			// Read in good values, and test to see if we match
      string filename = GetTestFilePath("mathutil", "tinv.txt");
			Console.WriteLine("Trying to read " + filename);
			StreamReader reader = File.OpenText(filename);
			do
			{
				// Pull out the actual numbers
				Match match = Regex.Match(reader.ReadLine(),
																	@"^\s*(?<df>-{0,1}\d+(\.\d+){0,1})\s+(?<cdf>-{0,1}\d+(\.\d+){0,1})\s+(?<x>-{0,1}\d+(\.\d+){0,1})\s*$",
																	RegexOptions.ExplicitCapture);
				if (match.Success)
				{
					df = double.Parse(match.Groups["df"].Value);
					cdf = double.Parse(match.Groups["cdf"].Value);
					x = double.Parse(match.Groups["x"].Value);
					// Calculate our value
					myX = StudentT.inverseCumulative(cdf, df);
					// Compare
					Assert.AreEqual( x, myX, epsilon );
				}
				else
				{
					Console.Error.WriteLine("Badly formed input line.");
				}
			}   
			while(reader.Peek() != -1);
			reader.Close();
		} // StudentTInverse

		[Test, Smoke]
		public void NormalInverseCDF()
		{
			// Locals
			double mu, sigma, x, cdf, myX;
			// Read in good values, and test to see if we match
      string filename = GetTestFilePath("mathutil", "norminv.txt");
			Console.WriteLine("Trying to read " + filename);
			StreamReader reader = File.OpenText(filename);
			do
			{
				// Pull out the actual numbers
				Match match = Regex.Match(reader.ReadLine(),
																	@"^\s*(?<mu>-{0,1}\d+(\.\d+){0,1})\s+(?<sigma>-{0,1}\d+(\.\d+){0,1})\s+(?<cdf>-{0,1}\d+(\.\d+){0,1})\s+(?<x>-{0,1}\d+(\.\d+){0,1})\s*$",
																	RegexOptions.ExplicitCapture);
				if (match.Success)
				{
					mu = double.Parse(match.Groups["mu"].Value);
					sigma = double.Parse(match.Groups["sigma"].Value);
					cdf = double.Parse(match.Groups["cdf"].Value);
					x = double.Parse(match.Groups["x"].Value);
					// Calculate our value
					myX = Normal.inverseCumulative(cdf, mu, sigma);
					// Compare
					Assert.AreEqual( x, myX, epsilon );
				}
				else
				{
					Console.Error.WriteLine("Badly formed input line.");
				}
			}   
			while(reader.Peek() != -1);
			reader.Close();
		} // NormalInverse

		[Test, Smoke]
		public void UniformInverseCDF()
		{
			// Locals
			double a, b, x, cdf, myX;
			// Read in good values, and test to see if we match
      string filename = GetTestFilePath("mathutil", "unifinv.txt");
			Console.WriteLine("Trying to read " + filename);
			StreamReader reader = File.OpenText(filename);
			do
			{
				// Pull out the actual numbers
				Match match = Regex.Match(reader.ReadLine(),
																	@"^\s*(?<a>-{0,1}\d+(\.\d+){0,1})\s+(?<b>-{0,1}\d+(\.\d+){0,1})\s+(?<cdf>-{0,1}\d+(\.\d+){0,1})\s+(?<x>-{0,1}\d+(\.\d+){0,1})\s*$",
																	RegexOptions.ExplicitCapture);
				if (match.Success)
				{
					a = double.Parse(match.Groups["a"].Value);
					b = double.Parse(match.Groups["b"].Value);
					cdf = double.Parse(match.Groups["cdf"].Value);
					x = double.Parse(match.Groups["x"].Value);
					// Calculate our value
					myX = Uniform.inverseCumulative(cdf, a, b);
					// Compare
					Assert.AreEqual( x, myX, epsilon );
				}
				else
				{
					Console.Error.WriteLine("Badly formed input line.");
				}
			}   
			while(reader.Peek() != -1);
			reader.Close();
		} // UniformInverse

		// Now we'll do PDFs and CDFs
		[Test, Smoke]
		public void ExponentialPdfCdf()
		{
			// Locals
			double lambda, x, pdf, cdf, myPDF, myCDF;
			// Read in good values, and test to see if we match
      string filename = GetTestFilePath("mathutil", "exponential.txt");
			Console.WriteLine("Trying to read " + filename);
			StreamReader reader = File.OpenText(filename);
			do
			{
				// Pull out the actual numbers
				Match match = Regex.Match(reader.ReadLine(),
																	@"^\s*(?<lambda>-{0,1}\d+(\.\d+){0,1})\s+(?<x>-{0,1}\d+(\.\d+){0,1})\s+(?<pdf>-{0,1}\d+(\.\d+){0,1})\s+(?<cdf>-{0,1}\d+(\.\d+){0,1})\s*$",
																	RegexOptions.ExplicitCapture);
				if (match.Success)
				{
					lambda = double.Parse(match.Groups["lambda"].Value);
					x = double.Parse(match.Groups["x"].Value);
					pdf = double.Parse(match.Groups["pdf"].Value);
					cdf = double.Parse(match.Groups["cdf"].Value);
					// Calculate our value
					myPDF = Exponential.density(x, lambda);
					myCDF = Exponential.cumulative(x, lambda);
					// Compare
					Assert.AreEqual( pdf, myPDF, epsilon, "exponential pdf" );
					Assert.AreEqual( cdf, myCDF, epsilon, "exponential cdf" );
				}
				else
				{
					Console.Error.WriteLine("Badly formed input line.");
				}
			}   
			while(reader.Peek() != -1);
			reader.Close();
		} // ExponentialPdfCdf

		[Test, Smoke]
		public void StudentTPdfCdf()
		{
			// Locals
			double df, x, pdf, cdf, myPDF, myCDF;
			// Read in good values, and test to see if we match
      string filename = GetTestFilePath("mathutil", "studentt.txt");
			Console.WriteLine("Trying to read " + filename);
			StreamReader reader = File.OpenText(filename);
			do
			{
				// Pull out the actual numbers
				Match match = Regex.Match(reader.ReadLine(),
																	@"^\s*(?<df>-{0,1}\d+(\.\d+){0,1})\s+(?<x>-{0,1}\d+(\.\d+){0,1})\s+(?<pdf>-{0,1}\d+(\.\d+){0,1})\s+(?<cdf>-{0,1}\d+(\.\d+){0,1})\s*$",
																	RegexOptions.ExplicitCapture);
				if (match.Success)
				{
					df = double.Parse(match.Groups["df"].Value);
					x = double.Parse(match.Groups["x"].Value);
					pdf = double.Parse(match.Groups["pdf"].Value);
					cdf = double.Parse(match.Groups["cdf"].Value);
					// Calculate our value
					myPDF = StudentT.density(x, df);
					myCDF = StudentT.cumulative(x, df);
					// Compare
					Assert.AreEqual( pdf, myPDF, epsilon, "Student t pdf" );
					Assert.AreEqual( cdf, myCDF, epsilon, "Student t cdf" );
				}
				else
				{
					Console.Error.WriteLine("Badly formed input line.");
				}
			}   
			while(reader.Peek() != -1);
			reader.Close();
		} // StudentTPdfCdf

		[Test, Smoke]
		public void NormalPdfCdf()
		{
			// Locals
			double mu, sigma, x, pdf, cdf, myPDF, myCDF;
			// Read in good values, and test to see if we match
      string filename = GetTestFilePath("mathutil", "normal.txt");
			Console.WriteLine("Trying to read " + filename);
			StreamReader reader = File.OpenText(filename);
			do
			{
				// Pull out the actual numbers
				Match match = Regex.Match(reader.ReadLine(),
																	@"^\s*(?<mu>-{0,1}\d+(\.\d+){0,1})\s+(?<sigma>-{0,1}\d+(\.\d+){0,1})\s+(?<x>-{0,1}\d+(\.\d+){0,1})\s+(?<pdf>-{0,1}\d+(\.\d+){0,1})\s+(?<cdf>-{0,1}\d+(\.\d+){0,1})\s*$",
																	RegexOptions.ExplicitCapture);
				if (match.Success)
				{
					mu = double.Parse(match.Groups["mu"].Value);
					sigma = double.Parse(match.Groups["sigma"].Value);
					x = double.Parse(match.Groups["x"].Value);
					pdf = double.Parse(match.Groups["pdf"].Value);
					cdf = double.Parse(match.Groups["cdf"].Value);
					// Calculate our value
					myPDF = Normal.density(x, mu, sigma);
					myCDF = Normal.cumulative(x, mu, sigma);
					// Compare
					Assert.AreEqual( pdf, myPDF, epsilon, "normal pdf" );
					Assert.AreEqual( cdf, myCDF, epsilon, "normal cdf" );
				}
				else
				{
					Console.Error.WriteLine("Badly formed input line.");
				}
			}   
			while(reader.Peek() != -1);
			reader.Close();
		} // NormalPdfCdf

		[Test, Smoke]
		public void UniformPdfCdf()
		{
			// Locals
			double a, b, x, pdf, cdf, myPDF, myCDF;
			// Read in good values, and test to see if we match
      string filename = GetTestFilePath("mathutil", "uniform.txt");
			Console.WriteLine("Trying to read " + filename);
			StreamReader reader = File.OpenText(filename);
			do
			{
				// Pull out the actual numbers
				Match match = Regex.Match(reader.ReadLine(),
																	@"^\s*(?<a>-{0,1}\d+(\.\d+){0,1})\s+(?<b>-{0,1}\d+(\.\d+){0,1})\s+(?<x>-{0,1}\d+(\.\d+){0,1})\s+(?<pdf>-{0,1}\d+(\.\d+){0,1})\s+(?<cdf>-{0,1}\d+(\.\d+){0,1})\s*$",
																	RegexOptions.ExplicitCapture);
				if (match.Success)
				{
					a = double.Parse(match.Groups["a"].Value);
					b = double.Parse(match.Groups["b"].Value);
					x = double.Parse(match.Groups["x"].Value);
					pdf = double.Parse(match.Groups["pdf"].Value);
					cdf = double.Parse(match.Groups["cdf"].Value);
					// Calculate our value
					myPDF = Uniform.density(x, a, b);
					myCDF = Uniform.cumulative(x, a, b);
					// Compare
					Assert.AreEqual( pdf, myPDF, epsilon, "uniform pdf" );
					Assert.AreEqual( cdf, myCDF, epsilon, "uniform cdf" );
				}
				else
				{
					Console.Error.WriteLine("Badly formed input line.");
				}
			}   
			while(reader.Peek() != -1);
			reader.Close();
		} // UniformPdfCdf
		[Test, Smoke]
		public void BivariateNormalPdf()
		{
			// Locals
			double mu1, sigma1, x1, mu2, sigma2, x2, rho, pdf,  myPDF;
			// Read in good values, and test to see if we match
      string filename = GetTestFilePath("mathutil", "bivariatenormal.txt");
			Console.WriteLine("Trying to read " + filename);
			StreamReader reader = File.OpenText(filename);
			do
			{
				// Pull out the actual numbers
				Match match = Regex.Match(reader.ReadLine(),
					@"^\s*(?<x1>-{0,1}\d+(\.\d+){0,1})\s+(?<mu1>-{0,1}\d+(\.\d+){0,1})\s+(?<sigma1>-{0,1}\d+(\.\d+){0,1})\s+(?<x2>-{0,1}\d+(\.\d+){0,1})\s+(?<mu2>-{0,1}\d+(\.\d+){0,1})\s+(?<sigma2>-{0,1}\d+(\.\d+){0,1})\s+(?<rho>-{0,1}\d+(\.\d+){0,1})\s+(?<pdf>-{0,1}\d+(\.\d+){0,1})\s*$",
					RegexOptions.ExplicitCapture);
				if (match.Success)
				{
					mu1 = double.Parse(match.Groups["mu1"].Value);
					sigma1 = double.Parse(match.Groups["sigma1"].Value);
					mu2 = double.Parse(match.Groups["mu2"].Value);
					sigma2 = double.Parse(match.Groups["sigma2"].Value);
					rho = double.Parse(match.Groups["rho"].Value);
					x1 = double.Parse(match.Groups["x1"].Value);
					x2 = double.Parse(match.Groups["x2"].Value);
					pdf = double.Parse(match.Groups["pdf"].Value);
					// Calculate our value
					myPDF = BivariateNormal.density(x1, mu1, sigma1, x2, mu2, sigma2, rho);
					// myCDF = Normal.cumulative(x, mu, sigma);
					// Compare
					Assert.AreEqual( pdf, myPDF, epsilon, "Bivariate normal pdf" );
					// Assert.AreEqual( cdf, myCDF, epsilon, "normal cdf" );
				}
				else
				{
					Console.Error.WriteLine("Badly formed input line.");
				}
			}   
			while(reader.Peek() != -1);
			reader.Close();
		} // BivariateNormalPdf
		[Test, Smoke]
		public void TrivariateNormalPdf()
		{
			// Locals
			double mu1, sigma1, x1, mu2, sigma2, x2, mu3, sigma3, x3, rho12, rho13, rho23, pdf,  myPDF;
			// Read in good values, and test to see if we match
      string filename = GetTestFilePath("mathutil", "trivariatenormal.txt");
			Console.WriteLine("Trying to read " + filename);
			StreamReader reader = File.OpenText(filename);
			do
			{
				// Pull out the actual numbers
				Match match = Regex.Match(reader.ReadLine(),
					@"^\s*(?<x1>-{0,1}\d+(\.\d+){0,1})\s+(?<mu1>-{0,1}\d+(\.\d+){0,1})\s+(?<sigma1>-{0,1}\d+(\.\d+){0,1})\s+(?<x2>-{0,1}\d+(\.\d+){0,1})\s+(?<mu2>-{0,1}\d+(\.\d+){0,1})\s+(?<sigma2>-{0,1}\d+(\.\d+){0,1})\s+(?<x3>-{0,1}\d+(\.\d+){0,1})\s+(?<mu3>-{0,1}\d+(\.\d+){0,1})\s+(?<sigma3>-{0,1}\d+(\.\d+){0,1})\s+(?<rho12>-{0,1}\d+(\.\d+){0,1})\s+(?<rho13>-{0,1}\d+(\.\d+){0,1})\s+(?<rho23>-{0,1}\d+(\.\d+){0,1})\s+(?<pdf>-{0,1}\d+(\.\d+){0,1})\s*$",
					RegexOptions.ExplicitCapture);
				if (match.Success)
				{
					mu1 = double.Parse(match.Groups["mu1"].Value);
					sigma1 = double.Parse(match.Groups["sigma1"].Value);
					mu2 = double.Parse(match.Groups["mu2"].Value);
					sigma2 = double.Parse(match.Groups["sigma2"].Value);
					mu3 = double.Parse(match.Groups["mu3"].Value);
					sigma3 = double.Parse(match.Groups["sigma3"].Value);
					rho12 = double.Parse(match.Groups["rho12"].Value);
					rho13 = double.Parse(match.Groups["rho13"].Value);
					rho23 = double.Parse(match.Groups["rho23"].Value);
					x1 = double.Parse(match.Groups["x1"].Value);
					x2 = double.Parse(match.Groups["x2"].Value);
					x3 = double.Parse(match.Groups["x3"].Value);
					pdf = double.Parse(match.Groups["pdf"].Value);
					// Calculate our value
					myPDF = TrivariateNormal.density(x1, mu1, sigma1, x2, mu2, sigma2, x3, mu3, sigma3, rho12, rho13, rho23);
					// myCDF = Normal.cumulative(x, mu, sigma);
					// Compare
					Assert.AreEqual( pdf, myPDF, epsilon, "Trivariate normal pdf" );
					// Assert.AreEqual( cdf, myCDF, epsilon, "normal cdf" );
				}
				else
				{
					Console.Error.WriteLine("Badly formed input line.");
				}
			}   
			while(reader.Peek() != -1);
			reader.Close();
		} // TrivariateNormalPdf

	  private static string GetTestFilePath(string folder, string filename)
	  {
	    return Path.Combine(SystemContext.InstallDir, "toolkit", "test", folder, filename);
	  }
	}
}
