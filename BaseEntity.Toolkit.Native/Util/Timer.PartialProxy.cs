using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace BaseEntity.Toolkit.Util
{
    partial class Timer
    {
      public void Start() { start(); }
      public void Stop() { stop(); }
      public void Resume() { resume(); }
      public double Elapsed => getElapsed();
    }
}
