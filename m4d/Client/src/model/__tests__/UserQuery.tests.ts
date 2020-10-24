import { UserQuery } from "../UserQuery";

describe("user query", () => {
  it("should handle a empty", () => {
    const uq = new UserQuery();
    expect(uq).toBeDefined();
    expect(uq).toBeInstanceOf(UserQuery);
    expect(uq.isDefault()).toBeTruthy();
    expect(uq.isEmpty).toBeTruthy();
    expect(uq.query).toEqual("");
    expect(uq.userName).toBeFalsy();
    expect(uq.description).toEqual("");
    // expect(uq.include).toBeTruthy();
    // expect(uq.tag).toBeTruthy();
  });

  it("should handle a default (exclude hate)", () => {
    const uq = new UserQuery("-me|h");
    expect(uq).toBeDefined();
    expect(uq).toBeInstanceOf(UserQuery);
    expect(uq.isDefault("administrator")).toBeTruthy();
    expect(uq.isEmpty).toBeFalsy();
    expect(uq.query).toEqual("-me|h");
    expect(uq.userName).toEqual("me");
    expect(uq.include).toBeFalsy();
    expect(uq.hate).toBeTruthy();
    expect(uq.userName).toEqual("me");
    expect(uq.description).toEqual("excluding songs disliked by me");
  });

  it("should handle exclude like", () => {
    const uq = new UserQuery("-me|l");
    expect(uq).toBeDefined();
    expect(uq).toBeInstanceOf(UserQuery);
    expect(uq.isDefault("administrator")).toBeFalsy();
    expect(uq.isEmpty).toBeFalsy();
    expect(uq.query).toEqual("-me|l");
    expect(uq.include).toBeFalsy();
    expect(uq.like).toBeTruthy();
    expect(uq.description).toEqual("excluding songs liked by me");
  });

  it("should handle exclude tagged", () => {
    const uq = new UserQuery("-me|");
    expect(uq).toBeDefined();
    expect(uq).toBeInstanceOf(UserQuery);
    expect(uq.isDefault("administrator")).toBeFalsy();
    expect(uq.isEmpty).toBeFalsy();
    expect(uq.query).toEqual("-me|");
    expect(uq.include).toBeFalsy();
    expect(uq.tag).toBeTruthy();
    expect(uq.description).toEqual("excluding songs edited by me");
  });

  it("should handle include like", () => {
    const uq = new UserQuery("+me|l");
    expect(uq).toBeDefined();
    expect(uq).toBeInstanceOf(UserQuery);
    expect(uq.isDefault("administrator")).toBeFalsy();
    expect(uq.isEmpty).toBeFalsy();
    expect(uq.query).toEqual("+me|l");
    expect(uq.include).toBeTruthy();
    expect(uq.like).toBeTruthy();
    expect(uq.description).toEqual("including songs liked by me");
  });

  it("should handle include hate", () => {
    const uq = new UserQuery("+me|h");
    expect(uq).toBeDefined();
    expect(uq).toBeInstanceOf(UserQuery);
    expect(uq.isDefault("administrator")).toBeFalsy();
    expect(uq.isEmpty).toBeFalsy();
    expect(uq.query).toEqual("+me|h");
    expect(uq.include).toBeTruthy();
    expect(uq.hate).toBeTruthy();
    expect(uq.description).toEqual("including songs disliked by me");
  });

  it("should handle include tag", () => {
    const uq = new UserQuery("+me|");
    expect(uq).toBeDefined();
    expect(uq).toBeInstanceOf(UserQuery);
    expect(uq.isDefault("administrator")).toBeFalsy();
    expect(uq.isEmpty).toBeFalsy();
    expect(uq.query).toEqual("+me|");
    expect(uq.include).toBeTruthy();
    expect(uq.tag).toBeTruthy();
    expect(uq.description).toEqual("including songs edited by me");
  });
});

// describe('user query description', () => {
//     it ('should handle a single dance', () => {
//     });
// });
