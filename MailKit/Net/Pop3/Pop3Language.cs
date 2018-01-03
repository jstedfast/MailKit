//
// Pop3Language.cs
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

namespace MailKit.Net.Pop3 {
	/// <summary>
	/// A POP3 language.
	/// </summary>
	/// <remarks>
	/// A POP3 language.
	/// </remarks>
	public class Pop3Language
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3Language"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Pop3Language"/>.
		/// </remarks>
		internal Pop3Language (string lang, string desc)
		{
			Language = lang;
			Description = desc;
		}

		/// <summary>
		/// Get the language code.
		/// </summary>
		/// <remarks>
		/// Gets the language code. This is the value that should be given to
		/// <see cref="Pop3Client.SetLanguage(string,System.Threading.CancellationToken)"/>.
		/// </remarks>
		/// <value>The language.</value>
		public string Language {
			get; private set;
		}

		/// <summary>
		/// Get the description.
		/// </summary>
		/// <remarks>
		/// Gets the description.
		/// </remarks>
		/// <value>The description.</value>
		public string Description {
			get; private set;
		}
	}
}
