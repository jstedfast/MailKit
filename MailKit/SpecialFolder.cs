//
// SpecialFolder.cs
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
	/// An enumeration of special folders.
	/// </summary>
	/// <remarks>
	/// An enumeration of special folders.
	/// </remarks>
	public enum SpecialFolder {
		/// <summary>
		/// The special folder containing an aggregate of all messages.
		/// </summary>
		All,

		/// <summary>
		/// The special folder that contains archived messages.
		/// </summary>
		Archive,

		/// <summary>
		/// The special folder that contains message drafts.
		/// </summary>
		Drafts,

		/// <summary>
		/// The special folder that contains important messages.
		/// </summary>
		Flagged,

		/// <summary>
		/// The special folder that contains spam messages.
		/// </summary>
		Junk,

		/// <summary>
		/// The special folder that contains sent messages.
		/// </summary>
		Sent,

		/// <summary>
		/// The special folder that contains deleted messages.
		/// </summary>
		Trash
	}
}
