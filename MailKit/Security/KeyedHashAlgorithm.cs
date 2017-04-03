//
// KeyedHashAlgorithm.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2017 Xamarin Inc. (www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using Windows.Storage.Streams;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;

namespace MailKit.Security {
	/// <summary>
	/// A keyed hash algorithm.
	/// </summary>
	/// <remarks>
	/// A keyed hash algorithm.
	/// </remarks>
	public abstract class KeyedHashAlgorithm : IDisposable
	{
		CryptographicHash hmac;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.KeyedHashAlgorithm"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new keyed hash algorithm context.
		/// </remarks>
		/// <param name="algorithm">The MAC algorithm name.</param>
		/// <param name="key">The secret key.</param>
		protected KeyedHashAlgorithm (string algorithm, byte[] key)
		{
			var mac = MacAlgorithmProvider.OpenAlgorithm (algorithm);
			var buf = CryptographicBuffer.CreateFromByteArray (key);
			hmac = mac.CreateHash (buf);
		}

		/// <summary>
		/// Computes the hash code for the buffer.
		/// </summary>
		/// <remarks>
		/// Computes the hash code for the buffer.
		/// </remarks>
		/// <returns>The computed hash code.</returns>
		/// <param name="buffer">The buffer.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="buffer"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The keyed hash algorithm context has been disposed.
		/// </exception>
		public byte[] ComputeHash (byte[] buffer)
		{
			if (buffer == null)
				throw new ArgumentNullException ("data");

			hmac.Append (CryptographicBuffer.CreateFromByteArray (buffer));
			var value = hmac.GetValueAndReset ();
			byte[] hash;

			CryptographicBuffer.CopyToByteArray (value, out hash);

			return hash;
		}

		/// <summary>
		/// Releases all resource used by the <see cref="MailKit.Security.KeyedHashAlgorithm"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose()"/> when you are finished using the <see cref="MailKit.Security.KeyedHashAlgorithm"/>. The
		/// <see cref="Dispose()"/> method leaves the <see cref="MailKit.Security.KeyedHashAlgorithm"/> in an unusable state. After calling
		/// <see cref="Dispose()"/>, you must release all references to the <see cref="MailKit.Security.KeyedHashAlgorithm"/> so the
		/// garbage collector can reclaim the memory that the <see cref="MailKit.Security.KeyedHashAlgorithm"/> was occupying.</remarks>
		public void Dispose ()
		{
		}
	}

	/// <summary>
	/// The HMAC SHA-1 algorithm.
	/// </summary>
	/// <remarks>
	/// The HMAC SHA-1 algorithm.
	/// </remarks>
	public class HMACSHA1 : KeyedHashAlgorithm
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.HMACSHA1"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new HMAC SHA-1 context.
		/// </remarks>
		/// <param name="key">The secret key.</param>
		public HMACSHA1 (byte[] key) : base (MacAlgorithmNames.HmacSha1, key)
		{
		}
	}

	/// <summary>
	/// The HMAC SHA-256 algorithm.
	/// </summary>
	/// <remarks>
	/// The HMAC SHA-256 algorithm.
	/// </remarks>
	public class HMACSHA256 : KeyedHashAlgorithm
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.HMACSHA256"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new HMAC SHA-256 context.
		/// </remarks>
		/// <param name="key">The secret key.</param>
		public HMACSHA256 (byte[] key) : base (MacAlgorithmNames.HmacSha256, key)
		{
		}
	}
}
