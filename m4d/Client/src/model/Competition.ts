import "reflect-metadata";
import { jsonMember, jsonObject, jsonArrayMember } from "typedjson";
import { DanceInstance } from "@/model/DanceStats";

@jsonObject
export class CompetitionCategory {
  @jsonMember public name!: string;
  @jsonMember public group!: string;
  @jsonMember public categoryType!: string;
  @jsonMember public canonicalName!: string;
  @jsonMember public fullRoundName!: string;
  @jsonArrayMember(DanceInstance) public round!: DanceInstance[];
  @jsonArrayMember(DanceInstance) public extras!: DanceInstance[];

  public get fullRoundTitle(): string {
    return `An ${this.fullRoundName} consists of the following:`;
  }

  public get extraDancesTitle(): string {
    return this.extras && this.extras.length > 0
      ? `Other dances categorized as ${this.name}, although they aren't part of the competition round, are:`
      : "";
  }
}

@jsonObject
export class CompetitionGroup {
  @jsonMember public name!: string;
  @jsonArrayMember(CompetitionCategory)
  public categories!: CompetitionCategory[];
}

@jsonObject
export class CompetitionGroupModel {
  @jsonMember public currentCategoryName!: string;
  @jsonMember public group!: CompetitionGroup;

  public get currentCategory(): CompetitionCategory {
    const name = this.currentCategoryName.toLowerCase();
    return this.group.categories.find(
      (cat) => cat.name.toLowerCase() === name
    )!;
  }

  public get otherCategories(): CompetitionCategory[] {
    const name = this.currentCategoryName.toLowerCase();
    return this.group.categories.filter(
      (cat) => cat.name.toLowerCase() !== name
    );
  }
}
