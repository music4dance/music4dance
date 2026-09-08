import { watchEffect } from "vue";

export type QueryParamValue = string | string[] | undefined;

// Builds a "?a=1&a=2&b=3" style query string from a plain object. Arrays become repeated keys
// (matching ASP.NET Core's List<string> model-binding convention already used by every
// server-side action these pages target, e.g. HomeController.Tempi's styles/types/meters).
// undefined and empty-string values are omitted entirely rather than serialized as "key=".
export function buildQueryString(params: Record<string, QueryParamValue>): string {
  const usp = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined) {
      continue;
    }
    if (Array.isArray(value)) {
      value.forEach((v) => usp.append(key, v));
    } else if (value !== "") {
      usp.set(key, value);
    }
  }
  const query = usp.toString();
  return query ? `?${query}` : "";
}

// Keeps the browser's address bar continuously in sync with a page's current configuration via
// history.replaceState, so the current URL is always a valid, shareable/bookmarkable link to
// whatever the visitor has configured - without a page navigation or extra browser history
// entries. Each page supplies its own params as a getter (matching the project's per-page
// filter-construction convention) so this stays reactive to whatever refs/computeds it reads,
// and owns nothing about what those params mean.
export function useUrlQuerySync(getParams: () => Record<string, QueryParamValue>): void {
  watchEffect(() => {
    const search = buildQueryString(getParams());
    const url = `${window.location.pathname}${search}${window.location.hash}`;
    window.history.replaceState(null, "", url);
  });
}
