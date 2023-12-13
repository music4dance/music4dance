import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { wordsToKebab } from "@/helpers/StringHelpers";
import { DanceInstance } from "@/models/DanceInstance";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";

const danceDatabase = safeDanceDatabase();

@jsonObject
export class CompetitionCategory {
  @jsonMember(String) public name!: string;
  @jsonMember(String) public group!: string;
  @jsonMember(String) public categoryType!: string;
  @jsonArrayMember(String, { name: "round" }) public roundIds!: string[];
  @jsonArrayMember(String, { name: "extras" }) public extrasIds!: string[];

  public get round(): DanceInstance[] {
    return CompetitionCategory.getInstances(this.roundIds);
  }

  public get extras(): DanceInstance[] {
    return CompetitionCategory.getInstances(this.extrasIds);
  }

  public get fullRoundName(): string {
    const num = this.round.length === 4 ? "four" : "five";
    return `${this.name} ${num} dance round`;
  }

  public get fullRoundTitle(): string {
    return `An ${this.fullRoundName} consists of the following:`;
  }

  public get canonicalName(): string {
    return wordsToKebab(this.name);
  }

  public get extraDancesTitle(): string {
    return this.extras && this.extras.length > 0
      ? `Other dances categorized as ${this.name}, although they aren't part of the competition round, are:`
      : "";
  }

  private static getInstances(ids: string[]): DanceInstance[] {
    return ids.map((id) => danceDatabase.instanceFromId(id)!);
  }
}

@jsonObject
export class CompetitionGroup {
  @jsonMember(String) public name!: string;
  @jsonArrayMember(CompetitionCategory)
  public categories!: CompetitionCategory[];
}

@jsonObject
export class CompetitionGroupModel {
  @jsonMember(String) public currentCategoryName!: string;
  @jsonMember(CompetitionGroup) public group!: CompetitionGroup;

  public get currentCategory(): CompetitionCategory {
    const name = this.currentCategoryName.toLowerCase();
    return this.group.categories.find((cat) => cat.name.toLowerCase() === name)!;
  }

  public get otherCategories(): CompetitionCategory[] {
    const name = this.currentCategoryName.toLowerCase();
    return this.group.categories.filter((cat) => cat.name.toLowerCase() !== name);
  }
}
