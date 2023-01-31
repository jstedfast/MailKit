using System;

using MailKit;

namespace ImapClientDemo
{
	class ConnectionFailedEventArgs<T> : EventArgs where T : IMailService
	{
		public ConnectionFailedEventArgs (ClientConnection<T> connection, Exception ex)
		{
			Connection = connection;
			Exception = ex;
		}

		public ClientConnection<T> Connection {
			get; private set;
		}

		public Exception Exception {
			get; private set;
		}
	}
}
