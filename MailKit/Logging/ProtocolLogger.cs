using System;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MailKit.Logging
{
	/// <inheritdoc />
	public class ProtocolLogger : IProtocolLogger
	{
		private readonly ILogger<ProtocolLogger> _logger;

		/// <summary>
		/// Log messages via Microsoft Extension Logging (MEL) Microsoft ILogger <see cref="ILogger"/>.
		/// </summary>
		/// <remarks>
		/// For more information how configure it see example in <seealso cref="https://github.com/NLog/NLog/wiki/Getting-started-with-.NET-Core-2---Console-application"/>
		/// </remarks>
		/// <param name="logger">The logger that resolves through Dependency injector (DI)</param>
		public ProtocolLogger (ILogger<ProtocolLogger> logger)
		{
			_logger = logger;
		}

		/// <inheritdoc />
		public void LogConnect (Uri uri)
		{
			if (uri == null)
				throw new ArgumentNullException (nameof (uri));

			_logger.Log (LogLevel.Trace, $"Connected to {uri}");
		}

		/// <inheritdoc />
		public void LogClient (byte[] buffer, int offset, int count)
		{
			var message = Encoding.UTF8
				.GetString (buffer)
				.TrimEnd ('\0')
				.Replace (Environment.NewLine, string.Empty);

			_logger.Log (LogLevel.Trace, $"Client: {message}");
		}

		/// <inheritdoc />
		public void LogServer (byte[] buffer, int offset, int count)
		{
			var message = Encoding.UTF8
				.GetString (buffer)
				.TrimEnd ('\0')
				.Replace (Environment.NewLine, string.Empty);

			_logger.Log (LogLevel.Trace, $"Server: {message}");
		}
	}
}
