//
// FolderNotFoundException.cs
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
using System.Security;
using System.Runtime.Serialization;
#endif

namespace MailKit {
	/// <summary>
	/// The exception that is thrown when a folder could not be found.
	/// </summary>
	/// <remarks>
	/// This exception is thrown by <see cref="IMailFolder.GetSubfolder(string,System.Threading.CancellationToken)"/>.
	/// </remarks>
#if SERIALIZABLE
	[Serializable]
#endif
	public class FolderNotFoundException : Exception
	{
#if SERIALIZABLE
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNotFoundException"/> class.
		/// </summary>
		/// <remarks>
		/// Deserializes a <see cref="FolderNotFoundException"/>.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <c>null</c>.
		/// </exception>
		protected FolderNotFoundException (SerializationInfo info, StreamingContext context) : base (info, context)
		{
			FolderName = info.GetString ("FolderName");
		}
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNotFoundException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="FolderNotFoundException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="folderName">The name of the folder.</param>
		/// <param name="innerException">The inner exception.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="folderName"/> is <c>null</c>.
		/// </exception>
		public FolderNotFoundException (string message, string folderName, Exception innerException) : base (message, innerException)
		{
			if (folderName == null)
				throw new ArgumentNullException (nameof (folderName));

			FolderName = folderName;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNotFoundException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="FolderNotFoundException"/>.
		/// </remarks>
		/// <param name="message">The error message.</param>
		/// <param name="folderName">The name of the folder.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="folderName"/> is <c>null</c>.
		/// </exception>
		public FolderNotFoundException (string message, string folderName) : base (message)
		{
			if (folderName == null)
				throw new ArgumentNullException (nameof (folderName));

			FolderName = folderName;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNotFoundException"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="FolderNotFoundException"/>.
		/// </remarks>
		/// <param name="folderName">The name of the folder.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="folderName"/> is <c>null</c>.
		/// </exception>
		public FolderNotFoundException (string folderName) : this ("The requested folder could not be found.", folderName)
		{
		}

		/// <summary>
		/// Gets the name of the folder that could not be found.
		/// </summary>
		/// <remarks>
		/// Gets the name of the folder that could not be found.
		/// </remarks>
		/// <value>The name of the folder.</value>
		public string FolderName {
			get; private set;
		}

#if SERIALIZABLE
		/// <summary>
		/// When overridden in a derived class, sets the <see cref="System.Runtime.Serialization.SerializationInfo"/>
		/// with information about the exception.
		/// </summary>
		/// <remarks>
		/// Serializes the state of the <see cref="FolderNotFoundException"/>.
		/// </remarks>
		/// <param name="info">The serialization info.</param>
		/// <param name="context">The streaming context.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="info"/> is <c>null</c>.
		/// </exception>
		[SecurityCritical]
		public override void GetObjectData (SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException (nameof (info));

			info.AddValue ("FolderName", FolderName);

			base.GetObjectData (info, context);
		}
#endif
	}
}
