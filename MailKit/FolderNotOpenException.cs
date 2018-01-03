//
// FolderNotOpenException.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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
#if SERIALIZABLE
using System.Runtime.Serialization;
#endif

namespace MailKit {
	/// <summary>
	/// The exception that is thrown when a folder is not open.
	/// </summary>
	/// <remarks>
	/// This exception is thrown when an operation on a folder could not be completed
	/// due to the folder being in a closed state. For example, the
	/// <see cref="IMailFolder.GetMessage(UniqueId,System.Threading.CancellationToken, ITransferProgress)"/>
	/// method will throw a <see cref="FolderNotOpenException"/> if the folder is not
	/// current open.
	/// </remarks>
#if SERIALIZABLE
	[Serializable]
#endif
	public class FolderNotOpenException : InvalidOperationException
	{
#if SERIALIZABLE
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNotOpenException"/> class.
		/// </summary>
		/// <remarks>
		/// Deserializes a <see cref="FolderNotOpenException"/>.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <c>null</c>.
		/// </exception>
		protected FolderNotOpenException (SerializationInfo info, StreamingContext context) : base (info, context)
		{
			var value = info.GetString ("FolderAccess");
			FolderAccess access;

			if (!Enum.TryParse (value, out access))
				FolderAccess = FolderAccess.ReadOnly;
			else
				FolderAccess = access;

			FolderName = info.GetString ("FolderName");
		}
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNotOpenException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="FolderNotOpenException"/>.
		/// </remarks>
		/// <param name="folderName">The folder name.</param>
		/// <param name="access">The minimum folder access required by the operation.</param>
		/// <param name="message">The error message.</param>
		/// <param name="innerException">The inner exception.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="folderName"/> is <c>null</c>.
		/// </exception>
		public FolderNotOpenException (string folderName, FolderAccess access, string message, Exception innerException) : base (message, innerException)
		{
			if (folderName == null)
				throw new ArgumentNullException (nameof (folderName));

			FolderName = folderName;
			FolderAccess = access;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNotOpenException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="FolderNotOpenException"/>.
		/// </remarks>
		/// <param name="folderName">The folder name.</param>
		/// <param name="access">The minimum folder access required by the operation.</param>
		/// <param name="message">The error message.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="folderName"/> is <c>null</c>.
		/// </exception>
		public FolderNotOpenException (string folderName, FolderAccess access, string message) : base (message)
		{
			if (folderName == null)
				throw new ArgumentNullException (nameof (folderName));

			FolderName = folderName;
			FolderAccess = access;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNotOpenException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="FolderNotOpenException"/>.
		/// </remarks>
		/// <param name="folderName">The folder name.</param>
		/// <param name="access">The minimum folder access required by the operation.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="folderName"/> is <c>null</c>.
		/// </exception>
		public FolderNotOpenException (string folderName, FolderAccess access) : this (folderName, access, GetDefaultMessage (access))
		{
		}

		/// <summary>
		/// Get the name of the folder.
		/// </summary>
		/// <remarks>
		/// Gets the name of the folder.
		/// </remarks>
		/// <value>The name of the folder.</value>
		public string FolderName {
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

		static string GetDefaultMessage (FolderAccess access)
		{
			if (access == FolderAccess.ReadWrite)
				return "The folder is not currently open in read-write mode.";

			return "The folder is not currently open.";
		}

#if SERIALIZABLE
		/// <summary>
		/// When overridden in a derived class, sets the <see cref="System.Runtime.Serialization.SerializationInfo"/>
		/// with information about the exception.
		/// </summary>
		/// <remarks>
		/// Serializes the state of the <see cref="FolderNotOpenException"/>.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <c>null</c>.
		/// </exception>
		public override void GetObjectData (SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException (nameof (info));

			info.AddValue ("FolderAccess", FolderAccess.ToString ());
			info.AddValue ("FolderName", FolderName);

			base.GetObjectData (info, context);
		}
#endif
	}
}
