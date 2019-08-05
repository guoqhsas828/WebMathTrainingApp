//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.Serialization;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using static BaseEntity.Toolkit.Tests.Helpers.LoadData;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestCcrVolatilityCalibrations
  {
    [Test]
    public void FromAtmSwaption1Factor()
    {
      Dt asOf = new Dt(20100715);
      double tol = 1E-10;
      var distribution = DistributionType.LogNormal;
      var discountCurve = new DiscountCurve(asOf, 0.04);
      var swaptionExpiries = new[] {new Tenor(5, TimeUnit.Years)};
      var swaptionTenors = new[] {new Tenor(5, TimeUnit.Years)};
      var atmSwaptionVolSurface = new[,] {{(distribution == DistributionType.LogNormal)? 0.25: 0.0000250}};
      var swapRateEffective = new[] {new Dt(20100715)};
      var swapRateMaturities = new[] {new Dt(20110715)};
      var factorLoadings = new double[1,1] {{1.0}};
      var bespokeCapletTenors = new[]
      {
        new Dt(20101015),
        new Dt(20110115),
        new Dt(20110715),
        new Dt(20120715),
        new Dt(20130715),
        new Dt(20150715),
        new Dt(20170715),
        new Dt(20200715),
        new Dt(20220715),
        new Dt(20250715),
        new Dt(20300715),
        new Dt(20350715),
      };

      // Partition
      var Partition = new double[]
      {
        92.0, 184.0, 365.0, 731.0, 1096.0, 1826.0, 2557.0, 3653.0, 4383.0, 5479.0, 7305.0, 9131.0,
      };
      var dates = bespokeCapletTenors;

      //Fraction
      var Fraction = new double[dates.Length];
      Fraction[0] = Partition[0] / 365.0;
      for (int i = 1; i < dates.Length; i++)
      {
        Fraction[i] = (Partition[i] - Partition[i - 1]) / 365.0;
      }

      // Initial Forward Libor rate
      var Initial = new double[12];
      Initial[0] = (1 / discountCurve.DiscountFactor(asOf, dates[0]) - 1) / Fraction[0];
      for (int i = 1; i < 12; i++)
      {
        Initial[i] = (1 / discountCurve.DiscountFactor(dates[i - 1], dates[i]) - 1) / Fraction[i];
      }

      // pi_, ai_, df_
      var pi_ = new double[12];
      var ai_ = new double[12];
      var df_ = new double[12];

      double df = 1.0, a0 = 0.0;
      for (int i = 0; i < 12; ++i)
      {
        double delta = Fraction[i];
        df *= 1.0 / (1.0 + delta * Initial[i]);
        a0 += delta * df;
        pi_[i] = delta * df;
        ai_[i] = a0;
        df_[i] = df;
      }


      var bespokeCapletFactors = new double[12, 1];
      var bespokeVols = new VolatilityCurve[12];
      for (int i = 0; i < bespokeVols.Length; ++i)
        bespokeVols[i] = new VolatilityCurve(asOf);

      BaseEntity.Toolkit.Models.Simulations.CalibrationUtils.FromAtmSwaptionVolatilitySurface(
        asOf, discountCurve, swaptionExpiries, swaptionTenors, atmSwaptionVolSurface,
        swapRateEffective, swapRateMaturities, factorLoadings, bespokeCapletTenors,
        out bespokeVols, out bespokeCapletFactors, distribution, true);

      var expectFactors = new double[12, 1]
      {
        {1.0},
        {1.0},
        {1.0},
        {1.0},
        {1.0},
        {1.0},
        {1.0},
        {1.0},
        {1.0},
        {1.0},
        {1.0},
        {1.0},
      };

      for (int rows = expectFactors.GetLength(0), i = 0; i < rows; ++i)
        for (int cols = expectFactors.GetLength(1), j = 0; j < cols; ++j)
        {
          Assert.AreEqual(expectFactors[i, j], bespokeCapletFactors[i, j],
            1E-10, "Factor[" + i + "," + j + "]");
        }

    // Initialize expectVols under two distribution types  
      var expectVols = (distribution == DistributionType.LogNormal) 
        ? new double[][]
      {
        new []{0.0},
        new[]{0.25},
        new[]{0.25,0.25},
        new[]{0.25,0.25,0.25,},
        new[]{0.25,0.25,0.25, 0.25},
        new[]{0.25,0.25,0.25, 0.25, 0.25},
        new[]{0.25,0.25,0.25, 0.25, 0.25, 0.25},
        new[]{0.25,0.25,0.25, 0.25, 0.25, 0.25, 0.25},
        new[]{0.25,0.25,0.25, 0.25, 0.25, 0.25, 0.25, 0.25},
        new[]{0.25,0.25,0.25, 0.25, 0.25, 0.25, 0.25, 0.25, 0.25},
        new[]{0.25,0.25,0.25, 0.25, 0.25, 0.25, 0.25, 0.25, 0.25, 0.25},
        new[]{0.25,0.25,0.25, 0.25, 0.25, 0.25, 0.25, 0.25, 0.25, 0.25, 0.25},
      }
      : new double[][]
      {
        new []{0.0},
        new[]{0.0001},
        new[]{0.0001,0.0001},
        new[]{0.0001,0.0001,0.0001,},
        new[]{0.0001,0.0001,0.0001, 0.0001},
        new[]{0.0001,0.0001,0.0001, 0.0001, 0.0001},
        new[]{0.0001,0.0001,0.0001, 0.0001, 0.0001, 0.0001},
        new[]{0.0001,0.0001,0.0001, 0.0001, 0.0001, 0.0001, 0.0001},
        new[]{0.0001,0.0001,0.0001, 0.0001, 0.0001, 0.0001, 0.0001, 0.0001},
        new[]{0.0001,0.0001,0.0001, 0.0001, 0.0001, 0.0001, 0.0001, 0.0001, 0.0001},
        new[]{0.0001,0.0001,0.0001, 0.0001, 0.0001, 0.0001, 0.0001, 0.0001, 0.0001,0.0001},
        new[]{0.0001,0.0001,0.0001, 0.0001, 0.0001, 0.0001, 0.0001, 0.0001, 0.0001, 0.0001, 0.0001},
      };

    // compare the bespoke vols with expectVols 
      for (int n = expectVols.Length, i = 1; i < n; ++i)
      {
        var expects = expectVols[i];
        var curve = bespokeVols[i];
        for (int count = curve.Count, j = 0; j < count; ++j)
          Assert.AreEqual(expects[j], curve.GetVal(j), 1E-7,
            "Point " + j + " at Curve " + i);
      }

      

      // update the LIBOR rate volatility (multiplied by square root of corresponding time length)
      var LiborVols = new double[][]
      {
        new[] {0.0},
        new[] {0.0},
        new[] {0.0, 0.0},
        new[] {0.0, 0.0, 0.0,},
        new[] {0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
      };


      for (int i = 1; i < LiborVols.GetLength(0); i++)
        for (int k = 0; k < i; k++)
        {
          double last = (k == 0)
            ? 0
            : expectVols[i][k - 1] * expectVols[i][k - 1] * Partition[k - 1] / 365.0;

          LiborVols[i][k] =
            Math.Sqrt(expectVols[i][k] * expectVols[i][k] * Partition[k] / 365.0 - last);
        }

      //Initialize modelVols
      var ModelVols = new double[][]
      {
        new[] {0.0},
        new[] {0.0, 0.0},
        new[] {0.0, 0.0, 0.0,},
        new[] {0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
      };

      // Calculate the forward swap rate with Input ExpectedVols and ExpectedFactors
      var nu = new double[12];

      for (int beta = 1; beta <= 11; ++beta)
        for (int alpha = 0; alpha < beta; alpha++)
        {
          double T_alpha = Partition[alpha] / 365.0;
          double delta = 0;
          for (int i = alpha + 1; i <= beta; ++i)
          {
            for (int k = 0; k < 12; k++)
              nu[k] = 0.0;

            for (int j = alpha + 1; j <= i; ++j)
              for (int k = 0; k <= alpha; k++)
                nu[j] += Fraction[k] * expectVols[j][k] * expectVols[i][k];


           
            double bi = (distribution == DistributionType.Normal) ? pi_[i] : pi_[i] * Initial[i];
            delta += bi * bi * nu[i];

            for (int j = alpha + 1; j < i; ++j)
            {
              double bj = (distribution == DistributionType.Normal) ? pi_[j] : pi_[j] * Initial[j]; 

              double rho = 0;
              for (int k = 0; k < 1; k++)
              {
                rho += expectFactors[i, k] * expectFactors[j, k];
              }

              delta += 2 * bi * bj * rho * nu[j];
            }
          }
          double floater = df_[alpha] - df_[beta];
          double annuity = ai_[beta] - ai_[alpha];
          var actual = ModelVols[beta - 1][alpha] = (distribution == DistributionType.Normal) ? Math.Sqrt(delta / (T_alpha * annuity * annuity)) : Math.Sqrt(delta / (T_alpha * floater * floater)) ;
          Assert.AreEqual(atmSwaptionVolSurface[0, 0], actual, tol);
        }



      return;
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void FromAtmSwaption2Factor(int caseNo)
    {
      var caseData = _data[caseNo];
      var distribution = (caseData.normal == true) ? DistributionType.Normal : DistributionType.LogNormal;
      var tol = caseData.Tolerance;
      var corr = caseData.Correlation;
      Dt asOf = new Dt(20100715);
      var discountCurve = new DiscountCurve(asOf, 0.04);
      var swaptionExpiries = new[] {new Tenor(5, TimeUnit.Years)};
      var swaptionTenors = new[] { new Tenor(5, TimeUnit.Years) };
      var atmSwaptionVolSurface = new[,] { { 0.250 } };
      var swapRateEffective = new[] {new Dt(20100715), new Dt(20100715),};
      var swapRateMaturities = new[] {new Dt(20110715), new Dt(20200715),};
      var swapRateIndex = new int[2] {2, 7};
      var factorLoadings = new[,] {{1.0, 0.0}, {corr, Math.Sqrt(1-corr*corr)}};
      var bespokeCapletTenors = new[]
      {
        new Dt(20101015),
        new Dt(20110115),
        new Dt(20110715),
        new Dt(20120715),
        new Dt(20130715),
        new Dt(20150715),
        new Dt(20170715),
        new Dt(20200715),
        new Dt(20220715),
        new Dt(20250715),
        new Dt(20300715),
        new Dt(20350715),
      };
      
      // Partition
      var Partition = new double[]
      {
        92.0, 184.0, 365.0, 731.0, 1096.0, 1826.0, 2557.0, 3653.0, 4383.0, 5479.0, 7305.0, 9131.0,
      };
      var dates = bespokeCapletTenors;
      var partions = bespokeCapletTenors.Select(d => d - asOf).ToArray();

      //Fraction
      var Fraction = new double[dates.Length];
      Fraction[0] = Partition[0] / 365.0;
      for (int i = 1; i < dates.Length; i++)
      {
        Fraction[i] = (Partition[i] - Partition[i - 1]) / 365.0;
      }

      // Initial Forward Libor rate
      var Initial = new double[12];
      Initial[0] = (1/discountCurve.DiscountFactor(asOf,dates[0]) - 1) / Fraction[0];
      for (int i = 1; i < 12; i++)
      {
        Initial[i] = (1 / discountCurve.DiscountFactor(dates[i - 1], dates[i]) - 1) / Fraction[i];
      }

      // pi_, ai_, df_
      var pi_ = new double[12];
      var ai_ = new double[12];
      var df_ = new double[12];

      double df = 1.0, a0 = 0.0;
      for (int i = 0; i < 12; ++i)
      {
        double delta = Fraction[i];
        df *= 1.0 / (1.0 + delta * Initial[i]);
        a0 += delta * df;
        pi_[i] = delta * df;
        ai_[i] = a0;
        df_[i] = df;
      }

      
      // get the IR model from CCR engine
      var bespokeCapletFactors = new double[12, 2];
      var bespokeVols = new VolatilityCurve[12];
      for (int i = 0; i < bespokeVols.Length; ++i)
        bespokeVols[i] = new VolatilityCurve(asOf);

      
      BaseEntity.Toolkit.Models.Simulations.CalibrationUtils.FromAtmSwaptionVolatilitySurface(
        asOf, discountCurve, swaptionExpiries, swaptionTenors, atmSwaptionVolSurface,
        swapRateEffective, swapRateMaturities, factorLoadings, bespokeCapletTenors,
        out bespokeVols, out bespokeCapletFactors, distribution, true);

      var expectFactors = caseData.ExpectedFactors;
      if (IsGeratingExpects)
      {
        expectFactors = caseData.ExpectedFactors = bespokeCapletFactors;
      }
      else
      {
        for (int rows = expectFactors.GetLength(0), i = 0; i < rows; ++i)
          for (int cols = expectFactors.GetLength(1), j = 0; j < cols; ++j)
          {
            Assert.AreEqual(expectFactors[i, j], bespokeCapletFactors[i, j],
              1E-9, "Factor[" + i + "," + j + "]");
          }
      }

      var expectVols = caseData.ExpectedVols;
      if (IsGeratingExpects)
      {
        expectVols = caseData.ExpectedVols = bespokeVols
          .Select(curve => curve.Select(p => p.Value).ToArray())
          .ToArray();
      }
      else
      {
        for (int n = expectVols.Length, i = 1; i < n; ++i)
        {
          var expects = expectVols[i];
          var curve = bespokeVols[i];
          for (int count = curve.Count, j = 0; j < count; ++j)
            Assert.AreEqual(expects[j], curve.GetVal(j), 1E-7,
              "Point " + j + " at Curve " + i);
        }
      }



      // update the LIBOR rate volatility (multiplied by square root of corresponding time length)
      var LiborVols = new double[][]
      {
        new[] {0.0},
        new[] {0.0},
        new[] {0.0, 0.0},
        new[] {0.0, 0.0, 0.0,},
        new[] {0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
      };

      for (int i = 1; i < LiborVols.GetLength(0); i++)
        for (int k = 0; k < i; k++)
        {
          double last = (k == 0)
            ? 0
            : expectVols[i][k - 1] * expectVols[i][k - 1] * Partition[k - 1] / 365.0;

          LiborVols[i][k] =
            Math.Sqrt(expectVols[i][k] * expectVols[i][k] * Partition[k] / 365.0 - last);
        }


      //Initialize modelVols
      var ModelVols = new double[][]
      {
        new[] {0.0},
        new[] {0.0, 0.0},
        new[] {0.0, 0.0, 0.0,},
        new[] {0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
      };
      
      // Calculate the forward swap rate with Input ExpectedVols and ExpectedFactors
      var nu = new double[12];

      for (int beta = 1; beta <= 11; ++beta)
        for (int alpha = 0; alpha < beta; alpha++)
        {
          double T_alpha = Partition[alpha]/365.0;
          double delta = 0;
          for (int i = alpha + 1; i <= beta; ++i)
          {
            for (int k = 0; k < 12; k++)
              nu[k] = 0.0;

            for (int j = alpha + 1; j <= i; ++j)
              for (int k = 0; k <= alpha; k++)
                nu[j] += LiborVols[j][k] * LiborVols[i][k];
                

            double bi = (distribution == DistributionType.Normal)? pi_[i] : pi_[i] * Initial[i]; // if normal , pi_[i]
            delta += bi * bi * nu[i];

            for (int j = alpha + 1; j < i; ++j)
            {
              double bj = (distribution == DistributionType.Normal)? pi_[j] : pi_[j] * Initial[j]; // if normal pi_[j]

              double rho = 0;
              for (int k = 0; k < 2; k++)
              {
                rho += caseData.ExpectedFactors[i,k] * caseData.ExpectedFactors[j,k];
              }

              delta += 2 * bi * bj * rho * nu[j];
            }
          }
          double floater = df_[alpha] - df_[beta];
          double annuity = ai_[beta] - ai_[alpha];
          var actual = ModelVols[beta-1][alpha] = (distribution == DistributionType.Normal)
            ?  Math.Sqrt(delta / (T_alpha * annuity * annuity))
            : Math.Sqrt(delta / (T_alpha * floater * floater)); // If normal Math.Sqrt(delta / (t1 * annuity * annuity))
          if (Math.Abs(atmSwaptionVolSurface[0, 0]- actual) > tol)
            Assert.AreEqual(atmSwaptionVolSurface[0, 0], actual, tol);
        }

      // Calculate the factor loading of proxy swap rate with Input ExpectedVols and ExpectedFactors
      
      var ModelFL = new double[2, 2];
      
      for (int i = 0; i < 2; i++)
        for(int j = 0; j < 2; j++)
        {
          double top = 0;
          for (int k = 1; k <= swapRateIndex[i]; k++)        
          {
            double add= (distribution == DistributionType.Normal)
              ? pi_[k]*expectFactors[k, j]*LiborVols[k][0]
              : pi_[k]*Initial[k]*expectFactors[k, j]*LiborVols[k][0];
            top += add;
          }
          top *= Fraction[0];

          double down = 0;
          for (int p = 1; p <= swapRateIndex[i]; p++)
            for (int q = 1; q <= p; q++)
            {
              double rho = expectFactors[p, 0]*expectFactors[q, 0] + expectFactors[p, 1]*expectFactors[q, 1];
              double add = (distribution == DistributionType.Normal)
                ? pi_[p]*pi_[q]*rho*LiborVols[p][0]*LiborVols[q][0]
                : pi_[p]*Initial[p]*pi_[q]*Initial[q]*rho*LiborVols[p][0]*LiborVols[q][0];
              down += (p == q) ? add : 2*add;              
            }
          down = Math.Sqrt(down);
          ModelFL[i, j] = top / down / Fraction[0];
        }
      
      return;
    }

    [OneTimeSetUp]
    public void Initialize()
    {
      var serializer = new SimpleXmlSerializer(
          typeof(CaseData[]), new[]
          {
            new KeyValuePair<string, Type>(null,typeof(CaseData)),
          })
      {
        RootElementName = "ArrayOfCaseData",
      };
      // read data;
      var file = GetTestFilePath(_expectFile);
      using (var reader = XmlReader.Create(file))
      {
        _data = (CaseData[]) serializer.ReadObject(reader);
      }
      _serializer = BaseEntityContext.IsGeneratingExpects ? serializer : null;
    }

    [OneTimeTearDown]
    public void CleanUp()
    {
      var serializer = _serializer;
      if (serializer == null) return;
      using (var stream = File.CreateText(GetTestFilePath(_expectFile)))
      using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
      {
        Indent = true,
        Encoding = Encoding.UTF8,
      }))
      {
        serializer.WriteObject(writer, _data);
      }
    }

    private SimpleXmlSerializer _serializer = null;
    private readonly string _expectFile = "data/TestCcrVolatilityCalibrations.expects";

    private bool IsGeratingExpects => _serializer != null;

    private class CaseData
    {
      public Double Correlation;
      public double Tolerance;
      public bool normal;
      public double[,] ExpectedFactors;
      public double[][] ExpectedVols;
    }

    private static CaseData[] _data;
  }
}
