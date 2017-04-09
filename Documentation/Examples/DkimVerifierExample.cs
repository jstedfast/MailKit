using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;

using Heijden.DNS;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;

using MimeKit;
using MimeKit.Cryptography;

namespace DkimVerifier
{
    class DkimPublicKeyLocator : IDkimPublicKeyLocator
    {
        readonly Dictionary<string, AsymmetricKeyParameter> cache;
        readonly Resolver resolver;

        public DkimPublicKeyLocator ()
        {
            cache = new Dictionary<string, AsymmetricKeyParameter> ();

            resolver = new Resolver ("8.8.8.8") {
                TransportType = TransportType.Udp,
                UseCache = true,
                Retries = 3
            };
        }

        AsymmetricKeyParameter DnsLookup (string domain, string selector, CancellationToken cancellationToken)
        {
            var query = selector + "._domainkey." + domain;
            AsymmetricKeyParameter pubkey;

            // checked if we've already fetched this key
            if (cache.TryGetValue (query, out pubkey))
                return pubkey;

            // make a DNS query
            var response = resolver.Query (query, QType.TXT);
            var builder = new StringBuilder ();

            // combine the TXT records into 1 string buffer
            foreach (var record in response.RecordsTXT) {
                foreach (var text in record.TXT)
                    builder.Append (text);
            }

            var txt = builder.ToString ();
            string k = null, p = null;
            int index = 0;

            // parse the response (will look something like: "k=rsa; p=<base64>")
            while (index < txt.Length) {
                while (index < txt.Length && char.IsWhiteSpace (txt[index]))
                    index++;

                if (index == txt.Length)
                    break;

                // find the end of the key
                int startIndex = index;
                while (index < txt.Length && txt[index] != '=')
                    index++;

                if (index == txt.Length)
                    break;

                var key = txt.Substring (startIndex, index - startIndex);

                // skip over the '='
                index++;

                // find the end of the value
                startIndex = index;
                while (index < txt.Length && txt[index] != ';')
                    index++;

                var value = txt.Substring (startIndex, index - startIndex);

                switch (key) {
                case "k": k = value; break;
                case "p": p = value; break;
                }

                // skip over the ';'
                index++;
            }

            if (k != null && p != null) {
                var data = "-----BEGIN PUBLIC KEY-----\r\n" + p + "\r\n-----END PUBLIC KEY-----\r\n";
                var rawData = Encoding.ASCII.GetBytes (data);

                using (var stream = new MemoryStream (rawData, false)) {
                    using (var reader = new StreamReader (stream)) {
                        var pem = new PemReader (reader);

                        pubkey = pem.ReadObject () as AsymmetricKeyParameter;

                        if (pubkey != null) {
                            cache.Add (query, pubkey);

                            return pubkey;
                        }
                    }
                }
            }

            throw new Exception (string.Format ("Failed to look up public key for: {0}", domain));
        }

        public AsymmetricKeyParameter LocatePublicKey (string methods, string domain, string selector, CancellationToken cancellationToken = default (CancellationToken))
        {
            var methodList = methods.Split (new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < methodList.Length; i++) {
                if (methodList[i] == "dns/txt")
                    return DnsLookup (domain, selector, cancellationToken);
            }

            throw new NotSupportedException (string.Format ("{0} does not include any suported lookup methods.", methods));
        }
    }

    class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0) {
                Help ();
                return;
            }

            for (int i = 0; i < args.Length; i++) {
                if (args[i] == "--help") {
                    Help ();
                    return;
                }
            }

            var locator = new DkimPublicKeyLocator ();

            for (int i = 0; i < args.Length; i++) {
                if (!File.Exists (args[i])) {
                    Console.Error.WriteLine ("{0}: No such file.", args[i]);
                    continue;
                }

                Console.Write ("{0} -> ", args[i]);

                var message = MimeMessage.Load (args[i]);
                var index = message.Headers.IndexOf (HeaderId.DkimSignature);

                if (index == -1) {
                    Console.WriteLine ("NO SIGNATURE");
                    continue;
                }

                var dkim = message.Headers[index];

                if (message.Verify (dkim, locator)) {
                    // the DKIM-Signature header is valid!
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine ("VALID");
                    Console.ResetColor ();
                } else {
                    // the DKIM-Signature is invalid!
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine ("INVALID");
                    Console.ResetColor ();
                }
            }
        }

        static void Help ()
        {
            Console.WriteLine ("Usage is: DkimVerifier [options] [messages]");
            Console.WriteLine ();
            Console.WriteLine ("Options:");
            Console.WriteLine ("  --help               This help menu.");
        }
    }
}
