//
// ClientMetrics.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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
using System.Diagnostics.CodeAnalysis;

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

		internal static bool TryGetErrorType (Exception exception, [NotNullWhen (true)] out string? errorType)
		{
			if (SocketMetrics.TryGetErrorType (exception, false, out errorType))
				return true;

			if (exception is SslHandshakeException) {
				// Note: The string "secure_connection_error" is used by HttpClient for SSL/TLS handshake errors.
				errorType = "secure_connection_error";
				return true;
			}

			if (exception is ProtocolException protocol) {
				// Note: "response_ended" and "invalid_response" mimic the error.type values used by HttpClient.
				errorType = protocol.ErrorType switch {
					ProtocolErrorType.UnexpectedDisconnect => "response_ended",
					ProtocolErrorType.InvalidResponse => "invalid_response",
					ProtocolErrorType.ServerDisconnected => "server_disconnected",
					ProtocolErrorType.ResponseTooLarge => "response_too_large",
					_ => "protocol_error"
				};
				return true;
			}

			if (exception is SmtpCommandException smtp) {
				errorType = ((int) smtp.StatusCode).ToString (CultureInfo.InvariantCulture);
				return true;
			}

			if (exception is CommandException command) {
				errorType = command.ErrorType switch {
					CommandErrorType.Rejected => "rejected",
					CommandErrorType.InvalidCommand => "invalid_command",
					CommandErrorType.NotSupported => "not_supported",
					CommandErrorType.PermissionDenied => "permission_denied",
					CommandErrorType.NotFound => "not_found",
					CommandErrorType.AlreadyExists => "already_exists",
					CommandErrorType.QuotaExceeded => "quota_exceeded",
					CommandErrorType.LimitExceeded => "limit_exceeded",
					CommandErrorType.InUse => "in_use",
					CommandErrorType.TemporaryFailure => "temporary_failure",
					CommandErrorType.ServerError => "server_error",
					_ => "command_error"
				};
				return true;
			}

			// Fall back to using the exception type name.
			errorType = exception.GetType ().FullName;

			return errorType != null;
		}

		internal static TagList GetTags (Uri uri, Exception? ex)
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

		public void RecordClientDisconnected (long startTimestamp, Uri uri, Exception? ex = null)
		{
			if (ConnectionDuration.Enabled) {
				var duration = Stopwatch.GetElapsedTime (startTimestamp).TotalSeconds;
				var tags = GetTags (uri, ex);

				ConnectionDuration.Record (duration, tags);
			}
		}
	}
}

#endif // NET6_0_OR_GREATER
