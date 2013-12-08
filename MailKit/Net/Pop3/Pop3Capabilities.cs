//
// Pop3Capabilities.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013 Jeffrey Stedfast
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

namespace MailKit.Net.Pop3 {
	[Flags]
	public enum Pop3Capabilities {
		None                   = 0,
		Apop                   = (1 << 0),
		LoginDelay             = (1 << 1),
		Pipelining             = (1 << 2),
		ResponseCodes          = (1 << 3),
		Sasl                   = (1 << 4),
		StartTLS               = (1 << 5),
		Top                    = (1 << 6),
		UIDL                   = (1 << 7),
		User                   = (1 << 8),

		// manually probed
		ProbedTop              = (1 << 9),
		ProbedUIDL             = (1 << 10),
		ProbedUser             = (1 << 11),

		CapaMask               = 0x01fe
	}
}
