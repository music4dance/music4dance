export function wordsToKebab(words: string): string {
    return words.toLowerCase().replace(' ', '-');
}

export function kebabToWords(kebab: string): string {
    return toTitleCase(kebab, '-');
}

export function toTitleCase(sentance: string, separator?: string): string {
    const words = sentance.split(separator ?? ' ');
    return words.map((w) => w.substr(0, 1).toUpperCase() + w.substr(1)).join(' ');
}
