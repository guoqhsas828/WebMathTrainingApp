////
//// Copyright (c)    2018. All rights reserved.
////

//using System;
//using BaseEntity.Shared;
//using BaseEntity.Toolkit.Base;
//using BaseEntity.Toolkit.Base.Serialization;
//using NUnit.Framework;

//namespace BaseEntity.Toolkit.Tests
//{

//  /// <summary>
//  ///   Test serialization of anonymous delegates.
//  /// </summary>
//  [TestFixture]
//  public class DelegateSerializationTests
//  {
//    [Test]
//    public void WrapFunc()
//    {
//      var fn = GetMyFunction(1.0, 2.0);
//      var sf = fn.WrapSerializableDelegate();
//      var v1 = fn(1.5, 0.5);
//      var v2 = sf(1.5, 0.5);
//      Assert.AreEqual(v1, v2, 1E-14);
//    }

//    [Test]
//    public void WrapAction()
//    {
//      var fn = GetAction(1.0, 2.0);
//      fn(1.5, 0.5);
//      var v1 = _result;
//      var sf = fn.WrapSerializableDelegate();
//      sf(1.5, 0.5);
//      var v2 = _result;
//      Assert.AreEqual(v1, v2, 1E-14);
//    }

//    [Test]
//    public void TestSurfaceInterpolator()
//    {
//      const string anonymousClassSig = "<>c__DisplayClass";
//      var surface = new VolatilitySurfaceInterpolator(
//        (start, end) => Dt.RelativeTime(start, end),
//        GetFunction(1.0, 2.0));
//      // Actually do serialization.
//      var surface2 = surface.CloneObjectGraph(CloneMethod.Serialization);
//      // check that surface has unwrapped function.
//      var n1 = surface.SurfaceFunction.Target.GetType().FullName;
//      Assert.IsTrue(n1 != null && n1.Contains(anonymousClassSig));
//      // check that surface2 has unwrapped function.
//      var n2 = surface2.SurfaceFunction.Target.GetType().FullName;
//      Assert.IsTrue(n2 != null && n2.Contains(anonymousClassSig));
//      // check both functions produce the same results.
//      var v1 = surface.SurfaceFunction(1.5, 0.5, SmileInputKind.Strike);
//      var v2 = surface2.SurfaceFunction(1.5, 0.5, SmileInputKind.Strike);
//      Assert.AreEqual(v1, v2, 1E-14);
//    }

//    private delegate double MyFunc(double x1, double x2);
//    private MyFunc GetMyFunction(double a, double b)
//    {
//      return (x, y) => a * x + b * y;
//    }

//    private Func<double,double,SmileInputKind,double> GetFunction(double a, double b)
//    {
//      return (x, y, kind) => a * x + b * y;
//    }


//    private Action<double, double> GetAction(double a, double b)
//    {
//      return (x, y) => { _result = a * x + b * y; };
//    }

//    private double _result;
//  }

//}
