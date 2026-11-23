// Page loads go through here so tests can see where the page goes without jsdom navigating
export function navigateTo(url: string): void {
  window.location.assign(url);
}

export function reviewPageUrl(page: number, message?: string): string {
  const params = new URLSearchParams({ page: String(page) });
  if (message) {
    params.set("message", message);
  }
  return `/SharedIds?${params.toString()}`;
}
