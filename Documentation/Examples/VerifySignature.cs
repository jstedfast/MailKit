if (entity is MultipartSigned) {
    var signed = (MultipartSigned) entity;

    foreach (var signature in signed.Verify ()) {
        try {
            bool valid = signature.Verify ();

            // If valid is true, then it signifies that the signed content has not been
            // modified since this particular signer signed the content.
            //
            // However, if it is false, then it indicates that the signed content has
            // been modified.
        } catch (DigitalSignatureVerifyException) {
            // There was an error verifying the signature.
        }
    }
}
