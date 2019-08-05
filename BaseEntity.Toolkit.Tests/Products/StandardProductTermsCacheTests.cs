//
// Test of standard products cache
// Copyright (c)    2002-2016. All rights reserved.
//
using System.IO;
using System.Linq;
using NUnit.Framework;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products.StandardProductTerms;
using BaseEntity.Toolkit.Tests;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Products
{
  [TestFixture, Smoke]
  public class StandardProductTermsCacheTests
  {
    [OneTimeSetUp]
    public void SetUp()
    {
      ToolkitCache.StandardProductTermsCache.Initialise(TestInitialise);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
      ToolkitCache.StandardProductTermsCache.Initialise();
    }

    /// <summary>
    /// Save built-in terms of a file and re-load to check consistency
    /// </summary>
    [Test]
    public void XmlSaveLoadConsistency()
    {
      // Load built-it terms
      var builtin = ToolkitCache.StandardProductTermsCache.Values.OrderBy(t => t.Key).ToArray();
      // Save to file
      var tmpFile = Path.GetTempFileName();
      ToolkitCache.StandardProductTermsCache.SaveXmlTerms(tmpFile);
      // Re-load from file
      var cache = new StandardProductTermsCache();
      cache.LoadFromPath(tmpFile);
      var loaded = cache.Values.OrderBy(t => t.Key).ToArray();
      // Compare for consistency
      var mismatch = ObjectStatesChecker.Compare(builtin, loaded);
      Assert.IsNull(mismatch, "mismatch");
    }

    /// <summary>
    /// Load default terms from file and compare with built-in terms for consistency
    /// </summary>
    [Test]
    public void LoadExistingXmlData()
    {
      // Load standard terms from default file
      var path = @"toolkit\Data\StandardProductTerms.xml".GetFullPath();
      var cache = new StandardProductTermsCache();
      cache.LoadFromPath(path);
      var loaded = cache.Values.OrderBy(t => t.Key).ToArray();
      // Get built-in standard terms
      var data = ToolkitCache.StandardProductTermsCache.Values.OrderBy(t => t.Key).ToArray();
      // Compare for consistency
      var mismatch = ObjectStatesChecker.Compare(data, loaded);
      Assert.IsNull(mismatch, "mismatch");
    }

    /// <summary>
    /// Load default terms from file and compare with built-in terms for consistency
    /// </summary>
    [Test]
    public void CompareBuiltInToXmlFile()
    {
      // Load defaults from standard file
      var path = @"toolkit\Data\StandardProductTerms.xml".GetFullPath();
      var cache = new StandardProductTermsCache();
      cache.LoadFromPath(path);
      var loaded = cache.Values.OrderBy(t => t.Key).ToArray();
      // Load built-in terms
      ToolkitCache.StandardProductTermsCache.Clear();
      StandardProductTermsDefaults.Initialise(ToolkitCache.StandardProductTermsCache);
      var builtin = ToolkitCache.StandardProductTermsCache.Values.OrderBy(t => t.Key).ToArray();
      // Compare for consistency
      var mismatch = ObjectStatesChecker.Compare(builtin, loaded);
      Assert.IsNull(mismatch, "mismatch");
    }
    
    #region Utility Methods

    /// <summary>
    /// Initialiser for Reference Rate Cache for testing - force use of public cache
    /// </summary>
    private static void TestInitialise(IStandardTermsCache<IStandardProductTerms> cache)
    {
      StandardProductTermsDefaults.Initialise(cache as StandardProductTermsCache);
    }

    #endregion
  }
}
