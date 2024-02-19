//
// Telemetry.cs
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
using System.Diagnostics.Metrics;

using MailKit.Net;

namespace MailKit {
	/// <summary>
	/// Telemetry constants for MailKit.
	/// </summary>
	/// <remarks>
	/// Telemetry constants for MailKit.
	/// </remarks>
	public static class Telemetry
	{
		/// <summary>
		/// The socket-level telemetry information.
		/// </summary>
		public static class Socket
		{
			/// <summary>
			/// The name of the socket-level meter.
			/// </summary>
			public const string MeterName = "mailkit.net.socket";

			/// <summary>
			/// The version of the socket-level meter.
			/// </summary>
			public const string MeterVersion = "0.1";

			static Meter Meter;

			internal static SocketMetrics Metrics { get; private set; }

			/// <summary>
			/// Configure socket metering.
			/// </summary>
			/// <remarks>
			/// Configures socket metering.
			/// </remarks>
			public static void Configure ()
			{
				Meter ??= new Meter (MeterName, MeterVersion);
				Metrics ??= new SocketMetrics (Meter);
			}

#if NET8_0_OR_GREATER
			/// <summary>
			/// Configure socket telemetry.
			/// </summary>
			/// <remarks>
			/// Configures socket telemetry.
			/// </remarks>
			/// <param name="meterFactory">The meter factory.</param>
			/// <exception cref="ArgumentNullException">
			/// <paramref name="meterFactory"/> is <c>null</c>.
			/// </exception>
			public static void Configure (IMeterFactory meterFactory)
			{
				if (meterFactory is null)
					throw new ArgumentNullException (nameof (meterFactory));

				Meter ??= meterFactory.Create (MeterName, MeterVersion);
				Metrics ??= new SocketMetrics (Meter);
			}
#endif
		}

		/// <summary>
		/// The SmtpClient-level telemetry information.
		/// </summary>
		/// <remarks>
		/// The SmtpClient-level telemetry information.
		/// </remarks>
		public static class SmtpClient
		{
			/// <summary>
			/// The name of the SmtpClient activity source used for tracing.
			/// </summary>
			public const string ActivitySourceName = "MailKit.Net.SmtpClient";

			/// <summary>
			/// The version of the SmtpClient activity source used for tracing.
			/// </summary>
			public const string ActivitySourceVersion = "0.1";

			internal static readonly ActivitySource ActivitySource = new ActivitySource (ActivitySourceName, ActivitySourceVersion);

			/// <summary>
			/// The name of the SmtpClient meter.
			/// </summary>
			public const string MeterName = "mailkit.net.smtp";

			/// <summary>
			/// The version of the SmtpClient meter.
			/// </summary>
			public const string MeterVersion = "0.1";

			static Meter Meter;

			internal static ClientMetrics Metrics { get; private set; }

			internal static ClientMetrics CreateMetrics (Meter meter)
			{
				return new ClientMetrics (Meter, MeterName, "an", "SMTP");
			}

			/// <summary>
			/// Configure SmtpClient telemetry.
			/// </summary>
			/// <remarks>
			/// Configures SmtpClient telemetry.
			/// </remarks>
			public static void Configure ()
			{
				Meter ??= new Meter (MeterName, MeterVersion);
				Metrics ??= CreateMetrics (Meter);
			}

#if NET8_0_OR_GREATER
			/// <summary>
			/// Configure SmtpClient telemetry.
			/// </summary>
			/// <remarks>
			/// Configures SmtpClient telemetry.
			/// </remarks>
			/// <param name="meterFactory">The meter factory.</param>
			/// <exception cref="ArgumentNullException">
			/// <paramref name="meterFactory"/> is <c>null</c>.
			/// </exception>
			public static void Configure (IMeterFactory meterFactory)
			{
				if (meterFactory is null)
					throw new ArgumentNullException (nameof (meterFactory));

				Meter = meterFactory.Create (MeterName, MeterVersion);
				Metrics ??= CreateMetrics (Meter);
			}
#endif
		}

