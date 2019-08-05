//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class CppTests
  {
    [Test, TestCaseSource(nameof(CppCases))]
    public static void RunCases(uint index)
    {
      string msg = null;
      if (!RunTestCase(index, s => msg += s))
        throw new AssertionException(msg);
      return;
    }

    /// <summary>
    ///   Gets a full list of all the C++ test cases.
    /// </summary>
    private static IEnumerable<TestCaseData> CppCases
    {
      get
      {
        BaseEntityContext.Initialize();
        var count = GetTestCaseCount();
        for (uint i = 0; i < count; ++i)
        {
          var name = Marshal.PtrToStringAnsi(GetTestCaseName(i));
          yield return new TestCaseData(i).SetName(name);
        }
      }
    }

    private static bool RunTestCase(uint caseIndex, Action<string> fn)
    {
      return RunTestCase(caseIndex, (d, s) => fn(Marshal.PtrToStringAnsi(s)),
        IntPtr.Zero);
    }

    #region Interfaces with the native functions

    private delegate void MessageReceiver(IntPtr data, IntPtr msg);

    [DllImport("CppTests", CallingConvention = CallingConvention.StdCall)]
    private static extern uint GetTestCaseCount();

    [DllImport("CppTests", CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr GetTestCaseName(uint caseIndex);

    [DllImport("CppTests", CallingConvention = CallingConvention.StdCall)]
    private static extern bool RunTestCase(uint caseIndex, MessageReceiver sinker, IntPtr data);

    #endregion
  }
}