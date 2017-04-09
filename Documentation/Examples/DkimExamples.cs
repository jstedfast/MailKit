using System;
using System.IO;

using MimeKit;

namespace MimeKit.Examples
{
    public static class DkimExamples
    {
        #region DkimSign
        public static void DkimSign (MimeMessage message)
        {
            var headers = new HeaderId[] { HeaderId.From, HeaderId.Subject, HeaderId.Date };
            var headerAlgorithm = DkimCanonicalizationAlgorithm.Simple;
            var bodyAlgorithm = DkimCanonicalizationAlgorithm.Simple;
            var signer = new DkimSigner ("privatekey.pem") {
                SignatureAlgorithm = DkimSignatureAlgorithm.RsaSha1,
                AgentOrUserIdentifier = "@eng.example.com",
                QueryMethod = "dns/txt",
            };

            // Prepare the message body to be sent over a 7bit transport (such as older versions of SMTP).
            // Note: If the SMTP server you will be sending the message over supports the 8BITMIME extension,
            // then you can use `EncodingConstraint.EightBit` instead.
            message.Prepare (EncodingConstraint.SevenBit);

            message.Sign (signer, headers, headerAlgorithm, bodyAlgorithm);
        }
        #endregion
    }
}