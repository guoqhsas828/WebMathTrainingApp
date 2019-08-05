//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;

using NUnit.Framework;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestDigitalBarrierOptionModel : ToolkitTestBase
  {
    private double[,] testData = {
      {105, 9.7264, 9.7264},
      {95, 11.6553, 11.6553},
      {105, 64.8427, 64.8427}, //68.0848, 68.0848
      {95, 77.7017, 77.7017}, //11.6553, 11.6553
      {105, 9.3604, 9.3604},
      {95, 11.2223, 11.2223},
      {105, 64.8426, 64.8426},
      {95, 77.7017, 77.7017},
      {105, 4.9081, 4.9081},
      {95, 3.0461, 3.0461},
      {105, 40.1574, 40.1574},
      {95, 17.2983, 17.2983},
      {105, 4.9289, 6.215},
      {95, 5.8926, 7.4519}, //5.371, 7.4519
      {105, 37.2782, 45.853},
      {95, 44.5294, 54.9262},
      {105, 4.4314, 3.1454},
      {95, 5.3297, 3.7704},
      {105, 27.5644, 18.9896},
      {95, 33.1723, 22.7755}, //38.7533, 22.7755
      {105, 4.8758, 4.9081},
      {95, 0, 0.0407},
      {105, 39.9391, 40.1574},
      {95, 0, 0.2676},
      {105, 0.0323, 0},
      {95, 3.0461, 3.0054},
      {105, 0.2183, 0},
      {95, 17.2983, 17.0306},
    };

    const OptionBarrierFlag notouch = OptionBarrierFlag.NoTouch;
    const OptionBarrierFlag cash = OptionBarrierFlag.Regular;
    const OptionBarrierFlag asset = OptionBarrierFlag.PayAsset;

    const OptionBarrierFlag touch = OptionBarrierFlag.OneTouch;
    const OptionBarrierFlag atHit = touch | OptionBarrierFlag.PayAtBarrierHit;
    const OptionBarrierFlag expiry = touch;

    private Tuple<OptionType, OptionBarrierType, OptionBarrierFlag>[] barriers = 
    {
      // Touch options
      Tuple.Create(OptionType.None, OptionBarrierType.DownIn, cash | atHit),
      Tuple.Create(OptionType.None, OptionBarrierType.UpIn, cash | atHit),
      Tuple.Create(OptionType.None, OptionBarrierType.DownIn, asset | atHit),
      Tuple.Create(OptionType.None, OptionBarrierType.UpIn, asset | atHit),
      Tuple.Create(OptionType.None, OptionBarrierType.DownIn, cash | expiry),
      Tuple.Create(OptionType.None, OptionBarrierType.UpIn, cash | expiry),
      Tuple.Create(OptionType.None, OptionBarrierType.DownIn, asset | expiry),
      Tuple.Create(OptionType.None, OptionBarrierType.UpIn, asset | expiry),
      Tuple.Create(OptionType.None, OptionBarrierType.DownOut, notouch | cash),
      Tuple.Create(OptionType.None, OptionBarrierType.UpOut, notouch | cash),
      Tuple.Create(OptionType.None, OptionBarrierType.DownOut, notouch | asset),
      Tuple.Create(OptionType.None, OptionBarrierType.UpOut, notouch | asset),

      // Digital barrier options
      Tuple.Create(OptionType.Call, OptionBarrierType.DownIn, cash),
      Tuple.Create(OptionType.Call, OptionBarrierType.UpIn, cash),
      Tuple.Create(OptionType.Call, OptionBarrierType.DownIn, asset),
      Tuple.Create(OptionType.Call, OptionBarrierType.UpIn, asset),

      Tuple.Create(OptionType.Put, OptionBarrierType.DownIn, cash),
      Tuple.Create(OptionType.Put, OptionBarrierType.UpIn, cash),
      Tuple.Create(OptionType.Put, OptionBarrierType.DownIn, asset),
      Tuple.Create(OptionType.Put, OptionBarrierType.UpIn, asset),

      Tuple.Create(OptionType.Call, OptionBarrierType.DownOut, cash),
      Tuple.Create(OptionType.Call, OptionBarrierType.UpOut, cash),
      Tuple.Create(OptionType.Call, OptionBarrierType.DownOut, asset),
      Tuple.Create(OptionType.Call, OptionBarrierType.UpOut, asset),

      Tuple.Create(OptionType.Put, OptionBarrierType.DownOut, cash),
      Tuple.Create(OptionType.Put, OptionBarrierType.UpOut, cash),
      Tuple.Create(OptionType.Put, OptionBarrierType.DownOut, asset),
      Tuple.Create(OptionType.Put, OptionBarrierType.UpOut, asset),
    };

    //TODO: symmetry test

    [Test]
    public void Prices()
    {
      double H = 100, K = 15, T = 0.5, r = 0.1, d = 0, sigma = 0.2;
      double X1 = 102, X2 = 98;
      for (int i = 0; i < 28; ++i)
      {
        var S = testData[i, 0];
        var optionType = barriers[i].Item1;
        var barrierType = barriers[i].Item2;
        var flags = barriers[i].Item3;
        var p1 = DigitalBarrierOption.Price(
          optionType, barrierType, T, S, X1, H, K, r, d, sigma, flags);
        var e1 = testData[i, 1];
        Assert.AreEqual(e1, p1, 1E-4);
        var p2 = DigitalBarrierOption.Price(
          optionType, barrierType, T, S, X2, H, K, r, d, sigma, flags);
        var e2 = testData[i, 2];
        Assert.AreEqual(e2, p2, 1E-4);
      }
    }

    [Test, Description(
      "Consistency of DigitalBarrierOption and TimeDependentBarrierOption "
      + "in the case of touch options")]
    public void TouchOptionConsistency()
    {
      double H = 100, K = 15, T = 0.5, r = 0.1, d = 0, sigma = 0.2;
      double X1 = 102, X2 = 98;
      for (int i = 0; i < 12; ++i)
      {
        var S = testData[i, 0];
        var optionType = barriers[i].Item1;
        var barrierType = barriers[i].Item2;
        var flags = barriers[i].Item3;
        var p1 = DigitalBarrierOption.Price(
          optionType, barrierType, T, S, X1, H, K, r, d, sigma, flags);
        var p2 = TouchPrice(barrierType, T, S, X1, H, K, r, d, sigma, flags);
        Assert.AreEqual(p1, p2, 5E-14);
        p1 = DigitalBarrierOption.Price(
          optionType, barrierType, T, S, X2, H, K, r, d, sigma, flags);
        p2 = TouchPrice(barrierType, T, S, X1, H, K, r, d, sigma, flags);
        Assert.AreEqual(p1, p2, 5E-14);
      }
    }


    /// <summary>
    ///  Calculate the touch option prices based on the Time Dependent Barrier Option model.
    /// </summary>
    /// <remarks>>
    ///  <c>TimeDependentBarrierOption</c> is the model for FX options.  To use it for
    ///   options on assets like stocks and commodities, it is necessary to make adjustments.
    /// </remarks>
    /// <param name="barrierType">Type of the barrier.</param>
    /// <param name="time">The time.</param>
    /// <param name="spot">The spot.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="barrier">The barrier.</param>
    /// <param name="cashAmount">The cash amount.</param>
    /// <param name="rd">The rd.</param>
    /// <param name="rf">The rf.</param>
    /// <param name="sigma">The sigma.</param>
    /// <param name="flags">The flags.</param>
    /// <returns>System.Double.</returns>
    private static double TouchPrice(OptionBarrierType barrierType,
      double time, double spot, double strike, double barrier, double cashAmount,
      double rd, double rf, double sigma, OptionBarrierFlag flags)
    {
      var touchType = barrierType;
      if ((flags & OptionBarrierFlag.OneTouch) != 0)
        touchType = OptionBarrierType.OneTouch;
      else if ((flags & OptionBarrierFlag.NoTouch) != 0)
        touchType = OptionBarrierType.NoTouch;
      var p = TimeDependentBarrierOption.Price(OptionType.None,
        touchType, time, spot, strike, barrier, 0.0,
        rd, rf, sigma, (int)flags);
      return (flags & OptionBarrierFlag.PayAsset) != 0
        ? p * ((flags & OptionBarrierFlag.PayAtBarrierHit) != 0
          ? spot
          : (spot * Math.Exp((rd - rf) * time)))
        : p * cashAmount;
    }
  }
}
