# Migrate Email off Azure Communication Services

**Type:** Plan
**Status:** Proposed
**Last verified:** 2026-10-31
**Code:** `m4d/Services/EmailSender.cs`, `m4d/Configuration/ServiceCollectionExtensions.cs`, `m4d/Configuration/SecretBackedServices.cs`

All outgoing email (account confirmation, password reset, email change, and the admin
service-health emails) goes through Azure Communication Services (ACS) Email. Microsoft is
retiring ACS as a standalone offering, and Email is on the retired list, not the
breaking-change list: after the retirement date it stops working entirely. This plan moves
sending to an SMTP relay from a transactional email provider, in a way that lets us cut over
and roll back by changing configuration.

## The retirement

From Microsoft's [retirement and breaking changes guide](https://aka.ms/acs-retirement)
(updated 2026-10-29):

- **Announced 2026-09-23. Retirement date 2028-09-30.** After that, ACS Email API calls fail.
- **Maintenance mode until then.** Security and critical fixes only. Quota increases are
  case by case.
- **Existing resources keep working.** Resources created before 2026-10-23 keep running.
  Verified domains (ours is `music4dance.net`) stay supported until the retirement date.
- **Telemetry is deleted after retirement.** Export any delivery logs we want to keep first.
  We don't think we need any.
- Some secondary sources, including a Microsoft GitHub knowledge file verified before the
  announcement, give 2028-07-31. The official guide says 2028-09-30. Plan as if the earlier
  date were real.

There's no hard pressure before 2028, but ACS gets no further investment. Doing this in the
first half of 2027 leaves plenty of slack.

## Current state

- [EmailSender.cs](../../m4d/Services/EmailSender.cs) is the only code that talks to ACS. It
  implements ASP.NET Identity's `IEmailSender` with `Azure.Communication.Email`'s `EmailClient`,
  sends HTML, and waits for completion (`WaitUntil.Completed`). The sender address
  `donotreply@music4dance.net` is hard-coded.
- `AddEmailSenderWithResilience` in
  [ServiceCollectionExtensions.cs](../../m4d/Configuration/ServiceCollectionExtensions.cs)
  resolves `IEmailSender` per request. It returns `EmailSender` when
  `Authentication:AzureCommunicationServices:ConnectionString` is set, and `NullEmailSender`
  otherwise. The key is read on each resolution, so email starts working when App
  Configuration recovers ([service-resilience](../infrastructure/service-resilience.md)).
- [SecretBackedServices.cs](../../m4d/Configuration/SecretBackedServices.cs) owns the key name
  and the `EmailService` health requirement.
- `m4d.Sandbox` registers `FileEmailSender` directly, so it isn't affected.
- Callers are the Identity pages under `m4d/Areas/Identity/Pages/Account/` and
  `ServiceHealthNotifier`. None of them depend on anything ACS-specific.
- Existing bug: `ServiceHealth:AdminNotifications:SenderAddress` is documented in
  [configure-failure-email](../runbooks/configure-failure-email.md) and bound in
  `ServiceHealthNotifier`, but it's never used. `EmailSender` always sends from the hard-coded
  address.

## Options

Microsoft's suggested replacements don't fit a site that emails the public:

