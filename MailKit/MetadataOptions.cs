//
// MetadataOptions.cs
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

namespace MailKit
{
	/// <summary>
	/// A set of options to use when requesting metadata.
	/// </summary>
	/// <remarks>
	/// A set of options to use when requesting metadata.
	/// </remarks>
	public class MetadataOptions
	{
		int depth;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.MetadataOptions"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new set of options to use when requesting metadata.
		/// </remarks>
		public MetadataOptions ()
		{
		}

		/// <summary>
		/// Get or set the depth.
		/// </summary>
		/// <remarks>
		/// <para>When the <see cref="Depth"/> option is specified, it extends the list of metadata tag
		/// values returned by the GetMetadata() call. For each <see cref="MetadataTag"/> specified in the
		/// the GetMetadata() call, the method returns the value of the specified metadata tag (if it exists),
		/// plus all metadata tags below the specified entry up to the specified depth.</para>
		/// <para>Three values are allowed for <see cref="Depth"/>:</para>
		/// <para><c>0</c> - no entries below the specified metadata tag are returned.</para>
		/// <para><c>1</c> - only entries immediately below the specified metadata tag are returned.</para>
		/// <para><see cref="System.Int32.MaxValue"/> - all entries below the specified metadata tag are returned.</para>
		/// <para>Thus, a depth of <c>1</c> for a tag entry of <c>"/a"</c> will match <c>"/a"</c> as well as its children
		/// entries (e.g., <c>"/a/b"</c>), but will not match grandchildren entries (e.g., <c>"/a/b/c"</c>).</para>
		/// <para>If the Depth option is not specified, this is the same as specifying <c>0</c>.</para>
		/// </remarks>
		/// <value>The depth.</value>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="value"/> is out of range.
		/// </exception>
		public int Depth {
			get { return depth; }
			set {
				if (!(value == 0 || value == 1 || value == int.MaxValue))
					throw new ArgumentOutOfRangeException (nameof (value));

				depth = value;
			}
		}

		/// <summary>
		/// Get or set the max size of the metadata tags to request.
		/// </summary>
		/// <remarks>
		/// When specified, the <see cref="MaxSize"/> property is used to filter the metadata tags
		/// returned by the GetMetadata() call to only those with a value shorter than the max size
		/// specified.
		/// </remarks>
		/// <value>The size of the max.</value>
		public uint? MaxSize {
			get; set;
		}

		/// <summary>
		/// Get the length of the longest metadata value.
		/// </summary>
		/// <remarks>
		/// If the <see cref="MaxSize"/> property is specified, once the GetMetadata() call returns,
		/// the <see cref="LongEntries"/> property will be set to the length of the longest metadata
		/// value that exceeded the <see cref="MaxSize"/> limit, otherwise a value of <c>0</c> will
		/// be set.
		/// </remarks>
		/// <value>The length of the longest metadata value that exceeded the max size.</value>
		public uint LongEntries {
			get; set;
		}
	}
}
