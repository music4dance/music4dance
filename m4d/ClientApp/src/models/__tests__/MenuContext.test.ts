import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import axios, { type InternalAxiosRequestConfig } from "axios";
import { MenuContext } from "../MenuContext";

function mockLocation(pathname: string, search: string = "") {
  Object.defineProperty(window, "location", {
    value: { pathname, search },
    writable: true,
    configurable: true,
  });
}

describe("MenuContext.getAccountLink", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("when on a non-identity page", () => {
    beforeEach(() => mockLocation("/song", "?filter=CHA"));

    it("uses the current path and search as returnUrl", () => {
      const ctx = new MenuContext({});
      expect(ctx.getAccountLink("login")).toBe(
        `/identity/account/login?returnUrl=${encodeURIComponent("/song?filter=CHA")}`,
      );
    });

    it("works without a query string", () => {
      mockLocation("/home/contribute");
      const ctx = new MenuContext({});
      expect(ctx.getAccountLink("register")).toBe(
        `/identity/account/register?returnUrl=${encodeURIComponent("/home/contribute")}`,
      );
    });
  });

  describe("when already on an identity page", () => {
    it("passes through the existing returnUrl, not the identity page URL", () => {
      mockLocation("/identity/account/login", "?returnUrl=%2Fsong");
      const ctx = new MenuContext({});
      expect(ctx.getAccountLink("register")).toBe(
        `/identity/account/register?returnUrl=${encodeURIComponent("/song")}`,
      );
    });

    it("uses / if no returnUrl is present on the identity page", () => {
      mockLocation("/identity/account/register", "");
      const ctx = new MenuContext({});
      expect(ctx.getAccountLink("login")).toBe(
        `/identity/account/login?returnUrl=${encodeURIComponent("/")}`,
      );
    });

    it("falls back to / if the extracted returnUrl is itself an auth page", () => {
      // e.g. login?returnUrl=/identity/account/register — no real destination, don't chain
      mockLocation("/identity/account/login", "?returnUrl=%2Fidentity%2Faccount%2Fregister");
      const ctx = new MenuContext({});
      expect(ctx.getAccountLink("register")).toBe(
        `/identity/account/register?returnUrl=${encodeURIComponent("/")}`,
      );
    });

    it("does not chain returnUrls when switching between login and register", () => {
      // On the login page with a real returnUrl — simulates clicking "register" from here
      mockLocation("/identity/account/login", "?returnUrl=%2Fsong");
      const ctx = new MenuContext({});
      const link = ctx.getAccountLink("register");
      // returnUrl should be /song, not the full login page URL
      expect(link).toBe(`/identity/account/register?returnUrl=${encodeURIComponent("/song")}`);
      // The returnUrl param should appear exactly once
      const returnUrlCount = (link.match(/returnUrl/g) ?? []).length;
      expect(returnUrlCount).toBe(1);
    });

    it("is case-insensitive for /Identity/ path detection", () => {
      mockLocation("/Identity/Account/Login", "?returnUrl=%2Fsong");
      const ctx = new MenuContext({});
      const link = ctx.getAccountLink("register");
      expect(link).toBe(`/identity/account/register?returnUrl=${encodeURIComponent("/song")}`);
    });

    it("preserves query string on the destination when passing through returnUrl", () => {
      // Arrived at login with returnUrl=/song?filter=CHA — clicking register should keep that intact
      mockLocation("/identity/account/login", "?returnUrl=%2Fsong%3Ffilter%3DCHA");
      const ctx = new MenuContext({});
      expect(ctx.getAccountLink("register")).toBe(
        `/identity/account/register?returnUrl=${encodeURIComponent("/song?filter=CHA")}`,
      );
    });
  });
});

describe("MenuContext.axiosXsrf", () => {
  beforeEach(() => sessionStorage.clear());
  afterEach(() => vi.restoreAllMocks());

  // Runs a request through the instance's interceptors and returns the headers it would send.
  async function sentHeaders(ctx: MenuContext): Promise<InternalAxiosRequestConfig["headers"]> {
    let captured: InternalAxiosRequestConfig | undefined;
    await ctx.axiosXsrf.get("/api/suggestion/wal", {
      adapter: async (config) => {
        captured = config;
        return { data: {}, status: 200, statusText: "OK", headers: {}, config };
      },
    });
    return captured!.headers;
  }

  it("sends the page's token", async () => {
    const get = vi.spyOn(axios, "get");

    const headers = await sentHeaders(new MenuContext({ xsrfToken: "page-token" }));

    expect(headers.get("RequestVerificationToken")).toBe("page-token");
    expect(get).not.toHaveBeenCalled();
  });

  it("fetches a token first on an anonymous page", async () => {
    vi.spyOn(axios, "get").mockResolvedValue({ data: { token: "fetched-token" } });

    const headers = await sentHeaders(new MenuContext({ xsrfToken: "" }));

    expect(headers.get("RequestVerificationToken")).toBe("fetched-token");
  });

  it("still sends the request when no token can be fetched", async () => {
    vi.spyOn(axios, "get").mockRejectedValue(new Error("offline"));

    const headers = await sentHeaders(new MenuContext({ xsrfToken: "" }));

    expect(headers.get("RequestVerificationToken")).toBeUndefined();
  });
});
