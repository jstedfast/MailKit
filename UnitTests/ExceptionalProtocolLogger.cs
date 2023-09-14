using MailKit;

namespace UnitTests {
	enum ExceptionalProtocolLoggerMode
	{
		ThrowOnLogConnect,
		ThrowOnLogClient,
		ThrowOnLogServer,
	}

	class ExceptionalProtocolLogger : IProtocolLogger
	{
		readonly ExceptionalProtocolLoggerMode mode;

		public IAuthenticationSecretDetector AuthenticationSecretDetector { get; set; }

		public ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode mode)
		{
			this.mode = mode;
		}

		public void LogConnect (Uri uri)
		{
			if (mode == ExceptionalProtocolLoggerMode.ThrowOnLogConnect)
				throw new NotImplementedException ();
		}

		public void LogClient (byte[] buffer, int offset, int count)
		{
			if (mode == ExceptionalProtocolLoggerMode.ThrowOnLogClient)
				throw new NotImplementedException ();
		}

		public void LogServer (byte[] buffer, int offset, int count)
		{
			if (mode == ExceptionalProtocolLoggerMode.ThrowOnLogServer)
				throw new NotImplementedException ();
		}

		public void Dispose ()
		{
		}
	}
}
