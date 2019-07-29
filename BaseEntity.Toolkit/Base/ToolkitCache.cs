//
//   2015. All rights reserved.
//

using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Products.StandardProductTerms;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  Toolkit caches
  /// </summary>
  public static class ToolkitCache
  {
    /// <summary>
    ///  Standard product cache
    /// </summary>
    public static StandardProductTermsCache StandardProductTermsCache
    {
      get
      {
        if (_standardProductTermsCache != null)
          return _standardProductTermsCache;
        _standardProductTermsCache = new StandardProductTermsCache();
        return _standardProductTermsCache;
      }
    }

    private static StandardProductTermsCache _standardProductTermsCache;
  }
}
