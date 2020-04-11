## TODO

* SASL Authentication
  * Include code to fetch an OAuth2 token?
  * ANONYMOUS
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
  * Reduce API bloat for Fetch(), [Add,Remove,Set]Flags() and [Add,Remove,Set]Labels().
    * Fetch() could take a FetchQuery argument that has all of the options (other than UIDs/indexes).
      This *could* possibly allow requesting specific headers and/or NOT specific headers.
    * [Add,Remove,Set]Flags() and Labels() could become Store() and take the following arguments:
      * UniqueId, IList<UniqueId>, int, or IList<int> to specify which messages.
      * `StoreAction action`: enum that specifies Add/Remove/Set.
      * `StoreOptions options`: a class would have: `bool Silent` and `ulong UnchangedSince`.
      * and the flags/keywords(/labels).
    * Append() could use a similar approach so that the MimeMessage and the flags/date/annotations/etc.
      are all contained within an AppendRequest object. This would simplify MUTLIAPPEND support. We
      could also allow Append()ing raw Streams?
* MessageThreader
  * Fix UniqueId property to be just a UniqueId instead of Nullable<UniqueId>.
* IMailFolder
  * Modify Append() methods to simply return UniqueId instead of Nullable<UniqueId>?
  * Modify CopyTo/MoveTo() methods to also return UniqueId instead of Nullable<UniqueId>?
* Maildir
* Thunderbird mbox folder trees
