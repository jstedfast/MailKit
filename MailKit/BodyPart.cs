//
// BodyPart.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Jeffrey Stedfast
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

using MimeKit;

namespace MailKit {
	/// <summary>
	/// An abstract body part of a message.
	/// </summary>
	/// <remarks>
	/// Each body part will actually be a <see cref="BodyPartBasic"/>,
	/// <see cref="BodyPartText"/>, <see cref="BodyPartMessage"/>, or
	/// <see cref="BodyPartMultipart"/>.
	/// </remarks>
	public abstract class BodyPart
	{
		/// <summary>
		/// Gets the Content-Type of the body part.
		/// </summary>
		/// <value>The content type.</value>
		public ContentType ContentType {
			get; internal set;
		}

		/// <summary>
		/// Gets the part specifier.
		/// </summary>
		/// <value>The part specifier.</value>
		public string PartSpecifier {
			get; internal set;
		}
	}
}
