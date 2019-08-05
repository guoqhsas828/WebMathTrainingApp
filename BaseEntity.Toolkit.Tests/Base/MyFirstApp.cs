#region My_First_App

using System;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Base;

namespace MyFirstToolkitApp
{
  public class MyFirstApp
  {
    public void Main()
    {
      // Initialise Toolkit
      Configurator.Init();
      // Show standard IMM date for Dec, 2008
      Console.WriteLine("IMM 5Yr is {0}", Dt.ImmDate(Dt.Today(), "EDZ8"));
      Console.ReadLine();
    }
  }
}

#endregion
