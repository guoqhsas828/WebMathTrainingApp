/*
 * Logger.cs - 
 *
 *
 */

using System;
using System.Runtime.InteropServices;
using BaseEntity.Configuration;
using log4net;
using log4net.Core;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Util
{
  /// <summary>
  ///   Helper class to simplify configuration of log4net &amp; log4cplus
  /// </summary>
  public class Logger
  {
    private static readonly ILog MyLogger = LogManager.GetLogger(typeof(Logger));

		// Callback to get the log level
		internal delegate int ConfigDelegateType(string loggerName);
		private	static readonly ConfigDelegateType ConfigDelegate = ConfigCallback;
		private	static int ConfigCallback( string loggerName )
		{
			ILog logger = LogUtil.GetLogger(loggerName);
			if (logger.IsDebugEnabled)
				return  Level.Debug.Value ;
			else if (logger.IsInfoEnabled)
				return Level.Info.Value;
			else if (logger.IsWarnEnabled)
				return Level.Warn.Value;
			else if (logger.IsErrorEnabled)
				return Level.Error.Value;
			else
				return Level.Fatal.Value;
		}

		// Callback to do the work
		internal delegate void LogDelegateType(string loggerName, int logLevel, string message);
		private	static readonly LogDelegateType LogDelegate = LogCallback;
		private	static void LogCallback( string loggerName, int logLevel, string message )
		{
			ILog logger = LogUtil.GetLogger(loggerName);
			if( logLevel==Level.Fatal.Value)
				logger.Fatal( message );
			else if (logLevel==Level.Error.Value)
				logger.Error( message );
			else if (logLevel==Level.Warn.Value)
				logger.Warn( message );
			else if (logLevel==Level.Info.Value)
				logger.Info( message );
			else if (logLevel==Level.Debug.Value)
				logger.Debug( message );
			else
				logger.Fatal( message );
		}

		// C++ helper functions
		[DllImport("MagnoliaIGNative")] internal static extern void RegisterLoggerCallbacks(ConfigDelegateType configDelegate, LogDelegateType logDelegate);
		[DllImport("MagnoliaIGNative")] internal static extern void DeregisterLoggerCallbacks();

		/// <summary>
		/// Called when log settings is changed in Logger window in AddinMgr 
		/// </summary>
		[DllImport("MagnoliaIGNative")] public static extern void UpdateLogLevel();

    /// <summary>
    /// To enable log4net to capture C++ log event.
    /// </summary>
		public static void InitLog4CPlus()
    {
      MyLogger.Debug("ENTER InitLog4CPlus");

      RegisterLoggerCallbacks(ConfigDelegate, LogDelegate);

      AppDomain.CurrentDomain.DomainUnload += HandleDomainUnload;

      MyLogger.Debug("EXIT InitLog4CPlus");
    }

		/// <summary>
		/// Close and destroy all loggers
		/// </summary>
		public static void Close()
		{
      DeregisterLoggerCallbacks();
		}

    private static void HandleDomainUnload(object sender, EventArgs args)
    {
      MyLogger.Debug("ENTER HandleDomainUnload");

		  AppDomain.CurrentDomain.DomainUnload -= HandleDomainUnload;

      DeregisterLoggerCallbacks();

      MyLogger.Debug("EXIT HandleDomainUnload");
    }

    /// <summary>
    ///   Deprecated (call Configure.Init directly instead)
    /// </summary>
    public static void DoDefaultConfigure()
    {
      Configurator.Init();
    }

  } // class Logger
}
 
