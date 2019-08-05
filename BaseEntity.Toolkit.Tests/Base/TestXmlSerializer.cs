//
// TestXmlSerializer.cs
// Copyright (c) 2004-2008,   . All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;
using System.IO;
using System.Xml.Serialization;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests
{
  [TestFixture]
  public class DtXmlSerializerTest : ToolkitTestBase
  {
    [Serializable]
    public class ProductFoo
    {
      public readonly Calendar SCAL = Calendar.ZAB;

      public Dt Maturity { get; set; }
      public double Coupon { get; set; }
      public Calendar Calendar { get; set; }
    }

    [Test, Smoke]
    public void TestXMLSerialization()
    {
      //create a sample product
      var maturity = new Dt(15, 1, 2008);
      double coupon = 0.08;
      var cal = Calendar.NYB;

      var product = new ProductFoo(
        )
        {
          Maturity = maturity,
          Coupon= coupon,
          Calendar= cal
        };

      string xml;
      var serializer = new XmlSerializer(typeof(ProductFoo));

      using (var sw = new StringWriter())
      {
        serializer.Serialize(sw, product);
        xml = sw.ToString();
      }

      using (var sr=new StringReader(xml))
      {
        var product2 = (ProductFoo)serializer.Deserialize(sr);
        Assert.AreEqual(product2.Coupon, product.Coupon, "Dt/Calendar xml serialization failed. Serialized text:"+xml);
        Assert.AreEqual(product2.Calendar, product.Calendar, "Calendar xml serialization failed.");
        Assert.AreEqual(product2.Maturity, product.Maturity, "Dt xml serialization failed.");
      }

    }
  }
}