		/// <summary>
		/// The Pop3Client-level telemetry information.
		/// </summary>
		/// <remarks>
		/// The Pop3Client-level telemetry information.
		/// </remarks>
		public static class Pop3Client
		{
			/// <summary>
			/// The name of the Pop3Client meter.
			/// </summary>
			public const string MeterName = "mailkit.net.pop3";

			/// <summary>
			/// The version of the Pop3Client meter.
			/// </summary>
			public const string MeterVersion = "1.0";

			internal static Meter Meter { get; private set; }

			/// <summary>
			/// Configure Pop3Client telemetry.
			/// </summary>
			/// <remarks>
			/// Configures Pop3Client telemetry.
			/// </remarks>
			public static void Configure ()
			{
				Meter ??= new Meter (MeterName, MeterVersion);
			}

#if NET8_0_OR_GREATER
			/// <summary>
			/// Configure Pop3Client telemetry.
			/// </summary>
			/// <remarks>
			/// Configures Pop3Client telemetry.
			/// </remarks>
			/// <param name="meterFactory">The meter factory.</param>
			/// <exception cref="ArgumentNullException">
			/// <paramref name="meterFactory"/> is <c>null</c>.
			/// </exception>
			public static void Configure (IMeterFactory meterFactory)
			{
				if (meterFactory is null)
					throw new ArgumentNullException (nameof (meterFactory));

				Meter = meterFactory.Create (MeterName, MeterVersion);
			}
#endif
		}

		/// <summary>
		/// The ImapClient-level telemetry information.
		/// </summary>
		/// <remarks>
		/// The ImapClient-level telemetry information.
		/// </remarks>
		public static class ImapClient
		{
			/// <summary>
			/// The name of the ImapClient meter.
			/// </summary>
			public const string MeterName = "mailkit.net.imap";

			/// <summary>
			/// The version of the ImapClient meter.
			/// </summary>
			public const string MeterVersion = "1.0";

			internal static Meter Meter { get; private set; }

			/// <summary>
			/// Configure ImapClient telemetry.
			/// </summary>
			/// <remarks>
			/// Configures ImapClient telemetry.
			/// </remarks>
			public static void Configure ()
			{
				Meter ??= new Meter (MeterName, MeterVersion);
			}

#if NET8_0_OR_GREATER
			/// <summary>
			/// Configure ImapClient telemetry.
			/// </summary>
			/// <remarks>
			/// Configures ImapClient telemetry.
			/// </remarks>
			/// <param name="meterFactory">The meter factory.</param>
			/// <exception cref="ArgumentNullException">
			/// <paramref name="meterFactory"/> is <c>null</c>.
			/// </exception>
			public static void Configure (IMeterFactory meterFactory)
			{
				if (meterFactory is null)
					throw new ArgumentNullException (nameof (meterFactory));

				Meter = meterFactory.Create (MeterName, MeterVersion);
			}
#endif
		}

		/// <summary>
		/// Configure telemetry in MailKit.
		/// </summary>
		/// <remarks>
		/// Configures telemetry in MailKit.
		/// </remarks>
		public static void Configure ()
		{
			Socket.Configure ();
			SmtpClient.Configure ();
			Pop3Client.Configure ();
			ImapClient.Configure ();
		}

#if NET8_0_OR_GREATER
		/// <summary>
		/// Configure telemetry in MailKit.
		/// </summary>
		/// <remarks>
		/// Configures telemetry in MailKit.
		/// </remarks>
		/// <param name="meterFactory">The meter factory.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="meterFactory"/> is <c>null</c>.
		/// </exception>
		public static void Configure (IMeterFactory meterFactory)
		{
			if (meterFactory is null)
				throw new ArgumentNullException (nameof (meterFactory));

			Socket.Configure (meterFactory);
			SmtpClient.Configure (meterFactory);
			Pop3Client.Configure (meterFactory);
			ImapClient.Configure (meterFactory);
		}
#endif
	}
}

#endif // NET6_0_OR_GREATER
