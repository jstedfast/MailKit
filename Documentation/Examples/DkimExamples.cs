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
            var signer = new DkimSigner ("privatekey.pem", "example.com", "brisbane", DkimSignatureAlgorithm.RsaSha256) {
		HeaderCanonicalizationAlgorithm = DkimCanonicalizationAlgorithm.Simple,
		BodyCanonicalizationAlgorithm = DkimCanonicalizationAlgorithm.Simple,
                AgentOrUserIdentifier = "@eng.example.com",
                QueryMethod = "dns/txt",
            };

            // Prepare the message body to be sent over a 7bit transport (such as older versions of SMTP).
            // Note: If the SMTP server you will be sending the message over supports the 8BITMIME extension,
            // then you can use `EncodingConstraint.EightBit` instead.
            message.Prepare (EncodingConstraint.SevenBit);

            signer.Sign (message, headers);
        }
        #endregion
    }
}
