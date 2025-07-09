﻿//
// MailService.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2025 .NET Foundation and Contributors
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
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using SslProtocols = System.Security.Authentication.SslProtocols;

using MailKit.Net;
using MailKit.Net.Proxy;
using MailKit.Security;

using NetworkStream = MailKit.Net.NetworkStream;

namespace MailKit {
	/// <summary>
	/// An abstract mail service implementation.
	/// </summary>
	/// <remarks>
	/// An abstract mail service implementation.
	/// </remarks>
	public abstract class MailService : IMailService
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MailService"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="MailKit.MailService"/> class.
		/// </remarks>
		/// <param name="protocolLogger">The protocol logger.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="protocolLogger"/> is <see langword="null" />.
		/// </exception>
		protected MailService (IProtocolLogger protocolLogger)
		{
			if (protocolLogger == null)
				throw new ArgumentNullException (nameof (protocolLogger));

			// Default the SslProtocols value to `None` which allows the operating system to choose the best
			// protocol to use and to block protocols that are not secure.
			SslProtocols = SslProtocols.None;
			CheckCertificateRevocation = true;
			ProtocolLogger = protocolLogger;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MailService"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="MailKit.MailService"/> class.
		/// </remarks>
		protected MailService () : this (new NullProtocolLogger ())
		{
		}

		/// <summary>
		/// Releases unmanaged resources and performs other cleanup operations before the
		/// <see cref="MailService"/> is reclaimed by garbage collection.
		/// </summary>
		/// <remarks>
		/// Releases unmanaged resources and performs other cleanup operations before the
		/// <see cref="MailService"/> is reclaimed by garbage collection.
		/// </remarks>
		~MailService ()
		{
			Dispose (false);
		}

		/// <summary>
		/// Get an object that can be used to synchronize access to the service.
		/// </summary>
		/// <remarks>
		/// <para>Gets an object that can be used to synchronize access to the service.</para>
		/// </remarks>
		/// <value>The sync root.</value>
		public abstract object SyncRoot {
			get;
		}

		/// <summary>
		/// Get the protocol supported by the message service.
		/// </summary>
		/// <remarks>
		/// Gets the protocol supported by the message service.
		/// </remarks>
		/// <value>The protocol.</value>
		protected abstract string Protocol {
			get;
		}

		/// <summary>
		/// Get the protocol logger.
		/// </summary>
		/// <remarks>
		/// Gets the protocol logger.
		/// </remarks>
		/// <value>The protocol logger.</value>
		public IProtocolLogger ProtocolLogger {
			get; private set;
		}

		/// <summary>
		/// Get or set the set of enabled SSL and/or TLS protocol versions that the client is allowed to use.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets the enabled SSL and/or TLS protocol versions that the client is allowed to use.</para>
		/// <para>By default, MailKit initializes this value to <see cref="SslProtocols.None"/> which allows the
		/// operating system to choose the best protocol to use and to block protocols that are not secure.</para>
		/// <note type="note">This property should be set before calling any of the
		/// <a href="Overload_MailKit_MailService_Connect.htm">Connect</a> or
		/// <a href="Overload_MailKit_MailService_ConnectAsync.htm">ConnectAsync</a> methods.</note>
		/// </remarks>
		/// <value>The SSL and TLS protocol versions that are enabled.</value>
		public SslProtocols SslProtocols {
			get; set;
		}

#if NET5_0_OR_GREATER
		/// <summary>
		/// Get or set the cipher suites allowed to be used when negotiating an SSL or TLS connection.
		/// </summary>
		/// <remarks>
		/// <para>Specifies the cipher suites allowed to be used when negotiating an SSL or TLS connection.
		/// When set to <see langword="null" />, the operating system default is used. Use extreme caution when
		/// changing this setting.</para>
		/// <note type="note">This property should be set before calling any of the
		/// <a href="Overload_MailKit_MailService_Connect.htm">Connect</a> or
		/// <a href="Overload_MailKit_MailService_ConnectAsync.htm">ConnectAsync</a> methods.</note>
		/// </remarks>
		/// <value>The cipher algorithms allowed for use when negotiating SSL or TLS encryption.</value>
		public CipherSuitesPolicy SslCipherSuitesPolicy {
			get; set;
		}

		/// <summary>
		/// Get the negotiated SSL or TLS cipher suite.
		/// </summary>
		/// <remarks>
		/// Gets the negotiated SSL or TLS cipher suite once an SSL or TLS connection has been made.
		/// </remarks>
		/// <value>The negotiated SSL or TLS cipher suite.</value>
		public abstract TlsCipherSuite? SslCipherSuite {
			get;
		}
#endif

		/// <summary>
		/// Get or set the client SSL certificates.
		/// </summary>
		/// <remarks>
		/// <para>Some servers may require the client SSL certificates in order
		/// to allow the user to connect.</para>
		/// <note type="note">This property should be set before calling any of the
		/// <a href="Overload_MailKit_MailService_Connect.htm">Connect</a> or
		/// <a href="Overload_MailKit_MailService_ConnectAsync.htm">ConnectAsync</a> methods.</note>
		/// </remarks>
		/// <value>The client SSL certificates.</value>
		public X509CertificateCollection ClientCertificates {
			get; set;
		}

		/// <summary>
		/// Get or set whether connecting via SSL/TLS should check certificate revocation.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets whether connecting via SSL/TLS should check certificate revocation.</para>
		/// <para>Normally, the value of this property should be set to <see langword="true" /> (the default) for security
		/// reasons, but there are times when it may be necessary to set it to <see langword="false" />.</para>
		/// <para>For example, most Certificate Authorities are probably pretty good at keeping their CRL and/or
		/// OCSP servers up 24/7, but occasionally they do go down or are otherwise unreachable due to other
		/// network problems between the client and the Certificate Authority. When this happens, it becomes
		/// impossible to check the revocation status of one or more of the certificates in the chain
		/// resulting in an <see cref="SslHandshakeException"/> being thrown in the
		/// <a href="Overload_MailKit_MailService_Connect.htm">Connect</a> method. If this becomes a problem,
		/// it may become desirable to set <see cref="CheckCertificateRevocation"/> to <see langword="false" />.</para>
		/// </remarks>
		/// <value><see langword="true" /> if certificate revocation should be checked; otherwise, <see langword="false" />.</value>
		public bool CheckCertificateRevocation {
			get; set;
		}

		/// <summary>
		/// Get or set a callback function to validate the server certificate.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets a callback function to validate the server certificate.</para>
		/// <note type="note">This property should be set before calling any of the
		/// <a href="Overload_MailKit_MailService_Connect.htm">Connect</a> or
		/// <a href="Overload_MailKit_MailService_ConnectAsync.htm">ConnectAsync</a> methods.</note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SslCertificateValidation.cs"/>
		/// </example>
		/// <value>The server certificate validation callback function.</value>
		public RemoteCertificateValidationCallback ServerCertificateValidationCallback {
			get; set;
		}

		/// <summary>
		/// Get or set the local IP end point to use when connecting to the remote host.
		/// </summary>
		/// <remarks>
		/// Gets or sets the local IP end point to use when connecting to the remote host.
		/// </remarks>
		/// <value>The local IP end point or <see langword="null" /> to use the default end point.</value>
		public IPEndPoint LocalEndPoint {
			get; set;
		}

		/// <summary>
		/// Get or set the proxy client to use when connecting to a remote host.
		/// </summary>
		/// <remarks>
		/// Gets or sets the proxy client to use when connecting to a remote host via any of the
		/// <a href="Overload_MailKit_MailService_Connect.htm">Connect</a> methods.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ProxyExamples.cs" region="ProxyClient" />
		/// </example>
		/// <value>The proxy client.</value>
		public IProxyClient ProxyClient {
			get; set;
		}

		/// <summary>
		/// Gets the authentication mechanisms supported by the mail server.
		/// </summary>
		/// <remarks>
		/// The authentication mechanisms are queried as part of the
		/// <a href="Overload_MailKit_MailService_Connect.htm">Connect</a> method.
		/// </remarks>
		/// <value>The authentication mechanisms.</value>
		public abstract HashSet<string> AuthenticationMechanisms {
			get;
		}

		/// <summary>
		/// Gets whether or not the client is currently connected to an mail server.
		/// </summary>
		/// <remarks>
		///<para>The <see cref="IsConnected"/> state is set to <see langword="true" /> immediately after
		/// one of the <a href="Overload_MailKit_MailService_Connect.htm">Connect</a>
		/// methods succeeds and is not set back to <see langword="false" /> until either the client
		/// is disconnected via <see cref="Disconnect(bool,CancellationToken)"/> or until a
		/// <see cref="ProtocolException"/> is thrown while attempting to read or write to
		/// the underlying network socket.</para>
		/// <para>When an <see cref="ProtocolException"/> is caught, the connection state of the
		/// <see cref="MailService"/> should be checked before continuing.</para>
		/// </remarks>
		/// <value><see langword="true" /> if the client is connected; otherwise, <see langword="false" />.</value>
		public abstract bool IsConnected {
			get;
		}

		/// <summary>
		/// Get whether or not the connection is secure (typically via SSL or TLS).
		/// </summary>
		/// <remarks>
		/// Gets whether or not the connection is secure (typically via SSL or TLS).
		/// </remarks>
		/// <value><see langword="true" /> if the connection is secure; otherwise, <see langword="false" />.</value>
		public abstract bool IsSecure {
			get;
		}

		/// <summary>
		/// Get whether or not the connection is encrypted (typically via SSL or TLS).
		/// </summary>
		/// <remarks>
		/// Gets whether or not the connection is encrypted (typically via SSL or TLS).
		/// </remarks>
		/// <value><see langword="true" /> if the connection is encrypted; otherwise, <see langword="false" />.</value>
		public abstract bool IsEncrypted {
			get;
		}

		/// <summary>
		/// Get whether or not the connection is signed (typically via SSL or TLS).
		/// </summary>
		/// <remarks>
		/// Gets whether or not the connection is signed (typically via SSL or TLS).
		/// </remarks>
		/// <value><see langword="true" /> if the connection is signed; otherwise, <see langword="false" />.</value>
		public abstract bool IsSigned {
			get;
		}

		/// <summary>
		/// Get the negotiated SSL or TLS protocol version.
		/// </summary>
		/// <remarks>
		/// <para>Gets the negotiated SSL or TLS protocol version once an SSL or TLS connection has been made.</para>
		/// </remarks>
		/// <value>The negotiated SSL or TLS protocol version.</value>
		public abstract SslProtocols SslProtocol {
			get;
		}

		/// <summary>
		/// Get the negotiated SSL or TLS cipher algorithm.
		/// </summary>
		/// <remarks>
		/// Gets the negotiated SSL or TLS cipher algorithm once an SSL or TLS connection has been made.
		/// </remarks>
		/// <value>The negotiated SSL or TLS cipher algorithm.</value>
		public abstract CipherAlgorithmType? SslCipherAlgorithm {
			get;
		}

		/// <summary>
		/// Get the negotiated SSL or TLS cipher algorithm strength.
		/// </summary>
		/// <remarks>
		/// Gets the negotiated SSL or TLS cipher algorithm strength once an SSL or TLS connection has been made.
		/// </remarks>
		/// <value>The negotiated SSL or TLS cipher algorithm strength.</value>
		public abstract int? SslCipherStrength {
			get;
		}

		/// <summary>
		/// Get the negotiated SSL or TLS hash algorithm.
		/// </summary>
		/// <remarks>
		/// Gets the negotiated SSL or TLS hash algorithm once an SSL or TLS connection has been made.
		/// </remarks>
		/// <value>The negotiated SSL or TLS hash algorithm.</value>
		public abstract HashAlgorithmType? SslHashAlgorithm {
			get;
		}

		/// <summary>
		/// Get the negotiated SSL or TLS hash algorithm strength.
		/// </summary>
		/// <remarks>
		/// Gets the negotiated SSL or TLS hash algorithm strength once an SSL or TLS connection has been made.
		/// </remarks>
		/// <value>The negotiated SSL or TLS hash algorithm strength.</value>
		public abstract int? SslHashStrength {
			get;
		}

		/// <summary>
		/// Get the negotiated SSL or TLS key exchange algorithm.
		/// </summary>
		/// <remarks>
		/// Gets the negotiated SSL or TLS key exchange algorithm once an SSL or TLS connection has been made.
		/// </remarks>
		/// <value>The negotiated SSL or TLS key exchange algorithm.</value>
		public abstract ExchangeAlgorithmType? SslKeyExchangeAlgorithm {
			get;
		}

		/// <summary>
		/// Get the negotiated SSL or TLS key exchange algorithm strength.
		/// </summary>
		/// <remarks>
		/// Gets the negotiated SSL or TLS key exchange algorithm strength once an SSL or TLS connection has been made.
		/// </remarks>
		/// <value>The negotiated SSL or TLS key exchange algorithm strength.</value>
		public abstract int? SslKeyExchangeStrength {
			get;
		}

		/// <summary>
		/// Get whether or not the client is currently authenticated with the mail server.
		/// </summary>
		/// <remarks>
		/// <para>Gets whether or not the client is currently authenticated with the mail server.</para>
		/// <para>To authenticate with the mail server, use one of the
		/// <a href="Overload_MailKit_MailService_Authenticate.htm">Authenticate</a> methods
		/// or any of the Async alternatives.</para>
		/// </remarks>
		/// <value><see langword="true" /> if the client is authenticated; otherwise, <see langword="false" />.</value>
		public abstract bool IsAuthenticated {
			get;
		}

		/// <summary>
		/// Gets or sets the timeout for network streaming operations, in milliseconds.
		/// </summary>
		/// <remarks>
		/// Gets or sets the underlying socket stream's <see cref="System.IO.Stream.ReadTimeout"/>
		/// and <see cref="System.IO.Stream.WriteTimeout"/> values.
		/// </remarks>
		/// <value>The timeout in milliseconds.</value>
		public abstract int Timeout {
			get; set;
		}

		const string AppleCertificateIssuer = "C=US, S=California, O=Apple Inc., CN=Apple Public Server RSA CA 11 - G1";
		const string GMailCertificateIssuer = "CN=WR2, O=Google Trust Services, C=US";
		const string OutlookCertificateIssuer = "CN=DigiCert Cloud Services CA-1, O=DigiCert Inc, C=US";
		const string LegacyYahooCertificateIssuer = "CN=DigiCert Global G2 TLS RSA SHA256 2020 CA1, O=DigiCert Inc, C=US";
		const string YahooImapCertificateIssuer = "CN=DigiCert SHA2 High Assurance Server CA, OU=www.digicert.com, O=DigiCert Inc, C=US";
		const string YahooSmtpCertificateIssuer = "CN=DigiCert Global G2 TLS RSA SHA256 2020 CA1, O=DigiCert Inc, C=US";
		const string GmxDotComCertificateIssuer = "CN=Sectigo RSA Organization Validation Secure Server CA, O=Sectigo Limited, L=Salford, S=Greater Manchester, C=GB";
		const string GmxDotNetCertificateIssuer = "CN=Telekom Security ServerID OV Class 2 CA, O=Deutsche Telekom Security GmbH, C=DE";

		// Note: This method auto-generated by https://gist.github.com/jstedfast/7cd36a51cee740ed84b18435106eaea5
		internal static bool IsKnownMailServerCertificate (X509Certificate2 certificate)
		{
			var cn = certificate.GetNameInfo (X509NameType.SimpleName, false);
			var fingerprint = certificate.Thumbprint;
			var serial = certificate.SerialNumber;
			var issuer = certificate.Issuer;

			switch (cn) {
			case "imap.gmail.com":
				switch (issuer) {
				case GMailCertificateIssuer:
					return (serial == "00D9367C2844571EE7105B621EF66F9945" && fingerprint == "CC7B019ED9E23970556C58187B9FC530F1530CC0") // Expires 7/14/2025 4:41:44 AM
						|| (serial == "5DD42BF4A7835A1F12C0230E711EEFC5" && fingerprint == "7221945A9E8F6AEB952792CA26003F0DB050DAB4") // Expires 7/22/2025 3:29:13 PM
						|| (serial == "590C74431E805B5B12539CDEACCA8612" && fingerprint == "40EF2C516811F8DCA23780D521FA7E57E378E2E9"); // Expires 8/25/2025 4:36:32 AM
				default:
					return false;
				}
			case "pop.gmail.com":
				switch (issuer) {
				case GMailCertificateIssuer:
					return (serial == "00B1353E4AC219849D10DC2EA4E9975E40" && fingerprint == "28A08A0C8B3B703D11C9AB3576AE720E1A076967") // Expires 7/14/2025 4:41:45 AM
						|| (serial == "008C6181902132E6FB0A0F5414D0DCEF99" && fingerprint == "6B35E29ADAD1FC21171CA1A8105C19EA6BEAB988") // Expires 7/22/2025 3:29:14 PM
						|| (serial == "4FC61DA48A42815D0967F223AB5D1A8D" && fingerprint == "B20D09B86CDA299A6BCC7D8201F3650B4282439F"); // Expires 8/25/2025 4:36:33 AM
				default:
					return false;
				}
			case "smtp.gmail.com":
				switch (issuer) {
				case GMailCertificateIssuer:
					return (serial == "00EE7BE01915BBD5BF107AC9B57B42421F" && fingerprint == "C60FA1FC3C2A32B924A591C6913324246EC09077") // Expires 7/14/2025 4:41:46 AM
						|| (serial == "2CF105575867D1C80ABFD71028077642" && fingerprint == "7EDE669DBAA017B4AB2BB0EB3AD0C8BE5A3EB475") // Expires 7/22/2025 3:29:15 PM
						|| (serial == "3569A27A689C6D4E10477112F0A205C2" && fingerprint == "AFF82D7112B27E3E95C42B442CD0D049D6C5B3B0"); // Expires 8/25/2025 4:36:34 AM
				default:
					return false;
				}
			case "outlook.com":
				switch (issuer) {
				case OutlookCertificateIssuer:
					return (serial == "0ECDAB152D2161F7C843D25F3F00FCDE" && fingerprint == "891A3E1C8074F9BCFD9BE2A05CB6FDB789032DE2") // Expires 6/26/2025 7:59:59 PM
						|| (serial == "07ECFAB580E06830E3EC580E3C1D4765" && fingerprint == "A6F7ECFB2BF631B3A84FEBB09FFDBB4E3B0F4211"); // Expires 3/28/2026 7:59:59 PM
				default:
					return false;
				}
			case "imap.mail.me.com":
				switch (issuer) {
				case AppleCertificateIssuer:
					return (serial == "53EAE7751B02B72138935D30BF550930" && fingerprint == "BF811C09DE8E8CC2004D48256351EE564C3D403E"); // Expires 8/20/2025 2:44:05 PM
				default:
					return false;
				}
			case "smtp.mail.me.com":
				switch (issuer) {
				case AppleCertificateIssuer:
					return (serial == "25B1CED1224CC151B02E0BCE0914FD2F" && fingerprint == "F159295A3EF8CCE6688B749E9E164D82CBDF63EE"); // Expires 9/3/2025 1:00:41 PM
				default:
					return false;
				}
			case "*.imap.mail.yahoo.com":
				switch (issuer) {
				case YahooImapCertificateIssuer:
					return (serial == "07044A69372807CB6259DB41B639DE1F" && fingerprint == "4227BA6BE7ED2A9AE7100563FD06F23775A078E9") // Expires 6/4/2025 7:59:59 PM
						|| (serial == "09C641C2C225697C08C5AF943A9CB506" && fingerprint == "8FA4BF6C8F434F0B2FBFE1FC2AA1D90AF9BD0525"); // Expires 10/29/2025 7:59:59 PM
				default:
					return false;
				}
			case "legacy.pop.mail.yahoo.com":
				switch (issuer) {
				case LegacyYahooCertificateIssuer:
					return (serial == "0FA8BA4B78577BBF220019EA07377C48" && fingerprint == "44648D48B6710FAB46959A7DF92812F2F59B2770"); // Expires 6/18/2025 7:59:59 PM
				case YahooImapCertificateIssuer:
					return (serial == "06B724155C88588D53D5FEAA612CBAC4" && fingerprint == "B3CFC90123BB0AAE08ABE9F1E46EF0007295548E"); // Expires 11/12/2025 6:59:59 PM
				default:
					return false;
				}
			case "smtp.mail.yahoo.com":
				switch (issuer) {
				case YahooSmtpCertificateIssuer:
					return (serial == "057189FA9C1D4C01E4020622AAFB54AB" && fingerprint == "EFD6A9EDB1265E4A9709D55966513F7D1AF54D9E"); // Expires 6/18/2025 7:59:59 PM
				case YahooImapCertificateIssuer:
					return (serial == "0F81BE58A5DF10692DD94FFDF27AFD0D" && fingerprint == "DB880030565F5680AACFF069C0C4152CF128F909"); // Expires 11/12/2025 6:59:59 PM
				default:
					return false;
				}
			case "mout.gmx.com":
				return issuer == GmxDotComCertificateIssuer && serial == "49F9B6205B93B1A9DCEC50C54192A0A5" && fingerprint == "34DC8F699802DC1FAE824560C27E985020ACCFCC"; // Expires 5/5/2026 7:59:59 PM
			case "mail.gmx.net":
				return issuer == GmxDotNetCertificateIssuer && serial == "3DF168E2174005F4FDC72F92FBEFD952" && fingerprint == "EDB2B7B7E9C624DF221ECDC46A7946D003F90ADE"; // Expires 1/24/2026 6:59:59 PM
			default:
				return false;
			}
		}

		static bool IsUntrustedRoot (X509Chain chain)
		{
			foreach (var status in chain.ChainStatus) {
				if (status.Status == X509ChainStatusFlags.NoError || status.Status == X509ChainStatusFlags.UntrustedRoot)
					continue;

				return false;
			}

			return true;
		}

		/// <summary>
		/// The default server certificate validation callback used when connecting via SSL or TLS.
		/// </summary>
		/// <remarks>
		/// <para>The default server certificate validation callback recognizes and accepts the certificates
		/// for a list of commonly used mail servers such as gmail.com, outlook.com, mail.me.com, yahoo.com,
		/// and gmx.net.</para>
		/// </remarks>
		/// <returns><see langword="true" /> if the certificate is deemed valid; otherwise, <see langword="false" />.</returns>
		/// <param name="sender">The object that is connecting via SSL or TLS.</param>
		/// <param name="certificate">The server's SSL certificate.</param>
		/// <param name="chain">The server's SSL certificate chain.</param>
		/// <param name="sslPolicyErrors">The SSL policy errors.</param>
		protected static bool DefaultServerCertificateValidationCallback (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			const SslPolicyErrors mask = SslPolicyErrors.RemoteCertificateNotAvailable | SslPolicyErrors.RemoteCertificateNameMismatch;

			if (sslPolicyErrors == SslPolicyErrors.None)
				return true;

			if ((sslPolicyErrors & mask) == 0) {
				// At this point, all that is left is SslPolicyErrors.RemoteCertificateChainErrors

				// If the problem is an untrusted root, then compare the certificate to a list of known mail server certificates.
				if (IsUntrustedRoot (chain) && certificate is X509Certificate2 certificate2) {
					if (IsKnownMailServerCertificate (certificate2) && DateTime.Now <= certificate2.NotAfter)
						return true;
				}
			}

			return false;
		}

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
		/// <summary>
		/// Gets the SSL/TLS client authentication options for use with .NET5's SslStream.AuthenticateAsClient() API.
		/// </summary>
		/// <remarks>
		/// Gets the SSL/TLS client authentication options for use with .NET5's SslStream.AuthenticateAsClient() API.
		/// </remarks>
		/// <param name="host">The target host that the client is connected to.</param>
		/// <param name="remoteCertificateValidationCallback">The remote certificate validation callback.</param>
		/// <returns>The client SSL/TLS authentication options.</returns>
		protected virtual SslClientAuthenticationOptions GetSslClientAuthenticationOptions (string host, RemoteCertificateValidationCallback remoteCertificateValidationCallback)
		{
			return new SslClientAuthenticationOptions {
				CertificateRevocationCheckMode = CheckCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck,
				// Note: Not all servers support Application Protocols, so this will break in some cases.
				//ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol (Protocol) },
				RemoteCertificateValidationCallback = remoteCertificateValidationCallback,
#if NET5_0_OR_GREATER
				CipherSuitesPolicy = SslCipherSuitesPolicy,
#endif
				ClientCertificates = ClientCertificates,
				EnabledSslProtocols = SslProtocols,
				TargetHost = host
			};
		}
#endif

		internal Stream ConnectNetwork (string host, int port, CancellationToken cancellationToken)
		{
			if (ProxyClient != null) {
				ProxyClient.LocalEndPoint = LocalEndPoint;

				return ProxyClient.Connect (host, port, Timeout, cancellationToken);
			}

			var socket = SocketUtils.Connect (host, port, LocalEndPoint, Timeout, cancellationToken);

			return new NetworkStream (socket, true);
		}

		internal async Task<Stream> ConnectNetworkAsync (string host, int port, CancellationToken cancellationToken)
		{
			if (ProxyClient != null) {
				ProxyClient.LocalEndPoint = LocalEndPoint;

				return await ProxyClient.ConnectAsync (host, port, Timeout, cancellationToken).ConfigureAwait (false);
			}

			var socket = await SocketUtils.ConnectAsync (host, port, LocalEndPoint, Timeout, cancellationToken).ConfigureAwait (false);

			return new NetworkStream (socket, true);
		}

		/// <summary>
		/// Establish a connection to the specified mail server.
		/// </summary>
		/// <remarks>
		/// Establishes a connection to the specified mail server.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessage"/>
		/// </example>
		/// <param name="host">The host name to connect to.</param>
		/// <param name="port">The port to connect to. If the specified port is <c>0</c>, then the default port will be used.</param>
		/// <param name="options">The secure socket options to when connecting.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="host"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>0</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is already connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.Net.Sockets.SocketException">
		/// A socket error occurred trying to connect to the remote host.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract void Connect (string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default);

		/// <summary>
		/// Asynchronously establish a connection to the specified mail server.
		/// </summary>
		/// <remarks>
		/// Asynchronously establishes a connection to the specified mail server.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="host">The host name to connect to.</param>
		/// <param name="port">The port to connect to. If the specified port is <c>0</c>, then the default port will be used.</param>
		/// <param name="options">The secure socket options to when connecting.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="host"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>0</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is already connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.Net.Sockets.SocketException">
		/// A socket error occurred trying to connect to the remote host.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract Task ConnectAsync (string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default);

		/// <summary>
		/// Establish a connection to the specified mail server using the provided socket.
		/// </summary>
		/// <remarks>
		/// <para>Establish a connection to the specified mail server using the provided socket.</para>
		/// <para>If a successful connection is made, the <see cref="AuthenticationMechanisms"/>
		/// property will be populated.</para>
		/// </remarks>
		/// <param name="socket">The socket to use for the connection.</param>
		/// <param name="host">The host name to connect to.</param>
		/// <param name="port">The port to connect to. If the specified port is <c>0</c>, then the default port will be used.</param>
		/// <param name="options">The secure socket options to when connecting.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="socket"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="host"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>0</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="socket"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <paramref name="host"/> is a zero-length string.</para>
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="IMailService"/> is already connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command was rejected by the mail server.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public abstract void Connect (Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default);

		/// <summary>
		/// Asynchronously establish a connection to the specified mail server using the provided socket.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously establishes a connection to the specified mail server using the provided socket.</para>
		/// <para>If a successful connection is made, the <see cref="AuthenticationMechanisms"/>
		/// property will be populated.</para>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="socket">The socket to use for the connection.</param>
		/// <param name="host">The host name to connect to.</param>
		/// <param name="port">The port to connect to. If the specified port is <c>0</c>, then the default port will be used.</param>
		/// <param name="options">The secure socket options to when connecting.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="socket"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="host"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>0</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="socket"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <paramref name="host"/> is a zero-length string.</para>
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="IMailService"/> is already connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command was rejected by the mail server.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public abstract Task ConnectAsync (Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default);

		/// <summary>
		/// Establish a connection to the specified mail server using the provided stream.
		/// </summary>
		/// <remarks>
		/// <para>Establish a connection to the specified mail server using the provided stream.</para>
		/// <para>If a successful connection is made, the <see cref="AuthenticationMechanisms"/>
		/// property will be populated.</para>
		/// </remarks>
		/// <param name="stream">The stream to use for the connection.</param>
		/// <param name="host">The host name to connect to.</param>
		/// <param name="port">The port to connect to. If the specified port is <c>0</c>, then the default port will be used.</param>
		/// <param name="options">The secure socket options to when connecting.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="stream"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="host"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>0</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="IMailService"/> is already connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command was rejected by the mail server.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public abstract void Connect (Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default);

		/// <summary>
		/// Asynchronously establish a connection to the specified mail server using the provided stream.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously establishes a connection to the specified mail server using the provided stream.</para>
		/// <para>If a successful connection is made, the <see cref="AuthenticationMechanisms"/>
		/// property will be populated.</para>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="stream">The stream to use for the connection.</param>
		/// <param name="host">The host name to connect to.</param>
		/// <param name="port">The port to connect to. If the specified port is <c>0</c>, then the default port will be used.</param>
		/// <param name="options">The secure socket options to when connecting.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="stream"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="host"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>0</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="IMailService"/> is already connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command was rejected by the mail server.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public abstract Task ConnectAsync (Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default);

		static bool IsAny (string value, params string[] anyOf)
		{
			foreach (var item in anyOf) {
				if (value.Equals (item, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		internal SecureSocketOptions GetSecureSocketOptions (Uri uri)
		{
			var query = uri.ParsedQuery ();
			var protocol = uri.Scheme;

			// Note: early versions of MailKit used "pop3" and "pop3s"
			if (protocol.Equals ("pop3s", StringComparison.OrdinalIgnoreCase))
				protocol = "pops";
			else if (protocol.Equals ("pop3", StringComparison.OrdinalIgnoreCase))
				protocol = "pop";

			if (protocol.Equals (Protocol + "s", StringComparison.OrdinalIgnoreCase))
				return SecureSocketOptions.SslOnConnect;

			if (!protocol.Equals (Protocol, StringComparison.OrdinalIgnoreCase))
				throw new ArgumentException ("Unknown URI scheme.", nameof (uri));

			if (query.TryGetValue ("starttls", out string value)) {
				if (IsAny (value, "always", "true", "yes"))
					return SecureSocketOptions.StartTls;

				if (IsAny (value, "never", "false", "no"))
					return SecureSocketOptions.None;

				return SecureSocketOptions.StartTlsWhenAvailable;
			}

			return SecureSocketOptions.StartTlsWhenAvailable;
		}

		/// <summary>
		/// Establish a connection to the specified mail server.
		/// </summary>
		/// <remarks>
		/// Establishes a connection to the specified mail server.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessageUri"/>
		/// </example>
		/// <param name="uri">The server URI.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// The <paramref name="uri"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="uri"/> is not an absolute URI.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is already connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.Net.Sockets.SocketException">
		/// A socket error occurred trying to connect to the remote host.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public void Connect (Uri uri, CancellationToken cancellationToken = default)
		{
			if (uri == null)
				throw new ArgumentNullException (nameof (uri));

			if (!uri.IsAbsoluteUri)
				throw new ArgumentException ("The uri must be absolute.", nameof (uri));

			var options = GetSecureSocketOptions (uri);

			Connect (uri.Host, uri.Port < 0 ? 0 : uri.Port, options, cancellationToken);
		}

		/// <summary>
		/// Asynchronously establish a connection to the specified mail server.
		/// </summary>
		/// <remarks>
		/// Asynchronously establishes a connection to the specified mail server.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="uri">The server URI.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// The <paramref name="uri"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="uri"/> is not an absolute URI.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is already connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.Net.Sockets.SocketException">
		/// A socket error occurred trying to connect to the remote host.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public Task ConnectAsync (Uri uri, CancellationToken cancellationToken = default)
		{
			if (uri == null)
				throw new ArgumentNullException (nameof (uri));

			if (!uri.IsAbsoluteUri)
				throw new ArgumentException ("The uri must be absolute.", nameof (uri));

			var options = GetSecureSocketOptions (uri);

			return ConnectAsync (uri.Host, uri.Port < 0 ? 0 : uri.Port, options, cancellationToken);
		}

		/// <summary>
		/// Establish a connection to the specified mail server.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to the specified mail server.</para>
		/// <note type="note">
		/// <para>The <paramref name="useSsl"/> argument only controls whether or
		/// not the client makes an SSL-wrapped connection. In other words, even if the
		/// <paramref name="useSsl"/> parameter is <see langword="false" />, SSL/TLS may still be used if
		/// the mail server supports the STARTTLS extension.</para>
		/// <para>To disable all use of SSL/TLS, use the
		/// <see cref="Connect(string,int,MailKit.Security.SecureSocketOptions,System.Threading.CancellationToken)"/>
		/// overload with a value of
		/// <see cref="MailKit.Security.SecureSocketOptions.None">SecureSocketOptions.None</see>
		/// instead.</para>
		/// </note>
		/// </remarks>
		/// <param name="host">The host to connect to.</param>
		/// <param name="port">The port to connect to. If the specified port is <c>0</c>, then the default port will be used.</param>
		/// <param name="useSsl"><see langword="true" /> if the client should make an SSL-wrapped connection to the server; otherwise, <see langword="false" />.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// The <paramref name="host"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is out of range (<value>0</value> to <value>65535</value>, inclusive).
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is already connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.Net.Sockets.SocketException">
		/// A socket error occurred trying to connect to the remote host.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public void Connect (string host, int port, bool useSsl, CancellationToken cancellationToken = default)
		{
			if (host == null)
				throw new ArgumentNullException (nameof (host));

			if (host.Length == 0)
				throw new ArgumentException ("The host name cannot be empty.", nameof (host));

			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException (nameof (port));

			Connect (host, port, useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
		}

		/// <summary>
		/// Asynchronously establish a connection to the specified mail server.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously establishes a connection to the specified mail server.</para>
		/// <note type="note">
		/// <para>The <paramref name="useSsl"/> argument only controls whether or
		/// not the client makes an SSL-wrapped connection. In other words, even if the
		/// <paramref name="useSsl"/> parameter is <see langword="false" />, SSL/TLS may still be used if
		/// the mail server supports the STARTTLS extension.</para>
		/// <para>To disable all use of SSL/TLS, use the
		/// <see cref="ConnectAsync(string,int,MailKit.Security.SecureSocketOptions,System.Threading.CancellationToken)"/>
		/// overload with a value of
		/// <see cref="MailKit.Security.SecureSocketOptions.None">SecureSocketOptions.None</see>
		/// instead.</para>
		/// </note>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="host">The host to connect to.</param>
		/// <param name="port">The port to connect to. If the specified port is <c>0</c>, then the default port will be used.</param>
		/// <param name="useSsl"><see langword="true" /> if the client should make an SSL-wrapped connection to the server; otherwise, <see langword="false" />.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// The <paramref name="host"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is out of range (<value>0</value> to <value>65535</value>, inclusive).
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is already connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.Net.Sockets.SocketException">
		/// A socket error occurred trying to connect to the remote host.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public Task ConnectAsync (string host, int port, bool useSsl, CancellationToken cancellationToken = default)
		{
			if (host == null)
				throw new ArgumentNullException (nameof (host));

			if (host.Length == 0)
				throw new ArgumentException ("The host name cannot be empty.", nameof (host));

			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException (nameof (port));

			return ConnectAsync (host, port, useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
		}

		/// <summary>
		/// Authenticate using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>Authenticates using the supplied credentials.</para>
		/// <para>If the server supports one or more SASL authentication mechanisms, then
		/// the SASL mechanisms that both the client and server support (not including any
		/// OAUTH mechanisms) are tried in order of greatest security to weakest security.
		/// Once a SASL authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <param name="encoding">The encoding to use for the user's credentials.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="encoding"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="MailKit.Security.AuthenticationException">
		/// Authentication using the supplied credentials has failed.
		/// </exception>
		/// <exception cref="MailKit.Security.SaslException">
		/// A SASL authentication error occurred.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract void Authenticate (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default);

		/// <summary>
		/// Asynchronously authenticate using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously authenticates using the supplied credentials.</para>
		/// <para>If the server supports one or more SASL authentication mechanisms, then
		/// the SASL mechanisms that both the client and server support (not including any
		/// OAUTH mechanisms) are tried in order of greatest security to weakest security.
		/// Once a SASL authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="encoding">The encoding to use for the user's credentials.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="encoding"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="MailKit.Security.AuthenticationException">
		/// Authentication using the supplied credentials has failed.
		/// </exception>
		/// <exception cref="MailKit.Security.SaslException">
		/// A SASL authentication error occurred.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract Task AuthenticateAsync (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default);

		/// <summary>
		/// Authenticate using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>Authenticates using the supplied credentials.</para>
		/// <para>If the server supports one or more SASL authentication mechanisms, then
		/// the SASL mechanisms that both the client and server support (not including any
		/// OAUTH mechanisms) are tried in order of greatest security to weakest security.
		/// Once a SASL authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="MailKit.Security.AuthenticationException">
		/// Authentication using the supplied credentials has failed.
		/// </exception>
		/// <exception cref="MailKit.Security.SaslException">
		/// A SASL authentication error occurred.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public void Authenticate (ICredentials credentials, CancellationToken cancellationToken = default)
		{
			Authenticate (Encoding.UTF8, credentials, cancellationToken);
		}

		/// <summary>
		/// Asynchronously authenticate using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously authenticates using the supplied credentials.</para>
		/// <para>If the server supports one or more SASL authentication mechanisms, then
		/// the SASL mechanisms that both the client and server support (not including any
		/// OAUTH mechanisms) are tried in order of greatest security to weakest security.
		/// Once a SASL authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="MailKit.Security.AuthenticationException">
		/// Authentication using the supplied credentials has failed.
		/// </exception>
		/// <exception cref="MailKit.Security.SaslException">
		/// A SASL authentication error occurred.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public Task AuthenticateAsync (ICredentials credentials, CancellationToken cancellationToken = default)
		{
			return AuthenticateAsync (Encoding.UTF8, credentials, cancellationToken);
		}

		/// <summary>
		/// Authenticate using the specified user name and password.
		/// </summary>
		/// <remarks>
		/// <para>Authenticates using the supplied credentials.</para>
		/// <para>If the server supports one or more SASL authentication mechanisms, then
		/// the SASL mechanisms that both the client and server support (not including any
		/// OAUTH mechanisms) are tried in order of greatest security to weakest security.
		/// Once a SASL authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <param name="encoding">The encoding to use for the user's credentials.</param>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="encoding"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="userName"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="MailKit.Security.AuthenticationException">
		/// Authentication using the supplied credentials has failed.
		/// </exception>
		/// <exception cref="MailKit.Security.SaslException">
		/// A SASL authentication error occurred.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public void Authenticate (Encoding encoding, string userName, string password, CancellationToken cancellationToken = default)
		{
			if (encoding == null)
				throw new ArgumentNullException (nameof (encoding));

			if (userName == null)
				throw new ArgumentNullException (nameof (userName));

			if (password == null)
				throw new ArgumentNullException (nameof (password));

			var credentials = new NetworkCredential (userName, password);

			Authenticate (encoding, credentials, cancellationToken);
		}

		/// <summary>
		/// Asynchronously authenticate using the specified user name and password.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously authenticates using the supplied credentials.</para>
		/// <para>If the server supports one or more SASL authentication mechanisms, then
		/// the SASL mechanisms that both the client and server support (not including any
		/// OAUTH mechanisms) are tried in order of greatest security to weakest security.
		/// Once a SASL authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="encoding">The encoding to use for the user's credentials.</param>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="encoding"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="userName"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="MailKit.Security.AuthenticationException">
		/// Authentication using the supplied credentials has failed.
		/// </exception>
		/// <exception cref="MailKit.Security.SaslException">
		/// A SASL authentication error occurred.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public Task AuthenticateAsync (Encoding encoding, string userName, string password, CancellationToken cancellationToken = default)
		{
			if (encoding == null)
				throw new ArgumentNullException (nameof (encoding));

			if (userName == null)
				throw new ArgumentNullException (nameof (userName));

			if (password == null)
				throw new ArgumentNullException (nameof (password));

			var credentials = new NetworkCredential (userName, password);

			return AuthenticateAsync (encoding, credentials, cancellationToken);
		}

		/// <summary>
		/// Authenticate using the specified user name and password.
		/// </summary>
		/// <remarks>
		/// <para>Authenticates using the supplied credentials.</para>
		///<para>If the server supports one or more SASL authentication mechanisms, then
		/// the SASL mechanisms that both the client and server support (not including any
		/// OAUTH mechanisms) are tried in order of greatest security to weakest security.
		/// Once a SASL authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessage"/>
		/// </example>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="MailKit.Security.AuthenticationException">
		/// Authentication using the supplied credentials has failed.
		/// </exception>
		/// <exception cref="MailKit.Security.SaslException">
		/// A SASL authentication error occurred.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public void Authenticate (string userName, string password, CancellationToken cancellationToken = default)
		{
			Authenticate (Encoding.UTF8, userName, password, cancellationToken);
		}

		/// <summary>
		/// Asynchronously authenticate using the specified user name and password.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously authenticates using the supplied credentials.</para>
		/// <para>If the server supports one or more SASL authentication mechanisms, then
		/// the SASL mechanisms that both the client and server support (not including any
		/// OAUTH mechanisms) are tried in order of greatest security to weakest security.
		/// Once a SASL authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="MailKit.Security.AuthenticationException">
		/// Authentication using the supplied credentials has failed.
		/// </exception>
		/// <exception cref="MailKit.Security.SaslException">
		/// A SASL authentication error occurred.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public Task AuthenticateAsync (string userName, string password, CancellationToken cancellationToken = default)
		{
			return AuthenticateAsync (Encoding.UTF8, userName, password, cancellationToken);
		}

		/// <summary>
		/// Authenticate using the specified SASL mechanism.
		/// </summary>
		/// <remarks>
		/// <para>Authenticates using the specified SASL mechanism.</para>
		/// <para>For a list of available SASL authentication mechanisms supported by the server,
		/// check the <see cref="AuthenticationMechanisms"/> property after the service has been
		/// connected.</para>
		/// </remarks>
		/// <param name="mechanism">The SASL mechanism.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="mechanism"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="MailKit.Security.AuthenticationException">
		/// Authentication using the supplied credentials has failed.
		/// </exception>
		/// <exception cref="MailKit.Security.SaslException">
		/// A SASL authentication error occurred.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract void Authenticate (SaslMechanism mechanism, CancellationToken cancellationToken = default);

		/// <summary>
		/// Asynchronously authenticate using the specified SASL mechanism.
		/// </summary>
		/// <remarks>
		/// <para>Authenticates using the specified SASL mechanism.</para>
		/// <para>For a list of available SASL authentication mechanisms supported by the server,
		/// check the <see cref="AuthenticationMechanisms"/> property after the service has been
		/// connected.</para>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="mechanism">The SASL mechanism.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="mechanism"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="MailKit.Security.AuthenticationException">
		/// Authentication using the supplied credentials has failed.
		/// </exception>
		/// <exception cref="MailKit.Security.SaslException">
		/// A SASL authentication error occurred.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract Task AuthenticateAsync (SaslMechanism mechanism, CancellationToken cancellationToken = default);

		/// <summary>
		/// Disconnect the service.
		/// </summary>
		/// <remarks>
		/// If <paramref name="quit"/> is <see langword="true" />, a logout/quit command will be issued in order to disconnect cleanly.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessage"/>
		/// </example>
		/// <param name="quit">If set to <see langword="true" />, a logout/quit command will be issued in order to disconnect cleanly.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		public abstract void Disconnect (bool quit, CancellationToken cancellationToken = default);

		/// <summary>
		/// Asynchronously disconnect the service.
		/// </summary>
		/// <remarks>
		/// If <paramref name="quit"/> is <see langword="true" />, a logout/quit command will be issued in order to disconnect cleanly.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="quit">If set to <see langword="true" />, a logout/quit command will be issued in order to disconnect cleanly.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		public abstract Task DisconnectAsync (bool quit, CancellationToken cancellationToken = default);

		/// <summary>
		/// Ping the mail server to keep the connection alive.
		/// </summary>
		/// <remarks>
		/// Mail servers, if left idle for too long, will automatically drop the connection.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="MailService"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="MailService"/> is not authenticated.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command was rejected by the mail server.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public abstract void NoOp (CancellationToken cancellationToken = default);

		/// <summary>
		/// Asynchronously ping the mail server to keep the connection alive.
		/// </summary>
		/// <remarks>
		/// Mail servers, if left idle for too long, will automatically drop the connection.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="MailService"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="MailService"/> is not authenticated.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command was rejected by the mail server.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public abstract Task NoOpAsync (CancellationToken cancellationToken = default);

		/// <summary>
		/// Occurs when the client has been successfully connected.
		/// </summary>
		/// <remarks>
		/// The <see cref="Connected"/> event is raised when the client
		/// successfully connects to the mail server.
		/// </remarks>
		public event EventHandler<ConnectedEventArgs> Connected;

		/// <summary>
		/// Raise the connected event.
		/// </summary>
		/// <remarks>
		/// Raises the connected event.
		/// </remarks>
		/// <param name="host">The name of the host that the client connected to.</param>
		/// <param name="port">The port that the client connected to on the remote host.</param>
		/// <param name="options">The SSL/TLS options that were used when connecting.</param>
		protected virtual void OnConnected (string host, int port, SecureSocketOptions options)
		{
			Connected?.Invoke (this, new ConnectedEventArgs (host, port, options));
		}

		/// <summary>
		/// Occurs when the client gets disconnected.
		/// </summary>
		/// <remarks>
		/// The <see cref="Disconnected"/> event is raised whenever the client
		/// gets disconnected.
		/// </remarks>
		public event EventHandler<DisconnectedEventArgs> Disconnected;

		/// <summary>
		/// Raise the disconnected event.
		/// </summary>
		/// <remarks>
		/// Raises the disconnected event.
		/// </remarks>
		/// <param name="host">The name of the host that the client was connected to.</param>
		/// <param name="port">The port that the client was connected to on the remote host.</param>
		/// <param name="options">The SSL/TLS options that were used by the client.</param>
		/// <param name="requested"><see langword="true" /> if the disconnect was explicitly requested; otherwise, <see langword="false" />.</param>
		protected virtual void OnDisconnected (string host, int port, SecureSocketOptions options, bool requested)
		{
			Disconnected?.Invoke (this, new DisconnectedEventArgs (host, port, options, requested));
		}

		/// <summary>
		/// Occurs when the client has been successfully authenticated.
		/// </summary>
		/// <remarks>
		/// The <see cref="Authenticated"/> event is raised whenever the client
		/// has been authenticated.
		/// </remarks>
		public event EventHandler<AuthenticatedEventArgs> Authenticated;

		/// <summary>
		/// Raise the authenticated event.
		/// </summary>
		/// <remarks>
		/// Raises the authenticated event.
		/// </remarks>
		/// <param name="message">The notification sent by the server when the client successfully authenticates.</param>
		protected virtual void OnAuthenticated (string message)
		{
			Authenticated?.Invoke (this, new AuthenticatedEventArgs (message));
		}

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="MailService"/> and
		/// optionally releases the managed resources.
		/// </summary>
		/// <remarks>
		/// Releases the unmanaged resources used by the <see cref="MailService"/> and
		/// optionally releases the managed resources.
		/// </remarks>
		/// <param name="disposing"><see langword="true" /> to release both managed and unmanaged resources;
		/// <see langword="false" /> to release only the unmanaged resources.</param>
		protected virtual void Dispose (bool disposing)
		{
			if (disposing)
				ProtocolLogger.Dispose ();
		}

		/// <summary>
		/// Releases all resource used by the <see cref="MailService"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose()"/> when you are finished using the <see cref="MailService"/>. The
		/// <see cref="Dispose()"/> method leaves the <see cref="MailService"/> in an unusable state. After
		/// calling <see cref="Dispose()"/>, you must release all references to the <see cref="MailService"/> so
		/// the garbage collector can reclaim the memory that the <see cref="MailService"/> was occupying.</remarks>
		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}
	}
}
