/*
 * Simulator.PartialProxy.cs
 *
 * Copyright (c)   2002-2010. All rights reserved.
 *
 */

using System;
using System.Runtime.InteropServices;
using BaseEntity.Toolkit.Native;

namespace BaseEntity.Toolkit.Models.Simulations.Native
{
  partial class Simulator : INativeObject
  {
    public HandleRef HandleRef => swigCPtr;

    public IntPtr Handle => HandleRef.Handle;

    public bool OwnMemory
    {
      get => swigCMemOwn;
      set => swigCMemOwn = value;
    }
  }
}