//
// SocketMetrics.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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

#if NET6_0_OR_GREATER

using System;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MailKit.Net {
	sealed class SocketMetrics
	{
		readonly Counter<long> connectCounter;
		readonly Histogram<double> connectDuration;

		public SocketMetrics (Meter meter)
		{
			connectCounter = meter.CreateCounter<long> (
				name: $"{Telemetry.Socket.MeterName}.connect.count",
				unit: "{attempt}",
				description: "The number of times a socket attempted to connect to a remote host.");

			connectDuration = meter.CreateHistogram<double> (
				name: $"{Telemetry.Socket.MeterName}.connect.duration",
				unit: "ms",
				description: "The number of milliseconds taken for a socket to connect to a remote host.");
		}

		static SocketException GetSocketException (Exception exception)
		{
			Exception ex = exception;

			do {
				if (ex is SocketException se)
					return se;

				ex = ex.InnerException;
			} while (ex is not null);

			return null;
		}

		internal static bool TryGetErrorType (Exception exception, bool exceptionTypeFallback, out string errorType)
		{
			if (exception is OperationCanceledException) {
				errorType = "cancelled";
				return true;
			}

			var socketException = GetSocketException (exception);

			if (socketException is not null) {
				switch (socketException.SocketErrorCode) {
				case SocketError.HostNotFound: errorType = "host_not_found"; return true;
				case SocketError.HostUnreachable: errorType = "host_unreachable"; return true;
				case SocketError.NetworkUnreachable: errorType = "network_unreachable"; return true;

				case SocketError.ConnectionAborted: errorType = "connection_aborted"; return true;
				case SocketError.ConnectionRefused: errorType = "connection_refused"; return true;
				case SocketError.ConnectionReset: errorType = "connection_reset"; return true;

				case SocketError.TimedOut: errorType = "timed_out"; return true;
				case SocketError.TooManyOpenSockets: errorType = "too_many_open_sockets"; return true;
				}
			}

			if (exceptionTypeFallback) {
				errorType = exception.GetType ().FullName;
				return true;
			}

			errorType = null;

			return false;
		}

		static TagList GetTags (IPAddress ip, string host, int port, Exception ex = null)
		{
			var tags = new TagList {
				{ "network.peer.address", ip.ToString () },
				{ "server.address", host },
				{ "server.port", port },
			};

			if (ex is not null && TryGetErrorType (ex, true, out var errorType))
				tags.Add ("error.type", errorType);

			return tags;
		}

		public void RecordConnected (long connectStartedTimestamp, IPAddress ip, string host, int port)
		{
			if (connectCounter.Enabled || connectDuration.Enabled) {
				var tags = GetTags (ip, host, port);

				if (connectDuration.Enabled) {
					var duration = TimeSpan.FromTicks (Stopwatch.GetTimestamp () - connectStartedTimestamp).TotalMilliseconds;

					connectDuration.Record (duration, tags);
				}

				if (connectCounter.Enabled)
					connectCounter.Add (1, tags);
			}
		}

		public void RecordConnectFailed (long connectStartedTimestamp, IPAddress ip, string host, int port, bool cancelled, Exception ex = null)
		{
			if (connectCounter.Enabled || connectDuration.Enabled) {
				var tags = GetTags (ip, host, port, ex);

				if (connectDuration.Enabled) {
					var duration = TimeSpan.FromTicks (Stopwatch.GetTimestamp () - connectStartedTimestamp).TotalMilliseconds;

					connectDuration.Record (duration, tags);
				}

				if (connectCounter.Enabled)
					connectCounter.Add (1, tags);
			}
		}
	}
}

#endif // NET6_0_OR_GREATER
