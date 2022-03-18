import { kebabToWords, wordsToKebab } from "../StringHelpers";

describe("string helper", () => {
  it("should convert words to kebab", () => {
    expect(wordsToKebab("Social")).toEqual("social");
    expect(wordsToKebab("International Latin")).toEqual("international-latin");
  });

  it("should convert kebab to words", () => {
    expect(kebabToWords("social")).toEqual("Social");
    expect(kebabToWords("international-latin")).toEqual("International Latin");
  });
});
