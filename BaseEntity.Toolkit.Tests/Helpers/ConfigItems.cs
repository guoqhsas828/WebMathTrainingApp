//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Xml;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Tests.Helpers
{
  /// <summary>
  ///  This class can be used to change the XML configuration
  ///  on the fly for a single test. 
  /// </summary>
  /// <example>
  ///   The following codes use the specified configuration settings
  ///   instead of the global ones to create survival curves and pricers.
  /// <code>
  ///    using (new ConfigItems
  ///    {
  ///      {"BasketPricer.ExactJumpToDefault", true},
  ///      {"SurvivalCalibrator.ToleranceX", 1E-7},
  ///      {"SurvivalCalibrator.ToleranceF", 1E-7},
  ///    }.Update())
  ///    {
  ///      CreatePricers();
  ///    }
  /// </code>
  /// </example>
  internal class ConfigItems : Dictionary<string, object>, IDisposable
  {
    #region Methods

    public IDisposable Update()
    {
      if (Count == 0) return this;
      LoadXmlConfig(_originalXml, this);
      return this;
    }

    public void Restore()
    {
      LoadXmlConfig(_originalXml, null);
    }

    private static void LoadXmlConfig(string xml, Dictionary<string, object> settings)
    {
      var xmlDoc = new XmlDocument();
      xmlDoc.LoadXml(xml);
      var root = xmlDoc.DocumentElement;
      if (settings != null)
      {
        foreach (var p in settings)
        {
          var names = p.Key.Split('/', '.');
          XmlNodeList nodeList = root.GetElementsByTagName(names[0]);
          if (names.Length != 2 || nodeList.Count != 1)
          {
            throw new ToolkitConfigException(string.Format(
              "{0}: key not found", p.Key), null);
          }
          ((XmlElement)nodeList[0]).SetAttribute(names[1], p.Value.ToString());
        }
      }
      ToolkitConfigurator.Init(root);
    }

    #endregion

    #region Data

    private readonly string _originalXml = ToolkitConfigUtil.WriteSettingsXml(
      ToolkitConfigurator.Settings, "ToolkitConfig", true);

    #endregion

    #region IDisposable Members

    void IDisposable.Dispose()
    {
      Restore();
    }

    #endregion
  }
}
