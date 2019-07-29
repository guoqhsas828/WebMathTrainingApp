/*
 * ToolkitConfigurator.cs
 *
 *   2004-2010. All rights reserved.
 *
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;
#if NETSTANDARD2_0 || NETSTANDARD2_1
using Unity;
#else
using Microsoft.Practices.Unity;
#endif

namespace BaseEntity.Toolkit.Util.Configuration
{
  /// <summary>
  ///   Toolkit configuration settings manager
  /// </summary>
  public static class ToolkitConfigurator
  {
    private static volatile ToolkitConfigSettings _settings;
    private static readonly ILog Logger = LogManager.GetLogger(typeof(ToolkitConfigurator));
    private static readonly object SyncObj = new object();

    /// <summary>
    ///   Default configuration settings, normally the recommended.
    /// </summary>
    public static readonly ToolkitConfigSettings DefaultSettings = new ToolkitConfigSettings();

    /// <summary>
    ///   Current configuration settings.
    /// </summary>
    public static ToolkitConfigSettings Settings
    {
      get
      {
        Validate();
        return _settings;
      }
    }

    /// <summary>
    /// Initialize Toolkit application
    /// </summary>
    public static void Init()
    {
      if (_settings != null) return;

      lock (SyncObj)
      {
        if (_settings != null) return;

        Init(null);

        // Setup toolkit unknown exception memory dump
        DebugHelper.Init();

        // Init C++ logging
       // Util.Logger.InitLog4CPlus();

        // Validate settings
        var settings = Settings;

        // Init Calendar cache
        string calendarDir = settings.CalendarCalc.CalendarDir;
        if (!String.IsNullOrEmpty(calendarDir))
        {
          string path = Environment.ExpandEnvironmentVariables(calendarDir);
          calendarDir = (Path.IsPathRooted(path))
            ? path
            : Path.Combine(SystemContext.InstallDir, path);
        }
        var calendarRepository = Configurator.Resolve<ICalendarRepository>();
        calendarRepository.InitNativeCalendarCalc(calendarDir, CalendarCalc.Init);

        Calendar.Init(
          CalendarCalc.GetCalendar, 
          CalendarCalc.CalendarName,
          CalendarCalc.IsValidCalendar,
          CalendarCalc.IsValidSettlement);

        // Init CashflowFactory
       BaseEntity.Toolkit.Cashflows.Native.CashflowFactory.Init(
          settings.CDSCashflowPricer.UseCycleDateForAccruals,
          settings.CashflowPricer.RollLastPaymentDate,
          settings.CDSCashflowPricer.IncludeMaturityAccrual,
          settings.CDSCashflowPricer.IncludeMaturityProtection,
          settings.CashflowPricer.BackwardCompatibleSchedule);
        BaseEntity.Toolkit.Models.Native.CashflowModel.Init((settings.CashflowPricer.BackwardCompatibleModel ? 1 : 0)
                           | (settings.CashflowPricer.IncludeAccruedOnDefaultAtSettle ? 2 : 0)
                           | (settings.CashflowPricer.IgnoreAccruedInProtection ? 4 : 0));
        BaseEntity.Toolkit.Models.Simulations.Native.CalibrationUtils.ChooseNewCalibrationFromSwap(
          settings.CcrPricer.EnableFastCalibrationFromSwaptionVolatility);

        // Register standard terms caches with Unity
        Logger.Info("Registering StandardProductTermsCache with Unity");
        Configurator.DefaultContainer.RegisterInstance(typeof(IStandardTermsCache<IStandardTerms>), "StandardProductTermsCache", ToolkitCache.StandardProductTermsCache);
        Logger.Info("Registering ReferenceRateCache with Unity");
        var rrCache = new ReferenceRateCache();
        Configurator.DefaultContainer.RegisterInstance(typeof(IStandardTermsCache<IStandardTerms>), "ReferenceRateCache", rrCache);

        // Register custom serialisers
        SpotPriceCurve.RegisterSerializer();
        ReferenceRateSerializer.Register();
        ReferenceIndexSerializer.Register();

        Logger.InfoFormat("Successfully loaded toolkit configuration.");
      }
    }

    /// <summary>
    /// Initialize Toolkit application
    /// </summary>
    public static void Init(XmlElement root)
    {
      // Load toolkit settings.
      if (root == null)
      {
        try
        {
          root = Configurator.GetConfigXml("ToolkitConfig", null);
        }
        catch (Exception ex)
        {
          throw new ToolkitConfigReadException(ex.Message, ex);
        }
        if (root == null)
          throw new ToolkitConfigReadException(
            "Toolkit config root \"ToolkitConfig\" not found.", null);
      }

      // publish
      var settings = ToolkitConfigUtil.LoadSettings<ToolkitConfigSettings>(root);
      if (settings == null)
      {
        throw new ToolkitConfigException("Failed to load toolkit configuration.", null);
      }

      // Initialize toolkit base.
      ToolkitBaseConfigurator.Init(settings);

      // Initialize BaseEntityPINVOKE
      NativeConfigurator.Initialize();

      // Save settings
      _settings = settings;
    }

    /// <summary>
    ///   Query the value of a configuration setting
    /// </summary>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="name">Configuration name in "GROUP.ITEM" format.</param>
    /// <returns>The current value.</returns>
    public static T GetValue<T>(string name)
    {
      return ToolkitConfigUtil.GetValue<T>(Settings, name);
    }

    /// <summary>
    ///   Get an array of all the configuration settings
    /// </summary>
    /// <returns>An array of settings.</returns>
    public static ToolkitConfigUtil.Item[] GetAllSettings()
    {
      ToolkitConfigSettings settings = Settings;
      if (settings == null)
      {
        throw new InvalidOperationException(
          "Must call ToolkitConfigurator.Initialize() first.");
      }
      return ToolkitConfigUtil.GetSettingsList(settings);
    }

    /// <summary>
    ///   Create a string in XML format representing the default settings.
    /// </summary>
    /// <returns>Xml string</returns>
    public static string CreateConfigXml()
    {
      ToolkitConfigSettings settings = DefaultSettings;
      Debug.Assert(settings != null, "ToolkitConfigurator.DefaultSttings is null.");
      return ToolkitConfigUtil.WriteSettingsXml(settings, "ToolkitConfig");
    }

    /// <summary>
    ///   Validate that settings are loaded.
    /// </summary>
    public static void Validate()
    {
      if (_settings == null)
        throw new InvalidOperationException(
          "ToolkitConfigurator.Init() must be called first.");
    }
  }
}
