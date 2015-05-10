MailKit is a cross-platform mail client library built on top of [MimeKit](https://github.com/jstedfast/MimeKit).

## Features

* SASL Authentication
  * CRAM-MD5
  * DIGEST-MD5
  * LOGIN
  * NTLM
  * PLAIN
  * SCRAM-SHA-1
  * XOAUTH2 (partial support - you need to fetch the auth tokens yourself)
* SMTP Client
  * Supports all of the SASL mechanisms listed above.
  * Supports SSL-wrapped connections via the "smtps" protocol.
  * Supports client SSL/TLS certificates.
  * Supports the following extensions: STARTTLS, SIZE, DSN, 8BITMIME, PIPELINING, BINARYMIME, SMTPUTF8
  * All APIs are cancellable.
  * Async APIs are available.
* POP3 Client
  * Supports all of the SASL mechanisms listed above.
  * Also supports authentication via APOP and USER/PASS.
  * Supports SSL-wrapped connections via the "pops" protocol.
  * Supports client SSL/TLS certificates.
  * Supports the following extensions: STLS, UIDL, PIPELINING, UTF8, LANG
  * All APIs are cancellable.
  * Async APIs are available.
* IMAP4 Client
  * Supports all of the SASL mechanisms listed above.
  * Supports SSL-wrapped connections via the "imaps" protocol.
  * Supports client SSL/TLS certificates.
  * Supports the following extensions:
    * ACL
    * QUOTA
    * LITERAL+
    * IDLE
    * NAMESPACE
    * ID
    * CHILDREN
    * LOGINDISABLED
    * STARTTLS
    * MULTIAPPEND
    * UNSELECT
    * UIDPLUS
    * CONDSTORE
    * ESEARCH
    * SASL-IR
    * COMPRESS
    * WITHIN
    * ENABLE
    * QRESYNC
    * SORT
    * THREAD
    * ESORT (partial)
    * SPECIAL-USE
    * SEARCH=FUZZY (partial)
    * MOVE
    * UTF8=ACCEPT
    * UTF8=ONLY
    * XLIST
    * X-GM-EXT1 (X-GM-MSGID, X-GM-THRID, X-GM-RAW and X-GM-LABELS)
  * All APIs are cancellable.
  * Async APIs are available.
* Client-side sorting and threading of messages.
