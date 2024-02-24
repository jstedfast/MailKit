//
// NetworkOperation.cs
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

using System;
using System.Diagnostics;

namespace MailKit.Net {
	class NetworkOperation : IDisposable
	{
		public const string Authenticate = nameof (Authenticate);
		public const string Connect = nameof (Connect);

		public enum StatusCode
		{
			Ok,
			Error,
			Cancelled,
		}

#if NET6_0_OR_GREATER
		readonly ClientMetrics metrics;
		readonly Activity activity;
		readonly long startTimestamp;
		readonly string name;
		readonly Uri uri;

		StatusCode statusCode;
		Exception ex;

		NetworkOperation (string name, Uri uri, Activity activity, ClientMetrics metrics)
		{
			this.name = name;
			this.uri = uri;
			this.activity = activity;
			this.metrics = metrics;

			if (activity is not null) {
				activity.AddTag ("url.scheme", uri.Scheme);
				activity.AddTag ("server.address", uri.Host);
				activity.AddTag ("server.port", uri.Port);
			}

			startTimestamp = Stopwatch.GetTimestamp ();
		}
#else
		NetworkOperation ()
		{
		}
#endif

		public void SetStatusCode (StatusCode statusCode, Exception ex = null)
		{
#if NET6_0_OR_GREATER
			this.statusCode = statusCode;
			this.ex = ex;

			if (activity is not null) {
				switch (statusCode) {
				case StatusCode.Ok:
					activity.SetStatus (ActivityStatusCode.Ok);
					break;
				case StatusCode.Error:
					activity.SetStatus (ActivityStatusCode.Error);
					break;
				case StatusCode.Cancelled:
					activity.SetStatus (ActivityStatusCode.Error, "Cancelled");
					break;
				}
			}
#endif
		}

		public void SetError (Exception ex)
		{
			SetStatusCode (ex is OperationCanceledException ? StatusCode.Cancelled : StatusCode.Error, ex);
		}

#if NET6_0_OR_GREATER
		static string GetStatusCodeValue (StatusCode statusCode)
		{
			switch (statusCode) {
			case StatusCode.Cancelled: return "cancelled";
			case StatusCode.Error: return "error";
			default: return "ok";
			};
		}

		// TagList is a huge struct, so we avoid storing it in a field to reduce the amount we allocate on the heap.
		TagList GetTags ()
		{
			var tags = new TagList {
				{ "url.scheme", uri.Scheme },
				{ "server.address", uri.Host },
				{ "server.port", uri.Port },
				{ "network.operation.status", GetStatusCodeValue (statusCode) }
			};

			if (!name.Equals ("connect", StringComparison.OrdinalIgnoreCase))
				tags.Add ("network.operation", name.ToLowerInvariant ());

			if (ex is not null && statusCode != StatusCode.Cancelled)
				tags.Add ("exception.type", ex.GetType ().FullName);

			return tags;
		}
#endif

		public void Dispose ()
		{
#if NET6_0_OR_GREATER
			if (metrics != null && (metrics.OperationCounter.Enabled || metrics.OperationDuration.Enabled)) {
				var tags = GetTags ();

				if (metrics.OperationDuration.Enabled) {
					var duration = TimeSpan.FromTicks (Stopwatch.GetTimestamp () - startTimestamp).TotalMilliseconds;

					metrics.OperationDuration.Record (duration, tags);
				}

				if (metrics.OperationCounter.Enabled)
					metrics.OperationCounter.Add (1, tags);
			}

			activity?.Dispose ();
#endif
		}

#if NET6_0_OR_GREATER
		public static NetworkOperation Start (string name, Uri uri, ActivitySource source, ClientMetrics metrics)
		{

			var activity = source?.StartActivity (name, ActivityKind.Client);

			return new NetworkOperation (name, uri, activity, metrics);
		}
#else
		public static NetworkOperation Start (string name, Uri uri)
		{
			return new NetworkOperation ();
		}
#endif
	}
}
