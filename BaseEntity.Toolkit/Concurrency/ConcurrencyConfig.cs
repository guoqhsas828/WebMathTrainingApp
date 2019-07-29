using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Concurrency
{
  /// <exclude />
  [Serializable]
  public class ConcurrencyConfig
  {
    /// <exclude />
    [ToolkitConfig("Number of concurrent worker threads (\"Automatic\", \"Disabled\", or a positive integer number).")]
    public readonly string WorkerThreads = "Automatic"; // added 9.2

    // Validation: this is called by ToolkitConfigurator
    // right after the setting is loaded.
    private int Validate()
    {
      int workers;
      string input = WorkerThreads;
      if (String.Compare(input, "Disabled", true) != 0
        && String.Compare(input, "Automatic", true) != 0
        && (!Int32.TryParse(input, out workers) || workers <= 0))
      {
        throw new ToolkitConfigException(String.Format(Message, input), null);
      }
      return 1;
    }

    // This is to be called within toolkit to get input.
    internal static int GetWorkerThreads(string input)
    {
      if (String.Compare(input, "Disabled", true) == 0)
        return Disabled;
      if (String.Compare(input, "Automatic", true) == 0)
        return Automatic;
      int workers;
      if (!Int32.TryParse(input, out workers) || workers <= 0)
        throw new ToolkitConfigException(String.Format(Message, input), null);
      return workers;
    }
    internal const int Automatic = 0;
    internal const int Disabled = 1;
    private const string Message = "Concurrency.WorkerThreads should be "
      + "\"Disabled\", \"Automatic\", or positive integers, not \"{0}\"";
  } // class ConcurrencyConfig

  /// <summary>
  ///  Dynamic control to disable/restore parallel support.
  /// </summary>
  /// <exclude/>
  public static class ParallelSupport
  {
    static ParallelSupport()
    {
      ToolkitConfigurator.Init();
      Enabled = ToolkitConfigurator.Settings.SemiAnalyticBasketPricer.MulticoreSupport;
    }

    /// <summary>
    ///   Restore the parallel support settings specified
    ///   in the current configuration file.
    /// </summary>
    /// <exclude/>
    public static void Restore()
    {
      Enabled = ToolkitConfigurator.Settings.SemiAnalyticBasketPricer.MulticoreSupport;
      int workers = ConcurrencyConfig.GetWorkerThreads(ToolkitConfigurator.Settings.Concurrency.WorkerThreads);
      Algorithms.Initialize(workers);
    }

    /// <summary>
    ///   Enable parallel support with automatic workers.
    /// </summary>
    /// <exclude/>
    public static void Enable()
    {
      Enabled = true;
      int workers = ConcurrencyConfig.GetWorkerThreads(ToolkitConfigurator.Settings.Concurrency.WorkerThreads);
      Algorithms.Initialize(workers);
    }

    /// <summary>
    ///   Disable parallel support.
    /// </summary>
    /// <exclude/>
    public static void Disable()
    {
      Enabled = false;
      Algorithms.Initialize(ConcurrencyConfig.Disabled);
    }

    /// <summary>
    /// Indicates if Multicore support is enabled
    /// </summary>
    public static bool Enabled { get; private set; }
  }
}
