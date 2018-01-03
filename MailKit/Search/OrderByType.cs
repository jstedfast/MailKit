//
// OrderByType.cs
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

namespace MailKit.Search {
	/// <summary>
	/// The field to sort by.
	/// </summary>
	/// <remarks>
	/// The field to sort by.
	/// </remarks>
	public enum OrderByType {
		/// <summary>
		/// Sort by the arrival date.
		/// </summary>
		Arrival,

		/// <summary>
		/// Sort by the Cc header.
		/// </summary>
		Cc,

		/// <summary>
		/// Sort by the Date header.
		/// </summary>
		Date,

		/// <summary>
		/// Sort by the Display Name of the From header.
		/// </summary>
		DisplayFrom,

		/// <summary>
		/// Sort by the Display Name of the To header.
		/// </summary>
		DisplayTo,

		/// <summary>
		/// Sort by the From header.
		/// </summary>
		From,

		/// <summary>
		/// Sort by the mod-sequence.
		/// </summary>
		ModSeq,

		/// <summary>
		/// Sort by the message size.
		/// </summary>
		Size,

		/// <summary>
		/// Sort by the message subject.
		/// </summary>
		Subject,

		/// <summary>
		/// Sort by the To header.
		/// </summary>
		To
	}
}
