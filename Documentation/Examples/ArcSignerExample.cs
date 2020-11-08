using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using MimeKit;
using MimeKit.Cryptography;

namespace ArcSignerExample
{
    class ExampleArcSigner : ArcSigner
    {
        public ExampleArcSigner (Stream stream, string domain, string selector, DkimSignatureAlgorithm algorithm = DkimSignatureAlgorithm.RsaSha256) : base (stream, domain, selector, algorithm)
        {
        }

        public ExampleArcSigner (string fileName, string domain, string selector, DkimSignatureAlgorithm algorithm = DkimSignatureAlgorithm.RsaSha256) : base (fileName, domain, selector, algorithm)
        {
        }

        public ExampleArcSigner (AsymmetricKeyParameter key, string domain, string selector, DkimSignatureAlgorithm algorithm = DkimSignatureAlgorithm.RsaSha256) : base (key, domain, selector, algorithm)
        {
        }

        public string AuthenticationServiceIdentifier {
            get; set;
        }

        /// <summary>
        /// Generate the ARC-Authentication-Results header.
        /// </summary>
        /// <remarks>
        /// The ARC-Authentication-Results header contains information detailing the results of
        /// authenticating/verifying the message via ARC, DKIM, SPF, etc.
        ///
        /// In the following implementation, we assume that all of these authentication results
        /// have already been determined by other mail software that has added some Authentication-Results
        /// headers containing this information.
        ///
        /// Note: This method is used when ArcSigner.Sign() is called instead of ArcSigner.SignAsync().
        /// </remarks>
        protected override AuthenticationResults GenerateArcAuthenticationResults (FormatOptions options, MimeMessage message, CancellationToken cancellationToken)
        {
            var results = new AuthenticationResults (AuthenticationServiceIdentifier);

            for (int i = 0; i < message.Headers.Count; i++) {
                var header = message.Headers[i];

                if (header.Id != HeaderId.AuthenticationResults)
                    continue;

                if (!AuthenticationResults.TryParse (header.RawValue, out AuthenticationResults authres))
                    continue;

                if (authres.AuthenticationServiceIdentifier != AuthenticationServiceIdentifier)
                    continue;

                foreach (var result in authres.Results) {
                    if (!results.Results.Any (r => r.Method == result.Method))
                        results.Results.Add (result);
                }
            }

            return results;
        }

        protected override Task<AuthenticationResults> GenerateArcAuthenticationResultsAsync (FormatOptions options, MimeMessage message, CancellationToken cancellationToken)
        {
            return Task.FromResult (GenerateArcAuthenticationResults (options, message, cancellationToken));
        }
    }

    class Program
    {
        public static void Main (string[] args)
        {
            if (args.Length < 2) {
                Help ();
                return;
            }

            for (int i = 0; i < args.Length; i++) {
                if (args[i] == "--help") {
                    Help ();
                    return;
                }
            }

            var headers = new HeaderId[] { HeaderId.From, HeaderId.Subject, HeaderId.Date };
            var signer = new ExampleArcSigner ("privatekey.pem", "example.com", "brisbane", DkimSignatureAlgorithm.RsaSha256) {
                HeaderCanonicalizationAlgorithm = DkimCanonicalizationAlgorithm.Simple,
                BodyCanonicalizationAlgorithm = DkimCanonicalizationAlgorithm.Simple,
                AgentOrUserIdentifier = "@eng.example.com",
            };

            if (!File.Exists (args[0])) {
                Console.Error.WriteLine ("{0}: No such file.", args[0]);
                return;
            }

            var message = MimeMessage.Load (args[0]);

            // Prepare the message body to be sent over a 7bit transport (such as older versions of SMTP).
            // Note: If the SMTP server you will be sending the message over supports the 8BITMIME extension,
            // then you can use `EncodingConstraint.EightBit` instead.
            message.Prepare (EncodingConstraint.SevenBit);

            signer.Sign (message, headers);

            using (var stream = File.Create (args[1]))
                message.WriteTo (stream);
        }

        static void Help ()
        {
            Console.WriteLine ("Usage is: ArcSigner [options] [message] [output]");
            Console.WriteLine ();
            Console.WriteLine ("Options:");
            Console.WriteLine ("  --help               This help menu.");
        }
    }
}
