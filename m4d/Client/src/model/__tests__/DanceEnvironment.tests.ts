import { getEnvironmentMock } from "@/helpers/MockEnvironmentManager";

describe("dance environment", () => {
  it("should load", () => {
    const loaded = getEnvironmentMock();

    expect(loaded).toBeDefined();
    expect(loaded?.stats).toBeDefined();
    expect(loaded?.stats?.length).toBeDefined();
    expect(loaded?.stats?.length).toEqual(7);
  });

  it("should return unique styles", async () => {
    const styles = getEnvironmentMock()?.styles;
    expect(styles).toBeDefined();
    expect(styles?.length).toBeDefined();
    expect(styles?.length).toEqual(6);
  });

  it("should return unique types", () => {
    const types = getEnvironmentMock()?.types;
    expect(types).toBeDefined();
    expect(types?.length).toBeDefined();
    expect(types?.length).toEqual(7);
  });
});
