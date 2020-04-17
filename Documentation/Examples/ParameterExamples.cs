using System;

using MimeKit;

namespace MimeKit.Examples
{
    public static class ParameterExamples
    {
        public void OverrideAllParameterEncodings (MimePart part)
        {
            #region OverrideAllParameterEncodings
            // Some versions of Outlook expect the rfc2047 style of encoding of parameter values.
            foreach (var parameter in part.ContentDisposition.Parameters)
                parameter.EncodingMehod = ParameerEncodngMethod.Rfc2047;
            #endregion OverrideAllParameterEncodings
        }

        public void OverrideFileNameParameterEncodings (MimePart part)
        {
            #region OverrideFileNameParameterEncoding
            // Some versions of Outlook expect the rfc2047 style of encoding for the filename parameter value.
            if (part.ContentDisposition.Parameters.TryGetValue ("filename", out var parameter))
                parameter.EncodingMehod = ParameerEncodngMethod.Rfc2047;
            #endregion OverrideFileNameParameterEncoding
        }
    }
}
