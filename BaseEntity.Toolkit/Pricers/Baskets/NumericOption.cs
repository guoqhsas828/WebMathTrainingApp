/*
 * NumericOptions.cs
 *
 *  -2008. All rights reserved.
 *
 * $Id$
 *
 */
using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers.Baskets
{
  /// <summary>Numerical utility class for basket pricing</summary>
  /// <exclude />
  public class NumericOption
  {
    /// <summary>Get the default number of quadrature points</summary>
    /// <exclude />
    static public int
    DefaultQuadraturePoints(Copula copula, int basketSize)
    {
      int points;
      switch (copula.CopulaType)
      {
        case CopulaType.Clayton:
          points = 200;
          break;
        case CopulaType.Gumbel:
        case CopulaType.Frank:
          points = 100;
          break;
        case CopulaType.Gauss:
        case CopulaType.NormalInverseGaussian:
        case CopulaType.RandomFactorLoading:
          if (basketSize < 40)
            points = 25;
          else
            points = 25 + (basketSize - 40) / 10;
          break;
        case CopulaType.DoubleT:
          if (basketSize < 40)
            points = 15;
          else
            points = 15 + (basketSize - 40) / 10;
          break;
        case CopulaType.StudentT:
          if (basketSize < 40)
            points = 12;
          else
            points = 12 + (basketSize - 40) / 10;
          break;
        case CopulaType.Poisson:
          points = 1;
          break;
        default:
          throw new ToolkitException("Unknown copula type");
      }

      return points;
    }

    /// <summary>Get the default number of quadrature points</summary>
    /// <exclude />
    static public int
    DefaultQuadraturePointsAdjust(
      double attachment, double detachment)
    {
      int adjust = (int)(30.0 - 500 * Math.Abs(attachment - 0.09)
        - 100 * (detachment - attachment));
      return adjust > 0 ? adjust : 0;
    }

    /// <summary>Get the default number of quadrature points</summary>
    /// <exclude />
    static public int
    DefaultQuadraturePointsAdjust(
      CopulaType copulaType, SyntheticCDO[] cdos)
    {
      int adjust = 0;
      switch (copulaType)
      {
        case CopulaType.Clayton:
        case CopulaType.Gumbel:
        case CopulaType.Frank:
          break;
        default:
          foreach (SyntheticCDO cdo in cdos)
            if (cdo != null)
            {
              int a = DefaultQuadraturePointsAdjust(
                cdo.Attachment, cdo.Detachment);
              if (a > adjust)
                adjust = a;
            }
          break;
      }
      return adjust;
    }

    /// <summary>Get the default number of quadrature points</summary>
    /// <exclude />
    static public int
    DefaultQuadraturePointsCorrAdjust(
      int origPoints, CopulaType copulaType, double corr)
    {
      int points = 0;
      switch (copulaType)
      {
        case CopulaType.Clayton:
        case CopulaType.Gumbel:
        case CopulaType.Frank:
        case CopulaType.DoubleT:
        case CopulaType.StudentT:
          break;
        default:
          points = (int)(550 * corr) - 295;
          break;
      }
      return points < origPoints ? origPoints : points;
    }

    /// <summary>Get the default number of quadrature points</summary>
    /// <exclude />
    static public int
    DefaultQuadraturePointsAdjust(double[] dps)
    {
      int adjust = 0;
      double ap = 0;
      foreach (double dp in dps)
        if (dp > 0 && dp <= 1)
        {
          int a = DefaultQuadraturePointsAdjust(ap, dp);
          if (a > adjust)
            adjust = a;
          ap = dp;
        }
      return adjust;
    }

  }
}
