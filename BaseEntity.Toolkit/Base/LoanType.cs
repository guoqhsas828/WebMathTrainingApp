/*
 * LoanType.cs
 *
 *   2008. All rights reserved.
 *
 * Created by rsmulktis on 2/4/2008 8:51:31 AM
 *
 */

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// LoanType enum.
  /// </summary>
  public enum LoanType
  {
    /// <summary>
    /// A loan that can be borrowed, repaid, and reborrowed within a defined period.
    /// </summary>
    Revolver = 0, 

    /// <summary>
    /// A loan that cannot be reborrowed once it is repaid.
    /// </summary>
    Term = 1, 

    /// <summary>
    /// A loan that grants institutions access to large amounts of cash in order to cover possible shortfalls from other debt commitments.  
    /// </summary>
    /// <remarks>As defined by Investopedia.</remarks>
    Swingline = 2
  }
}
