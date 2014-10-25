# Release Notes

### MailKit 1.0.1.0

* Fixed Pop3Client.GetMessages (int startIndex, int count, ...) to use 1-based sequence numbers.
* Fixed POP3 PIPELINING support to work as indtended (issue #114).
* Added a work-around for Office365.com IMAP to avoid ImapProtocolExceptions about unexpected '[' tokens when moving or copying messages between folders (issue #115).
