//
// ImapFolderInfo.cs
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

namespace MailKit.Net.Imap {
	/// <summary>
	/// Constructor arguments for <see cref="ImapFolder"/>.
	/// </summary>
	/// <remarks>
	/// Constructor arguments for <see cref="ImapFolder"/>.
	/// </remarks>
	public sealed class ImapFolderConstructorArgs
	{
		internal readonly string EncodedName;
		internal readonly ImapEngine Engine;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapFolderConstructorArgs"/> class.
		/// </summary>
		/// <param name="engine">The IMAP command engine.</param>
		/// <param name="encodedName">The encoded name.</param>
		/// <param name="attributes">The attributes.</param>
		/// <param name="delim">The directory separator.</param>
		internal ImapFolderConstructorArgs (ImapEngine engine, string encodedName, FolderAttributes attributes, char delim)
		{
			FullName = engine.DecodeMailboxName (encodedName);
			Name = GetBaseName (FullName, delim);
			DirectorySeparator = delim;
			EncodedName = encodedName;
			Attributes = attributes;
			Engine = engine;
		}

		ImapFolderConstructorArgs ()
		{
		}

		/// <summary>
		/// Get the folder attributes.
		/// </summary>
		/// <remarks>
		/// Gets the folder attributes.
		/// </remarks>
		/// <value>The folder attributes.</value>
		public FolderAttributes Attributes {
			get; private set;
		}

		/// <summary>
		/// Get the directory separator.
		/// </summary>
		/// <remarks>
		/// Gets the directory separator.
		/// </remarks>
		/// <value>The directory separator.</value>
		public char DirectorySeparator {
			get; private set;
		}

		/// <summary>
		/// Get the full name of the folder.
		/// </summary>
		/// <remarks>
		/// This is the equivalent of the full path of a file on a file system.
		/// </remarks>
		/// <value>The full name of the folder.</value>
		public string FullName {
			get; private set;
		}

		/// <summary>
		/// Get the name of the folder.
		/// </summary>
		/// <remarks>
		/// This is the equivalent of the file name of a file on the file system.
		/// </remarks>
		/// <value>The name of the folder.</value>
		public string Name {
			get; private set;
		}

		static string GetBaseName (string fullName, char delim)
		{
			var names = fullName.Split (new [] { delim }, StringSplitOptions.RemoveEmptyEntries);

			return names.Length > 0 ? names[names.Length - 1] : fullName;
		}
	}
}
