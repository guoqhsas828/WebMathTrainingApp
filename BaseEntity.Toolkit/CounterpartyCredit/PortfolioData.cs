using System;
using System.Collections.Generic;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// Pricer data
  /// </summary>
  [Serializable]
  public class PortfolioData
  {
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(PortfolioData));

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="ccy"> Set of currencies</param>
    /// <param name="portfolio">Set of pricers</param>
    /// <param name="nettingId">Id of the netting set for each pricer. Only exposures
    ///  belonging to the same netting set are netted out</param>
    /// <param name="map">Map of column names to column index</param>
    public PortfolioData(
      Currency[] ccy,
      IPricer[] portfolio,
      string[] nettingId,
      IDictionary<string, int> map)
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Creating PortfolioData...");
        LogArrays(portfolio, nettingId);
      }

      Map = (map != null) ? new Dictionary<string, int>(map) : new Dictionary<string, int>();
      var vanillaList = new List<Tuple<CcrPricer, int, int>>();
      var exoticList = new List<Tuple<IAmericanMonteCarloAdapter, int, int>>();
      for (int i = 0; i < nettingId.Length; ++i)
      {
        var id = nettingId[i];
        var p = portfolio[i];
        int idx;
        if (Map.TryGetValue(id, out idx))
        {
          var amcPricer = GetAmcAdapter(p);
          if ((amcPricer != null) && amcPricer.Exotic)
            exoticList.Add(new Tuple<IAmericanMonteCarloAdapter, int, int>(amcPricer, Array.IndexOf(ccy, p.ValuationCurrency), idx));
          else
          {
            var cp = CcrPricer.Get(p);
            vanillaList.Add(new Tuple<CcrPricer, int, int>(cp, Array.IndexOf(ccy, cp.Ccy), idx));
          }
        }
        else
        {
          idx = Map.Count;
          Map.Add(id, idx);
          var amcPricer = GetAmcAdapter(p);
          if ((amcPricer != null) && amcPricer.Exotic)
            exoticList.Add(new Tuple<IAmericanMonteCarloAdapter, int, int>(amcPricer,
                                                                       Array.IndexOf(ccy, p.ValuationCurrency), idx));
          else
          {
            var cp = CcrPricer.Get(p);
            vanillaList.Add(new Tuple<CcrPricer, int, int>(cp, Array.IndexOf(ccy, cp.Ccy), idx));
          }
        }
      }
      Portfolio = vanillaList.ToArray();
      Exotics = exoticList.ToArray();
    }

    private void LogArrays(IPricer[] portfolio, string[] nettingId)
    {
      {
        var builder = new StringBuilder();
        if (portfolio != null && portfolio.Length > 0)
        {
          builder.Append("Portfolio: ");
          var i = 0;
          for (; i < portfolio.Length - 1; ++i)
          {
            if (portfolio[i] == null || portfolio[i].Product == null || portfolio[i].Product.Description == null)
            {
              builder.Append("Portfolio is not incorrectly defined, ");
            }
            else
            {
              builder.Append(portfolio[i].Product.Description + ", ");
            }
          }
          if (portfolio[i] == null || portfolio[i].Product == null || portfolio[i].Product.Description == null)
          {
            builder.Append("Portfolio is not incorrectly defined");
          }
          else
          {
            builder.Append(portfolio[i].Product.Description);
          }
        }
        else
        {
          if (portfolio == null)
          {
            builder.Append("The Portfolio Array is null");
          }
          else
          {
            builder.Append("The Portfolio Array is empty");
          }
        }
        Logger.Debug(builder.ToString());
      }
      {
        var builder = new StringBuilder();
        if (nettingId != null && nettingId.Length > 0)
        {
          builder.Append("NettingId: ");
          var i = 0;
          for (; i < nettingId.Length - 1; ++i)
          {
            if (nettingId[i] == null)
            {
              builder.Append("NettingId element is null, ");
            }
            else
            {
              builder.Append(nettingId[i] + ", ");
            }
          }
          if (nettingId[i] == null)
          {
            builder.Append("NettingId element is null");
          }
          else
          {
            builder.Append(nettingId[i]);
          }
        }
        else
        {
          if (nettingId == null)
          {
            builder.Append("The NettingId Array is null");
          }
          else
          {
            builder.Append("The NettingId Array is empty");
          }
        }
        Logger.Debug(builder.ToString());
      }
    }

    public static IAmericanMonteCarloAdapter GetAmcAdapter(IPricer p)
    {
      var amcPricer = p as IAmericanMonteCarloAdapter;
      if (amcPricer != null) return amcPricer;
      var provider = p as IAmericanMonteCarloAdapterProvider;
      return provider != null ? provider.GetAdapter() : null;
    }

    #endregion

    #region Data

    public readonly Tuple<IAmericanMonteCarloAdapter, int, int>[] Exotics;
    public readonly Dictionary<string, int> Map;
    public readonly Tuple<CcrPricer, int, int>[] Portfolio;

    #endregion
  }
}