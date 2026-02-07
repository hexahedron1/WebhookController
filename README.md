# WebhookController

This program lets you manually send messages to a discord webhook. Requires no autorization, only the webhook URL.

# How to use
## Setup
First, you must define a webhook for the program to use. They are stored in `/home/USER/.local/whookctrl/config.json`. The program automatically creates a blank file on the first start:
```json
{
  "hooks": [],
  "profiles": []
}
```
The config can be refreshed by clicking the button in the header at any time.
### Defining webhooks
In the `hooks` array add an object like this:
```json
{
  "name": "(how it should be displayed in UI)",
  "url": "(the url to which requests will be sent)"
}
```
### Profiles
Profiles are easily selectable overrides for the hook's username and avatar. They are **completely optional**, but can be defined in a similar way in the `profiles` array:
```json
{
  "name": "(username)",
  "avatar": "(avatar url)"
}
```
## Sending messages
To send a message, select the webhook in the dropdown by the message bar, type it in the message bar and hit enter/press the send button, couldn't be simpler.  
Previous messages are displayed in the list above along with the used webhook. It can be cleared by a button in the window header.
### Profile overrides
The `Profile overrides` section governs how the hook's nickname and avatar are overridden for this message:
- None - default values defined in discord are kept
- Preset - uses the values defined in the config file (as explained before)
- Custom - lets you type in arbitrary values
