using System;
using System.IO;
using System.Linq;
using System.Text;

using MimeKit;
using MimeKit.Cryptography;

namespace Examples
{
	#region MyGnuPGContext
	public class MyGnuPGContext : GnuPGContext
	{
		public MyGnuPGContext ()
		{
		}

		protected override string GetPasswordForKey (PgpSecretKey key)
		{
			// prompt the user (or a secure password cache) for the password for the specified secret key.
			return "password";
		}
	}
	#endregion

	public class OpenPGPExamples
	{
		public void RegisterMyGnuPGeContext ()
		{
			#region RegisterCustomContext
			// Note: by registering our custom context it becomes the default OpenPGP context
			// instantiated by MimeKit when methods such as Encrypt(), Decrypt(), Sign(), and
			// Verify() are used without an explicit context.
			CryptographyContext.Register (typeof (MyGnuPGContext));
			#endregion
		}

		#region Encrypt
		public void Encrypt (MimeMessage message)
		{
			// encrypt our message body using our custom GnuPG cryptography context
			using (var ctx = new MyGnuPGContext ()) {
				// Note: this assumes that each of the recipients has a PGP key associated
				// with their email address in the user's public keyring.
				//
				// If this is not the case, you can use SecureMailboxAddresses instead of
				// normal MailboxAddresses which would allow you to specify the fingerprint
				// of their PGP keys. You could also choose to use one of the Encrypt()
				// overloads that take a list of PgpPublicKeys.
				message.Body = MultipartEncrypted.Encrypt (ctx, message.To.Mailboxes, message.Body);
			}
		}
		#endregion

		#region Decrypt
		public MimeEntity Decrypt (MimeMessage message)
		{
			if (message.Body is MultipartEncrypted) {
				// the top-level MIME part of the message is encrypted using PGP/MIME
				var encrypted = (MultipartEncrypted) entity;

				return encrypted.Decrypt ();
			} else {
				// the top-level MIME part is not encrypted
				return message.Body;
			}
		}
		#endregion

		#region Sign
		public void Sign (MimeMessage message)
		{
			// digitally sign our message body using our custom GnuPG cryptography context
			using (var ctx = new MyGnuPGContext ()) {
				// Note: this assumes that the Sender address has an S/MIME signing certificate
				// and private key with an X.509 Subject Email identifier that matches the
				// sender's email address.
				//
				// If this is not the case, you can use a SecureMailboxAddress instead of a
				// normal MailboxAddress which would allow you to specify the fingerprint
				// of the sender's private PGP key. You could also choose to use one of the
				// Create() overloads that take a PgpSecretKey, instead.
				var sender = message.From.Mailboxes.FirstOrDefault ();

				message.Body = MultipartSigned.Create (ctx, sender, DigestAlgorithm.Sha1, message.Body);
			}
		}
		#endregion

		#region SignWithKey
		public void Sign (MimeMessage message, PgpSecretKey key)
		{
			// digitally sign our message body using our custom GnuPG cryptography context
			using (var ctx = new MyGnuPGContext ()) {
				message.Body = MultipartSigned.Create (ctx, key, DigestAlgorithm.Sha1, message.Body);
			}
		}
		#endregion

		#region Verify
		public void Verify (MimeMessage message)
		{
			if (message.Body is MultipartSigned) {
				var signed = (MultipartSigned) message.Body;

				foreach (var signature in signed.Verify ()) {
					try {
						bool valid = signature.Verify ();

						// If valid is true, then it signifies that the signed content
						// has not been modified since this particular signer signed the
						// content.
						//
						// However, if it is false, then it indicates that the signed
						// content has been modified.
					} catch (DigitalSignatureVerifyException) {
						// There was an error verifying the signature.
					}
				}
			}
		}
		#endregion

		#region DecryptInlinePGP
		static Stream Decrypt (MimeMessage message)
		{
			var text = message.TextBody;

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var ctx = new MyGnuPGContext ()) {
					return ctx.GetDecryptedStream (memory);
				}
			}
		}
		#endregion
	}
}