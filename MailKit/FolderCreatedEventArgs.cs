//
// FolderCreatedEventArgs.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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

namespace MailKit {
	/// <summary>
	/// Event args used when a <see cref="IMailFolder"/> is created.
	/// </summary>
	/// <remarks>
	/// Event args used when a <see cref="IMailFolder"/> is created.
	/// </remarks>
	public class FolderCreatedEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.FolderCreatedEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="FolderCreatedEventArgs"/>.
		/// </remarks>
		/// <param name="folder">The newly created folder.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="folder"/> is <c>null</c>.
		/// </exception>
		public FolderCreatedEventArgs (IMailFolder folder)
		{
			if (folder == null)
				throw new ArgumentNullException (nameof (folder));

			Folder = folder;
		}

		/// <summary>
		/// Get the folder that was just created.
		/// </summary>
		/// <remarks>
		/// Gets the folder that was just created.
		/// </remarks>
		/// <value>The folder.</value>
		public IMailFolder Folder {
			get; private set;
		}
	}
}
