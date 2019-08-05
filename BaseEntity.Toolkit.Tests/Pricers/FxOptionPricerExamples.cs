//
// Copyright (c)    2015. All rights reserved.
//
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  using NUnit.Framework;

  /// <summary>
  ///  These tests are used in documentation.
  ///  Must compile and validate successfully.
  /// </summary>
  [TestFixture]
  public class FxOptionPricerExamples
  {
    [Test]
    public static void CreateRegularOption()
    {
      #region RegularCall

      Dt effective = new Dt(20171004), expiry = new Dt(20171108);
      var option = new FxOption
      {
        Effective = effective,
        Maturity = expiry,
        ReceiveCcy = Currency.EUR,
        PayCcy = Currency.USD,
        Flags = OptionBarrierFlag.Regular,
        Style = OptionStyle.European,
        Type = OptionType.Call,
        Strike = 1.2,
      };
      option.Validate();

      #endregion
    }

    [Test]
    public static void CreateDigitalOption()
    {
      #region DigitalPut

      Dt effective = new Dt(20171004), expiry = new Dt(20171108);
      var option = new FxOption
      {
        Effective = effective,
        Maturity = expiry,
        ReceiveCcy = Currency.EUR,
        PayCcy = Currency.USD,
        Flags = OptionBarrierFlag.Digital,
        Style = OptionStyle.European,
        Type = OptionType.Put,
        Strike = 1.2,
      };
      option.Validate();

      #endregion
    }

    [Test]
    public static void CreateSingleBarrierRegularOption()
    {
      #region SingleBarrierUpInCall

      Dt effective = new Dt(20171004), expiry = new Dt(20171108);
      var barrier = new Barrier
      {
        BarrierType = OptionBarrierType.UpIn,
        Value = 1.2,
      };
      var option = new FxOption
      {
        Effective = effective,
        Maturity = expiry,
        ReceiveCcy = Currency.EUR,
        PayCcy = Currency.USD,
        Flags = OptionBarrierFlag.Regular,
        Style = OptionStyle.European,
        Type = OptionType.Call,
        Strike = 1.1,
        Barriers = new[] { barrier },
      };
      option.Validate();

      #endregion
    }

    [Test]
    public static void CreateSingleBarrierDigitalOption()
    {
      #region SingleBarrierDownOutDigitalPut

      Dt effective = new Dt(20171004), expiry = new Dt(20171108);
      var barrier = new Barrier
      {
        BarrierType = OptionBarrierType.DownOut,
        Value = 1.05,
      };
      var option = new FxOption
      {
        Effective = effective,
        Maturity = expiry,
        ReceiveCcy = Currency.EUR,
        PayCcy = Currency.USD,
        Flags = OptionBarrierFlag.Digital,
        Style = OptionStyle.European,
        Type = OptionType.Put,
        Strike = 1.1,
        Barriers = new[] { barrier },
      };
      option.Validate();

      #endregion
    }

    [Test]
    public static void CreateOneTouchOption()
    {
      #region OneTouchUpIn

      Dt effective = new Dt(20171004), expiry = new Dt(20171108);
      var barrier = new Barrier
      {
        BarrierType = OptionBarrierType.UpIn,
        Value = 1.3,
      };
      var oneTouch = new FxOption
      {
        Flags = OptionBarrierFlag.OneTouch,
        Barriers = new[] { barrier },
        Effective = effective,
        Maturity = expiry,
        ReceiveCcy = Currency.EUR,
        PayCcy = Currency.USD,
      };
      oneTouch.Validate();

      #endregion
    }

    [Test]
    public static void CreateNoTouchOption()
    {
      #region NoTouchDownOut

      Dt effective = new Dt(20171004), expiry = new Dt(20171108);
      var barrier = new Barrier
      {
        BarrierType = OptionBarrierType.DownOut,
        Value = 1.1,
      };
      var noTouch = new FxOption
      {
        Flags = OptionBarrierFlag.NoTouch,
        Barriers = new[] { barrier },
        Effective = effective,
        Maturity = expiry,
        ReceiveCcy = Currency.EUR,
        PayCcy = Currency.USD,
      };
      noTouch.Validate();

      #endregion
    }


    [Test]
    public static void CreateOneTouchDoubleKnockInOption()
    {
      #region OneTouchDoubleKnockIn

      Dt effective = new Dt(20171004), expiry = new Dt(20171108);
      var lowerBarrier = new Barrier
      {
        BarrierType = OptionBarrierType.DownIn,
        Value = 1.1,
      };
      var upperBarrier = new Barrier
      {
        BarrierType = OptionBarrierType.UpIn,
        Value = 1.3,
      };
      var oneTouch = new FxOption
      {
        Flags = OptionBarrierFlag.OneTouch,
        Barriers = new[] { lowerBarrier, upperBarrier },
        Effective = effective,
        Maturity = expiry,
        ReceiveCcy = Currency.EUR,
        PayCcy = Currency.USD,
      };
      oneTouch.Validate();

      #endregion
    }


    [Test]
    public static void CreateNoTouchDoubleKnockOutOption()
    {
      #region NoTouchDoubleKnockOut

      Dt effective = new Dt(20171004), expiry = new Dt(20171108);
      var lowerBarrier = new Barrier
      {
        BarrierType = OptionBarrierType.DownOut,
        Value = 1.1,
      };
      var upperBarrier = new Barrier
      {
        BarrierType = OptionBarrierType.UpOut,
        Value = 1.3,
      };
      var noTouch = new FxOption
      {
        Flags = OptionBarrierFlag.NoTouch,
        Barriers = new[] { lowerBarrier, upperBarrier },
        Effective = effective,
        Maturity = expiry,
        ReceiveCcy = Currency.EUR,
        PayCcy = Currency.USD,
      };
      noTouch.Validate();

      #endregion
    }
  }
}
