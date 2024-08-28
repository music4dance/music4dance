import { wordsToKebab } from "@/helpers/StringHelpers";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { SerializableObject } from "../SerializableObject";

@jsonObject
export class NamedObject extends SerializableObject {
  @jsonMember(String, { name: "id" }) public internalId!: string;
  @jsonMember(String, { name: "name" }) public internalName!: string;

  public get id(): string {
    return this.internalId;
  }

  public get name(): string {
    return this.internalName;
  }

  public get hasSynonyms(): boolean {
    return !!(this.synonyms && this.synonyms.length > 0);
  }

  public get displayName(): string {
    return this.hasSynonyms ? `${this.name} (${this.synonyms?.join(",")})` : this.name;
  }

  @jsonMember(String) public description?: string;
  @jsonArrayMember(String) public synonyms?: string[];
  @jsonArrayMember(String) public searchonyms?: string[];

  public isMatch(s: string): boolean {
    const t = NamedObject.normalize(s);
    return !!this.normalizedNames.find((n) => n === t);
  }

  public get seoName(): string {
    return this.name ? wordsToKebab(this.name) : "???";
  }

  public hasString(s: string): boolean {
    const l = s.toLowerCase();
    return this.normalizedNames.find((n) => n.includes(l)) !== undefined;
  }

  private get normalizedNames(): string[] {
    return this.id && this.name
      ? [
          NamedObject.normalize(this.id),
          NamedObject.normalize(this.name),
          ...NamedObject.normalizedArray(this.synonyms),
          ...NamedObject.normalizedArray(this.searchonyms),
        ]
      : [];
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
