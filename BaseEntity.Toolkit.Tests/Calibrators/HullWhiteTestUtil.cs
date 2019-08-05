// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics.Rng;
using BaseEntity.Toolkit.Util;


namespace BaseEntity.Toolkit.Tests.Calibrators
{
  public struct InputData
  {
    public Dt AsOf { get; set; }
    public string[] ExpiryTenors;
    public string[] Tenors;
    public double[,] Vols;
    public double[,] Weights;
    public DistributionType VolType;
  }

  public struct DataPoint
  {
    public string ExpiryTenor;
    public string MaturityTenor;
    public double Volatility;
    public double StrikeValue;
  }

  internal static class HullWhiteTestUtil
  {
    internal static Dt[] GetExpiries(Dt asOf, string[] expiryTenors)
    {
      return expiryTenors
        .Select(eT => Dt.Add(asOf, Tenor.Parse(eT))).ToArray();
    }

    internal static double[,] InitializeArray(int n, int m, double value)
    {
      var weights = new double[n, m];
      for (int i = 0; i < n; i++)
      {
        for (int j = 0; j < m; j++)
        {
          weights[i, j] = value;
        }
      }
      return weights;
    }

    internal static double SingleCurveSwapRate(int bI, int eI, double[] p,
      double[] t, out double annuity)
    {
      var level = 0.0;
      for (int i = bI + 1; i <= eI; ++i)
      {
        level += p[i] * (t[i] - (i == 0 ? 0.0 : t[i - 1]));
      }
      annuity = level;
      return (p[bI] - p[eI]) / level;
    }


    internal static HullWhiteDataContainer GetInitialDataContainer(
      InputData d, DiscountCurve curve)
    {
      var vols = d.Vols;
      if (d.AsOf.IsEmpty()) d.AsOf = curve.AsOf;
      var factor = d.VolType == DistributionType.Normal ? 1E-4 : 1E-2;
      for (int i = 0; i < vols.GetLength(0); i++)
      {
        for (int j = 0; j < vols.GetLength(1); j++)
        {
          vols[i, j] *= factor;
        }
      }

      var expiries = GetExpiries(d.AsOf, d.ExpiryTenors);
      if (d.Weights == null)
        d.Weights = InitializeArray(d.ExpiryTenors.Length,
          d.Tenors.Length, 1.0);

      return new HullWhiteDataContainer(expiries, d.Tenors, vols,
        d.VolType, d.Weights, curve);
    }

    internal static Dt[] GetAllDates(Dt asOf, string[] expiryTenors, string[] tenors)
    {
      var expiries = expiryTenors
        .Select(eT => Dt.Add(asOf, Tenor.Parse(eT))).ToArray();

      var dates = new UniqueSequence<Dt>();
      for (int i = 0; i < expiries.Length; i++)
      {
        dates.Add(expiries[i]);
        for (int j = 0; j < tenors.Length; j++)
        {
          dates.Add(Dt.Add(expiries[i], Tenor.Parse(tenors[j])));
        }
      }
      return dates.ToArray();
    }

    internal static double[] GetRandomVariable(double[] x,
  double dev, RandomNumberGenerator rng)
    {
      var n = x.Length;
      var xnew = new double[n];

      for (int i = 0; i < n; ++i)
        xnew[i] = rng.Normal(x[i], dev);
      return xnew;
    }


    public static List<DataPoint[]> HandleXmlDataFile(string filePath,
      string fileName, string strike)
    {
      var hwXmlDir = SystemContext.InstallDir + filePath;
      var data = new List<DataPoint[]>();
      foreach (var file in Directory.EnumerateFiles(hwXmlDir, 
        fileName,SearchOption.TopDirectoryOnly))
      {
        data.Add(CollectData(file, strike));
      }
      return data;
    }

    private static DataPoint[] CollectData(string filePath, string strike)
    {
      var doc = new XmlDocument();
      doc.Load(filePath);
      var points = new List<DataPoint>();
      if (strike == null) strike = "ATM"; 

      foreach (XmlElement node in doc.GetElementsByTagName("Volsurfaces"))
      {
        var currency = node.GetAttribute("currency");
        if (currency != "EUR") continue;

        foreach (XmlElement surface in node.GetElementsByTagName("Volsurface"))
        {
          var underlying = surface.GetAttribute("underlying");
          var data = surface.GetElementsByTagName("Expiry").OfType<XmlElement>()
            .Select(e => new
            {
              Expiry = e.GetAttribute("ID"),
              Atm = e.GetElementsByTagName("Strike")
                .OfType<XmlElement>()
                .FirstOrDefault(s => s.GetAttribute("strike") == strike)
            }).ToArray();

          if (data.Length > 0)
          {
            foreach (var d in data)
            {
              var expiry = d.Expiry;
              var srikeValue = double.Parse(d.Atm.GetAttribute("strikeValue"));
              //var logNormal = double.Parse(d.Atm.GetAttribute("logNormalVol"));
              var vol = double.Parse(d.Atm.GetAttribute("normalVol"));
              var point = new DataPoint
              {
                ExpiryTenor = expiry,
                MaturityTenor = underlying,
                Volatility = vol,
                StrikeValue = srikeValue,
              };
              points.Add(point);
            }
          }
        }
      }
      return points.ToArray();
    }

    internal static void ConvertTo2DArray(int n, int m,
      DataPoint[] pointData, ref double[,] volatilities,
      ref double[,] strikes)
    {
      for (int i = 0, k = 0; i < m; i++)
      {
        for (int j = 0; j < n; j++)
        {
          volatilities[j, i] = pointData[k].Volatility;
          strikes[j, i] = pointData[k].StrikeValue/100.0;
          ++k;
        }
      }
    }
  }
}
