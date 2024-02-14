//
// SmtpClientMetrics.cs
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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MailKit.Net.Smtp {
	sealed class SmtpClientMetrics
	{
		readonly Counter<long> connectCounter;
		readonly Histogram<double> connectDuration;
		readonly Histogram<double> connectionDuration;
		readonly Counter<long> sendCounter;
		readonly Histogram<double> sendDuration;

		public SmtpClientMetrics (Meter meter)
		{
			connectCounter = meter.CreateCounter<long> (
				name: $"{Telemetry.SmtpClient.MeterName}.client.connect.count",
				unit: "{attempt}",
				description: "The number of times a client attempted to connect to an SMTP server.");

			connectDuration = meter.CreateHistogram<double> (
				name: $"{Telemetry.SmtpClient.MeterName}.client.connect.duration",
				unit: "ms",
				description: "The amount of time it takes for the client to connect to an SMTP server.");

			connectionDuration = meter.CreateHistogram<double> (
				name: $"{Telemetry.SmtpClient.MeterName}.client.connection_duration",
				unit: "s",
				description: "The duration of successfully established connections to an SMTP server.");

			sendCounter = meter.CreateCounter<long> (
				name: $"{Telemetry.SmtpClient.MeterName}.client.send.count",
				unit: "{message}",
				description: "The number of messages sent to an SMTP server.");

			sendDuration = meter.CreateHistogram<double> (
				name: $"{Telemetry.SmtpClient.MeterName}.client.send.duration",
				unit: "ms",
				description: "The amount of time it takes to send a message to an SMTP server.");
		}

		static SocketException GetSocketException (Exception ex)
		{
			if (ex is SocketException se)
				return se;

			if (ex is IOException)
				return ex.InnerException as SocketException;

			return null;
		}

		// TagList is a huge struct, so we avoid storing it in a field to reduce the amount we allocate on the heap.
		static TagList GetTags (Uri uri, EndPoint remoteEndPoint = null, Exception ex = null)
		{
			var tags = new TagList {
				{ "url.scheme", uri.Scheme },
				{ "server.address", uri.Host },
				{ "server.port", uri.Port }
			};

			if (remoteEndPoint is not null)
				tags.Add ("network.peer.address", remoteEndPoint.ToString ());

			if (ex is not null) {
				tags.Add ("error.type", ex.GetType ().FullName);

				if (ex is ServiceNotAuthenticatedException) {
					tags.Add ("smtp.status_code", (int) SmtpStatusCode.AuthenticationRequired);
				} else if (ex is SmtpCommandException cmd) {
					tags.Add ("smtp.status_code", (int) cmd.StatusCode);
				} else if (GetSocketException (ex) is SocketException se) {
					tags.Add ("socket.error", (int) se.SocketErrorCode);
				}
			}

			return tags;
		}

		public void ReportConnected (long connectStartedTimestamp, Uri uri, EndPoint remoteEndPoint)
		{
			if (connectCounter.Enabled || connectDuration.Enabled) {
				var tags = GetTags (uri, remoteEndPoint);

				if (connectDuration.Enabled) {
					var duration = TimeSpan.FromTicks (Stopwatch.GetTimestamp () - connectStartedTimestamp).TotalMilliseconds;

					connectDuration.Record (duration, tags);
				}

				if (connectCounter.Enabled)
					connectCounter.Add (1, tags);
			}
		}

		public void ReportConnectFailed (long connectStartedTimestamp, Uri uri, Exception ex)
		{
			if (connectCounter.Enabled || connectDuration.Enabled) {
				var tags = GetTags (uri, ex: ex);

				if (connectDuration.Enabled) {
					var duration = TimeSpan.FromTicks (Stopwatch.GetTimestamp () - connectStartedTimestamp).TotalMilliseconds;

					connectDuration.Record (duration, tags);
				}

				if (connectCounter.Enabled)
					connectCounter.Add (1, tags);
			}
		}

		public void ReportDisconnected (long sessionStartedTimestamp, Uri uri, EndPoint remoteEndPoint, Exception ex)
		{
			if (connectionDuration.Enabled) {
				var duration = TimeSpan.FromTicks (Stopwatch.GetTimestamp () - sessionStartedTimestamp).TotalSeconds;

				connectionDuration.Record (duration, GetTags (uri, remoteEndPoint, ex));
			}
		}

		public void ReportSendCompleted (long sendStartedTimestamp, Uri uri, EndPoint remoteEndPoint)
		{
			if (sendCounter.Enabled || sendDuration.Enabled) {
				var tags = GetTags (uri, remoteEndPoint);

				if (sendDuration.Enabled) {
					var duration = TimeSpan.FromTicks (Stopwatch.GetTimestamp () - sendStartedTimestamp).TotalMilliseconds;

					sendDuration.Record (duration, tags);
				}

				if (sendCounter.Enabled)
					sendCounter.Add (1, tags);
			}
		}

		public void ReportSendFailed (long sendStartedTimestamp, Uri uri, EndPoint remoteEndPoint, Exception ex)
		{
			if (sendCounter.Enabled || sendDuration.Enabled) {
				var tags = GetTags (uri, remoteEndPoint, ex);

				if (sendDuration.Enabled) {
					var duration = TimeSpan.FromTicks (Stopwatch.GetTimestamp () - sendStartedTimestamp).TotalMilliseconds;

					sendDuration.Record (duration, tags);
				}

				if (sendCounter.Enabled)
					sendCounter.Add (1, tags);
			}
		}
	}
}

#endif // NET6_0_OR_GREATER
