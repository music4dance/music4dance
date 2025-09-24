export function wordsToKebab(words: string): string {
  return words.toLowerCase().replaceAll(" ", "-");
}

export function toTitleCase(sentance: string, separator?: string): string {
  const words = sentance.split(separator ?? " ");
  return words.map((w) => w.substring(0, 1).toUpperCase() + w.substring(1)).join(" ");
}

export function kebabToWords(kebab: string): string {
  return toTitleCase(kebab, "-");
}

export function kebabToPascal(kebab: string): string {
  const words = kebab.split("-");
  return words.map((w) => w.substring(0, 1).toUpperCase() + w.substring(1)).join("");
}

export function pascalToCamel(pascal: string): string {
  if (!pascal || pascal.length === 0) {
    return pascal;
  }
  return pascal[0]!.toLowerCase() + pascal.substring(1);
}

export function camelToPascal(camel: string): string {
  if (!camel || camel.length < 1) {
    return camel;
  }
  return camel[0]!.toUpperCase() + camel.substring(1);
}
