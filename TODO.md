## TODO

* SASL Authentication
  * Include code to fetch an OAuth2 token?
  * ANONYMOUS
  * GSSAPI
* SMTP Client
  * CHUNKING (the BDAT command is already implemented and used by BINARYMIME but
    perhaps the BDAT command could be used always when the server supports the
    CHUNKING extension to avoid needing to byte-stuff the message?)
  * Throw an exception if the MimeMessage is larger than the SIZE value?
* IMAP4 Client
  * Consolidate MessageFlagsChanged, MessageLabelsChanged, and ModSeqChanged events into a single event?
  * Extensions:
    * BINARY
    * CATENATE
    * LIST-EXTENDED (Note: partially implemented already)
    * CONVERT (Note: none of the mainstream IMAP servers seem to support this)
    * ANNOTATE
    * NOTIFY (Note: only Dovecot seems to support this)
    * MULTISEARCH (Note: none of the mainstream IMAP servers seem to support this)
* Maildir
* Thunderbird mbox folder trees
