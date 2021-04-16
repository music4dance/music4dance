export function jsonClone<T>(obj: T): T {
  return JSON.parse(JSON.stringify(obj));
}

export function jsonCompare<T>(a: T, b: T): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}
