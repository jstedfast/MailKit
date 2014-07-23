## TODO

* SASL Authentication
  * Include code to fetch an OAuth2 token?
  * ANONYMOUS
  * GSSAPI
* SMTP Client
  * CHUNKING (hmmm, doesn't really seem all that useful...)
  * Throw an exception if the MimeMessage is larger than the SIZE value?
* POP3 Client
  * Rename Pop3Client.DeleteMessage() to Pop3Client.Delete()? Less verbose...
* IMAP4 Client
  * Extensions:
    * ACL
    * BINARY
    * CATENATE
    * LIST-EXTENDED (Note: partially implemented already for getting the special folders)
    * CONVERT (Note: none of the mainstream IMAP servers seem to support this)
    * ANNOTATE
    * METADATA
    * NOTIFY (Note: only Dovecot seems to support this)
    * FILTERS (Note: none of the mainstream IMAP servers seem to support this)
    * LIST-STATUS (Note: only Dovecot seems to support this)
    * CREATE-SPECIAL-USE (Note: not widely supported)
    * MULTISEARCH (Note: none of the mainstream IMAP servers seem to support this)
* Maildir
* Thunderbird mbox folder trees
