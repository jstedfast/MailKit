//
// BodyPartMultipart.cs
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
using System.Text;

using MimeKit;

namespace MailKit {
	/// <summary>
	/// A multipart body part.
	/// </summary>
	/// <remarks>
	/// A multipart body part.
	/// </remarks>
	public class BodyPartMultipart : BodyPart
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.BodyPartMultipart"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="BodyPartMultipart"/>.
		/// </remarks>
		public BodyPartMultipart ()
		{
			BodyParts = new BodyPartCollection ();
		}

		/// <summary>
		/// Gets the child body parts.
		/// </summary>
		/// <remarks>
		/// Gets the child body parts.
		/// </remarks>
		/// <value>The child body parts.</value>
		public BodyPartCollection BodyParts {
			get; private set;
		}

		/// <summary>
		/// Gets the Content-Disposition of the body part, if available.
		/// </summary>
		/// <remarks>
		/// Gets the Content-Disposition of the body part, if available.
		/// </remarks>
		/// <value>The content disposition.</value>
		public ContentDisposition ContentDisposition {
			get; set;
		}

		/// <summary>
		/// Gets the Content-Language of the body part, if available.
		/// </summary>
		/// <remarks>
		/// Gets the Content-Language of the body part, if available.
		/// </remarks>
		/// <value>The content language.</value>
		public string[] ContentLanguage {
			get; set;
		}

		/// <summary>
		/// Gets the Content-Location of the body part, if available.
		/// </summary>
		/// <remarks>
		/// Gets the Content-Location of the body part, if available.
		/// </remarks>
		/// <value>The content location.</value>
		public Uri ContentLocation {
			get; set;
		}

		/// <summary>
		/// Dispatches to the specific visit method for this MIME body part.
		/// </summary>
		/// <remarks>
		/// This default implementation for <see cref="MailKit.BodyPart"/> nodes
		/// calls <see cref="MailKit.BodyPartVisitor.VisitBodyPart"/>. Override this
		/// method to call into a more specific method on a derived visitor class
		/// of the <see cref="MailKit.BodyPartVisitor"/> class. However, it should still
		/// support unknown visitors by calling
		/// <see cref="MailKit.BodyPartVisitor.VisitBodyPart"/>.
		/// </remarks>
		/// <param name="visitor">The visitor.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="visitor"/> is <c>null</c>.
		/// </exception>
		public override void Accept (BodyPartVisitor visitor)
		{
			if (visitor == null)
				throw new ArgumentNullException (nameof (visitor));

			visitor.VisitBodyPartMultipart (this);
		}

		/// <summary>
		/// Encodes the <see cref="BodyPart"/> into the <see cref="System.Text.StringBuilder"/>.
		/// </summary>
		/// <remarks>
		/// Encodes the <see cref="BodyPart"/> into the <see cref="System.Text.StringBuilder"/>.
		/// </remarks>
		/// <param name="builder">The string builder.</param>
		protected override void Encode (StringBuilder builder)
		{
			Encode (builder, BodyParts);
			builder.Append (' ');
			Encode (builder, ContentType.MediaSubtype);
			builder.Append (' ');
			Encode (builder, ContentType.Parameters);
			builder.Append (' ');
			Encode (builder, ContentDisposition);
			builder.Append (' ');
			Encode (builder, ContentLanguage);
			builder.Append (' ');
			Encode (builder, ContentLocation);
		}
	}
}
