using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  /// Class that is used to store the probability distribution 
  /// </summary>
  public class ProbabilityDistribution
  {
    private double[] atoms_;
    private double[] pmf_;
    private double[] cdf_;
    /// <exclude></exclude>
    protected static ILog Log = LogManager.GetLogger(typeof(Distribution));

    #region Constructor
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="atoms">Atoms</param>
    /// <param name="probabilities">Probabilities</param>
    public ProbabilityDistribution(double[] atoms, double[] probabilities)
    {
      Array.Sort(atoms, probabilities);
      atoms_ = (double[])atoms.Clone();
      pmf_ = (double[])probabilities.Clone();
      cdf_ = new double[probabilities.Length];
      cdf_[0] = probabilities[0];
      for (int i = 1; i < cdf_.Length; i++)
        cdf_[i] = cdf_[i - 1] + probabilities[i];
    }
    #endregion

    #region Methods
    private int BinaryBracket(double[] array, double x)
    {
      unchecked
      {
        int n = array.Length - 1;
        if (x <= 0)
          return 0;
        if (x >= 1)
          return n;
        if (x < array[0])
          return -1;
        if (x >= array[n])
          return n;
        int k, kLow = 0;
        int kHi = n;
        while (kHi - kLow > 1)
        {
          k = (kHi + kLow) >> 1;
          if (array[k] > x)
            kHi = k;
          else kLow = k;
        }
        return kLow;
      }
    }

    /// <summary>
    /// Pseudo-inverse cdf of the empirical distribution of the exposure
    /// </summary>
    /// <param name="p">Probability</param>
    /// <returns>x such that P(X less than x) = p</returns>
    public double Inverse(double p)
    {
      if (p < 0 || p > 1)
        Log.ErrorFormat("{0} is not a valid probability", p);
      int idx = BinaryBracket(cdf_, p);
      if (idx < 0)
        return Atoms[0];
      else if (idx == cdf_.Length - 1)
        return Atoms[Atoms.Length - 1];
      else
      {
        double h = (p - cdf_[idx]) / (cdf_[idx + 1] - cdf_[idx]);
        return Atoms[idx + 1] * h + (1 - h) * Atoms[idx];
      }
    }

    /// <summary>
    /// Mean of the distribution
    /// </summary>
    /// <returns>Mean</returns>
    public double Mean()
    {
      double mean = 0.0;
      for (int i = 0; i < atoms_.Length; i++)
        mean += atoms_[i] * pmf_[i];
      return mean;
    }

    /// <summary>
    /// Second moment of the distribution
    /// </summary>
    /// <returns>Second moment</returns>
    public double SecondMoment()
    {
      double v = 0.0;
      for (int i = 0; i < atoms_.Length; i++)
        v += atoms_[i] * atoms_[i] * pmf_[i];
      return v;
    }
    #endregion

    #region Properties
    /// <summary>
    /// Probability mass function
    /// </summary>
    public double[] Pmf
    {
      get { return pmf_; }
    }
    /// <summary>
    /// Atoms
    /// </summary>
    public double[] Atoms
    {
      get { return atoms_; }
    }
    #endregion
  }
}
