﻿//
// Pop3Command.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2025 .NET Foundation and Contributors
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
using System.Text;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;

namespace MailKit.Net.Pop3 {
	/// <summary>
	/// POP3 command handler.
	/// </summary>
	/// <remarks>
	/// All exceptions thrown by the handler are considered fatal and will
	/// force-disconnect the connection. If a non-fatal error occurs, set
	/// it on the <see cref="Pop3Command.Exception"/> property.
	/// </remarks>
	delegate Task Pop3CommandHandler (Pop3Engine engine, Pop3Command pc, string text, bool doAsync, CancellationToken cancellationToken);

	enum Pop3CommandStatus {
		Queued         = -5,
		Active         = -4,
		Continue       = -3,
		ProtocolError  = -2,
		Error          = -1,
		Ok             =  0
	}

	class Pop3Command
	{
		public Pop3CommandHandler Handler { get; private set; }
		public Encoding Encoding { get; private set; }
		public string Command { get; private set; }

		// output
		public Pop3CommandStatus Status { get; internal set; }
		public ProtocolException Exception { get; set; }
		public string StatusText { get; set; }

		public object UserData { get; set; }

		public Pop3Command (Pop3CommandHandler handler, Encoding encoding, string format, params object[] args)
		{
			Command = string.Format (CultureInfo.InvariantCulture, format, args);
			Encoding = encoding;
			Handler = handler;
		}

		static Exception CreatePop3Exception (Pop3Command pc)
		{
			var command = pc.Command.Split (' ')[0].TrimEnd ();
			var message = string.Format ("POP3 server did not respond with a +OK response to the {0} command.", command);

			if (pc.Status == Pop3CommandStatus.Error)
				return new Pop3CommandException (message, pc.StatusText);

			return new Pop3ProtocolException (message);
		}

		public void ThrowIfError ()
		{
			if (Status != Pop3CommandStatus.Ok)
				throw CreatePop3Exception (this);

			if (Exception != null)
				throw Exception;
		}
	}
}
