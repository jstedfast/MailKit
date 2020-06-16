//
// Socks5ProxyListener.cs
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
	class Socks5ProxyListener : ProxyListener
	{
		enum Socks5ListenerState
		{
			NegotiateAuthMethod,
			Authenticate,
			Command
		}

		readonly byte[] request = new byte[1024];
		Socks5ListenerState state;
		int requestLength;

		public Socks5ProxyListener ()
		{
		}

		enum Socks5AddressType : byte
		{
			IPv4   = 0x01,
			Domain = 0x03,
			IPv6   = 0x04
		}

		enum Socks5AuthMethod : byte
		{
			Anonymous    = 0x00,
			GSSAPI       = 0x01,
			UserPassword = 0x02,
			NotSupported = 0xff
		}

		enum Socks5Command : byte
		{
			None         = 0,
			Connect      = 0x01,
			Bind         = 0x02,
			UdpAssociate = 0x03,
		}

		enum Socks5Reply : byte
		{
			Success                 = 0x00,
			GeneralServerFailure    = 0x01,
			ConnectionNotAllowed    = 0x02,
			NetworkUnreachable      = 0x03,
			HostUnreachable         = 0x04,
			ConnectionRefused       = 0x05,
			TTLExpired              = 0x06,
			CommandNotSupported     = 0x07,
			AddressTypeNotSupported = 0x08
		}

		enum Socks5ParseResult
		{
			Success = 0,
			NotEnoughData,
			InvalidRequest,
			InvalidCommand,
			InvalidAddrType,
		}

		static Socks5ParseResult Parse (byte[] request, int requestLength, out Socks5AuthMethod[] methods)
		{
			methods = new Socks5AuthMethod[0];

			// +-----+----------+----------+
			// | VER | NMETHODS | METHODS  |
			// +-----+----------+----------+
			// |  1  |    1     | 1 to 255 |
			// +-----+----------+----------+
			if (requestLength < 1) 
				return Socks5ParseResult.NotEnoughData;

			if (request[0] != 0x05)
				return Socks5ParseResult.InvalidRequest;

			if (requestLength < 2)
				return Socks5ParseResult.NotEnoughData;

			int n = request[1];

			if (requestLength < 2 + n)
				return Socks5ParseResult.NotEnoughData;

			methods = new Socks5AuthMethod[n];
			for (int i = 0; i < n; i++)
				methods[i] = (Socks5AuthMethod) request[2 + i];

			return Socks5ParseResult.Success;
		}

		byte[] NegotiateAuthMethod ()
		{
			Socks5AuthMethod[] methods;
			Socks5AuthMethod method;

			var result = Parse (request, requestLength, out methods);
			if (result == Socks5ParseResult.NotEnoughData)
				return null;

			if (result == Socks5ParseResult.Success) {
				var anonymous = false;
				var userpass = false;

				for (int i = 0; i < methods.Length; i++) {
					if (methods[i] == Socks5AuthMethod.Anonymous)
						anonymous = true;
					else if (methods[i] == Socks5AuthMethod.UserPassword)
						userpass = true;
				}

				if (userpass) {
					method = Socks5AuthMethod.UserPassword;
					state = Socks5ListenerState.Authenticate;
				} else if (anonymous) {
					method = Socks5AuthMethod.Anonymous;
					state = Socks5ListenerState.Command;
				} else {
					method = Socks5AuthMethod.NotSupported;
				}
			} else {
				method = Socks5AuthMethod.NotSupported;
			}

			// +-----+--------+
			// | VER | METHOD |
			// +-----+--------+
			// |  1  |   1    |
			// +-----+--------+
			var response = new byte[2];
			response[0] = 0x05;
			response[1] = (byte) method;

			return response;
		}

		static Socks5ParseResult Parse (byte[] request, int requestLength, out string user, out string passwd)
		{
			user = passwd = null;

			// +-----+-------------+----------+-------------+----------+
			// | VER | USER.LENGTH | USERNAME | PASS.LENGTH | PASSWORD |
			// +-----+-------------+----------+-------------+----------+
			// |  1  |      1      | 1 to 255 |       1     | 1 to 255 |
			// +-----+-------------+----------+-------------+----------+
			if (requestLength < 1)
				return Socks5ParseResult.NotEnoughData;

			if (request[0] != 0x05)
				return Socks5ParseResult.InvalidRequest;

			if (requestLength < 2)
				return Socks5ParseResult.NotEnoughData;

			int userLength = request[1];

			if (userLength == 0)
				return Socks5ParseResult.InvalidRequest;

			if (requestLength < 3 + userLength)
				return Socks5ParseResult.NotEnoughData;

			user = Encoding.UTF8.GetString (request, 2, userLength);

			int passwdLength = request[2 + userLength];

			if (passwdLength == 0)
				return Socks5ParseResult.InvalidRequest;

			if (requestLength != 3 + userLength + passwdLength)
				return Socks5ParseResult.NotEnoughData;

			passwd = Encoding.UTF8.GetString (request, 3 + userLength, passwdLength);

			return Socks5ParseResult.Success;
		}

		byte[] Authenticate ()
		{
			string user, passwd;

			var result = Parse (request, requestLength, out user, out passwd);
			if (result == Socks5ParseResult.NotEnoughData)
				return null;

			// +-----+--------+
			// | VER | METHOD |
			// +-----+--------+
			// |  1  |   1    |
			// +-----+--------+
			var response = new byte[2];
			response[0] = 0x05;
			if (result == Socks5ParseResult.Success && user == "username" && passwd == "password") {
				response[1] = (byte) Socks5Reply.Success;
				state = Socks5ListenerState.Command;
			} else {
				response[1] = (byte) Socks5Reply.ConnectionNotAllowed;
				state = Socks5ListenerState.NegotiateAuthMethod;
			}

			return response;
		}

		static Socks5ParseResult Parse (byte[] request, int requestLength, out Socks5Command cmd, out string host, out int port)
		{
			cmd = Socks5Command.None;
			host = null;
			port = 0;

			// +----+-----+-------+------+----------+----------+
			// |VER | CMD |  RSV  | ATYP | DST.ADDR | DST.PORT |
			// +----+-----+-------+------+----------+----------+
			// | 1  |  1  | X'00' |  1   | Variable |    2     |
			// +----+-----+-------+------+----------+----------+
			int n = 0;

			if (requestLength < n + 1)
				return Socks5ParseResult.NotEnoughData;

			if (request[n++] != 0x05)
				return Socks5ParseResult.InvalidRequest;

			if (requestLength < n + 1)
				return Socks5ParseResult.NotEnoughData;

			cmd = (Socks5Command) request[n++];
			if (cmd != Socks5Command.Connect)
				return Socks5ParseResult.InvalidCommand;

			// skip over RSV byte
			if (requestLength < n + 1)
				return Socks5ParseResult.NotEnoughData;

			n++;

			if (requestLength < n + 1)
				return Socks5ParseResult.NotEnoughData;

			var addrType = (Socks5AddressType) request[n++];
			int addrLength;

			switch (addrType) {
			case Socks5AddressType.Domain:
				if (requestLength < n + 1)
					return Socks5ParseResult.NotEnoughData;

				addrLength = request[n++];
				break;
			case Socks5AddressType.IPv6:
				addrLength = 16;
				break;
			case Socks5AddressType.IPv4:
				addrLength = 4;
				break;
			default:
				return Socks5ParseResult.InvalidAddrType;
			}

			if (requestLength < n + addrLength + 2)
				return Socks5ParseResult.NotEnoughData;

			var addr = new byte[addrLength];
			Buffer.BlockCopy (request, n, addr, 0, addrLength);
			n += addrLength;

			if (addrType == Socks5AddressType.Domain) {
				host = Encoding.UTF8.GetString (addr);
			} else {
				var ip = new IPAddress (addr);
				host = ip.ToString ();
			}

			port = (request[n++] << 8) | request[n++];

			return Socks5ParseResult.Success;
		}

		static byte[] GetCommandResponse (Socks5Reply reply, IPEndPoint server)
		{
			// +-----+-----+-------+------+----------+----------+
			// | VER | REP |  RSV  | ATYP | BND.ADDR | BND.PORT |
			// +-----+-----+-------+------+----------+----------+
			// |  1  |  1  | X'00' |  1   | Variable |    2     |
			// +-----+-----+-------+------+----------+----------+
			var addr = server?.Address.GetAddressBytes ();
			var response = new byte[6 + (addr != null ? addr.Length : 0)];
			int port = server != null ? server.Port : 0;
			int n = 0;

			response[n++] = 0x05;
			response[n++] = (byte) reply;
			response[n++] = 0x00;
			if (server != null) {
				if (server.Address.AddressFamily == AddressFamily.InterNetworkV6)
					response[n++] = (byte) Socks5AddressType.IPv6;
				else
					response[n++] = (byte) Socks5AddressType.IPv4;
				Buffer.BlockCopy (addr, 0, response, n, addr.Length);
				n += addr.Length;
				response[n++] = (byte) (port >> 8);
				response[n++] = (byte) port;
			}

			return response;
		}

		static Socks5Reply GetReply (SocketError error)
		{
			switch (error) {
			case SocketError.AddressFamilyNotSupported: return Socks5Reply.AddressTypeNotSupported;
			case SocketError.ConnectionRefused: return Socks5Reply.ConnectionRefused;
			case SocketError.HostUnreachable: return Socks5Reply.HostUnreachable;
			case SocketError.NetworkUnreachable: return Socks5Reply.NetworkUnreachable;
			case SocketError.TimedOut: return Socks5Reply.TTLExpired;
			default: return Socks5Reply.ConnectionNotAllowed;
			}
		}

		static byte[] GetCommandResponse (SocketError error)
		{
			var reply = GetReply (error);

			return GetCommandResponse (reply, null);
		}

		protected override async Task<Socket> ClientCommandReceived (NetworkStream client, byte[] buffer, int length, CancellationToken cancellationToken)
		{
			byte[] response = null;
			Socket server = null;

			Buffer.BlockCopy (buffer, 0, request, requestLength, length);
			requestLength += length;

			switch (state) {
			case Socks5ListenerState.NegotiateAuthMethod:
				response = NegotiateAuthMethod ();
				break;
			case Socks5ListenerState.Authenticate:
				response = Authenticate ();
				break;
			case Socks5ListenerState.Command:
				var result = Parse (request, requestLength, out Socks5Command cmd, out string host, out int port);
				switch (result) {
				case Socks5ParseResult.Success:
					try {
						server = await SocketUtils.ConnectAsync (host, port, null, true, cancellationToken).ConfigureAwait (false);
						var remote = (IPEndPoint) server.RemoteEndPoint;

						response = GetCommandResponse (Socks5Reply.Success, remote);
					} catch (OperationCanceledException) {
						throw;
					} catch (SocketException ex) {
						response = GetCommandResponse (ex.SocketErrorCode);
					} catch {
						response = GetCommandResponse (Socks5Reply.GeneralServerFailure, null);
					}
					break;
				case Socks5ParseResult.InvalidAddrType:
					response = GetCommandResponse (Socks5Reply.AddressTypeNotSupported, null);
					break;
				case Socks5ParseResult.InvalidCommand:
					response = GetCommandResponse (Socks5Reply.CommandNotSupported, null);
					break;
				case Socks5ParseResult.NotEnoughData:
					response = null;
					break;
				case Socks5ParseResult.InvalidRequest:
				default:
					response = GetCommandResponse (Socks5Reply.GeneralServerFailure, null);
					break;
				}
				break;
			}

			if (response == null)
				return null;

			await client.WriteAsync (response, 0, response.Length, cancellationToken).ConfigureAwait (false);
			requestLength = 0;

			return server;
		}
	}
}
