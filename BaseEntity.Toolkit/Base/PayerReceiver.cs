/*
 * PayerReceiver.cs
 *
 *  -2008. All rights reserved.
 *
 */

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
	///   Indices payer/receiver of the fixed leg (or premium leg for credit products)
	/// </summary>
	public enum PayerReceiver
	{
		/// <summary>Not specifed</summary>
		None,
		/// <summary>Payer</summary>
		Payer,
		/// <summary>Receiver</summary>
		Receiver
	}

}
