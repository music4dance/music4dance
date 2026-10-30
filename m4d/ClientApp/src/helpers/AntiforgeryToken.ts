import axios from "axios";

/**
 * The antiforgery request token for this page's POSTs, and for the GETs the server validates
 * (search suggestions).
 *
 * Signed-in pages carry the token in `menuContext.xsrfToken`. Anonymous pages don't, so their
 * HTML sets no antiforgery cookie and stays cacheable. They fetch a token from
 * `/api/antiforgery/token` the first time they need one, and that response also sets the
 * cookie. A fetched token is kept in sessionStorage for an hour, so later pages in the same
 * tab can use it synchronously; `navigator.sendBeacon` can't wait on a request. See
 * architecture/plans/bot-load-and-public-page-rendering.md (A3).
 */
export const ANTIFORGERY_TOKEN_ENDPOINT = "/api/antiforgery/token";

const STORAGE_KEY = "m4d-antiforgery-token";
const MAX_AGE_MS = 60 * 60 * 1000;

interface StoredToken {
  token: string;
  fetched: number;
}

let pending: Promise<string> | undefined;

function readStored(): string {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return "";
    }
    const stored = JSON.parse(raw) as StoredToken;
    return stored.token && Date.now() - stored.fetched < MAX_AGE_MS ? stored.token : "";
  } catch {
    return "";
  }
}

function writeStored(token: string): void {
  try {
    const stored: StoredToken = { token, fetched: Date.now() };
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(stored));
  } catch {
    // Storage unavailable: the token is just fetched again next time.
  }
}

function clearStored(): void {
  try {
    sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // Nothing stored if storage is unavailable.
  }
}

/**
 * The token available right now, without a request: the page's own token when it has one,
 * otherwise one fetched earlier in this tab, otherwise "".
 */
export function currentAntiforgeryToken(pageToken?: string): string {
  if (pageToken) {
    // A signed-in page; a token fetched while anonymous no longer applies.
    clearStored();
    return pageToken;
  }
  return readStored();
}

/** The current token, fetching one from the server if the page doesn't have one yet. */
export function ensureAntiforgeryToken(pageToken?: string): Promise<string> {
  const current = currentAntiforgeryToken(pageToken);
  if (current) {
    return Promise.resolve(current);
  }

  pending ??= axios
    .get<{ token: string }>(ANTIFORGERY_TOKEN_ENDPOINT)
    .then((response) => {
      const token = response.data?.token ?? "";
      if (token) {
        writeStored(token);
      }
      return token;
    })
    .finally(() => {
      pending = undefined;
    });
  return pending;
}
