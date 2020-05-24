import {wordsToKebab } from '../helpers/StringHelpers';

export interface ListOption {
    text: string;
    value: string;
}

export function valuesFromOptions(options: ListOption[]): string[] {
    return options.map(({value}) => value);
}

export function optionsFromText(text: string[]): ListOption[] {
    const test: ListOption = {text: 't', value: 'v'};
    return text.map((t) => ({ text: t, value: wordsToKebab(t) }));
}
