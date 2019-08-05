/*
 * PublicInfoSource.cs
 *
 */

namespace BaseEntity.Risk
{

  /// <summary>
	///   ISDA99 CDS Public Info Source
	/// </summary>
	///
	/// <remarks>
	///   Section 3.7. Public Source. "Public Source" means each source of
	///   Publicly Available Information specified as such in the related
	///   Confirmation (or, if a source is not so specified, each of Bloomberg
	///   Service, Dow Jones Telerate Service, Reuter Monitor Money Rates Services,
	///   Dow Jones News Wire, Wall Street Journal, New York Times, Nihon Keizai
	///   Shinbun and Financial Times (and successor publications which sources
	///   may be referred to collectively in a Confirmation as the "Standard
	///   Public Sources").
	/// </remarks>
  ///
	public enum PublicInfoSource
	{
		/// <summary>
    ///  None required
    /// </summary>
		None,

		/// <summary>
    ///  Standard sources
    /// </summary>
		Standard,

		/// <summary>
    ///  Other specified source
    /// </summary>
		Other

  } // enum PublicInfoSource
} 
