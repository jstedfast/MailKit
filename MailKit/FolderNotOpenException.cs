//
// FolderNotOpenException.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2015 Xamarin Inc. (www.xamarin.com)
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
	/// The exception that is thrown when a folder is not open.
	/// </summary>
	/// <remarks>
	/// This exception is thrown when an operation on a folder could not be completed
	/// due to the folder being in a closed state. For example, the
	/// <see cref="IMailFolder.GetMessage(UniqueId,System.Threading.CancellationToken)"/>
	/// method will throw a <see cref="FolderNotOpenException"/> if the folder is not
	/// current open.
	/// </remarks>
	public class FolderNotOpenException : InvalidOperationException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNotOpenException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="FolderNotOpenException"/>.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="access">The minimum folder access required by the operation.</param>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">The inner exception.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="folder"/> is <c>null</c>.
		/// </exception>
		public FolderNotOpenException (IMailFolder folder, FolderAccess access, string message, Exception innerException) : base (message, innerException)
		{
			if (folder == null)
				throw new ArgumentNullException ("folder");

			FolderAccess = access;
			Folder = folder;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNotOpenException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="FolderNotOpenException"/>.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">The inner exception.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="folder"/> is <c>null</c>.
		/// </exception>
		public FolderNotOpenException (IMailFolder folder, string message, Exception innerException) : this (folder, FolderAccess.ReadOnly, message, innerException)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNotOpenException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="FolderNotOpenException"/>.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="access">The minimum folder access required by the operation.</param>
		/// <param name="message">The error message.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="folder"/> is <c>null</c>.
		/// </exception>
		public FolderNotOpenException (IMailFolder folder, FolderAccess access, string message) : base (message)
		{
			if (folder == null)
				throw new ArgumentNullException ("folder");

			FolderAccess = access;
			Folder = folder;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNotOpenException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="FolderNotOpenException"/>.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="message">The error message.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="folder"/> is <c>null</c>.
		/// </exception>
		public FolderNotOpenException (IMailFolder folder, string message) : this (folder, FolderAccess.ReadOnly, message)
		{
		}

		internal static FolderNotOpenException Create (IMailFolder folder, FolderAccess access)
		{
			string message;

			if (access == FolderAccess.ReadWrite) {
				message = string.Format ("The '{0}' folder is not currently open in read-write mode.", folder.FullName);
			} else {
				message = string.Format ("The '{0}' folder is not currently open.", folder.FullName);
			}

			return new FolderNotOpenException (folder, access, message);
		}

		/// <summary>
		/// Get the folder that the operation could not be completed on.
		/// </summary>
		/// <remarks>
		/// Gets the folder that an operation could not be completed on.
		/// </remarks>
		/// <value>The folder.</value>
		public IMailFolder Folder {
			get; private set;
		}

		/// <summary>
		/// Get the minimum folder access required by the operation.
		/// </summary>
		/// <remarks>
		/// Gets the minimum folder access required by the operation.
		/// </remarks>
		/// <value>The minimum required folder access.</value>
		public FolderAccess FolderAccess {
			get; private set;
		}
	}
}
