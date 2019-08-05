//
// Copyright (c)    2002-2016. All rights reserved.
//

using System.IO;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products.StandardProductTerms;
using BaseEntity.Toolkit.Util;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Curves
{
  [TestFixture, Smoke]
  public class StandardProductTermsCacheTests
  {
    [OneTimeSetUp]
    public void SetUp()
    {
      ToolkitCache.StandardProductTermsCache.Initialise();
    }
    
    [OneTimeTearDown]
    public void TearDown()
    {
      ToolkitCache.StandardProductTermsCache.Initialise();
    }

    [Test]
    public void XmlLoadSaveConsistency()
    {
      var tmpFile = Path.GetTempFileName();
      var data = ToolkitCache.StandardProductTermsCache.Values
        .Select(o => o as IStandardProductTerms).ToArray();
      StandardTermsXmlSerializer.Save(tmpFile, data);
      var cloned = StandardTermsXmlSerializer.Load<IStandardProductTerms>(tmpFile);
      var mismatch = ObjectStatesChecker.Compare(data, cloned);
      Assert.IsNull(mismatch, "mismatch");
    }

    [Test]
    public void LoadExistingXmlData()
    {
      var path = @"toolkit\Data\StandardProductTerms.xml".GetFullPath();
      var loaded = StandardTermsXmlSerializer.Load<IStandardProductTerms>(path)
        .OrderBy(t => t.Key).ToArray();
      ToolkitCache.StandardProductTermsCache.Initialise();
      var data = ToolkitCache.StandardProductTermsCache.Values
        .Select(o => o as IStandardProductTerms)
        .OrderBy(t=>t.Key).ToArray();
      var mismatch = ObjectStatesChecker.Compare(data, loaded);
      Assert.IsNull(mismatch, "mismatch");
    }
  }
}
