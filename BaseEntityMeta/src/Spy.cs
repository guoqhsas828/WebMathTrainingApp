// 
// Copyright (c) WebMathTraining 2002-2014. All rights reserved.
// 

using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using log4net;

// TODO: Move to WebMathTraining.Reactive
namespace BaseEntity.Metadata
{
  /// <summary>
  /// 
  /// </summary>
  public static class ObservableExtensions
  {
    private static readonly ILog Log = LogManager.GetLogger(typeof(ObservableExtensions));

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="opName"></param>
    /// <returns></returns>
    public static IObservable<T> Spy<T>(this IObservable<T> source, string opName = null)
    {
      opName = opName ?? "IObservable";
      Log.DebugFormat("{0}: Observable obtained on Thread: {1}", opName, Thread.CurrentThread.ManagedThreadId);

      return Observable.Create<T>(obs =>
      {
        Log.DebugFormat("{0}: Subscribed to on Thread: {1}", opName, Thread.CurrentThread.ManagedThreadId);

        try
        {
          IDisposable subscription = source
            .Do(x => Log.DebugFormat("{0}: OnNext({1}) on Thread: {2}",
              opName,
              x,
              Thread.CurrentThread.ManagedThreadId),
              ex => Log.DebugFormat("{0}: OnError({1}) on Thread: {2}",
                opName,
                ex,
                Thread.CurrentThread.ManagedThreadId),
              () => Log.DebugFormat("{0}: OnCompleted() on Thread: {1}",
                opName,
                Thread.CurrentThread.ManagedThreadId)
            )
            .Subscribe(obs);
          return new CompositeDisposable(
            subscription, Disposable.Create(() => Log.DebugFormat("{0}: Cleaned up on Thread: {1}", opName, Thread.CurrentThread.ManagedThreadId)));
        }
        finally
        {
          Log.DebugFormat("{0}: Subscription completed.", opName);
        }
      });
    }
  }
}