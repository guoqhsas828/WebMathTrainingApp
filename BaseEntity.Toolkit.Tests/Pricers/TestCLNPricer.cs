using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Util;


namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestCLNPricer
  {
    [Test]
    public void TestCLN()
    {
      var path = Path.Combine(SystemContext.InstallDir,
        @"toolkit\test\data\CLNPricer.xml");

      const double expect = 753082.08217633516;

      var pricer = XmlSerialization.ReadXmlFile(path) as CreditLinkedNotePricer;
      if (pricer != null)
      {
        //flag is false
        var protectionPv = 0.0;
        using (new ConfigItems
        {
          {"CashflowPricer.IgnoreAccruedInProtection", false}
        }.Update())
        {
          protectionPv = pricer.ProtectionPv();
          NUnit.Framework.Assert.AreEqual(expect, protectionPv, 1e-14*pricer.Notional);
        }

        //flag is true
        var protPv = pricer.ProtectionPv();

        Assert.AreNotEqual(protPv, protectionPv);
      }
    }
  }
}
