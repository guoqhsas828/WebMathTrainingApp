//
// Copyright (c)    2002-2018. All rights reserved.
//

using System.Collections.Generic;
using System.IO;
using System.Linq;
using BaseEntity.Toolkit.Util;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests;

namespace BaseEntity.Toolkit.Tests
{

  [TestFixture, Smoke]
  public class XmlSerializationTests
  {
    /// <summary>
    /// Root directory for all xaved xml result files
    /// </summary>
    private static readonly string Folder = 
      Path.Combine(BaseEntityContext.InstallDir, "toolkit", "test", "data");

    /// <summary>
    /// Test for any changes in the format of saved XML results files
    /// </summary>
    /// <remarks>
    ///   Loads, saves, re-loades and compares consistency of all XML files
    /// </remarks>
    [TestCaseSource(nameof(Files)), Smoke]
    public void LoadSaveConsistency(string fileName)
    {
      // Load existing XML file
      var data = XmlSerialization.ReadXmlFile<object>($"{Folder}{fileName}");
      // Save it
      var tmpFile = Path.GetTempFileName();
      XmlSerialization.WriteXmlFile(data, tmpFile);
      // Reload
      var cloned = XmlSerialization.ReadXmlFile<object>(tmpFile);
      var mismatch = ObjectStatesChecker.Compare(data, cloned);
      Assert.IsNull(mismatch, $"mismatch - {mismatch}");
    }

    #region Load objects from XML files

    private static IEnumerable<string> Files
    {
      get
      {
        // Just test all sub-directories as root directory has many old-style XL
        return (new DirectoryInfo(Folder)).GetDirectories()
          .SelectMany(d => d
          .GetFiles("*.xml", SearchOption.AllDirectories)
          .Select(f => f.FullName.Replace(Folder, string.Empty)));
      }
    }

    #endregion
  }
}
