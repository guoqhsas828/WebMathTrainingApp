using System;
using System.Xml;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Base.Details;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  The configurator for the stand-alone BaseEntity.Toolkit.Base assembly.
  /// </summary>
  /// <exclude />
  public static class ToolkitBaseConfigurator
  {
    private static ToolkitBaseConfigSettings _settings;

    /// <summary>
    /// Gets the global configuration settings.
    /// </summary>
    /// <value>The settings.</value>
    public static ToolkitBaseConfigSettings Settings
    {
      get
      {
        if (_settings == null)
        {
          lock (typeof(ToolkitBaseConfigurator))
            _settings = LoadSettings();
        }
        return _settings;
      }
    }

    /// <summary>
    /// Initializes the stand-alone BaseEntity.Toolkit.Base settings.
    /// </summary>
    /// <remarks>
    /// The user can call this method to make sure the settings related to
    /// calendar and Dt calculations are loaded when BaseEntity.Toolkit.Base
    /// works as a stand-alone assembly.
    /// </remarks>
    public static void Init()
    {
      _settings = LoadSettings();

      var calendarRepository = Configurator.Resolve<ICalendarRepository>();
      calendarRepository.InitManagedCalendarCalc(_settings.CalendarCalc.CalendarDir);
    }

    // This method is called by ToolkitConfigurator only.
    // It is not supposed to be directly called from the user codes.
    public static void Init(ToolkitBaseConfigSettings settings)
    {
      _settings = settings;
    }

    private static ToolkitBaseConfigSettings LoadSettings()
    {
      Configurator.Init();

      XmlElement root;
      try
      {
        root = Configurator.GetConfigXml("ToolkitConfig", null);
      }
      catch (Exception ex)
      {
        throw new ToolkitConfigReadException(ex.Message, ex);
      }
      if (root == null)
      {
        if (logger == null)
        {
          logger = log4net.LogManager.GetLogger(typeof(ToolkitBaseConfigurator));
        }
        logger.Debug("ToolkitConfig not found.  Use the default configuration.");
        return new ToolkitBaseConfigSettings();
      }
      return ToolkitConfigUtil.LoadSettings<ToolkitBaseConfigSettings>(root);
    }

    private static log4net.ILog logger;
  }

  /// <summary>
  /// Class containing the settings related to calendar and Dt calculations.
  /// </summary>
  /// <exclude />
  [Serializable]
  public class ToolkitBaseConfigSettings
  {
    /// <exclude />
    public readonly CalendarCalcConfig CalendarCalc = new CalendarCalcConfig();
    
    /// <exclude />
    public readonly DtConfig Dt = new DtConfig();
  }
}
