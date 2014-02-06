//
// BodyPartMultipart.cs
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

using System;

using MimeKit;

namespace MailKit {
	/// <summary>
	/// A multipart body part.
	/// </summary>
	public class BodyPartMultipart : BodyPart
	{
		internal BodyPartMultipart ()
		{
			BodyParts = new BodyPartCollection ();
		}

		/// <summary>
		/// Gets the child body parts.
		/// </summary>
		/// <value>The child body parts.</value>
		public BodyPartCollection BodyParts {
			get; private set;
		}

		/// <summary>
		/// Gets the Content-Disposition of the body part, if available.
		/// </summary>
		/// <value>The content disposition.</value>
		public ContentDisposition ContentDisposition {
			get; internal set;
		}

		/// <summary>
		/// Gets the Content-Language of the body part, if available.
		/// </summary>
		/// <value>The content language.</value>
		public string[] ContentLanguage {
			get; internal set;
		}

		/// <summary>
		/// Gets the Content-Location of the body part, if available.
		/// </summary>
		/// <value>The content location.</value>
		public string ContentLocation {
			get; internal set;
		}
	}
}
