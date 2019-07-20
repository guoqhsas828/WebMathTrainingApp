// 
// Copyright (c) WebMathTraining 2002-2013. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using log4net;
using log4net.Core;
using log4net.Repository;
using log4net.Util;

namespace BaseEntity.Configuration
{
  /// <summary>
  /// This class provides functionality not directly supported by the log4net library.
  /// </summary>
  public static class LogUtil
  {
    private static readonly ConcurrentDictionary<string, Stopwatch> Watches =
      new ConcurrentDictionary<string, Stopwatch>();

    /// <summary>
    /// Determines whether [is timing enabled] [the specified log].
    /// </summary>
    /// <param name="log">The log.</param>
    /// <returns>
    /// 	<c>true</c> if [is timing enabled] [the specified log]; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsTimingEnabled(this ILog log)
    {
      return log.Logger.IsEnabledFor(Configurator.TimingLevel);
    }

    /// <summary>
    /// Starts timing for an arbitrary named piece of code.
    /// </summary>
    /// <param name="log">The log.</param>
    /// <param name="watchName">Name of the watch.</param>
    public static void StartTiming(this ILog log, string watchName)
    {
      if (log.Logger.IsEnabledFor(Configurator.TimingLevel))
      {
        var watch = new Stopwatch();
        log.Logger.Log(MethodBase.GetCurrentMethod().DeclaringType, Configurator.TimingLevel,
          string.Format("Started: {0}", watchName), null);
        Watches.TryAdd(watchName, watch);
        watch.Start();
      }
    }

    /// <summary>
    /// Stops timing for an arbitrary named piece of code.
    /// </summary>
    /// <param name="log">The log.</param>
    /// <param name="watchName">Name of the watch.</param>
    public static void StopTiming(this ILog log, string watchName)
    {
      if (log.Logger.IsEnabledFor(Configurator.TimingLevel))
      {
        Stopwatch watch;
        if (Watches.TryRemove(watchName, out watch))
        {
          watch.Stop();
          log.Logger.Log(MethodBase.GetCurrentMethod().DeclaringType, Configurator.TimingLevel,
            string.Format("Stopped: {0} ({1})", watchName, watch.Elapsed), null);
        }
        else
        {
          log.Logger.Log(MethodBase.GetCurrentMethod().DeclaringType, Configurator.TimingLevel,
            string.Format("No matching watch: {0}", watchName), null);
        }
      }
    }

    /// <summary>
    /// This method creates a new instance of ApplicationException with the supplied error message 
    /// and then invokes the ThrowException method with this newly created ApplicationException.
    /// </summary>
    /// <param name="logger">Logger used to log the exception message</param>
    /// <param name="message">Error message passed to the constructor of the ApplicationException class</param>
    public static void ThrowException(ILog logger, string message)
    {
      ThrowException(logger, new ApplicationException(message));
    }

    /// <summary>
    /// This method invokes the Error() method of the supplied logger object with the given Exception object.
    /// </summary>
    /// <param name="ex">The exception object</param>
    /// <param name="logger">Logger used to log the exception message</param>
    public static void ThrowException(log4net.ILog logger, Exception ex)
    {
      logger.Error(ex.Message, ex);
      throw ex;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="log"></param>
    /// <returns></returns>
    public static bool IsVerboseEnabled(this ILog log)
    {
      return log.Logger.IsEnabledFor(Level.Verbose);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="log"></param>
    /// <param name="message"></param>
    /// <param name="exception"></param>
    public static void Verbose(this ILog log, string message, Exception exception)
    {
      log.Logger.Log(MethodBase.GetCurrentMethod().DeclaringType, Level.Verbose, message, exception);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="log"></param>
    /// <param name="message"></param>
    public static void Verbose(this ILog log, string message)
    {
      log.Verbose(message, null);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="log"></param>
    /// <param name="format"></param>
    /// <param name="args"></param>
    public static void VerboseFormat(this ILog log, string format, params object[] args)
    {
      if (log.IsVerboseEnabled())
      {
        log.Logger.Log(MethodBase.GetCurrentMethod().DeclaringType, Level.Verbose,
          new SystemStringFormat(CultureInfo.InvariantCulture, format, args), null);
      }
    }

    #region Get arround the limitations of log4net 2.0.8

    /// <summary>
    /// Retrieves or creates a named logger.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Retrieves a logger named as the <paramref name="name"/>
    /// parameter. If the named logger already exists, then the
    /// existing instance will be returned. Otherwise, a new instance is
    /// created.
    /// </para>
    /// <para>By default, loggers do not have a set level but inherit
    /// it from the hierarchy. This is one of the central features of
    /// log4net.
    /// </para>
    /// </remarks>
    /// <param name="name">The name of the logger to retrieve.</param>
    /// <returns>The logger with the name specified.</returns>
    public static ILog GetLogger(string name)
    {
      return LogManager.GetLogger(Assembly.GetCallingAssembly(), name);
    }

    /// <summary>
    /// Returns the default <see cref="ILoggerRepository"/> instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Gets the <see cref="ILoggerRepository"/> for the repository specified
    /// by the callers assembly (<see cref="M:Assembly.GetCallingAssembly()"/>).
    /// </para>
    /// </remarks>
    /// <returns>The <see cref="ILoggerRepository"/> instance for the default repository.</returns>
    public static ILoggerRepository GetRepository()
    {
      return LogManager.GetRepository(Assembly.GetCallingAssembly());
    }


    /// <summary>
    /// Configures log4net using the specified configuration data stream.
    /// </summary>
    /// <param name="configStream">A stream to load the XML configuration from.</param>
    /// <remarks>
    /// <para>
    /// The configuration data must be valid XML. It must contain
    /// at least one element called <c>log4net</c> that holds
    /// the log4net configuration data.
    /// </para>
    /// <para>
    /// Note that this method will NOT close the stream parameter.
    /// </para>
    /// </remarks>
    public static ICollection XmlConfigure(Stream configStream)
    {
      return log4net.Config.XmlConfigurator.Configure(GetRepository(), configStream);
    }

    /// <summary>
    /// Configures log4net using the file specified, monitors the file for changes 
    /// and reloads the configuration if a change is detected.
    /// </summary>
    /// <param name="configFile">The XML file to load the configuration from.</param>
    /// <remarks>
    /// <para>
    /// The configuration file must be valid XML. It must contain
    /// at least one element called <c>log4net</c> that holds
    /// the configuration data.
    /// </para>
    /// <para>
    /// The configuration file will be monitored using a <see cref="FileSystemWatcher"/>
    /// and depends on the behavior of that class.
    /// </para>
    /// <para>
    /// For more information on how to configure log4net using
    /// a separate configuration file, see <see cref="M:Configure(FileInfo)"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="M:Configure(FileInfo)"/>
    public static ICollection XmlConfigureAndWatch(FileInfo configFile)
    {
      return log4net.Config.XmlConfigurator.ConfigureAndWatch(GetRepository(), configFile);
    }

    #endregion
  }
}