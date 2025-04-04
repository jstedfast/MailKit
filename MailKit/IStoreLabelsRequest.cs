﻿//
// IStoreLabelsRequest.cs
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

using System.Collections.Generic;

namespace MailKit {
	/// <summary>
	/// A request for storing GMail-style labels for a message.
	/// </summary>
	/// <remarks>
	/// A request for storing GMail-style labels for a message.
	/// </remarks>
	public interface IStoreLabelsRequest : IStoreRequest
	{
		/// <summary>
		/// Get the store action to perform.
		/// </summary>
		/// <remarks>
		/// Gets the store action to perform.
		/// </remarks>
		/// <value>The store action.</value>
		StoreAction Action { get; }

		/// <summary>
		/// Get the GMail-style labels that should be added, removed, or set.
		/// </summary>
		/// <remarks>
		/// Gets the GMail-style labels that should be added, removed, or set.
		/// </remarks>
		/// <value>The GMail-style labels.</value>
		ISet<string> Labels { get; }

		/// <summary>
		/// Get or set whether the store operation should run silently.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets whether the store operation should run silently.</para>
		/// <para>Normally, when flags or keywords are changed on a message, a <see cref="IMailFolder.MessageLabelsChanged"/> event is emitted.
		/// By setting <see cref="Silent"/> to <see langword="true" />, this event will not be emitted as a result of this store operation.</para>
		/// </remarks>
		/// <value><see langword="true" /> if the store operation should run silently (not emitting events for label changes); otherwise, <see langword="false" />.</value>
		bool Silent { get; set; }
	}
}
