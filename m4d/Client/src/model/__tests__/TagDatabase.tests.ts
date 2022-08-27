import { getTagDatabaseMock } from "@helpers/MockTagDatabaseManager";

describe("tag database", () => {
  it("should load", () => {
    const tagDatabase = getTagDatabaseMock();

    expect(tagDatabase).toBeDefined();
    expect(tagDatabase.tags).toBeDefined();
    expect(tagDatabase.tags.length > 0).toBeTruthy();
  });
});

// TAGTODO: What else do we want to test
