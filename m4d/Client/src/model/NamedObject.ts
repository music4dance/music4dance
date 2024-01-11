import { wordsToKebab } from "@/helpers/StringHelpers";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

@jsonObject
export class NamedObject {
  @jsonMember(String, { name: "id" }) public internalId!: string;
  @jsonMember(String, { name: "name" }) public internalName!: string;

  public get id(): string {
    return this.internalId;
  }

  public get name(): string {
    return this.internalName;
  }

  @jsonMember(String) public description?: string;
  @jsonArrayMember(String) public synonyms?: string[];
  @jsonArrayMember(String) public searchonyms?: string[];

  public isMatch(s: string): boolean {
    const t = NamedObject.normalize(s);
    return !!this.normalizedNames.find((n) => n === t);
  }

  public get seoName(): string {
    return wordsToKebab(this.name);
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
