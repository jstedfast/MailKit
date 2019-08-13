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
    * ANNOTATE-EXPERIMENT-1 (not widely supported)
    * BINARY
    * CATENATE
    * LIST-EXTENDED (Note: partially implemented already)
    * CONVERT (Note: none of the mainstream IMAP servers seem to support this)
    * ANNOTATE
    * MULTISEARCH (Note: none of the mainstream IMAP servers seem to support this)
    * UNAUTHENTICATE
  * Reduce API bloat for Fetch(), [Add,Remove,Set]Flags() and [Add,Remove,Set]Labels().
    * Fetch() could take a FetchQuery argument that has all of the options (other than UIDs/indexes).
    * [Add,Remove,Set]Flags() and Labels() could become Store() and take the following arguments:
      * UniqueId, IList<UniqueId>, int, or IList<int> to specify which messages.
      * `StoreAction action`: enum that specifies Add/Remove/Set.
      * `StoreOptions options`: a class would have: `bool Silent` and `ulong UnchangedSince`.
      * and the flags/keywords(/labels).
* Maildir
* Thunderbird mbox folder trees
