/*
 * IProgress.cs
 *
 */

namespace BaseEntity.Risk
{
	/// <summary>
	///   Interface to allow low level routines to support displaying a status to the user.
  /// </summary>
  /// 
  /// <remarks>
  ///   <para>Implementations of this interface can be passed into routines where a GUI
  ///   can provide a GUI implementation and a batch process can provide a console or null
  ///   implementation.</para>
  /// </remarks>
	///
	public interface IProgress
	{
		/// <summary>
		///   Consumer of this interface will use this to set the status text
		/// </summary>
		string StatusText { set;}

		/// <summary>
		///   Consumer of this interface will use this to set the error text
		/// </summary>
		string ErrorText { set;}

		/// <summary>
		///   Consumer of this interface will use this to determine if the user is trying to cancel the process
		/// </summary>
		bool IsCancelled { get;}

		/// <summary>
		///   Consumer of this interface will use this to notify when percent complete has changed
		/// </summary>
		/// <param name="numCompleted">number of items completed so far</param>
		/// <param name="total">total number of items being processed</param>
		void UpdatePercentComplete(long numCompleted, long total);

		/// <summary>
		///   If consumer is running Async processes (eg Thread) this is called to signal completion
		/// </summary>
		void Completed();

	} // interface IProgress
}  
