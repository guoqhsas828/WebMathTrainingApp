/*
 * BaseCorrelationBump.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Collections;
using System.Data;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Calibrators.BaseCorrelation
{
  /// <summary>
  ///    Base correlation bump object
  /// </summary>
  [Serializable]
	public class BaseCorrelationBump
  {
    #region Constructors
    /// <summary>
    ///   Default constructor
    /// </summary>
    public BaseCorrelationBump() { }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="trancheBumps">Bump sizes for tranches by tenors and detachments</param>
    /// <param name="indexBumps">Bump sizes for index by tenors</param>
    /// <param name="bumpTarget">Bump target (Quote or Correlation)</param>
    /// <param name="bumpMethod">Bump method (Absolute or Relative)</param>
    public BaseCorrelationBump(
      BumpSize[][] trancheBumps,
      BumpSize[] indexBumps,
      BumpTarget bumpTarget,
      BumpMethod bumpMethod)
    {
      trancheBumps_ = RemoveZeroBumps(trancheBumps);
      indexBumps_ = RemoveZeroBumps(indexBumps);
      target_ = bumpTarget;
      method_ = bumpMethod;
    }

    /// <summary>
    ///   Create a bump object.
    /// </summary>
    /// <param name="bct">The base correlation term structure to bump</param>
    /// <param name="selectTenorDates">Selected tenor dates to bump</param>
    /// <param name="selectDetachments">Selected detachment to bump</param>
    /// <param name="trancheBumpByDps">Array of bump sizes</param>
    /// <param name="indexBump">Size of bump on index quotes for the selected tenor dates</param>
    /// <param name="relative">True for relative bump; false otherwise</param>
    /// <param name="onquotes">True if bump on quotes; false if bump on correlations</param>
    /// <returns>
    ///   The bump object, or null if the requested tenors and detachments do not exist in the surface.
    /// </returns>
    internal static BaseCorrelationBump Create(
      BaseCorrelationTermStruct bct,
      Dt[] selectTenorDates,
      double[] selectDetachments,
      BumpSize[] trancheBumpByDps,
      BumpSize indexBump,
      bool relative, bool onquotes)
    {
      // Create bumps by detachments
      double[] dps = bct.BaseCorrelations[0].Detachments;
      if (selectDetachments == null || selectDetachments.Length == 0)
      {
        BumpSize[] tmp = new BumpSize[dps.Length];
        for (int i = 0; i < dps.Length; ++i)
          tmp[i] = (trancheBumpByDps.Length == 1 ? trancheBumpByDps[0] : trancheBumpByDps[i]);
        trancheBumpByDps = tmp;
      }
      else
      {
        BumpSize[] tmp = new BumpSize[dps.Length];                                                                                                                                                                                      
        bool not_exist = true;
        for (int i = 0; i < selectDetachments.Length; ++i)
        {
          int pos;
          if ((pos = Array.BinarySearch(dps, selectDetachments[i])) >= 0)
          {
            tmp[pos] = (trancheBumpByDps.Length == 1 ? trancheBumpByDps[0] : trancheBumpByDps[pos]);
            not_exist = false;
          }
        }
        if (not_exist) tmp = null;
        trancheBumpByDps = tmp;
      }

      // Create bumps by tenors
      BumpSize[][] trancheBumps = null;
      if (trancheBumpByDps != null)
      {
        Dt[] tds = bct.Dates;
        trancheBumps = new BumpSize[tds.Length][];
        if (selectTenorDates == null || selectTenorDates.Length == 0)
        {
          for (int i = 0; i < tds.Length; ++i)
            trancheBumps[i] = trancheBumpByDps;
        }
        else
        {
          bool noTrancheBump = true;
          for (int i = 0; i < selectTenorDates.Length; ++i)
          {
            int pos;
            if ((pos = Array.BinarySearch(tds, selectTenorDates[i])) >= 0)
            {
              trancheBumps[pos] = trancheBumpByDps;
              noTrancheBump = false;
            }
          }
          if (noTrancheBump) trancheBumps = null;
        }
      }

      // create bumps for index quotes
      BumpSize[] indexBumps = null;
      if (onquotes)
      {
        if (bct.Calibrator == null)
          throw new System.NullReferenceException("Base correlation calibrator is null");
        if (bct.Calibrator.TrancheQuotes==null)
          throw new System.NullReferenceException("No market quotes provided");
        CheckBounds(trancheBumps, bct.Calibrator.TrancheQuotes);
        if (indexBump != null && bct.Calibrator.IndexTerm != null)
          indexBumps = CreateIndexBumps(selectTenorDates, bct.Calibrator.IndexTerm, indexBump);
      }
      else
        CheckBounds(trancheBumpByDps, bct.MaxCorrelation, bct.MinCorrelation);

      // If nothing to bump, return null
      if (indexBumps == null && trancheBumps == null) return null;

      // Assembly the bump object
      BaseCorrelationBump bcb = new BaseCorrelationBump();
      bcb.trancheBumps_ = trancheBumps;
      bcb.indexBumps_ = indexBumps;
      bcb.target_ = onquotes ? BumpTarget.TrancheAndIndexQuotes : BumpTarget.Correlation;
      bcb.method_ = relative ? BumpMethod.Relative : BumpMethod.Absolute;
      return bcb;
    }

    #region Private Helpers

    private static BumpSize[] CreateIndexBumps(
      Dt[] selectTenorDates, IndexScalingCalibrator index, BumpSize indexBump)
    {
      MarketQuote[] marketQuotes = index.Quotes;
      if (marketQuotes == null) return null;
      Dt[] tds = Array.ConvertAll<CDX, Dt>(index.Indexes,
        delegate(CDX cdx) { return cdx == null ? Dt.Empty : cdx.Maturity; });
      BumpSize[] indexBumps = new BumpSize[tds.Length];
      if (selectTenorDates == null || selectTenorDates.Length == 0)
      {
        for (int i = 0; i < tds.Length; ++i)
          indexBumps[i] = indexBump;
      }
      else
      {
        bool noBump = true;
        for (int i = 0; i < selectTenorDates.Length; ++i)
        {
          int pos;
          if ((pos = Array.BinarySearch(tds, selectTenorDates[i])) >= 0)
          {
            indexBumps[pos] = indexBump;
            noBump = false;
          }
        }
        if (noBump) return null;
      }
      CheckBounds(indexBumps, marketQuotes);
      return indexBumps;
    }

    private static void CheckBounds(BumpSize[] bumps, double max, double min)
    {
      if (bumps == null) return;
      for (int i = 0; i < bumps.Length; ++i)
        if (bumps[i] != null)
        {
          if (Double.IsNaN(bumps[i].UpperBound))
            bumps[i].UpperBound = max;
          if (Double.IsNaN(bumps[i].LowerBound))
            bumps[i].LowerBound = min;
        }
      return;
    }

    private static void CheckBounds(BumpSize[][] bumps, MarketQuote[][] quotes)
    {
      if (bumps == null || quotes == null) return;
      for (int t = 0; t < quotes.Length; ++t)
        CheckBounds(bumps[t], quotes[t]);
      return;
    }

    private static void CheckBounds(BumpSize[] b, MarketQuote[] q)
    {
      if (q == null || b == null) return;
      for (int i = 0; i < q.Length; ++i)
        if (b[i] != null) b[i].CheckBounds(q[i].Type);
      return;
    }

    #region Remove Zero Bumps
    private static BumpSize[][] RemoveZeroBumps(BumpSize[][] trancheBumps)
    {
      if (trancheBumps != null)
      {
        int nonzeroBumps = 0;
        for (int t = 0; t < trancheBumps.Length; ++t)
          if (null != (trancheBumps[t] = RemoveZeroBumps(trancheBumps[t])))
            ++nonzeroBumps;
        if (nonzeroBumps == 0) trancheBumps = null;
      }
      return trancheBumps;
    }

    private static BumpSize[] RemoveZeroBumps(BumpSize[] b)
    {
      if (b != null)
      {
        int count = 0;
        for (int i = 0; i < b.Length; ++i)
          if (b[i] != null)
          {
            if (Math.Abs(b[i].Size) < 1E-12) b[i] = null;
            else ++count;
          }
        if (count == 0) b = null;
      }
      return b;
    }
    #endregion Remove zero Bumps

    #endregion Private Helpers

    #endregion Constructors

    #region Functions doing bumping
    /// <summary>
    ///   Bump an array of base correlation surfaces
    /// </summary>
    /// <param name="surfaces">Surfaces to bump</param>
    /// <param name="bumps">Array of bump objects</param>
    /// <param name="hedgeInfo">List to receive hedge deltas</param>
    /// <returns>Average bump size</returns>
    internal static double Bump(
      BaseCorrelationTermStruct[] surfaces,
      BaseCorrelationBump[] bumps,
      ArrayList hedgeInfo)
    {
      double avg = 0.0;
      if (bumps == null || bumps.Length == 0 || surfaces == null || surfaces.Length == 0)
        return avg;

      if (bumps.Length > 1 && bumps.Length != surfaces.Length)
        throw new ArgumentException("Bumps and compoenents not match");

      for (int i = 0; i < surfaces.Length; ++i)
      {
        BaseCorrelationBump bump = bumps.Length == 1 ? bumps[0] : bumps[i];
        avg += Bump(surfaces[i], bump, hedgeInfo);
      }
      return avg / surfaces.Length;
    }

    #region Private Helpers

    private static double Bump(
      BaseCorrelationTermStruct bct,
      BaseCorrelationBump bump,
      ArrayList hedgeInfo)
    {
      if (bump == null || bct == null)
        return 0.0;

      BaseCorrelationBumpResult result;
      if (bump.Target == BumpTarget.Correlation)
      {
        result = bump.Bump(bct);
        return result.Average;
      }

      BaseCorrelationCalibrator cal = bct.Calibrator;
      MarketQuote[][] savedQuotes =
        (hedgeInfo == null ? null : CloneUtil.Clone(cal.TrancheQuotes));
      double[] origIndexPvs = 
        bump.indexBumps_ == null || cal.IndexTerm == null ? null : cal.IndexTerm.MarketValues;
      result = bump.Bump(bct);
      if (result.Count == 0)
        return 0.0;
      int fromTenorIndex = 0, fromDpIndex = 0;
      if (cal.IndexTerm == null || cal.IndexTerm.IsScalingFactorsReady)
      {
        // index not bumped
        fromTenorIndex = result.FromTenorIndex;
        fromDpIndex = result.FromDpIndex;
      }

      bct.ReFit(fromTenorIndex, fromDpIndex);

      if (hedgeInfo != null && result.BumpedTenorIndices.Length != 0)
      {
        double[,] pvs = bct.PricesAt(cal, savedQuotes,
            result.BumpedTenorIndices, result.BumpDpIndices);
        if (hedgeInfo.Count > 0 && hedgeInfo[0] is DataSet)
        {
          DataSet ds = (DataSet)hedgeInfo[0];
          DataTable table = ds.Tables.Count == 0 ? CreateTable(ds) : ds.Tables[0];
          for (int i = 0; i < result.BumpDpIndices.Length; ++i)
          {
            double d = cal.Detachments[result.BumpDpIndices[i]];
            for (int j = 0; j < result.BumpedTenorIndices.Length; ++j)
            {
              Dt t = cal.TenorDates[result.BumpedTenorIndices[j]];
              AddRow(table, bct.Name, d.ToString(), t, pvs[i, j]);
            }
          }
        }
        else
          hedgeInfo.Add(pvs);
      }

      if (hedgeInfo != null && result.IsIndexBumped && origIndexPvs != null)
      {
        CDX[] cdx = cal.IndexTerm.Indexes;
        double[] indexPvs = cal.IndexTerm.MarketValues;
        for(int i = 0; i < indexPvs.Length;++i)
          indexPvs[i] -= origIndexPvs[i];
        if (hedgeInfo.Count > 0 && hedgeInfo[0] is DataSet)
        {
          DataSet ds = (DataSet)hedgeInfo[0];
          DataTable table = ds.Tables.Count == 0 ? CreateTable(ds) : ds.Tables[0];
          for (int i = 0; i < cdx.Length; ++i)
            if (Math.Abs(indexPvs[i]) > 1E-12)
              AddRow(table, cdx[i].Description, "0", cdx[i].Maturity, indexPvs[i]);
        }
        else
        {
          indexPvs = ArrayUtil.GenerateIf<double>(indexPvs.Length,
            delegate(int i) { return Math.Abs(indexPvs[i]) > 1E-12; },
            delegate(int i) { return indexPvs[i]; });
          if (indexPvs.Length > 0)
            hedgeInfo.Add(indexPvs);
        }
      }

      return result.Average;
    }

    private static void AddRow(DataTable table, string c, string d, Dt t, double pv)
    {
      DataRow row = table.NewRow();
      row["Component"] = c;
      row["Tenor Date"] = t;
      row["Detachment"] = d;
      row["Delta"] = pv * 1000000;
      table.Rows.Add(row);
      return;
    }

    private static DataTable CreateTable(DataSet ds)
    {
      DataTable dataTable = new DataTable("Hedge delta table");
      dataTable.Columns.Add(new DataColumn("Component", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Tenor Date", typeof(Dt)));
      dataTable.Columns.Add(new DataColumn("Detachment", typeof(string)));
      dataTable.Columns.Add(new DataColumn("Delta", typeof(double)));
      ds.Tables.Add(dataTable);
      return ds.Tables[0];
    }

    private BaseCorrelationBumpResult Bump(BaseCorrelationTermStruct bct)
    {
      BaseCorrelationBumpResult result = new BaseCorrelationBumpResult();

      if (bct == null) return result;

      if (target_ == BumpTarget.Correlation)
      {
        if (trancheBumps_ != null)
        {
          // do bumping correlation
          BaseEntity.Toolkit.Base.BaseCorrelation[] bcs = bct.BaseCorrelations;
          if (bcs.Length != trancheBumps_.Length)
            throw new System.ArgumentException(String.Format(
              "Number of bump tenors ({0} and term struct ({1}) not match",
              trancheBumps_.Length, bcs.Length));
          for (int t = 0; t < trancheBumps_.Length; ++t)
            if (trancheBumps_[t] != null)
              BumpCorrelation(trancheBumps_[t], bcs[t].Correlations, result);
        }
        return result;
      }

      // Here we need to bump market quotes
      if (bct.Calibrator == null)
        throw new System.NullReferenceException("Cannot bump quotes without calibrator");

      BaseCorrelationCalibrator cal = bct.Calibrator;
      if (indexBumps_ != null)
      {
        if (cal.IndexTerm == null || cal.IndexTerm.Quotes == null)
          throw new System.NullReferenceException("Index quotes not available.");
        BumpIndex(indexBumps_, cal.IndexTerm.Quotes, result);
        cal.IndexTerm.Reset();
      }
      if (trancheBumps_ != null)
      {
        // do bumping correlation
        MarketQuote[][] quotes = cal.TrancheQuotes;
        if (quotes.Length != trancheBumps_.Length)
          throw new System.ArgumentException(String.Format(
            "Number of bump tenors ({0} and term struct ({1}) not match",
            trancheBumps_.Length, quotes.Length));
        for (int t = 0; t < trancheBumps_.Length; ++t)
          if (BumpTranche(trancheBumps_[t], quotes[t], result))
            result.AddTenor(t);
      }
      return result;
    }

    private void BumpCorrelation(BumpSize[] bumps, double[] corrs,
      BaseCorrelationBumpResult result)
    {
      if (bumps.Length != corrs.Length)
        throw new System.ArgumentException(String.Format(
          "Number of bumps ({0} and correlations ({1}) not match",
          bumps.Length, corrs.Length));
      for (int i = 0; i < bumps.Length;++i)
        if (bumps[i]!=null)
        {
          double q = Bump(bumps[i], corrs[i], QuotingConvention.FlatPrice, method_);
          if (!Double.IsNaN(q))
          {
            double diff = bumps[i].GetDiff(corrs[i], q, QuotingConvention.FlatPrice);
            if (diff != 0.0)
            {
              result.AddBump(diff);
              corrs[i] = q;
            }
          }
        }
      return;
    }

    private void BumpIndex(BumpSize[] bumps, MarketQuote[] quotes,
      BaseCorrelationBumpResult result)
    {
      int count0 = result.Count;
      if (bumps.Length != bumps.Length)
        throw new System.ArgumentException(String.Format(
          "Number of bumps ({0} and index quotes ({1}) not match",
          bumps.Length, quotes.Length));
      for (int i = 0; i < bumps.Length; ++i)
        if (bumps[i]!=null && quotes[i].Type != QuotingConvention.None)
        {
          double q = Bump(bumps[i], quotes[i].Value, quotes[i].Type, method_);
          double diff = bumps[i].GetDiff(quotes[i].Value, q, quotes[i].Type);
          if (diff != 0.0)
          {
            result.AddBump(diff);
            quotes[i].Value = q;
          }
        }
      result.IsIndexBumped = result.Count != count0;
      return;
    }

    private bool BumpTranche(BumpSize[] bumps, MarketQuote[] quotes,
      BaseCorrelationBumpResult result)
    {
      if (bumps == null || quotes == null)
        return false;
      else if (bumps.Length != bumps.Length)
        throw new System.ArgumentException(String.Format(
          "Number of bumps ({0} and tranches ({1}) not match",
          bumps.Length, quotes.Length));
      bool bumped = false;
      for (int i = 0; i < bumps.Length; ++i)
        if (bumps[i] != null && quotes[i].Type != QuotingConvention.None)
        {
          double q = Bump(bumps[i], quotes[i].Value, quotes[i].Type, method_);
          double diff = bumps[i].GetDiff(quotes[i].Value, q, quotes[i].Type);
          if (diff != 0.0)
          {
            result.AddBump(diff);
            result.AddDetachment(i);
            quotes[i].Value = q;
            bumped = true;
          }
        }
      return bumped;
    }

    private static double Bump(BumpSize bump, double orig,
      QuotingConvention type, BumpMethod method)
    {
      return method == BumpMethod.Absolute ? bump.BumpAbsolute(orig, type)
        : bump.BumpRelative(orig, type);
    }

    #endregion Private Helpers

    #endregion Functions doing bumping

    #region Properties

    /// <summary>
    ///   Name
    /// </summary>
    public string Name
    {
      get { return name_; }
      set { name_ = value; }
    }

    /// <summary>
    ///   Bump target (quotes or correlations)
    /// </summary>
    public BumpTarget Target
    {
      get { return target_; }
      set { target_ = value; }
    }

    /// <summary>
    ///   Bump method (relative or absolute)
    /// </summary>
    public BumpMethod Method
    {
      get { return method_; }
      set { method_ = value; }
    }
    /// <summary>
    ///    Index bump sizes
    /// </summary>
    public BumpSize[] IndexBumps
    {
      get { return indexBumps_; }
      set { indexBumps_ = value; }
    }

    /// <summary>
    ///   Tranche bump sizes
    /// </summary>
    public BumpSize[][] TrancheBumps
    {
      get { return trancheBumps_; }
      set { trancheBumps_ = value; }
    }

    #endregion Properties

    #region Data
    private BumpTarget target_;
    private BumpMethod method_;
    private BumpSize[] indexBumps_;
    private BumpSize[][] trancheBumps_;
    private string name_;
    #endregion Data
  }
}
