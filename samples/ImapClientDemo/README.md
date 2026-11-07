# WPF IMAP Explorer

A small, read-only MailKit 5.0 sample for Windows and .NET 10. It demonstrates
folder browsing, newest-first pages of 100 message summaries, on-demand text
previews, and live changes using `ImapIdleSession` (with NOOP polling when the
server does not support IDLE).

## Run

Install the .NET 10 SDK on Windows. From the repository root:

```powershell
git submodule update --init --recursive
dotnet run --project samples\ImapClientDemo\ImapClientDemo\ImapClientDemo.csproj
```

Alternatively, open `ImapClientDemo.sln` in a Visual Studio version supporting
.NET 10 and the .NET desktop development workload. The project references the
repository's MailKit and MimeKit sources, not a published NuGet version.

Enter the server, port, user name, and either a password/app password or an
OAuth2 access token. TLS on connect (normally port 993) and mandatory STARTTLS
(normally port 143) are supported. Certificate validation is never disabled.
Credentials are not saved and protocol logging is off.

Supplied OAuth2 tokens must have the appropriate IMAP permissions for the
provider. Ordinary account passwords do not work with providers that require
OAuth2 or app passwords.

## Optional browser sign-in

Google and Microsoft 365 sign-in are **compiled out and disabled in the UI by
default**. No shared application registration or credentials are provided.
`OAuthSignIn.cs` contains the flows, setup comments, and documentation links.
Enabling a provider also enables its SDK package reference.

### Google

Create your own **Desktop app** OAuth client and configure the consent screen
and test users, following [Google's installed-app documentation](https://developers.google.com/identity/protocols/oauth2/native-app).
IMAP requires the restricted [`https://mail.google.com/` scope](https://developers.google.com/workspace/gmail/imap/xoauth2-protocol).
It grants full mailbox access even though this sample is read-only. Distribution
beyond testing may require Google's verification.

Set `MAILKIT_GOOGLE_CLIENT_ID` and `MAILKIT_GOOGLE_CLIENT_SECRET` in your local
environment using the values from that desktop app registration, then run:

```powershell
dotnet run --project samples\ImapClientDemo\ImapClientDemo\ImapClientDemo.csproj -p:EnableGoogleOAuth=true
```

Select **Google (browser)** and enter the mailbox address as the user name.
Choose that account in the browser. The Google SDK uses a loopback redirect with
PKCE. Desktop client secrets cannot be kept confidential; do not use a web/server
client registration, and do not commit your registration settings.

### Microsoft 365

[Register your application in Microsoft Entra](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app),
configure the **Mobile and desktop applications** platform with redirect URI
`http://localhost`, and add Office 365 Exchange Online's **delegated**
`IMAP.AccessAsUser.All` permission. Obtain tenant consent if required and ensure
IMAP is enabled for the mailbox. See [Exchange Online's OAuth instructions](https://learn.microsoft.com/en-us/exchange/client-developer/legacy-protocols/how-to-authenticate-an-imap-pop-smtp-application-by-using-oauth)
and [MSAL desktop sign-in](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/desktop-mobile/acquiring-tokens-interactively).

Set `MAILKIT_MICROSOFT_CLIENT_ID` in your environment. For a single-tenant app,
also set `MAILKIT_MICROSOFT_TENANT_ID` to your tenant's ID; otherwise the sample
defaults to `organizations` (work/school accounts). This is a **public client**:
do not create or embed a Microsoft client secret.

```powershell
dotnet run --project samples\ImapClientDemo\ImapClientDemo\ImapClientDemo.csproj -p:EnableMicrosoftOAuth=true
```

Select **Microsoft 365 (browser)** and enter the mailbox address. MSAL uses the
system browser with authorization code + PKCE. The scope is
`https://outlook.office.com/IMAP.AccessAsUser.All`, not a Microsoft Graph scope.
This sample targets the public Microsoft cloud.

Both build properties can be enabled together. Provider-issued tokens are only
sent to that provider's known IMAP host over TLS on port 993. The sign-in has a
five-minute deadline and can be cancelled using Disconnect/cancel.
Tokens are not persisted: Google uses `NullDataStore` and MSAL uses its in-memory
cache. Reconnecting signs in again; there is no background token-renewal service.
Never put client settings, passwords, or tokens in source control.

## Design

* `ImapSession` owns the client. A single asynchronous channel reader serializes
  commands and ends IDLE before executing queued work. No UI code uses a live
  `IMailFolder`, and no protocol event handler sends commands.
* `MailboxPage` contains immutable UI snapshots. Only a single page is kept,
  and BODYSTRUCTURE is fetched only for the selected message. Identity includes
  the folder, UIDVALIDITY, and UID; sequence numbers are used only to fetch pages.
* `MainWindowViewModel` owns selection, cancellation, and display state.
  Superseded queued requests are skipped; in-flight requests finish without
  publishing stale results. This avoids tearing down the IMAP connection every
  time the user selects another message.
* `MainWindow` handles WPF events and displays failures. Its timer only checks
  local change state; network monitoring belongs to the session. Notifications
  received during ordinary commands also invalidate the current page.

Folders are opened read-only, so viewing a message does not mark it as read.
Plain text is preferred; HTML-only messages are converted to inert text.
There is no browser, remote-content loading, attachment viewer, or mail editor.
Previewing downloads the selected text part, not the entire message.

Live arrivals and expunges refresh the current page. Pages are positions counted
from the newest message, so their contents may shift as the mailbox changes.
Selection and preview are preserved by UID when that message remains on the
page. Use Refresh to retry a failed page load, or disconnect and reconnect to
recover a lost connection or refresh the folder list. Automatic reconnection
and persistent caching are intentionally outside the sample's scope.

Disconnect/cancel and closing the window cooperatively stop pending work.
Commands have a 30-second deadline; IDLE shutdown and logout have five-second
grace periods before the connection is closed.

The protocol and pagination tests run with the existing test project:

```powershell
dotnet test UnitTests\UnitTests.csproj --filter FullyQualifiedName~ImapClientDemoTests
```
