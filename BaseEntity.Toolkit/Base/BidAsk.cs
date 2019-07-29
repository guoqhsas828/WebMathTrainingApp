// 
//  -2012. All rights reserved.
// 

using System;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  ///
  /// <summary>
  ///   A market quote (price/yield/etc) consisting of a bid
  ///   and an ask.
  /// </summary>
  ///
  [Serializable]
  public class BidAsk : BaseEntityObject
  {
    //
    // Constructors
    //

    /// <summary>
    ///   Constructor
    /// </summary>
    public BidAsk()
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="bid">Bid quote (Double.NaN = none)</param>
    /// <param name="ask">Ask quote (Double.NaN = none)</param>
    /// <param name="type">Quote type</param>
    ///
    public
      BidAsk(double bid, double ask, QuotingConvention type)
    {
      if (Double.IsNaN(bid) && Double.IsNaN(ask))
        throw new ArgumentException("Must specify either bid or ask quote");
      bid_ = bid;
      ask_ = ask;
      type_ = type;
    }

    //
    // Attributes
    //

    /// <summary>
    ///   Bid quote
    /// </summary>
    public double Bid
    {
      get
      {
        return !Double.IsNaN(bid_) ? bid_ : ask_;
      }
      set { bid_ = value; }
    }

    /// <summary>
    ///    Ask quote
    /// </summary>
    public double Ask
    {
      get
      {
        return !Double.IsNaN(ask_) ? ask_ : bid_;
      }
      set { ask_ = value; }
    }

    /// <summary>
    ///   Mid quote
    /// </summary>
    public double Mid
    {
      get
      {
        if (!Double.IsNaN(bid_))
        {
          if (!Double.IsNaN(ask_))
            return (bid_ + ask_) / 2.0;
          return bid_;
        }
        else
          return ask_;
      }
    }

    /// <summary>
    ///   Quote type
    /// </summary>
    public QuotingConvention Type
    {
      get { return type_; }
      set { type_ = value; }
    }

    //
    // data
    //
    private double bid_;
    private double ask_;
    private QuotingConvention type_;
  }
}
