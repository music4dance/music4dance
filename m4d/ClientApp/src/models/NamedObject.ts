import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

@jsonObject
export class NamedObject {
  @jsonMember(String) public id!: string;
  @jsonMember(String) public name!: string;
  @jsonMember(String) public description?: string;
  @jsonArrayMember(String) public synonyms?: string[];
  @jsonArrayMember(String) public searchonyms?: string[];

  public isMatch(s: string): boolean {
    const t = NamedObject.normalize(s);
    return !!this.normalizedNames.find((n) => n === t);
  }

  private get normalizedNames(): string[] {
    const r = [
      NamedObject.normalize(this.id),
      NamedObject.normalize(this.name),
      ...NamedObject.normalizedArray(this.synonyms),
      ...NamedObject.normalizedArray(this.searchonyms),
    ];
    return r;
  }

  private static normalizedArray(rg: string[] | undefined): string[] {
    return rg ? rg.map((n) => NamedObject.normalize(n)) : [];
  }

  private static normalize(s: string): string {
    return s
      .replaceAll("1", "one")
      .replaceAll("2", "two")
      .toLowerCase()
      .replace(/[^a-z]/g, "");
  }
}
