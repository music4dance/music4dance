const regex = /(?<field>Artist|Title|Albums):\((?<search>[^)]*)\)/g;

export class KeywordQuery {
  public static fromParts(parts: Map<string, string>): KeywordQuery {
    let simple = true;
    const segments = [];
    const everywhere = parts.get("Everywhere") ?? "";

    parts.forEach((value, key) => {
      if (value && key !== "Everywhere") {
        segments.push(`${key}:(${value})`);
        simple = false;
      }
    });
    if (everywhere) {
      segments.push(everywhere);
    }
    return new KeywordQuery(simple ? everywhere : "`" + segments.join(" "));
  }

  public constructor(s?: string) {
    this.data = s ?? "";
  }

  public get isLucene(): boolean {
    return this.data.startsWith("`");
  }

  public get search(): string {
    return this.isLucene ? this.data.substring(1) : this.data;
  }

  public get query(): string {
    return this.data;
  }

  public update(part: string, value: string): KeywordQuery {
    const parts = this.fields;
    if (value) {
      parts.set(part, value);
    } else {
      parts.delete(part);
    }
    return KeywordQuery.fromParts(parts);
  }

  public get description(): string {
    if (this.data === "") {
      return "";
    }

    if (!this.isLucene) {
      return `containing the text "${this.data}"`;
    }

    const fields = this.fields;
    const all = fields.get("Everywhere");
    fields.delete("Everywhere");

    if (all && fields.size === 0) {
      return `containing the text "${all}"`;
    }

    let result = "where";
    let first = true;
    if (all) {
      result = `containing the text "${all}" anywhere`;
      first = false;
    }

    fields.forEach((value, key) => {
      if (!first) {
        result += " and";
      }
      result += ` ${key.toLowerCase()} contains "${value}"`;
      first = false;
    });

    return result;
  }

  public getField(field: string): string {
    return this.fields.get(field) ?? "";
  }

  private get fields(): Map<string, string> {
    const search = this.search;
    const matches = search.matchAll(regex);
    const result = new Map<string, string>();
    for (const match of matches) {
      result.set(match.groups?.field ?? "", match.groups?.search ?? "");
    }
    const all = search.replaceAll(regex, "").trim();
    if (all) {
      result.set("Everywhere", all);
    }
    return result;
  }

  private data: string;
}
