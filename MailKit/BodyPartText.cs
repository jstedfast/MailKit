//
// BodyPartText.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2014 Xamarin Inc. (www.xamarin.com)
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

using System.Text;

namespace MailKit {
	/// <summary>
	/// A textual body part.
	/// </summary>
	/// <remarks>
	/// Represents any body part with a media type of "text".
	/// </remarks>
	public class BodyPartText : BodyPartBasic
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.BodyPartText"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="BodyPartText"/>.
		/// </remarks>
		public BodyPartText ()
		{
		}

		/// <summary>
		/// Gets the length of the text, in lines.
		/// </summary>
		/// <remarks>
		/// Gets the length of the text, in lines.
		/// </remarks>
		/// <value>The number of lines.</value>
		public uint Lines {
			get; set;
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
			base.Encode (builder);

			builder.Append (' ');
			Encode (builder, Lines);
		}
	}
}
