export function assign(trg: object, src?: object): object {
  if (!src) {
    return trg;
  }
  const descriptors = Object.getOwnPropertyDescriptors(src);
  for (const key in descriptors) {
    const descriptor = descriptors[key];
    if (descriptor.writable && descriptor.enumerable) {
      Object.defineProperty(trg, key, descriptor);
    }
  }
  return trg;
}
