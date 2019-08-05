//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Models
{


  [TestFixture]
  public class TestCholesky
  {
    /// <summary>
    /// Calculate the correlation matrix.
    /// </summary>
    /// <param name="factors">Factorloading matrix</param>
    /// <returns>Correlation matrix</returns>
    /// <remarks>
    /// Let F denote the factor loading matrix. The correlation matrix C = F * F^T. 
    /// </remarks>
    private double[,] calculate_corr(double[,] factors)
    {
      int n = factors.GetLength(0);
      double[,] corr = new double[n,n];
      // calculate the correlations
      for (int i = 0; i < n; ++i)
      {
        for (int j = 0; j <= i; ++j)
        {
          double cij = 0;
          for (int k = 0; k < n; ++k)
            cij += factors[i, k] * factors[j, k];
          corr[i, j] = corr[j, i] = cij;
        }
      }
      return corr;
    }

    /// <summary>
    /// Calculate the sum of square errors between matrix a and b.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns>
    /// The sum of square errors.
    /// </returns>
    private double SumSquareErrors(double[,] a, double[,] b)
    {
      if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1))
        throw new ArgumentException("The two matrix don't have the same dimension.");
      double sumsq = 0;
      for (int i = 0, n = a.GetLength(0); i < n; ++i)
        for (int j = 0; j < i; ++j)
          sumsq += (a[i, j] - b[i, j]) * (a[i, j] - b[i, j]);
      return sumsq;
    }


    [TestCase(0)]  
    [TestCase(1)]   
    [TestCase(2)]  
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)] 
    public void GetCorrelationFromOutputFactors(int caseNo)
    {
      double torrence = 1E-9;
      var caseData = _data[caseNo];

      // For simplicity, we require the input SwapRateFactors is a square matrix. 
      Assert.AreEqual(caseData.SwapRateFactors.GetLength(0),
        caseData.SwapRateFactors.GetLength(1), 0,
        "The input SwapRateFactors should be a square matrix"); 

      // The correlation matrix is n by n.
      int n = caseData.SwapRateFactors.GetLength(0);     

      var systemicCount = caseData.systemicCount;
      var expectCorrelation = calculate_corr(caseData.SwapRateFactors);
      var factors1 = (double[,])expectCorrelation.Clone();
      double[,] Correlation1 = new double[n, n];

      // Apply Cholesky decomposition and NLS on factors1, which will be overwritten with row of factors.
      BaseEntity.Toolkit.Models.Simulations.CalibrationUtils.FactorizeCorrelationMatrix(factors1, systemicCount, false);

      // Calculate the new correlation matrix from the factors1.
      Correlation1 = calculate_corr(factors1);

      // Calculate the sum of square errors between Correlation1 and expectCorrelation.
      double sse1 = SumSquareErrors(Correlation1, expectCorrelation);

      for(int i=0; i<n; i++)
       Assert.AreEqual(1.0, Correlation1[i,i],torrence,"The diagonal element in Correlation1 is not 1.");

      if (systemicCount <= 0 || systemicCount >= n) 
      {
        // In this case systemicCount = n.
        // we only apply Cholesky decomposition, which will recovery the original correlations.
        Assert.AreEqual(sse1, 0, torrence,
          "The factors didn't recover the original correlations");
      }
      else
      {
        // In this case, the systemicCount < n. We run minimization after Cholesky decomposition.
        // The optimization should have smaller error.
        var factors2 = (double[,])expectCorrelation.Clone();
        double[,] Correlation2 = new double[n, n];

        // Only apply Cholesky decomposition and truncation on factors2, which will be overwritten with row of factors.
        BaseEntity.Toolkit.Models.Simulations.CalibrationUtils.FactorizeCorrelationMatrix(factors2, systemicCount, true);

        // Calculate the new correlation matrix from the factors2.
        Correlation2 = calculate_corr(factors2);

        // Calculate the sum of square errors between Correlation2 and expectCorrelation.
        double sse2 = SumSquareErrors(Correlation2, expectCorrelation);

        for (int i = 0; i < n; i++)
          Assert.AreEqual(1.0, Correlation2[i, i], torrence, "The diagonal element in Correlation2 is not 1.");

        // Claim sse2 > sse1.
        Assert.Greater(sse2, sse1, "optimization didn't have smaller error.");
      }     
    }

    #region data

    private class CaseData
    {
      public double[,] SwapRateFactors;
      public int systemicCount;
    }

    private static readonly CaseData[] _data = new[]
    {
      new CaseData
      {
        SwapRateFactors = new [,]
        {
          {1.0,0.0,0.0},
          {0.8,0.6,0.0},
          {0.36,0.48,0.8}
        },
        systemicCount = 0 //cutoff = rank
      },



      new CaseData
      {
        SwapRateFactors = new double[,]
        {
          {1.0, 0.0, 0.0},
          {0.8, 0.6, 0.0},
          {0.6, 0.8, 0.0}
        },
        systemicCount = 0 //cutoff = rank
      },

       new CaseData
      {
        SwapRateFactors = new double[,]
         {
      { 1.0, 0.0, 0.0, 0.0 },
      { 0.8, 0.6, 0.0, 0.0 },
      { 0.80, 0.48, 0.36, 0.0 },
      { 0.36, 0.48, 0.80, 0.0 }
    },
        systemicCount = 0 //cutoff = rank
      }, 


    new CaseData
      {
        SwapRateFactors = new double[,]
         {
      { 1.0, 0.0, 0.0, 0.0 },
      { 0.8, 0.0, 0.0, 0.6 },
      { 0.80, 0.48, 0.36, 0.0 },
      { 0.36, 0.48, 0.0, 0.80 }
    },
        systemicCount = 0 //cutoff = rank
      },

   new CaseData
      {
        SwapRateFactors = new[,]
        {
          {1.0, 0.0, 0.0},
          {0.8, 0.6, 0.0},
          {0.36, 0.48, 0.8}
        },
        
        systemicCount = 2 // cutoff = 2
      },

      
      new CaseData
      {
        SwapRateFactors = new double[,]
        {
          {1.0, 0.0, 0.0, 0.0},
          {0.8, 0.6, 0.0, 0.0},
          {0.80, 0.48, 0.36, 0.0},
          {0.36, 0.48, 0.80, 0.0}
        },
        
        systemicCount = 2 //cutoff = 2
      },

      
      new CaseData
      {
        SwapRateFactors = new double[,]
        {
          {1.0, 0.0, 0.0, 0.0},
          {0.8, 0.0, 0.0, 0.6},
          {0.80, 0.48, 0.36, 0.0},
          {0.36, 0.48, 0.0, 0.80}
        },
        
        systemicCount = 3 // cutoff = 3
      }


    };

    #endregion data
  }

  
}
