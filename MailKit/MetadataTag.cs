//
// MetadataTag.cs
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

namespace MailKit {
	/// <summary>
	/// A metadata tag.
	/// </summary>
	/// <remarks>
	/// A metadata tag.
	/// </remarks>
	public struct MetadataTag
	{
		/// <summary>
		/// Indicates a method for contacting the server administrator.
		/// </summary>
		/// <remarks>
		/// Used to get the contact information of the administrator on a
		/// <see cref="IMailStore"/>.
		/// </remarks>
		public static readonly MetadataTag SharedAdmin = new MetadataTag ("/shared/admin");

		/// <summary>
		/// Indicates a private comment.
		/// </summary>
		/// <remarks>
		/// Used to get or set a private comment on a <see cref="IMailFolder"/>.
		/// </remarks>
		public static readonly MetadataTag PrivateComment = new MetadataTag ("/private/comment");

		/// <summary>
		/// Indicates a shared comment.
		/// </summary>
		/// <remarks>
		/// Used to get or set a shared comment on a <see cref="IMailStore"/>
		/// or <see cref="IMailFolder"/>.
		/// </remarks>
		public static readonly MetadataTag SharedComment = new MetadataTag ("/shared/comment");

		/// <summary>
		/// Indicates a method for specifying the special use for a particular folder.
		/// </summary>
		/// <remarks>
		/// Used to get or set the special use of a <see cref="IMailFolder"/>.
		/// </remarks>
		public static readonly MetadataTag PrivateSpecialUse = new MetadataTag ("/private/specialuse");

		readonly string id;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MetadataTag"/> struct.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MetadataTag"/>.
		/// </remarks>
		/// <param name="id">The metadata tag identifier.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="id"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="id"/> is an empty string.
		/// </exception>
		public MetadataTag (string id)
		{
			if (id == null)
				throw new ArgumentNullException (nameof (id));

			if (id.Length == 0)
				throw new ArgumentException ("A metadata tag identifier cannot be empty.");

			this.id = id;
		}

		/// <summary>
		/// Gets the metadata tag identifier.
		/// </summary>
		/// <remarks>
		/// Gets the metadata tag identifier.
		/// </remarks>
		/// <value>The metadata tag identifier.</value>
		public string Id {
			get { return id; }
		}

		/// <summary>
		/// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.MetadataTag"/>.
		/// </summary>
		/// <remarks>
		/// Determines whether the specified <see cref="System.Object"/> is equal to the current <see cref="MailKit.MetadataTag"/>.
		/// </remarks>
		/// <param name="obj">The <see cref="System.Object"/> to compare with the current <see cref="MailKit.MetadataTag"/>.</param>
		/// <returns><c>true</c> if the specified <see cref="System.Object"/> is equal to the current
		/// <see cref="MailKit.MetadataTag"/>; otherwise, <c>false</c>.</returns>
		public override bool Equals (object obj)
		{
			return Id.Equals (obj);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="MailKit.MetadataTag"/> object.
		/// </summary>
		/// <remarks>
		/// Serves as a hash function for a <see cref="MailKit.MetadataTag"/> object.
		/// </remarks>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode ()
		{
			return Id.GetHashCode ();
		}

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.MetadataTag"/>.
		/// </summary>
		/// <remarks>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.MetadataTag"/>.
		/// </remarks>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="MailKit.MetadataTag"/>.</returns>
		public override string ToString ()
		{
			return Id;
		}

		internal static MetadataTag Create (string id)
		{
			switch (id) {
			case "/shared/admin":       return SharedAdmin;
			case "/private/comment":    return PrivateComment;
			case "/shared/comment":     return SharedComment;
			case "/private/specialuse": return PrivateSpecialUse;
			default: return new MetadataTag (id);
			}
		}
	}
}
