# Release Notes

### MailKit 1.0.12

* Fixed ImapFolder.GetMessage(), GetBodyPart() and GetStream() to throw an
  ImapCommandException rather than returning null if the server did not
  response with the message data.
* Added new, much more usable, Connect() methods to ImapClient, Pop3Client,
  and SmtpClient that take a hostname, port, and SecureSocketOptions.
* Added a workaround for smtp.strato.de's blatant disregard for standards.
  (issue #162)
* Fixed ImapFolder.Close() to require ReadWrite access if expunge is true.
* Fixed IMAP SORT queries to inject "RETURN" before the orderBy param.
  (issue #164)
* Implemented support for the IMAP ACL extension.

### MailKit 1.0.11

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

### MailKit 1.0.10

* Added support for the IMAP ID extension.

### MailKit 1.0.9

* Modified UniqueId to contain a Validity value. This allows ImapFolder.Append(),
  CopyTo(), and MoveTo() to provide the caller with a way to make sure that the
  UIDs are (still) valid in the destination folder at a future point in time.
* Modified ImapFolder.UidValidity to be a uint instead of a UniqueId which not
  only makes more sense but also simplifies comparison.
* Fixed GMail Label APIs to use the modified UTF-7 encoding logic meant for
  folder names as it appears that GMail wants label names to be encoded in this
  way. (issue #154)

### MailKit 1.0.8

* Fixed the SMTP BINARYMIME extension support to work properly. (issue #151)
* Fixed ImapFolder.Open() to not set the PermanentFlags to None if another
  folder was open (preventing SetFlags/AddFlags/RemoveFlags from functioning
  properly). (issue #153)

### MailKit 1.0.7

* Marked Pop3Client methods that take UIDs as [Obsolete]. It is suggested that
  the equivalent methods that take indexes be used instead and that UID-to-index
  mapping is done by the developer. This takes the burden off of the Pop3Client
  to maintain a mapping of UIDs to indexes that it cannot easily maintain.
* Fixed SmtpCommandException to only serialize the Mailbox property when it is
  non-null. (issue #148)
* Fixed IMAP support to accept a UIDVALIDITY value of 0 (even though it is
  technically illegal) to work around a bug in SmarterMail 13.0. (issue #150)
* Fixed ImapFolder.GetSubfolders() to filter out non-child folders from the list
  that it returns (once again, a work-around for a SmarterMail 13.0 bug).
  (issue #149)

### MailKit 1.0.6

* Fixed some issues revealed by source analysis.
* Migrated the iOS assemblies to Xamarin.iOS Unified API for 64-bit support.

Note: If you are not yet ready to port your iOS application to the Unified API,
      you will need to stick with the 1.0.5 release. The Classic MonoTouch API
      is no longer supported.

### MailKit 1.0.5

* Added Connect() overloads which takes a Socket argument (issue #128).
* Added support for SMTP Delivery Status Notifications (issue #136).
* Modified the ImapFolder logic such that if the IMAP server does not
  send a PERMANENTFLAGS resp-code when SELECTing the folder, then it
  will assume that all flags are permanent (issue #140).

### MailKit 1.0.4

* Modified the IMAP BODYSTRUCTURE parser to allow NIL tokens for
  Content-Type and Content-Disposition parameter values. (issue #124)
* Added ImapFolder.GetBodyPart() overrides to allow fetching body parts
  based on a part specifier string. (issue #130)

### MailKit 1.0.3

* Added a new ImapFolder.Fetch() overload that takes a HashSet<string>
  of header fields to fetch instead of a HashSet<HeaderId> for
  developers that need the ability to request custom headers not
  defined in the HeaderId enum.
* Added an SmtpClient.MessageSent event and an OnMessageSent() method
  that can be overridden.

### MailKit 1.0.2

* Modified ProtocolLogger to flush the stream at the end of each Log().
* Fixed IMAP SEARCH queries with empty string arguments.
* Fixed the IMAP FETCH parser to accept qstrings and literals for
  header field names.
* Improved documentation.

### MailKit 1.0.1

* Fixed Pop3Client.GetMessages (int startIndex, int count, ...) to use
  1-based sequence numbers.
* Fixed POP3 PIPELINING support to work as indtended (issue #114).
* Added a work-around for Office365.com IMAP to avoid
  ImapProtocolExceptions about unexpected '[' tokens when moving or
  copying messages between folders (issue #115).
* Disabled SSLv3 for security reasons (POODLE), opting instead to use
  TLS.
