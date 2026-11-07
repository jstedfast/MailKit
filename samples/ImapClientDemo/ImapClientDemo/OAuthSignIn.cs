//
// OAuthSignIn.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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
using System.Threading;
using System.Threading.Tasks;

using MailKit.Security;

#if GOOGLE_OAUTH
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;
#endif
#if MICROSOFT_OAUTH
using Microsoft.Identity.Client;
#endif

namespace ImapClientDemo
{
	enum AuthenticationKind
	{
		Password,
		AccessToken,
		Google,
		Microsoft365
	}

	// These helpers acquire a token; MailKit still connects using IMAP and XOAUTH2.
	// The demo does not retain credentials for silent renewal: reconnecting signs in again.
	// Build gates exclude the provider SDKs until the reader configures an app registration.
	static class OAuthSignIn
	{
		public static bool GoogleEnabled {
			get {
#if GOOGLE_OAUTH
				return true;
#else
				return false;
#endif
			}
		}

		public static bool MicrosoftEnabled {
			get {
#if MICROSOFT_OAUTH
				return true;
#else
				return false;
#endif
			}
		}

		public static void ValidateServer (AuthenticationKind kind, string host, int port, SecureSocketOptions security)
		{
			string expected = kind switch {
				AuthenticationKind.Google => "imap.gmail.com",
				AuthenticationKind.Microsoft365 => "outlook.office365.com",
				_ => throw new ArgumentException ("Select a browser sign-in provider.", nameof (kind))
			};

			// Never send a provider-issued bearer token to an arbitrary user-entered server.
			if (!host.Equals (expected, StringComparison.OrdinalIgnoreCase) || port != 993 || security != SecureSocketOptions.SslOnConnect)
				throw new ArgumentException ($"Browser sign-in requires {expected}, port 993, with TLS on connect.");
		}

		public static Task<string> GetAccessTokenAsync (AuthenticationKind kind, string userName, CancellationToken cancellationToken)
		{
			switch (kind) {
#if GOOGLE_OAUTH
			case AuthenticationKind.Google:
				return SignInGoogleAsync (userName, cancellationToken);
#endif
#if MICROSOFT_OAUTH
			case AuthenticationKind.Microsoft365:
				return SignInMicrosoftAsync (userName, cancellationToken);
#endif
			default:
				throw new NotSupportedException ("Provider sign-in is disabled. See OAuthSignIn.cs and the sample README to enable it.");
			}
		}

#if GOOGLE_OAUTH || MICROSOFT_OAUTH
		static string GetSetting (string name)
		{
			var value = Environment.GetEnvironmentVariable (name);
			if (string.IsNullOrWhiteSpace (value))
				throw new InvalidOperationException ($"Set {name} for your own app registration. See the sample README.");
			return value;
		}
#endif

#if GOOGLE_OAUTH
		// Disabled by default: build with -p:EnableGoogleOAuth=true to include this code and its SDK.
		// Register a Desktop app OAuth client, configure consent/test users, and supply its client
		// ID and client secret via the environment variables below. Do not commit credentials.
		// Desktop apps cannot keep secrets confidential; PKCE is enabled for the loopback flow.
		// https://developers.google.com/identity/protocols/oauth2/native-app
		// https://developers.google.com/workspace/gmail/imap/xoauth2-protocol
		// The IMAP scope is restricted and grants full mailbox access, even though this demo
		// only opens folders read-only. Public distribution may require Google's verification.
		static async Task<string> SignInGoogleAsync (string userName, CancellationToken cancellationToken)
		{
			var initializer = new GoogleAuthorizationCodeFlow.Initializer {
				ClientSecrets = new ClientSecrets {
					ClientId = GetSetting ("MAILKIT_GOOGLE_CLIENT_ID"),
					ClientSecret = GetSetting ("MAILKIT_GOOGLE_CLIENT_SECRET")
				}
			};
			var scopes = new [] { "https://mail.google.com/" };

			// Passing null would use FileDataStore and write refresh tokens to disk.
			var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync (
				initializer, scopes, userName, true, cancellationToken, new NullDataStore ());

			using (credential.Flow)
				return await credential.GetAccessTokenForRequestAsync (cancellationToken: cancellationToken);
		}
#endif

#if MICROSOFT_OAUTH
		// Disabled by default: build with -p:EnableMicrosoftOAuth=true to include this code and MSAL.
		// Register a public Mobile and desktop application with redirect URI http://localhost.
		// Add the Office 365 Exchange Online delegated IMAP.AccessAsUser.All permission and
		// obtain consent as required by the tenant. IMAP must also be enabled for the mailbox.
		// Supply the client ID and optionally tenant ID below. Public clients need NO client secret.
		// https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app
		// https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/desktop-mobile/acquiring-tokens-interactively
		// https://learn.microsoft.com/en-us/exchange/client-developer/legacy-protocols/how-to-authenticate-an-imap-pop-smtp-application-by-using-oauth
		static async Task<string> SignInMicrosoftAsync (string userName, CancellationToken cancellationToken)
		{
			var tenant = Environment.GetEnvironmentVariable ("MAILKIT_MICROSOFT_TENANT_ID");

			if (string.IsNullOrWhiteSpace (tenant))
				tenant = "organizations";

			var app = PublicClientApplicationBuilder.Create (GetSetting ("MAILKIT_MICROSOFT_CLIENT_ID"))
				.WithAuthority (AzureCloudInstance.AzurePublic, tenant)
				.WithRedirectUri ("http://localhost")
				.Build ();

			// Exchange IMAP requires an Exchange Online token, not a Microsoft Graph token.
			var scopes = new [] { "https://outlook.office.com/IMAP.AccessAsUser.All" };

			// MSAL uses authorization code + PKCE and an in-memory cache; no persistent cache is wired up.
			var result = await app.AcquireTokenInteractive (scopes)
				.WithUseEmbeddedWebView (false)
				.WithLoginHint (userName)
				.WithPrompt (Prompt.SelectAccount)
				.ExecuteAsync (cancellationToken);

			return result.AccessToken;
		}
#endif
	}
}
