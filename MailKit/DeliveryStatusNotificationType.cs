//
// DeliveryStatusNotificationReturnType.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2019 .NET Foundation and Contributors
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

namespace MailKit.Net.Smtp
{
	/// <summary>
	/// Delivery status notification type.
	/// </summary>
	/// <remarks>
	/// The delivery status notification type specifies whether or not
	/// the full message should be included in any failed DSN issued for
	/// a message transmission as opposed to just the headers.
	/// </remarks>
	public enum DeliveryStatusNotificationType
	{
		/// <summary>
		/// The return type is unspecified, allowing the server to choose.
		/// </summary>
		Unspecified,

		/// <summary>
		/// The full message should be included in any failed delivery status notification issued by the server.
		/// </summary>
		Full,

		/// <summary>
		/// Only the headers should be included in any failed delivery status notification issued by the server.
		/// </summary>
		HeadersOnly,
	}
}
