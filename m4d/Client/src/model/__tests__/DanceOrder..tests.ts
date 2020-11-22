import { DanceOrder } from "../DanceOrder";
import { getEnvironmentMock } from "@/helpers/MockEnvironmentManager";

/* eslint-disable-next-line @typescript-eslint/no-explicit-any */
declare const global: any;

beforeAll(() => {
  global.environment = getEnvironmentMock();
});

describe("Dance Order", () => {
  it("should load", () => {
    const loaded = getEnvironmentMock();

    expect(loaded).toBeDefined();
    expect(loaded?.stats).toBeDefined();
    expect(loaded?.stats?.length).toBeDefined();
    expect(loaded?.stats?.length).toEqual(7);
  });

  it("should filter tempos", () => {
    const mock = getEnvironmentMock();
    const hundred = DanceOrder.dancesForTempo(mock?.stats!, 100, 4);

    expect(hundred).toBeDefined();
    expect(hundred.length).toBeDefined();
    expect(hundred.length).toEqual(9);
  });
});
