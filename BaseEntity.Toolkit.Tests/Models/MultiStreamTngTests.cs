//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Threading;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics.Rng;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture(MultiStreamRng.Type.MersenneTwister)]
  [TestFixture(MultiStreamRng.Type.Sobol, Ignore = "Not work yet")]
  [TestFixture(MultiStreamRng.Type.Projective, Ignore = "Not work yet")]
  public class MultiStreamRngTests
  {
    private readonly MultiStreamRng.Type _type;

    public MultiStreamRngTests(MultiStreamRng.Type type)
    {
      _type = type;
    }

    #region Test Dispose

    [Test]
    public void TestDispose()
    {
      Dt asOf = Dt.Today();
      var dates = ArrayUtil.Generate(5, i => Dt.AddMonth(asOf, (i + 1) * 3, false) - asOf);
      using (var rng1 = MultiStreamRng.Create(_type, 1, dates))
      {
        var idx = 10;
        var workspace = new double[dates.Length];
        using (var rng2 = rng1.Clone())
        {
          rng2.Uniform(idx, workspace);
        }
      }
      // Should throw no excpetion.
      return;
    }

    #endregion

    #region ParallelMC

    private class Thread
    {
      internal Thread(MultiStreamRng rng, int dim)
      {
        Generator = rng.Clone();
        Parallel = new double[dim];
      }

      internal double[] Parallel { get; private set; }
      internal MultiStreamRng Generator { get; private set; }
    }

    [Test]
    public void ParallelRandomNumbersFor()
    {
      const int nfactors = 10;
      const int ndates = 50;
      const int size = nfactors * ndates;
      const int paths = 500;
      Dt asOf_ = Dt.Today();
      var rng = MultiStreamRng.Create(
        _type, nfactors,
        ArrayUtil.Generate(ndates, i => Dt.Add(asOf_, i * 180) - asOf_));
      var serialRng = new RandomNumberGenerator(985456376);
      var allSerial = new double[size * paths];
      var allParallel = new double[size * paths];
      for (int i = 0, k = 0; i < paths; ++i)
      {
        for (int j = 0; j < size; ++j, ++k)
          allSerial[k] = serialRng.Uniform(0, 1);
      }
      ParallelFor(0, paths, () => new Thread(rng, size), (idx, thread) =>
      {
        System.Threading.Thread.Yield(); // simulate race condition
        thread.Generator.Uniform(idx, thread.Parallel);
        int rngidx = idx * size;
        for (int j = 0; j < size; ++j)
          allParallel[rngidx + j] = thread.Parallel[j];
      });
      bool success = true;
      for (int i = 0; i < allParallel.Length; ++i)
      {
        success &= allParallel[i].AlmostEquals(allSerial[i]);
      }
      Assert.That(success,Is.True);
      return;
    }

    [Test]
    public void ParallelRandomNumbersWithJump()
    {
      const int nfactors = 10;
      const int ndates = 50;
      const int size = nfactors * ndates;
      const int paths = 500;
      Dt asOf_ = Dt.Today();
      var rng = MultiStreamRng.Create(
        _type, nfactors,
        ArrayUtil.Generate(ndates, i => Dt.Add(asOf_, i * 180) - asOf_));
      var serialRng = new RandomNumberGenerator(985456376);
      var allSerial = new double[size * paths];
      var serial = new double[size];
      var parallel = new double[size];
      for (int i = 0, k = 0; i < paths; ++i)
      {
        for (int j = 0; j < size; ++j, ++k)
          allSerial[k] = serialRng.Uniform(0, 1);
      }
      var rand = new Random();
      bool success = true;
      for (int i = 0; i < 100; ++i)
      {
        int idx = rand.Next(500);
        rng.Uniform(idx, parallel);
        for (int j = 0; j < size; ++j)
          serial[j] = allSerial[idx * size + j];
        for (int j = 0; j < size; ++j)
          success &= parallel[j].AlmostEquals(serial[j]);
      }
      Assert.That(success,Is.True);
    }

    #endregion

    #region My Parallel for

    static void ParallelFor<U>(int start, int stop,
      Func<U> init, Action<int, U> action)
    {
      int count = stop - start;
      if (count <= 0) return;

      var states = new U[count];
      for (int i = 0; i < count; ++i)
        states[i] = init();

      var threads = new System.Threading.Thread[count];
      var cde = new CountdownEvent(count);
      for (int i = 0; i < count; ++i)
        threads[i] = new System.Threading.Thread((o) =>
        {
          var tuple = (Tuple<int, U>)o;
          action(tuple.Item1, tuple.Item2);
          cde.Signal();
        });

      for (int i = 0; i < count; ++i)
        threads[i].Start(System.Tuple.Create(i, states[i]));

      cde.Wait();
    }

    #endregion

  }
}
