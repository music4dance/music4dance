import { getEnvironmentMock } from "@/helpers/MockEnvironmentManager";

describe("dance environment", () => {
  it("should load", () => {
    const loaded = getEnvironmentMock();

    expect(loaded).toBeDefined();
    expect(loaded?.tree).toBeDefined();
    expect(loaded?.tree?.length).toBeDefined();
    expect(loaded?.tree?.length).toEqual(7);
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

  it("should return synonyms", () => {
    const env = getEnvironmentMock();
    expect(env).toBeDefined();

    const cft = env.danceFromId("CFT");
    expect(cft).toBeDefined();
    const synonyms = cft?.synonyms;
    expect(synonyms).toBeDefined();
    expect(synonyms?.length).toBe(1);
    expect(synonyms![0]).toEqual("Slow Dance");
  });

  it("should match various 2 steps", () => {
    const env = getEnvironmentMock();
    expect(env).toBeDefined();

    const nc2 = env.danceFromId("NC2");
    expect(nc2).toBeDefined();

    expect(nc2?.isMatch("NC2")).toBeTruthy();
    expect(nc2?.isMatch("Club 2 Step")).toBeTruthy();
    expect(nc2?.isMatch("Nightclub Twostep")).toBeTruthy();
    expect(nc2?.isMatch("Club two Step")).toBeTruthy();
    expect(nc2?.isMatch("Night Club 2 Step")).toBeTruthy();
  });

  it("should match various ecs", () => {
    const env = getEnvironmentMock();
    expect(env).toBeDefined();

    const nc2 = env.danceFromId("ECS");
    expect(nc2).toBeDefined();

    expect(nc2?.isMatch("ECS")).toBeTruthy();
    expect(nc2?.isMatch("Eastcoast Swing")).toBeTruthy();
    expect(nc2?.isMatch("East Coast")).toBeTruthy();
    expect(nc2?.isMatch("ECSwing")).toBeTruthy();
    expect(nc2?.isMatch("Swing")).toBeTruthy();
  });
});
