# Contributing to music4dance

Thanks for your interest in contributing code. This project has no CLA and no paperwork —
just a lightweight sign-off requirement described below, plus a working build.

## Getting your environment running

See [architecture/contributor-setup.md](architecture/contributor-setup.md) for how to build,
configure, and run the app with **no production access, no third-party API keys, and no
shared secrets** — either against your own empty local database, or against the fully
self-contained `m4d.Sandbox` project that needs nothing installed at all.

## Developer Certificate of Origin (DCO)

Every commit must be signed off, certifying you wrote it or otherwise have the right to submit
it under the project's license, per the [Developer Certificate of Origin](https://developercertificate.org/):

```text
Developer Certificate of Origin
Version 1.1

Copyright (C) 2004, 2006 The Linux Foundation and its contributors.
1 Letterman Drive
Suite D4700
San Francisco, CA, 94129

Everyone is permitted to copy and distribute verbatim copies of this
license document, but changing it is not allowed.

Developer's Certificate of Origin 1.1

By making a contribution to this project, I certify that:

(a) The contribution was created in whole or in part by me and I
    have the right to submit it under the open source license
    indicated in the file; or

(b) The contribution is based upon previous work that, to the best
    of my knowledge, is covered under an appropriate open source
    license and I have the right under that license to submit that
    work with modifications, whether created in whole or in part
    by me, under the same open source license (unless I am
    permitted to submit under a different license), as indicated
    in the file; or

(c) The contribution was provided directly to me by some other
    person who certified (a), (b) or (c) and I have not modified
    it.

(d) I understand and agree that this project and the contribution
    are public and that a record of the contribution (including all
    personal information I submit with it, including my sign-off) is
    maintained indefinitely and may be redistributed consistent with
    this project or the open source license(s) involved.
```

In practice, this means adding `-s` to your commits:

```sh
git commit -s -m "Your commit message"
```

which appends a trailer to the commit message:

```text
Signed-off-by: Your Name <your.email@example.com>
```

A GitHub check (`.github/workflows/dco.yml`) verifies every commit on a pull request carries
this trailer, and fails the check on the ones that don't. Forgot on an earlier commit? `git commit
--amend -s` (single commit) or `git rebase --signoff main` (whole branch) fixes it up.

## Pull requests

Fork the repo and open a PR against `main`. There's no CI deploy step for forked PRs — checks
run build + test only, so nothing you push touches production data, secrets, or infrastructure.
For end-to-end validation against a real deployed instance, ask in the PR and it can be deployed
to the test site on request — see
[contributor-test-environments.md](architecture/contributor-test-environments.md) for the full
menu of options and why fork-based PRs were chosen as the default.

## Code standards

See [CLAUDE.md](CLAUDE.md) for stack conventions, build/test commands, and coding standards.
