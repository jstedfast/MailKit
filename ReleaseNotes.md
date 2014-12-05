# Release Notes

### MailKit 1.0.3.0

* Added a new ImapFolder.Fetch() overload that takes a HashSet<string>
  of header fields to fetch instead of a HashSet<HeaderId> for
  developers that need the ability to request custom headers not
  defined in the HeaderId enum.
* Added an SmtpClient.MessageSent event and an OnMessageSent() method
  that can be overridden.

### MailKit 1.0.2.0

* Modified ProtocolLogger to flush the stream at the end of each Log().
* Fixed IMAP SEARCH queries with empty string arguments.
* Fixed the IMAP FETCH parser to accept qstrings and literals for
  header field names.
* Improved documentation.

### MailKit 1.0.1.0

* Fixed Pop3Client.GetMessages (int startIndex, int count, ...) to use
  1-based sequence numbers.
* Fixed POP3 PIPELINING support to work as indtended (issue #114).
* Added a work-around for Office365.com IMAP to avoid
  ImapProtocolExceptions about unexpected '[' tokens when moving or
  copying messages between folders (issue #115).
* Disabled SSLv3 for security reasons (POODLE), opting instead to use
  TLS.
