export function assign(trg: object, src?: object): object {
  if (!src) {
    return trg;
  }
  const descriptors = Object.getOwnPropertyDescriptors(src);
  for (const key in descriptors) {
    const descriptor = descriptors[key];
    if (descriptor?.writable && descriptor?.enumerable) {
      Object.defineProperty(trg, key, descriptor);
    }
  }
  return trg;
}

export function jsonCompare<T>(a: T, b: T): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

export function jsonClone<T>(obj: T): T {
  return JSON.parse(JSON.stringify(obj));
}

let currentId = 1;
export function getId(): string {
  return "__m4d__" + (currentId++).toString().padStart(6, "0");
}
