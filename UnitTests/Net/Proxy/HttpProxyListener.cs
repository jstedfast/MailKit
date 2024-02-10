//
// Socks5ProxyListener.cs
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

using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;

using MailKit.Net;

using SslStream = System.Net.Security.SslStream;

namespace UnitTests.Net.Proxy {
	class HttpProxyListener : ProxyListener
	{
		readonly X509Certificate2 certificate;
		readonly bool https;

		public HttpProxyListener ()
		{
		}

		public HttpProxyListener (X509Certificate2 certificate)
		{
			this.certificate = certificate;
			https = true;
		}

		protected override async Task<Stream> GetClientStreamAsync (Socket socket, CancellationToken cancellationToken)
		{
			var network = await base.GetClientStreamAsync (socket, cancellationToken).ConfigureAwait (false);

			if (!https)
				return network;

			var ssl = new SslStream (network, false);

			await ssl.AuthenticateAsServerAsync (certificate, false, false).ConfigureAwait (false);

			return ssl;
		}

		protected override async Task<Socket> ClientCommandReceived (Stream client, byte[] buffer, int length, CancellationToken cancellationToken)
		{
			byte[] response = null;
			Socket server = null;

			using (var stream = new MemoryStream (buffer, 0, length, false)) {
				using (var reader = new StreamReader (stream)) {
					string line;

					while ((line = reader.ReadLine ()) != null) {
						if (string.IsNullOrEmpty (line))
							break;

						if (line.StartsWith ("CONNECT ", StringComparison.OrdinalIgnoreCase)) {
							int startIndex = "CONNECT ".Length;
							int index = startIndex;

							while (index < line.Length && line[index] != ':')
								index++;

							var host = line.Substring (startIndex, index - startIndex);
							startIndex = ++index;

							while (index < line.Length && line[index] != ' ')
								index++;

							var portStr = line.Substring (startIndex, index - startIndex);
							int port = int.Parse (portStr, CultureInfo.InvariantCulture);
							index++;

							// HTTP/X.Y
							var httpVersion = line.Substring (index);

							try {
								server = await SocketUtils.ConnectAsync (host, port, null, cancellationToken).ConfigureAwait (false);
								var remote = (IPEndPoint) server.RemoteEndPoint;

								response = Encoding.ASCII.GetBytes ($"HTTP/1.1 200 Connected to {remote}\r\n\r\n");
							} catch {
								response = Encoding.ASCII.GetBytes ("HTTP/1.1 404 Not Found\r\n\r\n");
							}

							break;
						}
					}
				}
			}

			if (response == null)
				return null;

			await client.WriteAsync (response, 0, response.Length, cancellationToken).ConfigureAwait (false);

			return server;
		}
	}
}
