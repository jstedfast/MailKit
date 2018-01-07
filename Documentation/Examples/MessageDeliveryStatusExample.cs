//
// MessageDeliveryStatusExamples.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Linq;

using MimeKit;

namespace MimeKit.Examples {
    public static class MessageDeliveryStatusExamples
    {
        #region ProcessDeliveryStatusNotification
        public void ProcessDeliveryStatusNotification (MimeMessage message)
        {
            var report = message.Body as MultipartReport;

            if (report == null || report.ReportType == null || !report.ReportType.Equals ("delivery-status", StringComparison.OrdinalIgnoreCase)) {
                // this is not a delivery status notification message...
                return;
            }

            // process the report
            foreach (var mds in report.OfType<MessageDeliveryStatus> ()) {
                // process the status groups - each status group represents a different recipient

                // The first status group contains information about the message
                var envelopeId = mds.StatusGroups[0]["Original-Envelope-Id"];

                // all of the other status groups contain per-recipient information
                for (int i = 1; i < mds.StatusGroups.Length; i++) {
                    var recipient = mds.StatusGroups[i]["Original-Recipient"];
                    var action = mds.StatusGroups[i]["Action"];

                    if (recipient == null)
                        recipient = mds.StatusGroups[i]["Final-Recipient"];
                    
                    // the recipient string should be in the form: "rfc822;user@domain.com"
                    var index = recipient.IndexOf (';');
                    var address = recipient.Substring (index + 1);

                    switch (action) {
                    case "failed":
                        Console.WriteLine ("Delivery of message {0} failed for {1}", envelopeId, address);
                        break;
                    case "delayed":
                        Console.WriteLine ("Delivery of message {0} has been delayed for {1}", envelopeId, address);
                        break;
                    case "delivered":
                        Console.WriteLine ("Delivery of message {0} has been delivered to {1}", envelopeId, address);
                        break;
                    case "relayed":
                        Console.WriteLine ("Delivery of message {0} has been relayed for {1}", envelopeId, address);
                        break;
                    case "expanded":
                        Console.WriteLine ("Delivery of message {0} has been delivered to {1} and relayed to the the expanded recipients", envelopeId, address);
                        break;
                    }
                }
            }
        }
        #endregion
    }
}
