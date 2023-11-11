using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using MimeKit;

namespace Examples {
    class MimeParserExamples
    {
        #region ParseMessage
        public static MimeMessage ParseMessage (string fileName)
        {
            // Load a MimeMessage from a file path or stream
            using (var stream = File.OpenRead (fileName)) {
                var parser = new MimeParser (stream, MimeFormat.Entity);

                return parser.ParseMessage ();
            }
        }
        #endregion // ParseMessage

        #region ParseMbox
        public static void ParseMbox (string fileName)
        {
            // Load every message from a Unix mbox spool.
            using (var stream = fileName.OpenRead (fileName)) {
                var parser = new MimeParser (stream, MimeFormat.Mbox);

                while (!parser.IsEndOfStream) {
                    MimeMessage message = parser.ParseMessage ();
                    long mboxMarkerOffset = parser.MboxMarkerOffset;
                    string mboxMarker = parser.MboxMarker;

                    Console.WriteLine ($"MBOX marker found @ {mboxMarkerOffset}: {mboxMarker}");

                    // TODO: Do something with the message.
                }
            }
        }
        #endregion // ParseMboxSpool

        #region MessageOffsets
        class MimeOffsets
        {
            public string MimeType { get; set; }

            public long? MboxMarkerOffset { get; set; }

            public int LineNumber { get; set; }

            public long BeginOffset { get; set; }

            public long HeadersEndOffset { get; set; }

            public long EndOffset { get; set; }

            public MimeOffsets Message { get; set; }

            public List<MimeOffsets> Children { get; set; }

            public long Octets { get; set; }

            public int? Lines { get; set; }
        }

        public static void MimeOffsetsExample (string fileName)
        {
            using (var stream = fileName.OpenRead (fileName)) {
                var messages = new Dictionary<MimeMessage, MimeOffsets> ();
                var entities = new Dictionary<MimeEntity, MimeOffsets> ();
                MimeOffsets messageOffsets = null;

                var parser = new MimeParser (stream, MimeFormat.Entity);

                // Connect a handler to track MimeMessage begin offsets
                parser.MimeMessageBegin += delegate (sender, args) {
                    var parser = (MimeParser) sender;

                    // Create a new MimeOffsets for this message.
                    var offsets = new MimeOffsets {
                        BeginOffset = args.BeginOffset,
                        LineNumber = args.LineNumber
                    };

                    if (args.Parent != null) {
                        // If we get here, then it means that the MimeMessage is part of
                        // a message/rfc822 "attachment".
                        var parentOffsets = entities[args.Parent];
                        parentOffsets.Message = offsets;
                    } else {
                        // Otherwise, this is the top-level MimeMessage.
                        offsets.MboxMarkerOffset = parser.MboxMarkerOffset;
                        messageOffsets = offsets;
                    }

                    messages.Add (args.Message, offsets);
                };

                // Connect a handler to track MimeMessage end offsets
                parser.MimeMessageEnd += delegate (sender, args) {
                    // Our MimeMessageBegin event handler already created a MimeOffsets for
                    // this message. Use the `messages` dictionary to retrieve it.
                    var offsets = messages[args.Message];

                    // Track the size of the MimeMessage in octets (aka bytes), the offset
                    // for the end of the header block, and the end of the message.
                    offsets.Octets = args.EndOffset - args.HeadersEndOffset;
                    offsets.HeadersEndOffset = args.HeadersEndOffset;
                    offsets.EndOffset = args.EndOffset;
                };

                // Connect a handler to track MimeEntity begin offsets
                parser.MimeEntityBegin += delegate (sender, args) {
                    // Create a new MimeOffsets for this MIME entity (which could be a MimePart, MessagePart, or Multipart).
                    var offsets = new MimeOffsets {
                        MimeType = args.Entity.ContentType.MimeType,
                        BeginOffset = args.BeginOffset,
                        LineNumber = args.LineNumber
                    };

                    if (args.Parent != null && entities.TryGetValue (args.Parent, out var parentOffsets)) {
                        parentOffsets.Children ??= new List<MimeOffsets> ();
                        parentOffsets.Children.Add (offsets);
                    }

                    entities.Add (args.Entity, offsets);
                };

                // Connect a handler to track MimeEntity end offsets
                parser.MimeEntityEnd += delegate (sender, args) {
                    // Our MimeEntityBegin event handler already created a MimeOffsets for
                    // this entity. Use the `entities` dictionary to retrieve it.
                    var offsets = entities[args.Entity];

                    // Track the size of the MimeEntity in octets (aka bytes), the offset
                    // for the end of the header block, the end of the entity, and the
                    // line count.
                    offsets.Octets = args.EndOffset - args.HeadersEndOffset;
                    offsets.HeadersEndOffset = args.HeadersEndOffset;
                    offsets.EndOffset = args.EndOffset;
                    offsets.Lines = args.Lines;
                };

                // Parse the message (which will emit the events as appropriate).
                var message = parser.ParseMessage ();

                // Now we can find out the offsets of each MimePart:
                foreach (var bodyPart in message.BodyParts.OfType<MimePart> ()) {
                    var offsets = entities[bodyPart];

                    Console.WriteLine ($"The offsets for the MIME part for {bodyPart.ContentType} are:");
                    Console.WriteLine ($"  - LineNumber: {offsets.LineNumber}")
                    Console.WriteLine ($"  - BeginOffset: {offsets.BeginOffset}");
                    Console.WriteLine ($"  - HeadersEndOffset: {offsets.HeadersEndOffset}"); // Note: This is also where the *content* begins.
                    Console.WriteLine ($"  - EndOffset: {offsets.BeginOffset}");
                    Console.WriteLine ($"  - Octets: {offsets.Octets}");
                    Console.WriteLine ($"  - Lines: {offsets.Lines}");
                }
            }
        }
        #endregion // MessageOffsets
    }
}
