## TODO

* SASL Authentication
  * Include code to fetch an OAuth2 token?
  * GSSAPI
* SMTP Client
  * Throw an exception if the MimeMessage is larger than the SIZE value?
* IMAP4 Client
  * Consolidate MessageFlagsChanged, MessageLabelsChanged, and ModSeqChanged events into a single event?
  * Extensions:
    * BINARY
    * CATENATE
    * LIST-EXTENDED (Note: partially implemented already)
    * CONVERT (Note: none of the mainstream IMAP servers seem to support this)
    * MULTISEARCH (Note: none of the mainstream IMAP servers seem to support this)
    * UNAUTHENTICATE
* MessageThreader
  * Fix UniqueId property to be just a UniqueId instead of Nullable<UniqueId>.
* IMailFolder
  * Modify Append() methods to simply return UniqueId instead of Nullable<UniqueId>?
  * Modify CopyTo/MoveTo() methods to also return UniqueId instead of Nullable<UniqueId>?
* Maildir
* Thunderbird mbox folder trees
