//
// ClientMetrics.cs
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
using System.Diagnostics;
using System.Globalization;
using System.Diagnostics.Metrics;

using MailKit.Net.Smtp;
using MailKit.Security;

namespace MailKit.Net {
	sealed class ClientMetrics
	{
		public readonly Histogram<double> ConnectionDuration;
		public readonly Counter<long> OperationCounter;
		public readonly Histogram<double> OperationDuration;
		public readonly string MeterName;

		public ClientMetrics (Meter meter, string meterName, string an, string protocol)
		{
			MeterName = meterName;

			ConnectionDuration = meter.CreateHistogram<double> (
				name: $"{meterName}.client.connection.duration",
				unit: "s",
				description: $"The duration of successfully established connections to {an} {protocol} server.");

			OperationCounter = meter.CreateCounter<long> (
				name: $"{meterName}.client.operation.count",
				unit: "{operation}",
				description: $"The number of times a client performed an operation on {an} {protocol} server.");

			OperationDuration = meter.CreateHistogram<double> (
				name: $"{meterName}.client.operation.duration",
				unit: "ms",
				description: $"The amount of time it takes for the {protocol} server to perform an operation.");
		}

		static bool TryGetErrorType (Exception exception, out string errorType)
		{
			if (SocketMetrics.TryGetErrorType (exception, false, out errorType))
				return true;

			if (exception is SslHandshakeException) {
				// Note: The string "secure_connection_error" is used by HttpClient for SSL/TLS handshake errors.
				errorType = "secure_connection_error";
				return true;
			}

			if (exception is ProtocolException) {
				// TODO: ProtocolExceptions tend to be either "Unexpectedly disconnected" or "Parse error".
				// If we add a property to ProtocolException to tell us this, we could report it better here.
				//
				// To mimic HttpClient error.type values, we could use "response_ended" and "invalid_response", respectively.
				//
				// Alternatively, HttpClient also uses "http_protocol_error" so we could use "smtp/pop3/imap_protocol_error".
				errorType = "protocol_error";
				return true;
			}

			if (exception is SmtpCommandException smtp) {
				errorType = ((int) smtp.StatusCode).ToString (CultureInfo.InvariantCulture);
				return true;
			}

			if (exception is CommandException) {
				// FIXME: We need to add a property to CommandException to tell us the error type.
				errorType = "command_error";
				return true;
			}

			// Fall back to using the exception type name.
			errorType = exception.GetType ().FullName;

			return true;
		}

		internal static TagList GetTags (Uri uri, Exception ex)
		{
			var tags = new TagList {
				{ "url.scheme", uri.Scheme },
				{ "server.address", uri.Host },
				{ "server.port", uri.Port }
			};

			if (ex is not null && TryGetErrorType (ex, out var errorType))
				tags.Add ("error.type", errorType);

			return tags;
		}

		public void RecordClientDisconnected (long startTimestamp, Uri uri, Exception ex = null)
		{
			if (ConnectionDuration.Enabled) {
				var duration = TimeSpan.FromTicks (Stopwatch.GetTimestamp () - startTimestamp).TotalSeconds;
				var tags = GetTags (uri, ex);

				ConnectionDuration.Record (duration, tags);
			}
		}
	}
}

#endif // NET6_0_OR_GREATER
