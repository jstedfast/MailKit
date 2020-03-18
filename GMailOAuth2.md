# Settung up OAuth2 for use with Google Mail

## Register Your Application with Google

Go to [Google's Developer Console](https://cloud.google.com/console).

Click the *Select A Project* button in the *Navigation Bar* at the top of the screen.

![Click "Select A Project"](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/click-select-a-project.png)

Click the *New Project* button.

![Click "New Project"](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/click-new-project.png)

Fill in the name *Project Name*, and if appropriate, select the *Organization* that your program
should be associated with. Then click *Create*.

![Create New Project](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/create-new-project.png)

Click the *☰* symbol, move down to *APIs & Services* and then select *OAuth consent screen*.

![Click "OAuth consent screen"](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/click-oauth-consent-screen-menu.png)

Select the *External* radio item and then click *Create*.

![Select "External"](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/select-external.png)

Fill in the *Application name* and any other fields that are appropriate for your application and then click *Create*.

![OAuth consent screen](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/oauth-consent-screen.png)

Click *+ Create Credentials* and then select *OAuth client ID*.

![Click "Create Credentials"](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/click-create-credentials.png)

Select the *Other* radio item in the *Application type* section and then type in a name to use for the OAuth client ID.
Once completed, click *Create*.

![Select "Other"](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/select-application-type-other.png)

At this point, you will be presented with a web dialog that will allow you to copy the *Client ID* and *Client Secret*
strings into your clipboard to paste them into your program.

![Client ID and Secret](https://github.com/jstedfast/MailKit/blob/master/Documentation/media/google-developer-console/client-id-and-secret.png)

