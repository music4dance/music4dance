// TODO: Consider using this as a base class for all class that we are using with typedJson

export class SerializableObject {
  public constructor(init?: Partial<SerializableObject>) {
    Object.assign(this, init);
  }

  public toJSON() {
    const excludeKeys = Object.getPrototypeOf(this).constructor.excludeKeys || [];
    const result: { [key: string]: unknown } = {};
    const self = this as Record<string, unknown>;

    SerializableObject.getSerializeableKeys(this).forEach((key) => {
      if (!excludeKeys.includes(key)) {
        result[key] = self[key];
      }
    });

    return result;
  }

  private static getSerializeableKeys(obj: object): string[] {
    const keys: string[] = [];
    do {
      if (obj.constructor.name === "SerializableObject") {
        break;
      }
      Object.getOwnPropertyNames(obj).forEach((prop) => {
        if (
          keys.indexOf(prop) === -1 &&
          SerializableObject.isData(obj, prop) &&
          !SerializableObject.excludePattern(prop)
        ) {
          keys.push(prop);
        }
      });
      obj = Object.getPrototypeOf(obj);
    } while (obj);

    return keys;
  }

  private static isData(obj: Record<string, unknown>, key: string): boolean {
    const descriptor = Object.getOwnPropertyDescriptor(obj, key);
    // if (!descriptor) {
    //   console.log(`No descriptor for ${key}`);
    // } else {
    //   console.log(`Desriptor ${key}:`, descriptor);
    // }
    if (descriptor && descriptor.get) {
      return true;
    }
    return typeof obj[key] !== "function";
  }

  private static excludePattern(key: string): boolean {
    return key.startsWith("internal") || key.startsWith("__");
  }
}
