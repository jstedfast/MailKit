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

		static TagList GetTags (IPAddress ip, string host, int port, string status, Exception ex = null)
		{
			var tags = new TagList {
				{ "network.peer.address", ip.ToString () },
				{ "socket.connect.result", status },
				{ "server.address", host },
				{ "server.port", port },
			};

			if (ex is not null) {
				tags.Add ("error.type", ex.GetType ().FullName);

				if (ex is SocketException se)
					tags.Add ("socket.error", (int) se.SocketErrorCode);
			}

			return tags;
		}

		public void ReportConnected (long connectStartedTimestamp, IPAddress ip, string host, int port)
		{
			if (connectCounter.Enabled || connectDuration.Enabled) {
				var tags = GetTags (ip, host, port, "succeeded");

				if (connectDuration.Enabled) {
					var duration = TimeSpan.FromTicks (Stopwatch.GetTimestamp () - connectStartedTimestamp).TotalMilliseconds;

					connectDuration.Record (duration, tags);
				}

				if (connectCounter.Enabled)
					connectCounter.Add (1, tags);
			}
		}

		public void ReportConnectFailed (long connectStartedTimestamp, IPAddress ip, string host, int port, bool cancelled, Exception ex = null)
		{
			if (connectCounter.Enabled || connectDuration.Enabled) {
				var tags = GetTags (ip, host, port, cancelled ? "cancelled" : "failed", ex);

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
