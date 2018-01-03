//
// DeliveryStatusNotification.cs
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

namespace MailKit {
	/// <summary>
	/// Delivery status notification types.
	/// </summary>
	/// <remarks>
	/// A set of flags that may be bitwise-or'd together to specify
	/// when a delivery status notification should be sent for a
	/// particlar recipient.
	/// </remarks>
	[Flags]
	public enum DeliveryStatusNotification {
		/// <summary>
		/// Never send delivery status notifications.
		/// </summary>
		Never   = 0,

		/// <summary>
		/// Send a notification on successful delivery to the recipient.
		/// </summary>
		Success = (1 << 0),

		/// <summary>
		/// Send a notification on failure to deliver to the recipient.
		/// </summary>
		Failure = (1 << 1),

		/// <summary>
		/// Send a notification when the delivery to the recipient has
		/// been delayed for an unusual amount of time.
		/// </summary>
		Delay   = (1 << 2)
	}
}
