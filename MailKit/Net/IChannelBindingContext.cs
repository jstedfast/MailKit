﻿//
// IChannelBindingContext.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2025 .NET Foundation and Contributors
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

using System.Security.Authentication.ExtendedProtection;

namespace MailKit.Net {
	/// <summary>
	/// An interface for resources that support acquiring channel-binding tokens.
	/// </summary>
	/// <remarks>
	/// An interface for resources that support acquiring channel-binding tokens.
	/// </remarks>
	interface IChannelBindingContext
	{
		/// <summary>
		/// Try to get a channel-binding token.
		/// </summary>
		/// <remarks>
		/// Tries to get the specified channel-binding token.
		/// </remarks>
		/// <param name="kind">The kind of channel-binding desired.</param>
		/// <param name="token">The channel-binding token.</param>
		/// <returns><see langword="true" /> if the channel-binding token was acquired; otherwise, <see langword="false" />.</returns>
		bool TryGetChannelBindingToken (ChannelBindingKind kind, out byte[] token);

		/// <summary>
		/// Retrive the requested channel binding.
		/// </summary>
		/// <param name="kind">The kind of channel-binding desired.</param>
		/// <returns>The requested channel binding or null if not supported</returns>
		ChannelBinding GetChannelBinding (ChannelBindingKind kind);
	}
}
