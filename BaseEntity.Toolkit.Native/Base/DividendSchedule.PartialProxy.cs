using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Base
{
  public sealed partial class DividendSchedule : IEnumerable<Tuple<Dt, DividendSchedule.DividendType, double>>
  {
    #region Properties

    /// <summary>
    /// As of date
    /// </summary>
    public Dt AsOf { get; private set; }

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor of empty schedule 
    /// </summary>
    /// <param name="asOf">As of date</param>
    public DividendSchedule(Dt asOf) : this()
    {
      AsOf = asOf;
    }

    /// <summary>
    /// Constructor of schedule
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="dividends">Dividends</param>
    public DividendSchedule(Dt asOf, IEnumerable<Tuple<Dt, DividendType, double>> dividends) : this()
    {
      AsOf = asOf;
      if (dividends == null)
        return;
      foreach (var dividend in dividends.Where(d => d.Item1 > asOf).OrderBy(div => div.Item1))
        Add(dividend.Item1, dividend.Item3, dividend.Item2);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Add fixed dividend
    /// </summary>
    /// <param name="date">Date</param>
    /// <param name="amount">Amount</param>
    public void Add(Dt date, double amount)
    {
      if (date > AsOf)
        Add(date, Dt.RelativeTime(AsOf, date), amount);
    }

    /// <summary>
    /// Add fixed/proportional dividend
    /// </summary>
    /// <param name="date">Date</param>
    /// <param name="amount">Amount</param>
    /// <param name="type">Type</param>
    public void Add(Dt date, double amount, DividendType type)
    {
      if (date > AsOf)
        Add(date, Dt.RelativeTime(AsOf, date), amount, (int)type);
    }

    /// <summary>
    /// Calculate Pv of stream of dividends
    /// </summary>
    /// <param name="spot">Spot asset value</param>
    /// <param name="rfr">Risk free rate</param>
    /// <param name="T">Maturity time(in years)</param>
    /// <returns>Pv of dividends assuming AsOf is the spot date</returns>
    public double PresentValue(double spot, double rfr, double T)
    {
      return Pv(spot, rfr, T);
    }

    /// <summary>
    /// Retrieve ith dividend type 
    /// </summary>
    /// <param name="i">Index</param>
    /// <returns>Dividend type</returns>
    public DividendType GetDividendType(int i)
    {
      return (DividendType)GetType(i);
    }

    #endregion

    #region DividendEnumerator

    private class DividendEnumerator : IEnumerator<Tuple<Dt, DividendType, double>>
    {
      #region Data

      private readonly DividendSchedule _dividendSchedule;
      private int _position = -1;

      #endregion

      #region Constructor

      public DividendEnumerator(DividendSchedule dividendSchedule)
      {
        _dividendSchedule = dividendSchedule;
      }

      #endregion

      #region Properties

      public Tuple<Dt, DividendType, double> Current
      {
        get
        {
          return new Tuple<Dt, DividendType, double>(_dividendSchedule.GetDt(_position), _dividendSchedule.GetDividendType(_position),
                                                     _dividendSchedule.GetAmount(_position));
        }
      }

      object IEnumerator.Current
      {
        get { return Current; }
      }

      #endregion

      #region Methods

      public bool MoveNext()
      {
        _position++;
        return (_position < _dividendSchedule.Size());
      }

      public void Reset()
      {
        _position = -1;
      }

      void IDisposable.Dispose()
      {}

      #endregion
    }

    #endregion
    
    #region IEnumerable<Tuple<Dt,DividendType,double>> Members
    
    /// <summary>
    /// Get IEnumerator
    /// </summary>
    /// <returns>IEnumerator</returns>
    public IEnumerator<Tuple<Dt, DividendType, double>> GetEnumerator()
    {
      return new DividendEnumerator(this);
    }

    #endregion

    #region IEnumerable Members
    
    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    #endregion
  }
}
