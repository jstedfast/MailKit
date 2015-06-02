var message = new MimeMessage ();
message.From.Add (new MailboxAddress ("Joey", "joey@friends.com"));
message.To.Add (new MailboxAddress ("Alice", "alice@wonderland.com"));
message.Subject = "How you doin?";

var builder = new BodyBuilder ();

// Set the plain-text version of the message text
builder.TextBody = @"Hey Alice,

What are you up to this weekend? Monica is throwing one of her parties on
Saturday and I was hoping you could make it.

Will you be my +1?

-- Joey
";

// Set the html version of the message text
builder.HtmlBody = @"<p>Hey Alice,<br>
<p>What are you up to this weekend? Monica is throwing one of her parties on
Saturday and I was hoping you could make it.<br>
<p>Will you be my +1?<br>
<p>-- Joey<br>
<center><img src=""sexy-pose.jpg""></center>";

// Since sexy-pose.jpg is referenced from the html text, we'll need to add it
// to builder.LinkedResources
builder.LinkedResources.Add ("C:\\Users\\Joey\\Documents\\Selfies\\sexy-pose.jpg");

// We may also want to attach a calendar event for Monica's party...
builder.Attachments.Add ("C:\\Users\Joey\\Documents\\party.ics");

// Now we just need to set the message body and we're done
message.Body = builder.ToMessageBody ();
