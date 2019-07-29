using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Util.Configuration
{
  /// <summary>
  /// Helper class to modify/restore in memory the global settings.
  /// This should be used with extreme cautions.
  /// </summary>
  /// <exclude>For internal use only</exclude>
  public class GlobalSettingsModifier
  {
    /// <summary>
    /// Updates the specified settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    public void Update(IEnumerable<KeyValuePair<string, object>> settings)
    {
      var currentXml = ToolkitConfigUtil.WriteSettingsXml(
        ToolkitConfigurator.Settings, "ToolkitConfig", true);
      Load(currentXml, settings);
    }

    /// <summary>
    /// Restore the original settings when this instance is initialized.
    /// </summary>
    public void Restore()
    {
      Load(_originalXml, null);
    }

    /// <summary>
    /// Loads the specified base settings with the user specified overrides.
    /// </summary>
    /// <param name="baseXml">The XML string representing the base settings</param>
    /// <param name="settingsToOverride">The settings to override</param>
    /// <exception cref="ToolkitConfigException">null</exception>
    private static void Load(string baseXml,
      IEnumerable<KeyValuePair<string, object>> settingsToOverride)
    {
      var xmlDoc = new XmlDocument();
      xmlDoc.LoadXml(baseXml);
      var root = xmlDoc.DocumentElement;
      if (settingsToOverride != null)
      {
        foreach (var p in settingsToOverride)
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

    /// <summary>
    /// The original XML settings
    /// </summary>
    private readonly string _originalXml = ToolkitConfigUtil
      .WriteSettingsXml(ToolkitConfigurator.Settings, "ToolkitConfig", true);
  }
}
