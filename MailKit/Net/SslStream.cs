//
// SslStream.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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
using System.IO;
using System.Net.Security;
using System.Security.Authentication.ExtendedProtection;

namespace MailKit.Net
{
	class SslStream : System.Net.Security.SslStream, IChannelBindingContext
	{
		ChannelBinding tlsServerEndPoint;
		ChannelBinding tlsUnique;

		public SslStream (Stream innerStream, bool leaveInnerStreamOpen, RemoteCertificateValidationCallback userCertificateValidationCallback) : base (innerStream, leaveInnerStreamOpen, userCertificateValidationCallback)
		{
		}

		new public Stream InnerStream {
			get { return base.InnerStream; }
		}

		ChannelBinding GetChannelBinding (ChannelBindingKind kind)
		{
			ChannelBinding channelBinding;

			try {
				// Note: Documentation for TransportContext.GetChannelBinding() states that it will return null if the
				// requested channel binding type is not supported, but it also states that it will throw
				// NotSupportedException, so we handle both.
				channelBinding = TransportContext?.GetChannelBinding (kind);
			} catch (NotSupportedException) {
				return null;
			}

			if (channelBinding == null || channelBinding.IsClosed || channelBinding.IsInvalid)
				return null;

			return channelBinding;
		}

		/// <summary>
		/// Try to get a channel-binding token.
		/// </summary>
		/// <remarks>
		/// Tries to get the specified channel-binding.
		/// </remarks>
		/// <param name="kind">The kind of channel-binding desired.</param>
		/// <param name="token">The channel-binding token.</param>
		/// <returns><c>true</c> if the channel-binding token was acquired; otherwise, <c>false</c>.</returns>
		public bool TryGetChannelBindingToken (ChannelBindingKind kind, out byte[] token)
		{
			ChannelBinding channelBinding = null;
			int identifierLength;

			token = null;

			if (kind == ChannelBindingKind.Endpoint) {
				channelBinding = tlsServerEndPoint ??= GetChannelBinding (kind);
				identifierLength = "tls-server-end-point:".Length;
			} else if (kind == ChannelBindingKind.Unique) {
				channelBinding = tlsUnique ??= GetChannelBinding (kind);
				identifierLength = "tls-unique:".Length;
			} else {
				return false;
			}

			if (channelBinding == null || channelBinding.Size <= 32 + identifierLength)
				return false;

			int tokenLength = (channelBinding.Size - 32) - identifierLength;
			token = new byte[tokenLength];

			unsafe {
				byte* inbuf = (byte*) channelBinding.DangerousGetHandle ().ToPointer ();
				byte* inptr = inbuf + 32 + identifierLength;
				byte* inend = inbuf + channelBinding.Size;

				fixed (byte* outbuf = token) {
					byte* outptr = outbuf;

					while (inptr < inend)
						*outptr++ = *inptr++;
				}
			}

			return true;
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				tlsServerEndPoint?.Close ();
				tlsServerEndPoint = null;
				tlsUnique?.Close ();
				tlsUnique = null;
			}

			base.Dispose (disposing);
		}
	}
}
