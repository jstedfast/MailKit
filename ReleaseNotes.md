# Release Notes

### MailKit 2010.1 (2021-01-02)

* A few NTLM improvements that I hope are correct.

### MailKit 2.10.0 (2020-11-20)

* Don't enable support for TLS v1.1 by default anymore.
  (issue [#1077](https://github.com/jstedfast/MailKit/issues/1077))
* Added support for the SCRAM-SHA-512 SASL mechanism.
  (issue [#1097](https://github.com/jstedfast/MailKit/issues/1097))
* Added support for the OAUTHBEARER SASL mechanism.
* Updated SSL certificate info for the common mail servers (GMail, outlook.com, Yahoo! Mail, etc).
* Improved the SslHandshakeException error message to report common mistakes like trying to initiate
  an SSL connection on a non-SSL port.
* Improved IMAP's "Unexpected token" exception messages a bit
* Updated code to use ArrayPools from System.Buffers.

### MailKit 2.9.0 (2020-09-12)

* Refactored Connect/ConnectAsync() logic to set timeouts *before* calling SslStream.AuthenticateAsClient()
  when connecting to an SSL-wrapped service.
  (issue [#1059](https://github.com/jstedfast/MailKit/issues/1059))
* Hardcode the value of SslProtocols.Tls13 for frameworks that do not support it and add it to the
  client's default SslProtocols. This adds TLS v1.3 support, by default, for apps using .NETStandard2.0
  where the app project is built against a version of .NETCore that supports TLS v1.3.
  (issue [#1058](https://github.com/jstedfast/MailKit/issues/1058))
* Initialize IMAP SearchResults with the UIDVALIDITY value.
  (issue [#1060](https://github.com/jstedfast/MailKit/issues/1060))
* Make sure the ImapStream is not null (can be null if user calls Disconnect() causing IDLE to abort).
  (issue [#1025](https://github.com/jstedfast/MailKit/issues/1025))
* Case-insenitively match IMAP folder attribute flags (e.g. \HasNoChildren and \NoSelect).
* Added support for the IMAP SAVEDATE extension.
* Added support for detecting SMTP's REQUIRETLS extension.

### MailKit 2.8.0 (2020-07-11)

* Make sure to use the InvariantCulture when converting port values to a string.
  (issue [#1040](https://github.com/jstedfast/MailKit/issues/1040))
* Fixed other instances of string formatting for integer values to always use
  CultureInfo.InvariantCulture.
* Added a work-around for broken IMAP servers that allow NIL message flags.
  (issue [#1042](https://github.com/jstedfast/MailKit/issues/1042))

### MailKit 2.7.0 (2020-05-30)

* Added a MessageSummary.Folder property and MessageThread.Message property
  to allow developers to thread messages from multiple IMAP folders and be
  able to figure out which folder each message belongs to.
* Added a work-around for IMAP servers that send a UIDNEXT response with a
  value of '0'. (issue [#1010](https://github.com/jstedfast/MailKit/issues/1010))
* Added an IMailFolder.Supports(FolderFeature) method so that developers can check
  whether a feature is supported by the folder without needing a reference to the
  corresponding ImapClient object in order to check the Capabilities.
* Fixed the HTTP proxy client to accept "200 OK" with an empty body as a successful
  connection. (issue [#1015](https://github.com/jstedfast/MailKit/issues/1015))
* Fixed the SOCKS5 proxy client to correctly send an authentication request.
  (issue [#1019](https://github.com/jstedfast/MailKit/issues/1019))
* Added support for customizable ProtocolLogger client/server prefixes.
  (issue [#1024](https://github.com/jstedfast/MailKit/issues/1024))
* Fixed an NRE in SslHandshakeException.Create() when running on Mono/Linux.
* Modified the SmtpClient to take advantage of the SMTPUTF8 extension for the
  `MAIL FROM` and `RCPT TO` commands even if a `options.International` is not
  explicitly set to `true` if any of the mailbox addresses are international
  addresses.
  (issue [#1026](https://github.com/jstedfast/MailKit/issues/1026))
* Added support for a new Important SpecialFolder ([rfc8457](https://tools.ietf.org/html/rfc8457)).
* Added support for the IMAP REPLACE extension ([rfc8508](https://tools.ietf.org/html/rfc8508)).
* NuGet packages now include the portable pdb's.

### MailKit 2.6.0 (2020-04-03)

* Properly handle connection drops in SmtpClient.NoOp() and NoOpAsync()
  methods.
* Improved default SSL certificate validation logic to be more secure
  and to recognize the most commonly used mail servers even if their
  Root CA Certificates are not available on the system.
* SslHandshakeException's Message has been improved to be based on the
  errors reported in the ServerCertificateValidationCallback and also
  now has 2 new X509Certificate properties which represent the
  ServerCertificate and the RootCertificateAuthority in order to help
  developers diagnose problems.
  (issue [#1002](https://github.com/jstedfast/MailKit/issues/1002))
* Improved the IMAP PreviewText to extract text from HTML bodies.
  (issue [#1001](https://github.com/jstedfast/MailKit/issues/1001))
* Renamed MessageSummaryItems.Id to MessageSummaryItems.EmailId to
  better map to the property name used in the IMAP OBJECTID
  specification.
* Updated NetworkStream.ReadAsync() and WriteAsync() mehods to make use of
  timeouts. (issue [#827](https://github.com/jstedfast/MailKit/issues/827))

### MailKit 2.5.2 (2020-03-14)

* Added work-around for ENVELOPE responses with a NIL address token in an address-list.
  (issue [#991](https://github.com/jstedfast/MailKit/issues/991))

### MailKit 2.5.1 (2020-02-15)

* Fixed the IMAP ENVELOPE parser to have a more lenient fallback if it fails to be able to
  parse the Message-Id token value.
  (issue [#976](https://github.com/jstedfast/MailKit/issues/976))
* Fixed MailService.DefaultServerCertificateValidationCallback() to compare certificates by
  their hashes rather than via Object.Equals().
  (issue [#977](https://github.com/jstedfast/MailKit/issues/977))
* Added work-around for IMAP servers that send `-1` as a line count or octet count in the
  BODYSTRUCTURE response.

### MailKit 2.5.0 (2020-01-18)

* Ignore NIL tokens in the body-fld-lang token list.
  (issue [#953](https://github.com/jstedfast/MailKit/issues/953))
* Added logic to handle unexpected <CRLF> in untagged FETCH responses.
  (issue [#954](https://github.com/jstedfast/MailKit/issues/954))
* Added a way to override SmtpClient's preference for using BDAT vs DATA
  via a new PreferSendAsBinaryData virtual property.
* Update SslHandshakeException message to mention the possibility of SSL/TLS
  version mismatch.
  (issue [#957](https://github.com/jstedfast/MailKit/issues/957))
* Fixed ImapFolder.GetStreamsAsync() to use an async callback delegate.
  (issue [#958](https://github.com/jstedfast/MailKit/issues/958))
* Added protocol-specific interfaces that inherit from IMailFolder,
  IMailStore, etc.
  (issue [#960](https://github.com/jstedfast/MailKit/issues/960))
* Maintain the STARTTLS capability bit flag after a STARTTLS command.
* Don't send the optional ANNOTATE parameter to SELECT/EXAMINE for
  SUN IMAP servers (such as Apple's IMAP servers).
  (issue [#970](https://github.com/jstedfast/MailKit/issues/970))

Note: Developers using ImapFolder.GetStreamsAsync() will need to update their code as
this release breaks API/ABI.

### MailKit 2.4.1 (2019-11-10)

* Don't use PublicSign on non-Windows NT machines when building.
* Work-around broken BODYSTRUCTUREs with `()` as a message/rfc822 body token.
  (issue [#944](https://github.com/jstedfast/MailKit/issues/944))
* Added work-around for an Exchange bug that forgets to quote folder names containing tabs.
  (issue [#945](https://github.com/jstedfast/MailKit/issues/945))
* Moved the SmtpDataFilter into the public API and updated the FAQ to show how to
  use it when writing messages into an IIS "pickup directory".
  (issue [#948](https://github.com/jstedfast/MailKit/issues/948))

### MailKit 2.4.0 (2019-11-02)

* Added work-around for IMAP ENVELOPE responses that do not include an In-Reply-To token.
  (issue [#932](https://github.com/jstedfast/MailKit/issues/932))
* Dropped support for WindowsPhone/Universal v8.1.
* Added a net48 assembly to the NuGet package which supports TLS v1.3.
* Added work-around for Yandex IMAP servers to disconnect immediately upon `* BYE`.
  (issue [#938](https://github.com/jstedfast/MailKit/issues/938))
* Fixed ImapClient.Idle() and IdleAsync().
  (issue [#942](https://github.com/jstedfast/MailKit/issues/942))
* Added work-around for Lotus Domino where it adds extra ()'s around some FETCH items.
  (issue [#943](https://github.com/jstedfast/MailKit/issues/943))

### MailKit 2.3.2 (2019-10-12)

* Fixed trimming delimeters from the end of IMAP folder names.
* Fixed fetching of IMAP PreviewText when message bodies do not contain any text parts.
* Fixed Pop3Client to never emit Authenticated events w/ null messages.
* Dropped SslProtocols.Tls (aka TLSv1.0) from the default SslProtocols used by IMAP, POP3
  and SMTP clients. To override this behavior, use the client.SslProtocols property
  to set the preferred SslProtocol(s).
* Fixed ImapFolder.Search(string query) to properly encode the query string when the query
  contains unicode characters.
* If an IMAP SEARCH fails due to BADCHARSET, retry the search query after flattening the
  query strings into US-ASCII. This *may* fix issues such as
  issue [#808](https://github.com/jstedfast/MailKit/issues/808).
* Added work-arounds for Exchange IMAP bugs causing it to send mal-formed body-fld-dsp
  parameters. (issue [#919](https://github.com/jstedfast/MailKit/issues/919))
* Go back to only using the BDAT command when the user is sending BINARYMIME in the SmtpClient.
  (issue [#921](https://github.com/jstedfast/MailKit/issues/921))

### MailKit 2.3.1 (2019-09-08)

* Fixed SmtpClient.Send*() to make sure never to add an extra CRLF sequence to the end of
  messages when sending via the DATA command.
  (issue [#895](https://github.com/jstedfast/MailKit/issues/895))
* Added assemblies for net46 and net47 to the NuGet package.

### MailKit 2.3.0 (2019-08-24)

* Improved the default SSL/TLS certificate validation logic.
* Improved exception messages for the POP3 LIST and STAT commands.
* Modified Pop3Client to accept negative values for the 'octets' value in the STAT response.
  (issue [#872](https://github.com/jstedfast/MailKit/issues/872))
* Added work-around for IMAP BODYSTRUCTURE responses that treat multiparts as basic parts.
  (issue [#878](https://github.com/jstedfast/MailKit/issues/878))
* Added check to make sure that MD5 is supported by the runtime and automatically disable
  support for CRAM-MD5 and DIGEST-MD5 SASL mechanisms when MD5 is not supported.
* Added a Stream property to ProtocolLogger.
* Fixed fetching of PreviewText items if the body's ContentTransferEncoding is NIL.
  (issue [#881](https://github.com/jstedfast/MailKit/issues/881))
* Improved processing of pipelined SMTP commands to provide better exception messages.
  (issue [#883](https://github.com/jstedfast/MailKit/issues/883))
* Modified SmtpClient.Send*() to not call MimeMessage.Prepare() if any DKIM or ARC headers
  are present in order to avoid the potential risk of altering the message and breaking
  the signatures within those headers.
* Added SmtpClient.SendCommand() and SendCommandAsync() to allow custom subclasses the
  ability to send custom commands to the SMTP server.
  (issue [#891](https://github.com/jstedfast/MailKit/issues/891))
* Allow SmtpClient subclasses to override message preparation by overriding a new
  SmtpClient.Prepare() method.
  (issue [#891](https://github.com/jstedfast/MailKit/issues/891))
* Improved ImapFolder's ModSeqChanged event to set the UniqueId property if available
  in unsolicited FETCH notifications including a MODSEQ and UID value.
* Fixed the IMAP client logic to properly handle lower or mixed case IMAP tokens.
  (issue [#893](https://github.com/jstedfast/MailKit/issues/893))
* Added support for IMAP's ANNOTATE-EXPERIMENT-1 extension.
  (issue [#818](https://github.com/jstedfast/MailKit/issues/818))
* Always use the SMTP BDAT command instead of DATA if CHUNKING is supported.
  (issue [#896](https://github.com/jstedfast/MailKit/issues/896))
* Improved SmtpClient to include a SIZE= parameter in the MAIL FROM command if the
  SIZE extension is supported. Progress reporting will now always have the expected
  message size available as well.

### MailKit 2.2.0 (2019-06-11)

* Optimized MailKit's logic for breaking apart long IMAP commands for
  GMail, Dovecot, and Yahoo! Mail.
* Fixed the IMAP stream tokenizer to properly handle UTF8 atom tokens.
  (issue [#859](https://github.com/jstedfast/MailKit/issues/859))
* Fixed IMAP search code to always handle untagged SEARCH responses even when
  the response SHOULD be an untagged ESEARCH response.
  (issue [#863](https://github.com/jstedfast/MailKit/issues/863))
* Replaced SearchQuery.SentAfter with SentSince to be more consistent with IMAP
  terminology.

### MailKit 2.1.5 (2019-05-13)

* Bumped the System.Net.Security dependency for security fixes (CVE-2017-0249).
* Reduced explicit nuget dependencies.
* Added a work-around for Microsoft Exchange IMAP servers that sometimes erroneously
  respond with unneeded continuation responses.
  (issue [#852](https://github.com/jstedfast/MailKit/issues/852))
* Fixed the ImapClient to Stop looping over SASL mechanisms if the server disconnects us.
  (issue [#851](https://github.com/jstedfast/MailKit/issues/851))
* Added support for HTTP proxies. (issue [#847](https://github.com/jstedfast/MailKit/issues/847))
* Fixed IMAP to properly handle EXPUNGE notifications during a FETCH request.
  (issue [#850](https://github.com/jstedfast/MailKit/issues/850))

### MailKit 2.1.4 (2019-04-13)

* Fixed ImapUtils.GetUniqueHeaders() to accept all valid header field name characters.
  (issue [#806](https://github.com/jstedfast/MailKit/issues/806))
* Catch all exceptions thrown in IdleComplete().
  (issue [#825](https://github.com/jstedfast/MailKit/issues/825))
* Improved cancellability of IMAP, POP3 and SMTP clients when sending commands to the server.
  (issue [#827](https://github.com/jstedfast/MailKit/issues/827))
* Break apart IMAP commands with really long uid-sets.
  (issue [#834](https://github.com/jstedfast/MailKit/issues/834))
* Rewrote Connect logic to use Socket.Connect (IPAddress, int) instead of Connect (string, int)
  in an attempt to fix https://stackoverflow.com/q/55382267/87117
* Fixed SmtpStream.ReadAheadAsync() to preserve remaining input.
  (issue [#842](https://github.com/jstedfast/MailKit/issues/842))

### MailKit 2.1.3 (2019-02-24)

* Fixed IMAP GetFolder() methods to match LIST responses case-insensitively.
  (issue [#803](https://github.com/jstedfast/MailKit/issues/803))
* Added a work-around to SmtpClient for a .NET 4.5.2 bug on Windows 7 SP1.
  (issue [#814](https://github.com/jstedfast/MailKit/issues/814))
* Added DeliveryStatusNotificationType and a property to SmtpClient to allow
  developers to specify the `RET` parameter value to the `MAIL FROM` command.
* Fixed a number of locations in the code to clear password buffers after using
  them.
* SmtpClient.Send() and SendAsync() methods that accept a FormatOptions argument
  will no longer hide Bcc, Resent-Bcc, nor Content-Length headers when uploading
  the raw message to the SMTP server. It is now up to callers to add these values
  to their custom FormatOptions.HiddenHeaders property.
  (issue [#360](https://github.com/jstedfast/MailKit/issues/360))

### MailKit 2.1.2 (2018-12-30)

* Fixed a bug in SmtpDataFilter. (issue [#788](https://github.com/jstedfast/MailKit/issues/788))
* Fixed ImapFolder.Sort() to always return the UIDs in the correct order.
  (issue [#789](https://github.com/jstedfast/MailKit/issues/789))
* Fixed *Client.ConnectAsync() to more reliably abort when the cancellation token is cancelled.
  (issue [#798](https://github.com/jstedfast/MailKit/issues/798))

### MailKit 2.1.1 (2018-12-16)

* Fixed ImapFolder.CopyTo() and ImapFolder.MoveTo() for IMAP servers that do not support UIDPLUS.
  (issue [#787](https://github.com/jstedfast/MailKit/issues/787))
* Fixed ImapClient.Connect() to capture authenticated state *before* calling OnConnected() so that
  developers that call Authenticate() inside of the Connected event handler do not receive 2 Authenticated
  events. (issue [#784](https://github.com/jstedfast/MailKit/issues/784))

### MailKit 2.1.0 (2018-12-01)

* A number of fixes to bugs exposed in new unit tests for NTLM authentication.
* Made SmtpClient, Pop3Client, and ImapClient's Connect() methods truly cancellable as well
  as made the underlying socket.Connect() call adhere to any specified client.Timeout value.
* Added support for connecting via a SOCKS4, SOCKS4a, or SOCKS5 proxy server.
* Fixed ImapClient's OnAuthenticated() method to protect aganst throwing an ArgumentNullException
  when trying to emit the Authenticated event if the server did not supply any resp-code-text in
  the OK response to the AUTHENTICATE command. (issue [#774](https://github.com/jstedfast/MailKit/issues/774))
* Modified ImapFolder.Create() to handle [ALREADYEXISTS] resp-codes.
* Fixed ImapFolder.Create() for GMail when the isMessageFolder parameter is false (GMail doesn't handle
  it when the client attempts to create a folder ending with a directory separator).
* Optimized ImapFolder's fallback for UID COPY command when UIDPLUS is not supported.
* Reduced string allocations in the Connect(Uri) wrapper.
* Added new ConnectedEventArgs and DisconnectedEventArgs that are used with the Connected and
  Disconnected events to provide developers with even more useful information about what
  server, port and SecureSocketOptions were used when connecting the client.
* Fixed SmtpClient to immediately throw stream reading exceptions instead of ignoring them.
  (issue [#776](https://github.com/jstedfast/MailKit/issues/776))
* Fixed ImapClient.GetFoldersAsync() to call ImapFolder.StatusAsync() instead of Status()
  when StatusItems are specified.
* Changed ImapFolder.GetSubfolders() to return IList<IMailFolder> instead of IEnumerable<IMailFolder>.
* Fixed ImapClient's NAMESPACE parser - it had Shared and Other namespace ordering reversed.
* Fixed ImapFolder.Create() (for special-use) to only use unique uses if any were specified multiple times.
* Modified ImapFolder.Open() to allow devs to re-Open() a folder with the same access in case they
  need to do this to work around an IMAP server bug(?).
* Fixed adding/removing/setting of GMail labels to use UTF-8 when enabled.
* Added support for the IMAP STATUS=SIZE extension which now provides a ImapFolder.Size property
  that specifies how large a folder is (in bytes). Clients can request this information using the
  StatusItems.Size enum with either ImapFolder.GetSubfolders() or ImapFolder.Status().
* Added support for the IMAP OBJECTID extension. ImapFolder and IMessageSummary now both have
  an Id property which is a globally unique identifier. IMessageSummary also now has a ThreadId
  property which is a unique identifier for the message thread/conversation that the message
  belongs to. This information can be retrieved for ImapFolders using ImapFolder.Status() with the
  new StatusItems.MailboxId enum value. The IMessageSummary.Id and ThreadId properties have
  the corresponding MessageSummaryItems enum values of Id and ThreadId, respectively.
* Added another work-around for bad GMail IMAP BODYSTRUCTURE responses.
  (issue [#777](https://github.com/jstedfast/MailKit/issues/777))
* Fixed all integer TryParse methods to use NumberStyles.None and CultureInfo.InvariantCulture.
* Added Connect() and ConnectAsync() overloads which accept a Stream instead of a Socket.
* All ImapFolder.MessageFlagsChanged, ModSeqChanged, and LabelsChanged events will now also be
  followed by a MessageSummaryFetched event containing the combined information of those events.
* Added support for IMAP's NOTIFY extension. Many thanks to [Steffen Kieß](https://github.com/steffen-kiess)
  for getting the ball rolling on this feature by implementing the neccessary ImapEvent, ImapEventGroup,
  and ImapMailboxFilter classes as well as the initial support.

API Changes Since 2.0.x:

* Obsoleted SearchQuery.HasCustomFlags() and SearchQuery.DoesNotHaveCustomFlags(). These are
  now SearchQuery.HasKeywords() and SearchQuery.NotKeywords(), respectively.
* Obsoleted SearchQuery.DoesNotHaveFlags() in favor of SearchQuery.NotFlags().
* Obsoleted the IMessageSummary.UserFlags property in favor of IMessageSummary.Keywords.
* Obsoleted the MessageFlagsChangedEventArgs.UserFlags property in favor of
  MessageFlagsChangedEventArgs.Keywords.
* All IMailFolder.Fetch and IMailFolder.FetchAsync methods that took a HashSet<string> userFlags
  argument now take an IEnumerable<string> keywords argument. Note: this only affects you if your
  code used named method parameters (e.g. userFlags: myUserFlags).

### MailKit 2.0.7 (2018-10-28)

* Added a work-around for Exchange IMAP servers that send broken multipart BODYSTRUCTURE responses
  without a `body-fld-dsp` token.
* Added support for detecting (but not using) the UNAUTHENTICATE IMAP extension.
* Reintroduced the Pop3Client.GetMessageCount() and GetMessageCountAsync() methods to allow developers
  to poll POP3 servers for new messages. (issue [#762](https://github.com/jstedfast/MailKit/issues/762))
* Fixed SmtpClient's status code logic to handle more than the expected error codes for the
  `MAIL FROM` and `RCPT TO` commands. (issue [#764](https://github.com/jstedfast/MailKit/issues/764))
* Added a work-around for IMAP servers that quote FLAGS responses.
  (issue [#771](https://github.com/jstedfast/MailKit/issues/771))
* Optimized SmtpClient's logic for byte-stuffing the message when writing it to the socket during
  the `DATA` command.
* Added an `SslProtocols` property to IMailService (was already in MailService).
* Fixed the DIGEST-MD5 charset handling.
* Fixed a bug in the BodyPart.TryParse() method that could be used when serializing and deserializing
  FETCH'd responses from an IMAP server.
* Fixed BodyPartCollection.IndexOf(Uri).
* Fixed Envelope.ToString() and TryParse() to properly deal with the rfc822 group address syntax.
* Fixed the ImapClient logic to properly handle parsing nested group addresses (not likely that
  anyone would hit this).
* Improved ImapClient's state tracking so that it is possible to re-connect the ImapClient in the
  Disconnected event handler. (issue [#770](https://github.com/jstedfast/MailKit/issues/770))
* Fixed IMAP API's that take IList of UIDs or indexes to accept 0 UIDs/indexes.
* Fixed ImapClient's BODYSTRUCTURE parser to properly handle multiple body-extensions tokens.
* Fixed ImapClient to properly handle the `* PREAUTH` greeting when connecting to an IMAP server.

### MailKit 2.0.6 (2018-08-04)

* Fixed ImapFolder.GetSubfolders (StatusItems) to make sure that the child folders exist before
  calling STATUS on them when the server does not support the LIST-STATUS command.
* Catch ArgumentExceptions when calling Encoding.GetEncoding(string).
  (issue [#740](https://github.com/jstedfast/MailKit/issues/740))
* Fixed parsing of IMAP threads where the root of a subtree is empty.
  (issue [#739](https://github.com/jstedfast/MailKit/issues/739))
* Added AuthorizationId property for PLAIN and DIGEST-MD5 SASL mechanisms.
* Added MessageSummaryItems.Headers enum to fetch all headers.
  (issue [#738](https://github.com/jstedfast/MailKit/issues/738))

### MailKit 2.0.5 (2018-07-07)

* When throwing AuthenticationException within SmtpClient, add an SmtpCommandException as the
  InnerException property to help consumers diagnose authentication problems.
  (issue [#717](https://github.com/jstedfast/MailKit/issues/717))
* Added support for the authzid to the SASL PLAIN mechanism.
* Modified ProtocolLogger file constructor to support Shared Read and an Append/Overwrite option.
  (issue [#730](https://github.com/jstedfast/MailKit/issues/730))

### MailKit 2.0.4 (2018-05-21)

* Fixed SmtpClient to use the IPv4 literal if the socket is IPv4 address mapped to IPv6.
  (issue [#704](https://github.com/jstedfast/MailKit/issues/704))
* Updated SmtpClient and ImapFolder.Append to use FormatOptions.EnsureNewLine.
  (MimeKit issue [#251](https://github.com/jstedfast/MimeKit/issues/251))

### MailKit 2.0.3 (2018-04-15)

* Fixed IMAP IDLE support.
* Ignore unknown tokens in IMAP untagged FETCH responses such as XAOL.SPAM.REASON.

### MailKit 2.0.2 (2018-03-18)

* Added work-around for ProtonMail's IMAP server. (issue [#674](https://github.com/jstedfast/MailKit/issues/674))
* Added work-around for IMAP servers that do not include msgid in the ENVELOPE response.
  (issue [#669](https://github.com/jstedfast/MailKit/issues/669))
* Added MessageSummaryItems.PreviewText to allow fetching a small preview of the message.
  (issue [#650](https://github.com/jstedfast/MailKit/issues/650))
* Added support for batch fetching IMAP message streams.
  (issue [#650](https://github.com/jstedfast/MailKit/issues/650))

### MailKit 2.0.1 (2018-01-06)

* Obsoleted all SaslMechanism constructors that took a Uri argument and replaced them
  with variants that no longer require the Uri and instead take a NetworkCredential
  or a set of strings for the user name and password. This simplifies authenticating
  with OAuth 2.0:

```csharp
var oauth2 = new SaslMechanismOAuth2 (username, auth_token);

client.Authenticate (oauth2);
```

### MailKit 2.0.0 (2017-12-22)

* Updated MailKit to fully support async IO instead of using Task.Run() wrappers.
* Fixed a resource leak when fetching IMAP body parts gets an exception.
* Fixed each of the Client.Connect() implementtions to catch exceptions thrown by
  IProtocolLogger.LogConnect().
* Removed the ImapFolder.MessagesArrived event.
* Added new Authenticate() methods that take a SaslMechanism to avoid the need to
  manipulate Client.AuthenticationMechanisms in order to tweak which SASL mechanisms
  you'd like the client to use in Authenticate().
* Added new SslHandshakeException with a helpful error message that can be thrown by
  the Connect() methods. This replaces the obscure SocketExceptions previously thrown
  by SslStream.
* Fixed support for the IMAP UTF8=ACCEPT extension.
* Improved ImapFolder.CommitStream() API to provide section, offset and length.
* Treat the SMTP X-EXPS capability in an EHLO response the same as AUTH.
  (issue [#603](https://github.com/jstedfast/MailKit/issues/603))
* Dropped support for .NET 4.0.

Note: As of 2.0, XOAUTH2 is no longer in the list of SASL mechanisms that is tried
when using the Authenticate() methods that have existed pre-MailKit 2.0.
Instead, you must now use Authenticate(SaslMechanism, CancellationToken).

An example usage might look like this:

```csharp
// Note: The Uri isn't used except with ICredentials.GetCredential (Uri) so unless
// you implemented your own ICredentials class, the Uri is a dummy argument.
var uri = new Uri ("imap://imap.gmail.com");
var oauth2 = new SaslMechanismOAuth2 (uri, username, auth_token);

client.Authenticate (oauth2);
```

### MailKit 1.22.0 (2017-11-24)

* Enable TLSv1.1 and 1.2 for .NETStandard.
* Read any remaining literal data after parsing headers. Fixes an issue when requesting
  specific headers in an ImapFolder.Fetch() request if the server sends an extra newline.

### MailKit 1.20.0 (2017-10-28)

* Fixed UniqueIdRange.ToString() to always output a string in the form ${start}:${end} even if
  start == end. (issue [#572](https://github.com/jstedfast/MailKit/issues/572))

### MailKit 1.18.1 (2017-09-03)

* Gracefully handle IMAP COPYUID resp-codes without src or dest uid-set tokens.
  (issue [#555](https://github.com/jstedfast/MailKit/issues/555))
* Be more lenient with unquoted IMAP folder names containing ']'.
  (issue [#557](https://github.com/jstedfast/MailKit/issues/557))

### MailKit 1.18.0 (2017-08-07)

* Improved logic for cached FolderAttributes on ImapFolder objects.
* If/when the \NonExistent flag is present, reset ImapFolder state as it probably means
  another client has deleted the folder.
* Added work-around for home.pl which sends an untagged `* [COPYUID ...]` response
  without an `OK` (technically, the COPYUID resp-code should only appear in the tagged
  response, but accept it anyway).

### MailKit 1.16.2 (2017-07-01)

* Added a leaveOpen param to the ProtocolLogger .ctor.
  (issue [#506](https://github.com/jstedfast/MailKit/issues/506))
* Added a CheckCertificateRevocation property on MailService.
  (issue [#520](https://github.com/jstedfast/MailKit/issues/520))
* Fixed ImapFolder to update the Count property and emit CountChanged when the IMAP server sends
  an untagged VANISHED response. (issue [#521](https://github.com/jstedfast/MailKit/issues/521))
* Fixed ImapEngine to properly handle converting character tokens into strings.
  (issue [#522](https://github.com/jstedfast/MailKit/issues/522))
* Fixed SmtpClient to properly handle DIGEST-MD5 auth errors in order to fall back to the next
  authentication mechanism.
* Fixed Pop3Client to properly detect APOP tokens after arbitrary text.
  (issue [#529](https://github.com/jstedfast/MailKit/issues/529))
* Disabled NTLM authentication since it often doesn't work properly.
  (issue [#532](https://github.com/jstedfast/MailKit/issues/532))

### MailKit 1.16.1 (2017-05-05)

* Properly handle a NIL body-fld-params token for body-part-mpart.
  (issue [#503](https://github.com/jstedfast/MailKit/issues/503))

### MailKit 1.16.0 (2017-04-21)

* Improved IMAP ENVELOPE parser to prevent exceptions when parsing invalid mailbox addresses.
  (issue [#494](https://github.com/jstedfast/MailKit/issues/494))
* Fixed UniqueId and UniqueIdRange to prevent developers from creating invalid UIDs and ranges.
* Fixed ImapFolder.FetchStream() to properly emit MODSEQ changes if the server sends them.
* Fixed SmtpClient to call OnNoRecipientsAccepted even in the non-PIPELINE case.
  (issue [#491](https://github.com/jstedfast/MailKit/issues/491))

### MailKit 1.14.0 (2017-04-09)

* Improved IMAP's BODYSTRUCTURE parser to sanitize the Content-Disposition values.
  (issue [#486](https://github.com/jstedfast/MailKit/issues/486))
* Improved robustness of IMAP's BODYSTRUCTURE parser in cases where qstring tokens have unescaped
  quotes. (issue [#485](https://github.com/jstedfast/MailKit/issues/485))
* Fixed IMAP to properly handle NIL as a folder name in LIST, LSUB and STATUS responses.
  (issue [#482](https://github.com/jstedfast/MailKit/issues/482))
* Added ImapFolder.GetHeaders() to allow developers to download the entire set of message headers.
* Added SMTP support for International Domain Names in email addresses used in the MAIL FROM and
  RCPT TO commands.
* Modified SmtpClient to no longer throw a NotSupportedException when trying to send messages to
  a recipient with a unicode local-part in the email address when the SMTP server does not support
  the SMTPUTF8 extension. Instead, the local-part is passed through as UTF-8, leaving it up to the
  server to reject either the command or the message. This seems to provide the best interoperability.

### MailKit 1.12.0 (2017-03-12)

* Allow an empty string text argument for SearchQuery.ContainsHeader().
  (issue [#451](https://github.com/jstedfast/MailKit/issues/451))
* Fixed SaslMechanism.IsProhibited() logic to properly use logical ands. Thanks to
  Stefan Seering for this fix.

### MailKit 1.10.2 (2017-01-28)

* Added an IsAuthenticated property to IMailService.
* Fixed the ImapFolder.Quota class to not be public.

### MailKit 1.10.1 (2016-12-04)

* Modified the ImapClient to always LIST the INBOX even if it is a namespace in order to get any
  flags set on it.
* Fixed ImapFolder to handle Quota Roots that do not match an existing folder.
  (issue [#433](https://github.com/jstedfast/MailKit/issues/433))
* Added work-around for Courier-IMAP sending "* 0 FETCH ..." on flag changes.
  (issue [#428](https://github.com/jstedfast/MailKit/issues/428))
* Updated MessageSorter to be smarter about validating arguments such that it will only
  check for IMessageSummary fields that it will *actually* need in order to perform
  the specified sort.
* Fixed SmtpClient.Authenticate() to throw an AuthenticationException with a message
  from the SMTP server if available.

### MailKit 1.10.0 (2016-10-31)

* Added SearchQuery.Uids() to allow more powerful search expressions involving sets of uids.
* Changed ImapClient.GetFolders() to return IList instead of IEnumerable.
* Fixed a bug in MessageThreader.
* Fixed bugs in Envelope.ToString() and Envelope.TryParse().
* Fixed NTLM's Type2Message.Encode() logic to properly handle a null TargetInfo field.
* Obsoleted some ImapFolder.Search() methods and replaced them with an equivalent ImapFolder.Sort()
  method.
* Added a ResponseText property to ImapCommandException.
* Fixed ImapFolder to emit a HighestModSeqChanged event when we get untagged FETCH responses with
  a higher MODSEQ value.
* Improved SearchQuery optimization for IMAP.
* Added SearchOptions.None.

### MailKit 1.8.1 (2016-09-26)

* Fixed the NuGet packages to reference MimeKit 1.8.0.
* Added an SmtpClient.QueryCapabilitiesAfterAuthenticating property to work around broken SMTP servers
  where sending EHLO after a successful AUTH command incorrectly resets their authenticated state.

### MailKit 1.8.0 (2016-09-26)

* Added a new Search()/SearchAsync() to ImapFolder that take a raw query string.
* Implemented support for the IMAP FILTERS extension and improved support for the METADATA extension.
* Fixed NTLM authentication support to use NTLMv2. (issue [#397](https://github.com/jstedfast/MailKit/issues/397))
* Added support for IMAP's SEARCH=FUZZY relevancy scores.
* Added an IMailFolder.ModSeqChanged event.
* Added UniqueIdRange.All for convenience.

### MailKit 1.6.0 (2016-09-11)

* Added support for the new IMAP LITERAL- extension.
* Added support for the new IMAP APPENDLIMIT extension.
* Fixed APOP authentication in the Pop3Client. (issue [#395](https://github.com/jstedfast/MailKit/issues/395))
* Reset the SmtpClient's Capabilities after disconnecting.
* Modified ImapFolder.Search() to return a UniqueIdSet for IMAP servers that do not support
  the ESEARCH extension (which already returns a UniqueIdSet).
* Added mail.shaw.ca to the list of SMTP servers that break when sending EHLO after AUTH.
  (issue [#393](https://github.com/jstedfast/MailKit/issues/393))
* Work around broken POP3 servers that reply "+OK" instead of "+" in SASL negotiations.
  (issue [#391](https://github.com/jstedfast/MailKit/issues/391))
* Modified the IMAP parser to properly allow "[" to appear within flag tokens.
  (issue [#390](https://github.com/jstedfast/MailKit/issues/390))

### MailKit 1.4.2.1 (2016-08-16)

* Fixed a regression in 1.4.2 where using a bad password in ImapClient.Authenticate() did not properly
  throw an exception when using a SASL mechanism. (issue [#383](https://github.com/jstedfast/MailKit/issues/383))

### MailKit 1.4.2 (2016-08-14)

* Properly initialize the private Uri fields in Connect() for Windows Universal 8.1.
  (issue [#381, #382](https://github.com/jstedfast/MailKit/issues/381, #382))
* Added SecuritySafeCritical attributes to try and match base Exception in case that matters.
* Added missing GetObjectData() implementation to Pop3CommandException.
* Strong-name the .NET Core assemblies.
* Make sure to process Alert resp-codes in ImapClient.
  (issue [#377](https://github.com/jstedfast/MailKit/issues/377))

### MailKit 1.4.1 (2016-07-17)

* Updated the NTLM SASL mechanism to include a Windows OS version in the response if the server
  requests it (apparently this should only happen if the server is in debug mode).
* Updated the IMAP BODYSTRUCTURE parser to try and work around BODYSTRUCTURE responses that
  do not properly encode the mime-type of a part where it only provides the media-subtype token
  instead of both the media-type and media-subtype tokens.
  (issue [#371](https://github.com/jstedfast/MailKit/issues/371))
* Added smtp.dm.aliyun.com to the list of broken SMTP servers that failed to read the SMTP
  specifications and improperly reset their state after sending an EHLO command after
  authenticating (which the specifications explicitly state the clients SHOULD do).
  (issue [#370](https://github.com/jstedfast/MailKit/issues/370))

### MailKit 1.4.0 (2016-07-01)

* Added support for .NET Core 1.0

### MailKit 1.2.24 (2016-06-16)

* Fixed logic for constructing the HELO command on WP8. (issue [#351](https://github.com/jstedfast/MailKit/issues/351))
* Modified ImapFolder.Search() to not send the optional CHARSET search param if the charset
  is US-ASCII. This way work around some broken IMAP servers that do not properly implement
  support for the CHARSET parameter. (issue [#348](https://github.com/jstedfast/MailKit/issues/348))
* Added more MailService methods to IMailService.

### MailKit 1.2.23 (2016-05-22)

* Properly apply SecurityCriticalAttribute to GetObjectData() on custom Exceptions.
  (issue [#340](https://github.com/jstedfast/MailKit/issues/340))

### MailKit 1.2.22 (2016-05-07)

* Updated IMAP BODY parser to handle a NIL media type by treating it as "application".
* Updated IMAP SEARCH response parser to work around search-return-data pairs within parens.
* Added a missing SmtpStatusCode enum value for code 555.
  (issue [#327](https://github.com/jstedfast/MailKit/issues/327))
* Opened up more of the SearchQuery API to make it possible to serialize/deserialize via JSON.
  (issue [#331](https://github.com/jstedfast/MailKit/issues/331))
* Updated to reference BouncyCastle via NuGet.org packages rather than via project references.

### MailKit 1.2.21 (2016-03-13)

* Replaced SmtpClient's virtual ProcessRcptToResponse() method with OnRecipientAccepted()
  and OnRecipientNotAccepted(). (issue [#309](https://github.com/jstedfast/MailKit/issues/309))
* Added MailService.DefaultServerCertificateValidationCallback() which accepts all
  self-signed certificates (a common operation that consumers want).
* Fixed encoding and decoding of IMAP folder names that include surrogate pairs.
* Fixed IMAP SEARCH logic for X-GM-LABELS.

### MailKit 1.2.20 (2016-02-28)

* Added a work-around for GoDaddy's ASP.NET web host which does not support the iso-8859-1
  System.Text.Encoding (used as a fallback encoding within MailKit) by falling back to
  Windows-1252 instead.
* Improved NTLM support.

### MailKit 1.2.19 (2016-02-13)

* Added support for the SMTP VRFY and EXPN commands.

### MailKit 1.2.18 (2016-01-29)

* If the IMAP server sends a `* ID NIL` response, return null for ImapClient.Identify().
* Allow developers to override the charset used when authenticating.
  (issue [#292](https://github.com/jstedfast/MailKit/issues/292))

### MailKit 1.2.17 (2016-01-24)

* Exposed MailKit.Search.OrderByType and MailKit.Search.SortOrder to the public API.
* Modified IMailFolder.CopyTo() and MoveTo() to return a UniqueIdMap instead of a UniqueIdSet.
* Improved ImapProtocolException error messages to be more informative.
* Added an IsSecure property to ImapClient, Pop3Client and SmtpClient.
* Fixed support for the IMAP COMPRESS=DEFLATE extension to work properly.
* Modified UniqueId.Id and .Validity to be properties instead of fields.
* Reduced memory usage for UniqueIdRange (-33%) and UniqueIdSet (-50%).
* Vastly improved the performance of UniqueIdSet (~2x).
* Added an ImapClient.GetFolders() overload that also requests the status of each folder.
* Modified the headersOnly parameter to the various Pop3Client.GetStream() methods to default to
  false instead of forcing developers to pass in a value.
* Updated the IMAP, POP3 and SMTP clients to be stricter with validating SSL certificates.

### MailKit 1.2.16 (2016-01-01)

* Added support for the SCRAM-SHA-256 SASL mechanism.
* Added support for the CREATE-SPECIAL-USE IMAP extension.
* Added support for the METADATA IMAP extension.
* Added support for the LIST-STATUS IMAP extension.

### MailKit 1.2.15 (2015-11-29)

* Be more forgiving during SASL auth when a POP3 server sends unexpected text after a + response.
  (issue [#268](https://github.com/jstedfast/MailKit/issues/268))

### MailKit 1.2.14 (2015-11-22)

* Fixed ImapFolder.Search() to not capitalize the date strings in date queries.
  (issue [#252](https://github.com/jstedfast/MailKit/issues/252))
* Fixed filtering logic in ImapFolder.GetSubfolders() to not filter out subfolders named Inbox.
  (issue [#255](https://github.com/jstedfast/MailKit/issues/255))
* Exposed SmtpClient.ProcessRcptToResponse() as virtual protected to allow subclasses to override
  error handling. (issue [#256](https://github.com/jstedfast/MailKit/issues/256))
* Modified SmtpCommandException .ctors to be public and fixed serialization logic.
  (issue [#257](https://github.com/jstedfast/MailKit/issues/257))
* Added workaround for broken smtp.sina.com mail server.
* Throw a custom ImapProtocolException on "* BYE" during connection instead of "unexpected token".
  (issue [#262](https://github.com/jstedfast/MailKit/issues/262))

### MailKit 1.2.13 (2015-10-18)

* Fixed SmtpClient to not double dispose the socket.
* Added a BodyPartVisitor class.
* Fixed ImapFolder to allow NIL tokens for body parts. (issue [#244](https://github.com/jstedfast/MailKit/issues/244))

### MailKit 1.2.12 (2015-09-20)

* Allow developers to specify a local IPEndPoint to use for connecting to remote servers.
  (issue [#247](https://github.com/jstedfast/MailKit/issues/247))
* Added support for NIL GMail labels. (issue [#244](https://github.com/jstedfast/MailKit/issues/244))

### MailKit 1.2.11.1 (2015-09-08)

* Fixed ImapFolder.GetSubfolders() to work with Yahoo! Mail and other IMAP servers that
  do not use the canonical INBOX naming convention for the INBOX folder.
  (issue [#242](https://github.com/jstedfast/MailKit/issues/242))

### MailKit 1.2.11 (2015-09-06)

* Fixed SmtpStream logic for determining if a call to ReadAhead() is needed.
  (issue [#232](https://github.com/jstedfast/MailKit/issues/232))
* Fixed ImapFolder.Close() to change the state to Closed even if the IMAP server does not
  support the UNSELECT command.
* Allow the UIDVALIDITY argument to the COPYUID and APPENDUID resp-codes to be 0 even though
  that value is illegal. Improves compatibility with SmarterMail.
  (issue [#240](https://github.com/jstedfast/MailKit/issues/240))

### MailKit 1.2.10 (2015-08-16)

* Added an SslProtocols property to ImapClient, Pop3Client, and SmtpClient to allow
  developers to override which SSL protocols are to be allowed for SSL connections.
  (issue [#229](https://github.com/jstedfast/MailKit/issues/229))
* Added a work-around for GMail IMAP (and other IMAP servers) that sometimes send an
  illegal MODSEQ value of 0. (issue [#228](https://github.com/jstedfast/MailKit/issues/228))

### MailKit 1.2.9 (2015-08-08)

* Fixed ImapFolder.Append() methods to make sure to encode the message with <CR><LF>
  line endings.
* Added UniqueId.Invalid that can be used for error conditions.
* Added UniqueId.IsValid property to check that the UniqueId is valid.
* Added Opened and Closed events to IMailFolder.
* Fixed the QRESYNC version of the IMailFolder.Open() method to take a uint uidValidity
  instead of a UniqueId uidValidity argument for consistency.
* Updated MessageSorter.Sort() to be an extension method and added a List<T> overload.
* Updated MessageThreader.Thread() to be extension methods (required reordering of args).
* Merged ISortable and IThreadable interfaces into IMessageSummary in order to
  remove duplicated properties and simplify things.
* Renamed IMessageSummary.MessageSize to IMessageSummary.Size.
* Modified IMessageSummary.UniqueId to no longer be nullable.
* Added TextBody, HtmlBody, BodyParts and Attachments properties to IMessageSummary.
* Modified the IMAP parser to allow NIL for the Content-Type and subtype strings in
  BODY and BODYSTRUCTURE values even though it is illegal. (issue [#226](https://github.com/jstedfast/MailKit/issues/226))
* Modified the IMAP parser to properly handle Message-Id tokens that are not properly
  encapsulated within angle brackets. (issue [#224](https://github.com/jstedfast/MailKit/issues/224))
* Fixed IMAP to properly deal with folder names that contained unescaped square brackets.
  (issue [#222](https://github.com/jstedfast/MailKit/issues/222))

### MailKit 1.2.8 (2015-07-19)

* Fixed ImapFolder to dispose the temporary streams used in GetMessage and GetBodyPart.
* Added a MessageNotFoundException.
* Added an ImapCommandResponse property to ImapCommandException.
* Fixed SmtpClient to filter out duplicate recipient addresses in RCPT TO.
* Modified MessageSorter/Threader to take IList<OrderBy> arguments instead of OrderBy[].
* Added support for parsing group addresses in IMAP ENVELOPE responses.
* Disable SASL-IR support for the LOGIN mechanism. (issue [#216](https://github.com/jstedfast/MailKit/issues/216))
* Capture whether or not the IMAP server supports the I18NLEVEL and LANGUAGE extensions.

### MailKit 1.2.7 (2015-07-06)

* Fixed ImapFolder.Rename() to properly emit the Renamed event for child folders as well.
* Fixed ImapFolder.Fetch() to always fill in the Headers property when requesting specific
  headers even if the server replies with an empty list. (issue [#210](https://github.com/jstedfast/MailKit/issues/210))

### MailKit 1.2.6 (2015-06-25)

* Fixed UniqueIdSet.CopyTo() to work properly (also fixes LINQ usage).
* Fixed ImapFolder.Status() where StatusItems.HighestModSeq is used.

### MailKit 1.2.5 (2015-06-22)

* Added support for extended IMAP search options (see the SearchOptions flags).
* Added TryParse() convenience methods for UniqueIdSet, UniqueIdRange, and UniqueId.
* Added a workaround for a GMail IMAP BODYSTRUCTURE bug. (issue [#205](https://github.com/jstedfast/MailKit/issues/205))
* Added a ProtocolLogger property for ImapClient, Pop3Client, and SmtpClient.
* Fixed the ImapFolder.GetStream() methods that take a BodyPart to call the
  proper overload.

### MailKit 1.2.4 (2015-06-14)

* Updated SmtpClient to use MimeMessage.Prepare() instead of implementing its own logic.
* Added a new ITransferProgress interface and updated IMAP, POP3 and SMTP methods to
  take an optional ITransferProgress parameter to allow for progress reporting.
* Implemented client-side UID EXPUNGE for IMAP servers that do not support the UIDPLUS
  extension.
* Improved API documentation.

### MailKit 1.2.3 (2015-06-01)

* Fixed ImapFolder.AddFlags() to throw FolderNotOpenException if the folder is not
  opened in read-write mode. (issue [#202](https://github.com/jstedfast/MailKit/issues/202))
* Fixed ImapFolder.GetMessage/BodyPart/Stream() to not modify a dictionary while
  looping over it. (issue [#201](https://github.com/jstedfast/MailKit/issues/201))
* Fixed ImapFolder to throw FolderNotFoundException instead of ArgumentException
  when the command fails due to the folder not existing.

### MailKit 1.2.2 (2015-05-31)

* Added ImapClient.GetFolders(FolderNamespace, ...) to allow getting the full
  (recursive) list of folders for a particular namespace.
* Added a FolderAttributes.Inbox flag that gets set on the Inbox folder.
* Fixed the IMAP code to properly treat the INBOX folder name case-insensitively.
* Added ServiceNotConnectedException, ServiceNotAuthenticatedException, and
  FolderNotOpenException as a more specific errors than InvalidOperationException.
  (Note: they all subclass InvalidOperationException so old code continues to work).
* Added Pop3Client.GetStream() to allow fetching messages or headers as an unparsed
  stream. (issue [#198](https://github.com/jstedfast/MailKit/issues/198))
* Fixed usage of Socket.Poll() to not loop 1000 times per second.
* Added more ImapFolder.GetStream() overloads.
* Added ImapFolder.CreateStream() and CommitStream() protected methods which are meant
  for subclasses that intend to implement caching.

### MailKit 1.2.1 (2015-05-26)

* Added hooks to allow subclassing ImapFolder.

### MailKit 1.2.0 (2015-05-24)

* Added new ImapFolder.GetStream() overloads that allow fetching only the TEXT
  stream.
* Fixed ImapFolder.Search() to always treat the search results as UIDs even
  when the server (such as AOL) does not include the required UID tag in the
  ESEARCH response. (issue [#191](https://github.com/jstedfast/MailKit/issues/191))
* Fixed ImapClient to set the engine.Uri even for Windows*81 profiles (fixes
  a NullReferenceException for the various Windows*81 profiles). (issue [#192](https://github.com/jstedfast/MailKit/issues/192))
* Work around a GMail bug where it does not quote flags containing []'s.
  (issue [#193](https://github.com/jstedfast/MailKit/issues/193))
* Fixed the IMAP code to accept GMail label names that start with a '+'.
  (issue [#195](https://github.com/jstedfast/MailKit/issues/195))
* Delay throwing ProtocolException due to an unexpected disconnect when reading
  responses to PIPELINE'd SMTP commands in case one of the responses to those
  commands contains an error code that might hint at why the server disconnected.
  (issue [#194](https://github.com/jstedfast/MailKit/issues/194))

### MailKit 1.0.17 (2015-05-12)

* Fixed a STARTTLS regression in SmtpClient that was introduced in 1.0.15.
  (issue [#187](https://github.com/jstedfast/MailKit/issues/187))

### MailKit 1.0.16 (2015-05-10)

* Modified the Pop3Client to immediately query for the message count once the
  client is authenticated. This allows the Pop3Client to now have a Count
  property that replaces the need for calling GetMessageCount(). (issue [#184](https://github.com/jstedfast/MailKit/issues/184))

### MailKit 1.0.15 (2015-05-09)

* Added SearchQuery.HeaderContains() and obsoleted SearchQuery.Header() for
  API consistency.
* Added workaround for GMail's broken FETCH command parser that does not accept
  aliases. (issue [#183](https://github.com/jstedfast/MailKit/issues/183))

### MailKit 1.0.14 (2015-04-11)

* Added a ServerCertificateValidationCallback property to all clients so that
  it is not necessary to set the global
  System.Net.ServicePointManager.ServerCertificateValidationCallback property.
* Fixed MailService.Connect(Uri) to properly handle Uri's with Port value that
  had not been explicitly set. (issue [#170](https://github.com/jstedfast/MailKit/issues/170))
* Added logic to properly handle MODSEQ-based search responses.
  (issue [#166 and issue #173](https://github.com/jstedfast/MailKit/issues/166 and issue #173))
* When an ImapClient gets disconnected, if an ImapFolder was in an opened state,
  update its state to closed to prevent confusion once the ImapClient is
  reconnected.
* Fixed a bug in Pop3Client.Authenticate() for servers that just reply with
  "+OK\r\n" to the SASL challenge. (issue [#171](https://github.com/jstedfast/MailKit/issues/171))
* Clear the POP3 capability flags if the POP3 server responds with -ERR at
  any time. Some servers will reply with a list of capabilities until the
  client is authenticated, and then reply with -ERR meaning that the client
  should not attempt to use previously listed capabilities. (issue [#174](https://github.com/jstedfast/MailKit/issues/174))

### MailKit 1.0.13 (2015-03-29)

* Added a FileName convenience property to BodyPartBasic which works the same
  way as the MimeKit.MimePart.FileName property.
* Added a MessageSummaryFetched event to IMailFolder to better enable developers
  to both provide progress feedback to their users as well as enable them to
  better recover from exceptions (such as a dropped connection) occurring during
  the fetching of message summaries.
* Added support for the IMAP SORT=DISPLAY extension.
* Added a work-around for Cyrus IMAP 2.4.16 sending untagged SEARCH responses
  when untagged ESEARCH responses are expected.

### MailKit 1.0.12 (2015-03-21)

* Fixed ImapFolder.GetMessage(), GetBodyPart() and GetStream() to throw an
  ImapCommandException rather than returning null if the server did not
  response with the message data.
* Added new, much more usable, Connect() methods to ImapClient, Pop3Client,
  and SmtpClient that take a hostname, port, and SecureSocketOptions.
* Added a workaround for smtp.strato.de's blatant disregard for standards.
  (issue [#162](https://github.com/jstedfast/MailKit/issues/162))
* Fixed ImapFolder.Close() to require ReadWrite access if expunge is true.
* Fixed IMAP SORT queries to inject "RETURN" before the orderBy param.
  (issue [#164](https://github.com/jstedfast/MailKit/issues/164))
* Implemented support for the IMAP ACL extension.

### MailKit 1.0.11 (2015-03-14)

* Make sure that the IMAP stream supports timeouts before using them (fixes a
  regression introduced in 1.0.10).
* Added BodyParts and Attachments convenience properties to MessageSummary.
* Added TextBody and HtmlBody convenience properties to MessageSummary.
* Added ImapClient.IsAuthenticated, Pop3Client.IsAuthenticated and
  SmtpClient.IsAuthenticated properties.
* Changed the ImapClient.Inbox property to throw InvalidOperationException if
  you try to access it before authenticating instead of returning null.
* Added an ImapClient.IsIdle property to check if the ImapClient is currently
  in the IDLE state.

### MailKit 1.0.10 (2015-03-08)

* Added support for the IMAP ID extension.

### MailKit 1.0.9 (2015-03-02)

* Modified UniqueId to contain a Validity value. This allows ImapFolder.Append(),
  CopyTo(), and MoveTo() to provide the caller with a way to make sure that the
  UIDs are (still) valid in the destination folder at a future point in time.
* Modified ImapFolder.UidValidity to be a uint instead of a UniqueId which not
  only makes more sense but also simplifies comparison.
* Fixed GMail Label APIs to use the modified UTF-7 encoding logic meant for
  folder names as it appears that GMail wants label names to be encoded in this
  way. (issue [#154](https://github.com/jstedfast/MailKit/issues/154))

### MailKit 1.0.8 (2015-02-19)

* Fixed the SMTP BINARYMIME extension support to work properly. (issue [#151](https://github.com/jstedfast/MailKit/issues/151))
* Fixed ImapFolder.Open() to not set the PermanentFlags to None if another
  folder was open (preventing SetFlags/AddFlags/RemoveFlags from functioning
  properly). (issue [#153](https://github.com/jstedfast/MailKit/issues/153))

### MailKit 1.0.7 (2015-02-17)

* Marked Pop3Client methods that take UIDs as [Obsolete]. It is suggested that
  the equivalent methods that take indexes be used instead and that UID-to-index
  mapping is done by the developer. This takes the burden off of the Pop3Client
  to maintain a mapping of UIDs to indexes that it cannot easily maintain.
* Fixed SmtpCommandException to only serialize the Mailbox property when it is
  non-null. (issue [#148](https://github.com/jstedfast/MailKit/issues/148))
* Fixed IMAP support to accept a UIDVALIDITY value of 0 (even though it is
  technically illegal) to work around a bug in SmarterMail 13.0. (issue [#150](https://github.com/jstedfast/MailKit/issues/150))
* Fixed ImapFolder.GetSubfolders() to filter out non-child folders from the list
  that it returns (once again, a work-around for a SmarterMail 13.0 bug).
  (issue [#149](https://github.com/jstedfast/MailKit/issues/149))

### MailKit 1.0.6 (2015-01-18)

* Fixed some issues revealed by source analysis.
* Migrated the iOS assemblies to Xamarin.iOS Unified API for 64-bit support.

Note: If you are not yet ready to port your iOS application to the Unified API,
      you will need to stick with the 1.0.5 release. The Classic MonoTouch API
      is no longer supported.

### MailKit 1.0.5 (2015-01-08)

* Added Connect() overloads which takes a Socket argument (issue [#128](https://github.com/jstedfast/MailKit/issues/128)).
* Added support for SMTP Delivery Status Notifications (issue [#136](https://github.com/jstedfast/MailKit/issues/136)).
* Modified the ImapFolder logic such that if the IMAP server does not
  send a PERMANENTFLAGS resp-code when SELECTing the folder, then it
  will assume that all flags are permanent (issue [#140](https://github.com/jstedfast/MailKit/issues/140)).

### MailKit 1.0.4 (2014-12-13)

* Modified the IMAP BODYSTRUCTURE parser to allow NIL tokens for
  Content-Type and Content-Disposition parameter values. (issue [#124](https://github.com/jstedfast/MailKit/issues/124))
* Added ImapFolder.GetBodyPart() overrides to allow fetching body parts
  based on a part specifier string. (issue [#130](https://github.com/jstedfast/MailKit/issues/130))

### MailKit 1.0.3 (2014-12-05)

* Added a new ImapFolder.Fetch() overload that takes a HashSet<string>
  of header fields to fetch instead of a HashSet<HeaderId> for
  developers that need the ability to request custom headers not
  defined in the HeaderId enum.
* Added an SmtpClient.MessageSent event and an OnMessageSent() method
  that can be overridden.

### MailKit 1.0.2 (2014-11-23)

* Modified ProtocolLogger to flush the stream at the end of each Log().
* Fixed IMAP SEARCH queries with empty string arguments.
* Fixed the IMAP FETCH parser to accept qstrings and literals for
  header field names.
* Improved documentation.

### MailKit 1.0.1 (2014-10-27)

* Fixed Pop3Client.GetMessages (int startIndex, int count, ...) to use
  1-based sequence numbers.
* Fixed POP3 PIPELINING support to work as intended (issue [#114](https://github.com/jstedfast/MailKit/issues/114)).
* Added a work-around for Office365.com IMAP to avoid
  ImapProtocolExceptions about unexpected '[' tokens when moving or
  copying messages between folders (issue [#115](https://github.com/jstedfast/MailKit/issues/115)).
* Disabled SSLv3 for security reasons (POODLE), opting instead to use TLS.
