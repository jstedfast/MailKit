using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using MailKit;
using MailKit.Security;

namespace ImapClientDemo
{
	class ClientConnection<T> : IDisposable where T : IMailService
	{
		bool disposed;

		public ClientConnection (T client, string host, int port, SecureSocketOptions sslOptions, NetworkCredential credentials)
		{
			Client = client ?? throw new ArgumentNullException (nameof (client));
			Host = host ?? throw new ArgumentNullException (nameof (host));
			Port = port >= 0 && port <= 65535 ? port : throw new ArgumentOutOfRangeException (nameof (port));
			SslOptions = sslOptions;
			Credentials = credentials ?? throw new ArgumentNullException (nameof (credentials));
		}

		public T Client { get; private set; }

		public string Host { get; set; }

		public int Port { get; set; }

		public SecureSocketOptions SslOptions { get; set; }

		public NetworkCredential Credentials { get; set; }

		public void EnsureConnected (CancellationToken cancellationToken)
		{
			if (Client.IsConnected)
				return;

			Client.Connect (Host, Port, SslOptions, cancellationToken);
		}

		public Task EnsureConnectedAsync (CancellationToken cancellationToken)
		{
			if (Client.IsConnected)
				return Task.CompletedTask;

			return Client.ConnectAsync (Host, Port, SslOptions, cancellationToken);
		}

		public void EnsureAuthenticated (CancellationToken cancellationToken)
		{
			if (Client.IsAuthenticated)
				return;

			Client.Authenticate (Credentials, cancellationToken);
		}

		public Task EnsureAuthenticatedAsync (CancellationToken cancellationToken)
		{
			if (Client.IsAuthenticated)
				return Task.CompletedTask;

			return Client.AuthenticateAsync (Credentials, cancellationToken);
		}

		protected virtual void Dispose (bool disposing)
		{
			if (disposing && !disposed) {
				Client.Dispose ();
				disposed = true;
			}
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}
	}
}
