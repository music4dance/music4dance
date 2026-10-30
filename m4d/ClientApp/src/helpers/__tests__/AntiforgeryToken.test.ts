import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import axios from "axios";
import {
  ANTIFORGERY_TOKEN_ENDPOINT,
  currentAntiforgeryToken,
  ensureAntiforgeryToken,
} from "../AntiforgeryToken";

describe("AntiforgeryToken", () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it("uses the page's own token without a request", async () => {
    const get = vi.spyOn(axios, "get");

    expect(currentAntiforgeryToken("page-token")).toBe("page-token");
    await expect(ensureAntiforgeryToken("page-token")).resolves.toBe("page-token");
    expect(get).not.toHaveBeenCalled();
  });

  it("fetches a token once for concurrent callers and keeps it for the tab", async () => {
    const get = vi.spyOn(axios, "get").mockResolvedValue({ data: { token: "fetched" } });

    const [first, second] = await Promise.all([ensureAntiforgeryToken(), ensureAntiforgeryToken()]);

    expect(first).toBe("fetched");
    expect(second).toBe("fetched");
    expect(get).toHaveBeenCalledTimes(1);
    expect(get).toHaveBeenCalledWith(ANTIFORGERY_TOKEN_ENDPOINT);
    expect(currentAntiforgeryToken()).toBe("fetched");
  });

  it("fetches again once the kept token is an hour old", async () => {
    vi.useFakeTimers();
    const get = vi
      .spyOn(axios, "get")
      .mockResolvedValueOnce({ data: { token: "first" } })
      .mockResolvedValueOnce({ data: { token: "second" } });

    await ensureAntiforgeryToken();
    vi.advanceTimersByTime(61 * 60 * 1000);

    expect(currentAntiforgeryToken()).toBe("");
    await expect(ensureAntiforgeryToken()).resolves.toBe("second");
    expect(get).toHaveBeenCalledTimes(2);
  });

  it("drops a kept token when a signed-in page supplies its own", async () => {
    vi.spyOn(axios, "get").mockResolvedValue({ data: { token: "anonymous" } });
    await ensureAntiforgeryToken();

    expect(currentAntiforgeryToken("signed-in")).toBe("signed-in");
    expect(currentAntiforgeryToken()).toBe("");
  });

  it("lets a failed fetch be retried", async () => {
    const get = vi
      .spyOn(axios, "get")
      .mockRejectedValueOnce(new Error("offline"))
      .mockResolvedValueOnce({ data: { token: "fetched" } });

    await expect(ensureAntiforgeryToken()).rejects.toThrow("offline");
    await expect(ensureAntiforgeryToken()).resolves.toBe("fetched");
    expect(get).toHaveBeenCalledTimes(2);
  });
});
