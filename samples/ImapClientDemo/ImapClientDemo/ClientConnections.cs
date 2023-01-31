using System;
using System.Collections.Concurrent;

using MailKit;

namespace ImapClientDemo
{
	class ClientConnections<T> where T : IMailService
	{
		readonly ConcurrentDictionary<object, ClientConnection<T>> connections;

		public ClientConnections ()
		{
			connections = new ConcurrentDictionary<object, ClientConnection<T>> ();
		}

		public void Add (ClientConnection<T> connection)
		{
			if (!connections.TryAdd (connection.Client.SyncRoot, connection))
				throw new InvalidOperationException ();
		}

		public bool TryGetValue (IMailService client, out ClientConnection<T> connection)
		{
			client = client ?? throw new ArgumentNullException (nameof (client));

			return connections.TryGetValue (client.SyncRoot, out connection);
		}

		public bool TryGetValue (IMailFolder folder, out ClientConnection<T> connection)
		{
			folder = folder ?? throw new ArgumentNullException (nameof (folder));

			return connections.TryGetValue (folder.SyncRoot, out connection);
		}
	}
}
