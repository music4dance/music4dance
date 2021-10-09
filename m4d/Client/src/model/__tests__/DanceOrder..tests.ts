import { getEnvironmentMock } from "@/helpers/MockEnvironmentManager";
import { DanceOrder } from "../DanceOrder";

describe("Dance Order", () => {
  it("should load", () => {
    const loaded = getEnvironmentMock();

    expect(loaded).toBeDefined();
    expect(loaded?.tree).toBeDefined();
    expect(loaded?.tree?.length).toBeDefined();
    expect(loaded?.tree?.length).toEqual(7);
  });

  it("should filter tempos", () => {
    const mock = getEnvironmentMock();
    if (!mock || !mock.tree) {
      throw new Error("Mock not created");
    }
    expect(mock.dances).toBeDefined();
    const hundred = DanceOrder.dancesForTempo(mock.dances!, 100, 4);

    expect(hundred).toBeDefined();
    expect(hundred.length).toBeDefined();
    expect(hundred.length).toEqual(8);
  });
});
