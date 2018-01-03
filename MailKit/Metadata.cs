//
// Metadata.cs
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

namespace MailKit {
	/// <summary>
	/// A metadata tag and value.
	/// </summary>
	/// <remarks>
	/// A metadata tag and value.
	/// </remarks>
	public class Metadata
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Metadata"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Metadata"/>.
		/// </remarks>
		/// <param name="tag">The metadata tag.</param>
		/// <param name="value">The meatdata value.</param>
		public Metadata (MetadataTag tag, string value)
		{
			Value = value;
			Tag = tag;
		}

		/// <summary>
		/// Gets the metadata tag.
		/// </summary>
		/// <remarks>
		/// Gets the metadata tag.
		/// </remarks>
		/// <value>The metadata tag.</value>
		public MetadataTag Tag {
			get; private set;
		}

		/// <summary>
		/// Gets the metadata value.
		/// </summary>
		/// <remarks>
		/// Gets the metadata value.
		/// </remarks>
		/// <value>The metadata value.</value>
		public string Value {
			get; private set;
		}
	}
}
