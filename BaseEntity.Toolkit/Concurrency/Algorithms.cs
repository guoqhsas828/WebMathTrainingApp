/*
 * Algorithms.cs - 
 *
 *   2004-2008. All rights reserved.
 *
 * General class for parallel execution
 *
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Concurrency
{
  /// <summary>
  ///   Class Implementing various parallel algorithms
  /// </summary>
  /// <remarks>
  ///   <para><b>Nested algorithms</b></para>
  ///   <para>
  ///    Parallel algorithms can be nested within another parallel algorithm.
  ///    For example, you can call a function which uses parallel algorithms
  ///    within a parallel algorithm call.
  ///    The class <see cref="Algorithms">Algorithms</see>
  ///    maintains an internal count of the workers in use, enabling it to find
  ///    avaiable workers.  When there is no workers avaible, the algorithm
  ///    switches to single thread implementation in order to avoid contentions.
  ///   </para>
  /// </remarks>
  /// <exclude/>
  public static partial class Algorithms
  {
    private static readonly log4net.ILog logger =
      log4net.LogManager.GetLogger(typeof(Algorithms));

    #region Config

    private static int workersAllocated_;

    static Algorithms()
    {
      int workers = ConcurrencyConfig.GetWorkerThreads(
        ToolkitConfigurator.Settings.Concurrency.WorkerThreads);
      Initialize(workers);
      return;
    }

    // Initiliaze with given number of worker threads.
    internal static void Initialize(int workers)
    {
      if (workers <= 0)
      {
        int nprocs = Environment.ProcessorCount;
        int threadsUsed = GetRunningThreadCount();
        if (threadsUsed >= nprocs)
          workers = 1;
        else if (threadsUsed > 1)
          workers = 1 + nprocs - threadsUsed;
        else
          workers = nprocs;
      }
      workersAllocated_ = workers;
      logger.InfoFormat("BaseEntity.Toolkit.Util.Concurrency.WorkerThreadCount: {0}", workers);
    }
    // Find the number of concurrently running threads in a process.
    private static int GetRunningThreadCount()
    {
      ProcessThreadCollection threads = Process.GetCurrentProcess().Threads;
      int running = 0;
      foreach (ProcessThread t in threads)
      {
        if (t.ThreadState == System.Diagnostics.ThreadState.Running)
          ++running;
      }
      logger.InfoFormat("BaseEntity.Toolkit.Util.Concurrency.RunningThreadCount: {0}", running);
      return running;
    }
    #endregion Config

    #region Types

    private struct Range
    {
      public Range(int begin, int end)
      {
        Begin = begin;
        End = end;
      }
      public readonly int Begin;
      public readonly int End;
      public static readonly Range Empty = new Range(0, 0);
    };

    interface IWorkerThread
    {
      void Proc(object states);
      bool Done { get;}
      Exception Exception { get; }
    }

    private class CountDownEvent
    {
      internal CountDownEvent(int threshold)
      {
        Debug.Assert(threshold > 0, "threshold must be positive");
        count_ = threshold;
        event_ = new AutoResetEvent(false);
      }
      internal void Increment()
      {
        Interlocked.Increment(ref count_);
      }
      internal void Decrement()
      {
        Set();
      }
      internal void Set()
      {
        if (count_ > 0)
        {
          int count = Interlocked.Decrement(ref count_);
          if (count <= 0)
            event_.Set();
        }
      }
      internal void Wait()
      {
        event_.WaitOne();
        // The event is of no use once WaitOne returned.
        // So we close it immediately.
        event_.Close();
      }
      private int count_;
      private readonly AutoResetEvent event_;
    }

    private class WorkerCountIncrementer : IDisposable
    {
      internal WorkerCountIncrementer()
      {
        Interlocked.Increment(ref workersUsed_);
      }
      public void Dispose()
      {
        Interlocked.Decrement(ref workersUsed_);
      }
    }
    private static void IncreaseWorkerCount()
    {
      Interlocked.Increment(ref workersUsed_);
    }
    private static void DecreaseWorkerCount()
    {
      Interlocked.Decrement(ref workersUsed_);
    }
    private static IDisposable WorkerCountIncrement
    {
      get { return new WorkerCountIncrementer(); }
    }
    #endregion Types

    #region Private Methods
    private static int GetAvailableWorkers()
    {
      int workersAvailable = workersAllocated_ - workersUsed_;
      if (workersAvailable < 2)
        return 1;
      return workersAvailable;
    }

    private static int GetWorkersAndRange(
      int start, int stop, ref ArrayPool<Range> pool)
    {
      int workersAvailable = workersAllocated_ - workersUsed_;
      if (workersAvailable < 2)
        return 1;

      int nitems = stop - start;
      if (nitems < 2)
        return 1;

      int nworkers = (nitems < workersAvailable ? nitems : workersAvailable);
      int nranges = (2 * nworkers <= nitems ? (2 * nworkers) : nworkers);
      Range[] ranges = new Range[nranges];
      int begin = start;
      for (int i = 0; i < nranges - 1; ++i)
      {
        int end = start + nitems * (i + 1) / nranges;
        ranges[i] = new Range(begin, end);
        begin = end;
      }
      ranges[nranges - 1] = new Range(begin, stop);
      pool = new ArrayPool<Range>(ranges);
      return nworkers;
    }
    #endregion Private Methods

    #region Properties
    /// <summary>
    ///   Whether the parallel algorithms are enabled.
    /// </summary>
    public static bool Enabled
    {
      get { return workersAllocated_ > 1; }
    }
    #endregion Properties

    #region Data
    private static int workersUsed_ = 0;
    #endregion Data
  } // partial class Executor
}
