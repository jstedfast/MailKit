//
// IReplaceRequest.cs
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
using System.Collections.Generic;

using MimeKit;

namespace MailKit {
	/// <summary>
	/// A request for replacing a message in a folder.
	/// </summary>
	/// <remarks>
	/// A request for replacing a message in a folder.
	/// </remarks>
	public interface IReplaceRequest : IAppendRequest
	{
		/// <summary>
		/// Get or set the folder where the replacement message should be appended.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets the folder where the replacement message should be appended.</para>
		/// <para>If no destination folder is specified, then the replacement message will be
		/// appended to the original folder.</para>
		/// </remarks>
		/// <value>The destination folder.</value>
		IMailFolder Destination { get; set; }
	}
}
