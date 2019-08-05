/*
 * MoodysRating.cs
 *
 */

using BaseEntity.Shared;

namespace BaseEntity.Risk
{
  /// <summary>
	///   Moodys rating categories
	/// </summary>
  [AlphabeticalOrderEnum]
	public enum MoodysRating
	{
		/// <summary>Not Rated</summary>
		NR,
		/// <summary>Aaa</summary>
		AAA,
		/// <summary>Aa1</summary>
		AA1,
		/// <summary>Aa2</summary>
		AA2,
		/// <summary>Aa3</summary>
		AA3,
		/// <summary>A1</summary>
		A1,
		/// <summary>A2</summary>
		A2,
		/// <summary>A3</summary>
		A3,
		/// <summary>Baa1</summary>
		BAA1,
		/// <summary>Baa2</summary>
		BAA2,
		/// <summary>Baa3</summary>
		BAA3,
		/// <summary>Ba1</summary>
		BA1,
		/// <summary>Ba2</summary>
		BA2,
		/// <summary>Ba3</summary>
		BA3,
		/// <summary>B1</summary>
		B1,
		/// <summary>B2</summary>
		B2,
		/// <summary>B3</summary>
		B3,
		/// <summary>Caa</summary>
		CAA,
    /// <summary>D</summary>
		D,
		/// <summary>Unrated</summary>
		UNR,
	}

} 
