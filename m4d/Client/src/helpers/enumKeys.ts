export function enumKeys<
  O extends Record<string, unknown>,
  K extends keyof O = keyof O
>(obj: O): K[] {
  return Object.keys(obj).filter((k) => {
    return Number.isNaN(+k);
  }) as K[];
}