| Option | Verdict |
| --- | --- |
| Microsoft 365 High Volume Email (HVE) | No. It sends only to recipients inside the sender's own Microsoft 365 organization, and our recipients are site users. |
| Exchange Online (SMTP AUTH or Graph from a licensed mailbox) | Possible at our volume, but it needs a Microsoft 365 license. It's designed for person-to-person mail and has per-mailbox rate limits. Not recommended. |
| Infobip / Telesign (Microsoft's listed Marketplace partners) | Workable, but they're built for large multi-channel messaging. More platform than we need. |
| Transactional email provider (Postmark, Amazon SES, Mailgun, Brevo, Resend, SendGrid) | **Recommended.** Built for exactly this: low-volume app-to-person mail with domain authentication. |

### Choosing a provider

What matters for us: deliverability of account email (confirmation and reset mail that lands
in spam effectively breaks registration), a low fixed cost at low volume, SMTP support, and
DKIM on our own domain. Two good fits:

- **Postmark.** Transactional-only, known for deliverability, simple setup. Small monthly fee
  at our volume.
- **Amazon SES.** The cheapest per message. It needs an AWS account, a request to leave the
  SES sandbox, and a little more DNS work. Adds a second cloud to the stack.

Recommendation: **Postmark**, unless AWS is already in use for something else. Check current
pricing and free tiers when deciding. They change often, and SendGrid in particular has
dropped its permanent free plan.

### SMTP or the provider's HTTP API

Send over **SMTP with [MailKit](https://github.com/jstedfast/MailKit)**, not a vendor SDK.

- Every candidate provider accepts SMTP, so switching providers later is a configuration
  change: new host and credentials, no code.
- MailKit is the de facto .NET SMTP client. `System.Net.Mail.SmtpClient` is marked obsolete
  for new development.
- What we give up is message IDs in the response and richer error detail. Neither is used
  today: `EmailSender` only logs the ACS message ID.

Azure App Service blocks outbound port 25, so use port 587 (STARTTLS) or 465 (implicit TLS).

## Design

### `SmtpEmailSender`

A new `IEmailSender` in `m4d/Services/` alongside `EmailSender`:

- Built from an `SmtpEmailOptions` record: `Host`, `Port`, `UserName`, `Password`,
  `SenderAddress` (default `donotreply@music4dance.net`), and optionally `SenderName`.
- Builds the `MimeMessage` in a separate static method so tests can check it without a
  server: HTML body, single recipient, sender from options.
- Connects, authenticates, sends and disconnects for each message. At our volume a pooled
  connection isn't worth the complexity.
- Logs and rethrows on failure, like `EmailSender` does today. Callers already handle the
  exception.

### Configuration keys

New keys under a provider-neutral `Email:Smtp` section, stored in App Configuration with the
password as a Key Vault reference:

| Key | Secret | Notes |
| --- | --- | --- |
| `Email:Smtp:Host` | No | e.g. `smtp.postmarkapp.com` |
| `Email:Smtp:Port` | No | 587 |
| `Email:Smtp:UserName` | Yes | Provider token or SMTP user |
| `Email:Smtp:Password` | Yes | Key Vault reference |
| `Email:SenderAddress` | No | Optional. Overrides the default sender for all mail |

### Selection and health during the transition

`AddEmailSenderWithResilience` chooses per resolution, as it does now:

1. All of `Host`, `UserName` and `Password` set: `SmtpEmailSender`.
2. Otherwise, the ACS connection string set: `EmailSender` (ACS).
3. Otherwise: `NullEmailSender`.

`SecretBackedServices.IsEmailConfigured` and the `EmailService` requirement accept either
configuration during the transition. This gives a configuration-only cutover: add the SMTP keys
and the next resolution uses SMTP. Rollback is just as easy: delete the SMTP keys and the app
falls back to ACS. App Configuration only reloads when `Configuration:Sentinel` changes, so
bump the sentinel after changing keys, or restart.

### Fix the sender address

Read the sender from `Email:SenderAddress` for all mail, and remove the unused
`ServiceHealth:AdminNotifications:SenderAddress` setting and its runbook mention. One sender
setting is enough, and today's dead setting suggests a per-feature sender works when it
doesn't.

### Tests

- `SecretBackedServicesTests`: SMTP keys switch `NullEmailSender` to `SmtpEmailSender`; SMTP
  takes precedence when both are configured; ACS alone still resolves `EmailSender` (until
  cleanup).
- `SmtpEmailSender` message construction: sender, recipient, subject, HTML body.
- No test sends real email. Check end to end on `m4d-test` (see Rollout).

## DNS

Do this before the cutover. Both providers have to be authorized at the same time.

- **SPF:** a domain can have only one SPF TXT record. Add the provider's `include:` to the
  existing record next to ACS's, don't add a second record. Remove the ACS include during
  cleanup.
- **DKIM:** add the provider's DKIM records (usually CNAME or TXT under a provider-specific
  selector). These don't conflict with ACS's selectors.
- **Return-Path / bounce domain:** some providers (Postmark among them) want a CNAME for a
  custom return path so SPF aligns for DMARC. Add it if offered.
- **DMARC:** check the existing `_dmarc` record. With aligned DKIM from the new provider
  nothing needs to change, but watch the aggregate reports for a week or two after cutover.
- Verify the domain in the provider's console before adding the keys to the app.

## Rollout

1. **Choose the provider and set up the account.** Verify `music4dance.net` and add the DNS
   records above. Get approval to send to arbitrary recipients if the provider requires it
   (SES sandbox exit, Postmark account approval).
2. **Ship the code PR.** `SmtpEmailSender`, dual selection, sender fix, tests, doc updates.
   This changes nothing on its own: no SMTP keys exist yet.
3. **Test environment.** Add the `Email:Smtp:*` keys with label `Staging` (password in Key
   Vault) and bump the sentinel. On `m4d-test`: register a new account and get the
   confirmation, request a password reset, and restart to get the startup status email. Check
   the headers for DKIM `pass` and SPF `pass`.
4. **Production.** Add the keys with label `Production` and bump the sentinel. Repeat the
   checks with a real account. Watch the logs for `Failed to send` and the provider's bounce
   dashboard.
5. **Soak for 2 to 4 weeks.** To roll back, delete the SMTP keys and bump the sentinel. The app
   falls back to ACS.
6. **Cleanup PR.** Remove `EmailSender` (ACS), the `Azure.Communication.Email` package
   reference in `Directory.Packages.props` and `m4d.csproj`, the ACS key from
   `SecretBackedServices`, and the ACS fallback in selection. Delete the ACS connection string
   from App Configuration and Key Vault, remove the ACS SPF include and DKIM records, then
   delete the ACS Email and Communication Services resources in Azure.

## Docs to update when this ships

Fold the as-built design into the reference docs and delete this plan in the cleanup PR:

- [service-resilience](../infrastructure/service-resilience.md): the `EmailService` row and the
  App Configuration note.
- [configuration-and-feature-flags](../infrastructure/configuration-and-feature-flags.md): replace
  the ACS key with the `Email:*` keys.
- [hosting-and-identity](../infrastructure/hosting-and-identity.md) and
  [overview](../overview.md): the external services list.
- [configure-failure-email](../runbooks/configure-failure-email.md): new keys, how to get SMTP
  credentials, and drop `SenderAddress`.
- [provision-app-service](../runbooks/provision-app-service.md): the email prerequisite.
- [contributor-environments-next](contributor-environments-next.md) and
  [key-vault-rbac-migration](key-vault-rbac-migration.md): the ACS mentions, including the
  "ACS supports managed identity" footnote, which is moot after this.

## Open questions

- **Provider.** Postmark is recommended. SES if AWS is already in play.
- **Volume.** Check the ACS resource's send metrics for the last few months to pick a plan
  tier. Expected to be well under 10k per month.
- **Who owns the DNS zone,** and whether the existing SPF record is near the 10-DNS-lookup
  limit.

## Related

- [service-resilience](../infrastructure/service-resilience.md): how email degrades and recovers.
- [configure-failure-email](../runbooks/configure-failure-email.md): admin email setup.
- [hosting-and-identity](../infrastructure/hosting-and-identity.md): secrets and App
  Configuration.
