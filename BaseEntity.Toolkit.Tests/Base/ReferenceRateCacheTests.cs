//
// Copyright (c)    2002-2016. All rights reserved.
//

using System.IO;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceRates;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests
{
  [TestFixture, Smoke]
  public class ReferenceRateCacheTests
  {
    [OneTimeSetUp]
    public void SetUp()
    {
      ReferenceRate.CacheInitialise(UseBuiltInTerms);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
      // Re-initialise terms cache
      ReferenceRate.CacheInitialise();
    }

    /// <summary>
    /// Save built-in terms of a file and re-load to check consistency
    /// </summary>
    [Test]
    public void XmlLoadSaveConsistency()
    {
      // Load built-it terms
      var cache = new ReferenceRateCache(UseBuiltInTerms);
      var builtin = cache.Values.OrderBy(t => t.Key).ToArray();
      // Save to file
      var tmpFile = Path.GetTempFileName();
      cache.SaveXmlTerms(tmpFile);
      // Re-load from file
      cache = new ReferenceRateCache();
      cache.LoadFromPath(tmpFile);
      var loaded = cache.Values.OrderBy(t => t.Key).ToArray();
      // Compare for consistency
      var mismatch = ObjectStatesChecker.Compare(builtin, loaded);
      Assert.IsNull(mismatch, $"mismatch - {mismatch}");
    }

    /// <summary>
    /// Load default terms from file and compare with built-in terms for consistency
    /// </summary>
    [Test]
    public void LoadExistingXmlData()
    {
      // Load standard terms from default file
      var path = @"toolkit\Data\ReferenceRates.xml".GetFullPath();
      var cache = new ReferenceRateCache();
      cache.LoadFromPath(path);
      var loaded = cache.Values.OrderBy(t => t.Key).ToArray();
      // Get built-in standard terms
      var data = ReferenceRate.CacheValues.OrderBy(t =>  t.Key).ToArray();
      // Compare for consistency
      var mismatch = ObjectStatesChecker.Compare(data, loaded);
      if (mismatch != null)
      {
        // Difference between default terms and terms in default file. Save new default terms for convenience
        var updatedPath = @"toolkit\Data\ReferenceRates_Updated.xml".GetFullPath(false);
        ReferenceRate.CacheSaveXmlTerms(updatedPath);
        throw new AssertionException($"mismatch - {mismatch}\nSee updated terms file {updatedPath}");
      }
    }

    /// <summary>
    /// Test saving and loading of custom Reference Rate
    /// </summary>
    /// <remarks>
    ///   <para>A reference rate not in the cache should be saved and loaded in full rather than just the key.</para>
    /// </remarks>
    [Test]
    public void CustomReferenceRateLoadSave()
    {
      // Get cached and custom reference rates
      var cached = InterestReferenceRate.Get("USDLIBOR");
      var custom = new InterestReferenceRate("CUSTOM", cached.Description, cached.Currency, cached.DayCount,
        cached.OvernightDaysToSpot, cached.DaysToSpot, cached.Calendar, cached.BDConvention, cached.RollCalendar,
        cached.ResetDateRule, cached.PublicationFrequency, cached.ValidTenors, Tenor.Empty);
      var data = new[] { cached, custom };
      // Save terms
      var tmpFile = Path.GetTempFileName();
      StandardTermsXmlSerializer.Save(tmpFile, data);
      // System.Console.WriteLine($@"Saved test ReferenceRates to {tmpFile}"); // For info if we want to manually inspect the XML saved
      // Load terms
      var loaded = StandardTermsXmlSerializer.Load<InterestReferenceRate>(tmpFile);
      // Check standard ReferenceRate points back to cache and custom ReferenceRate does not
      Assert.IsTrue(loaded[0] == data[0], $@"XML Load did not map standard ReferenceRate to cached ReferenceRate, created a new one");
      Assert.IsTrue(loaded[1] != data[1], $@"XML Load mapped custom ReferenceRate to existing ReferenceRate, should have created a new one");
      // Compare results loaded for match
      Assert.IsTrue(loaded.Length == data.Length, $"#Loaded terms ({loaded.Length}) does not matched # saved terms ({data.Length})");
      Assert.IsNull(ObjectStatesChecker.Compare(loaded[0], data[0]), "mismatch");
      Assert.IsNull(ObjectStatesChecker.Compare(loaded[1], data[1]), "mismatch");
    }

    #region Utility Methods

    /// <summary>
    /// Initialiser for Reference Rate Cache for testing - force use of internal cache
    /// </summary>
    private static void UseBuiltInTerms(IStandardTermsCache<IReferenceRate> cache)
    {
      ReferenceRateDefaults.Initialise(cache as ReferenceRateCache);
    }

    #endregion
  }
}
