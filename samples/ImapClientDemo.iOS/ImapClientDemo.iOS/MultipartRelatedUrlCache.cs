//
// MultipartRelatedUrlCache.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2015 Xamarin Inc. (www.xamarin.com)
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
using System.IO;

using Foundation;

using MimeKit;

namespace ImapClientDemo.iOS {
	public class MultipartRelatedUrlCache : NSUrlCache
	{
		readonly MultipartRelated related;

		public MultipartRelatedUrlCache (MultipartRelated related)
		{
			this.related = related;
		}

		public override NSCachedUrlResponse CachedResponseForRequest (NSUrlRequest request)
		{
			var uri = (Uri) request.Url;
			int index;

			if ((index = related.IndexOf (uri)) != -1) {
				var part = related[index] as MimePart;

				if (part != null) {
					var mimeType = part.ContentType.MimeType;
					var charset = part.ContentType.Charset;
					NSUrlResponse response;
					NSData data;

					using (var content = part.Content.Open ())
						data = NSData.FromStream (content);

					response = new NSUrlResponse (request.Url, mimeType, (int) (uint)data.Length, charset);

					return new NSCachedUrlResponse (response, data);
				}
			}

			return base.CachedResponseForRequest (request);
		}
	}
}
