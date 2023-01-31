using System;
using System.Threading;

using MailKit;

namespace ImapClientDemo
{
	abstract class ClientCommand<T> where T : IMailService
	{
		protected ClientCommand (ClientConnection<T> connection)
		{
			Connection = connection ?? throw new ArgumentNullException (nameof (connection));
		}

		public ClientConnection<T> Connection { get; private set; }

		/// <summary>
		/// Run the client command.
		/// </summary>
		/// <remarks>
		/// <para>Runs the client command.</para>
		/// <para>This method will be called by the <see cref="ClientCommandPipeline{T}"/> on a background thread.</para>
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		public abstract void Run (CancellationToken cancellationToken);
	}
}
