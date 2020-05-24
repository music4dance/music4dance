export function wordsToKebab(words: string): string {
    return words.toLowerCase().replace(' ', '-');
}

export function kebabToWords(kebab: string): string {
    const words = kebab.split('-');
    return words.map((w) => w.substr(0, 1).toUpperCase() + w.substr(1)).join(' ');
}
