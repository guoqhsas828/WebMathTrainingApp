//
// Copyright (c)    2002-2015. All rights reserved.
//

using System.Collections.Generic;
using System.Xml;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Cashflows.Expressions
{
  internal class RateCurveData
  {
    string CurveName { get; set; }
    CurveTerms CurveTerms { get; set; }
    CalibratorSettings CalibratorSettings { get; set; }
    string[] Instruments { get; set; }
    string[] Tenors { get; set; }
    double[] Quotes { get; set; }

    internal DiscountCurve FitDiscountCurve(Dt asOf)
    {
      return DiscountCurveFitCalibrator.DiscountCurveFit(asOf,
        CurveTerms, CurveName, Quotes, Instruments, Tenors,
        CalibratorSettings);
    }

    internal DiscountCurve FitProjectionCurve(
      Dt asOf, DiscountCurve fundingCurve)
    {
      return ProjectionCurveFitCalibrator.ProjectionCurveFit(asOf,
        CurveTerms, fundingCurve, CurveName, Quotes, Instruments, Tenors,
        null, CalibratorSettings);
    }
  }

  internal static class CurveLoader
  {
    public static DiscountCurve GetDiscountCurve(
      string filename, Dt asOf)
    {
      return LoadXml<RateCurveData>(filename).FitDiscountCurve(asOf);
    }

    public static DiscountCurve GetProjectionCurve(
      string filename, DiscountCurve fundingCurve, Dt asOf)
    {
      return LoadXml<RateCurveData>(filename)
        .FitProjectionCurve(asOf, fundingCurve);
    }

    private static T LoadXml<T>(string filename)
    {
      var serializer = new Toolkit.Base.Serialization.SimpleXmlSerializer(
        typeof (T), null);
      serializer.MapCollectionType(
        typeof(CurveTenorCollection), typeof(IList<CurveTenor>));
      serializer.MapCollectionType(
        typeof(AssetTermList), typeof(IDictionary<string, AssetCurveTerm>));
      var settings = new XmlReaderSettings { IgnoreWhitespace = true };
      using (var reader = XmlReader.Create(filename.GetFullPath(), settings))
      {
        return (T)serializer.ReadObject(reader);
      }
    }
  }
}
