//
// Socks4ProxyListener.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;

using MailKit.Net;

using NetworkStream = MailKit.Net.NetworkStream;

namespace UnitTests.Net.Proxy {
	class Socks4ProxyListener : ProxyListener
	{
		public Socks4ProxyListener ()
		{
		}

		enum Socks4Command : byte
		{
			Connect = 0x01,
			Bind    = 0x02,
		}

		enum Socks4Reply : byte
		{
			RequestGranted        = 0x5a,
			RequestRejected       = 0x5b,
			RequestFailedNoIdentd = 0x5c,
			RequestFailedWrongId  = 0x5d
		}

		static bool IsInvalidIPAddress (byte[] ip)
		{
			return ip[0] == 0 && ip[1] == 0 && ip[2] == 0 && ip[3] != 0;
		}

		static bool TryParse (byte[] request, int requestLength, out Socks4Command cmd, out int port, out IPAddress addr, out string user)
		{
			// +----+-----+----------+----------+----------+-------+--------------+-------+
			// |VER | CMD | DST.PORT | DST.ADDR |  USERID  | NULL  |  DST.DOMAIN  | NULL  |
			// +----+-----+----------+----------+----------+-------+--------------+-------+
			// | 1  |  1  |    2     |    4     | VARIABLE | X'00' |   VARIABLE   | X'00' |
			// +----+-----+----------+----------+----------+-------+--------------+-------+
			int n = 0;

			cmd = Socks4Command.Connect;
			addr = null;
			user = null;
			port = 0;

			if (requestLength < 9 || request[n++] != (byte) 4)
				return false;

			cmd = (Socks4Command) request[n++];
			if (cmd != Socks4Command.Connect && cmd != Socks4Command.Bind)
				return false;

			port = (request[n++] << 8) | request[n++];

			var ip = new byte[4];
			Buffer.BlockCopy (request, n, ip, 0, 4);
			n += 4;

			if (!IsInvalidIPAddress (ip))
				addr = new IPAddress (ip);

			var buffer = new byte[256];
			int index = 0;

			while (n < requestLength && index < buffer.Length && request[n] != 0)
				buffer[index++] = request[n++];

			if (n >= requestLength || index >= buffer.Length)
				return false;

			user = Encoding.UTF8.GetString (buffer, 0, index);
			n++;

			return addr != null;
		}

		static byte[] GetResponse (Socks4Reply reply, IPEndPoint server)
		{
			// +-----+-----+----------+----------+
			// | VER | REP | BND.PORT | BND.ADDR |
			// +-----+-----+----------+----------+
			// |  1  |  1  |    2     |    4     |
			// +-----+-----+----------+----------+
			var response = new byte[8];
			int n = 0;

			response[n++] = (byte) 4;
			response[n++] = (byte) reply;
			if (reply == Socks4Reply.RequestGranted) {
				var addr = server.Address;
				int port = server.Port;
				byte[] ip;

				if (addr.AddressFamily == AddressFamily.InterNetworkV6)
					addr = addr.MapToIPv4 ();

				ip = addr.GetAddressBytes ();

				response[n++] = (byte)(port >> 8);
				response[n++] = (byte) port;

				Buffer.BlockCopy (ip, 0, response, n, ip.Length);
			}

			return response;
		}

		protected override async Task<Socket> ClientCommandReceived (NetworkStream client, byte[] buffer, int length, CancellationToken cancellationToken)
		{
			Socket server = null;
			Socks4Command cmd;
			byte[] response;
			IPAddress ip;
			string user;
			int port;

			if (TryParse (buffer, length, out cmd, out port, out ip, out user)) {
				var host = ip.ToString ();

				try {
					server = await SocketUtils.ConnectAsync (host, port, null, true, cancellationToken).ConfigureAwait (false);
					var remote = (IPEndPoint) server.RemoteEndPoint;

					response = GetResponse (Socks4Reply.RequestGranted, remote);
				} catch {
					response = GetResponse (Socks4Reply.RequestRejected, null);
				}
			} else {
				response = GetResponse (Socks4Reply.RequestRejected, null);
			}

			await client.WriteAsync (response, 0, response.Length, cancellationToken).ConfigureAwait (false);

			return server;
		}
	}
}
