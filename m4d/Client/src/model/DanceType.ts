import { kebabToWords, wordsToKebab } from "@/helpers/StringHelpers";
import "reflect-metadata";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { DanceInstance } from "./DanceInstance";
import { DanceObject } from "./DanceObject";
import { TempoRange } from "./TempoRange";

@jsonObject
export class DanceType extends DanceObject {
  @jsonArrayMember(String) public organizations!: string[];
  @jsonMember public link!: string;
  @jsonArrayMember(DanceInstance) public instances!: DanceInstance[];

  public get styles(): string[] {
    return this.instances.map((inst) => inst.style);
  }

  public get seoName(): string {
    return wordsToKebab(this.name);
  }

  public get competitionDances(): DanceInstance[] {
    return this.instances.filter((inst) => inst.competitionGroup);
  }

  public filteredStyles(filter: string[]): string[] {
    return filter
      ? this.styles.filter((s) => filter.indexOf(wordsToKebab(s)) !== -1)
      : this.styles;
  }

  public filteredTempo(
    styles: string[],
    organizations: string[]
  ): TempoRange | undefined {
    if (!styles.length && !organizations.length) {
      return this.tempoRange;
    }

    let ret: TempoRange | undefined;
    for (const inst of this.instances) {
      if (
        !inst.tempoRange ||
        (styles.length && !styles.find((s) => s === wordsToKebab(inst.style)))
      ) {
        continue;
      }

      const newRange = inst.filteredTempo(organizations);
      if (!ret) {
        ret = newRange;
      } else if (newRange) {
        ret = ret.combine(newRange);
      }
    }

    return ret;
  }

  private instanceFromStyle(style: string): DanceInstance | undefined {
    return this.instances.find((i) => i.style === kebabToWords(style));
  }

  // TODO: should we throw out the whole thing if the dancetype isn't specified by the organization?
  private buildOrganizationIds(organizations: string[]): string[] {
    const prefixes = ["Social", "DanceSport", "NDCA"];
    return prefixes.filter((p) =>
      organizations.find((o) => o.indexOf(p) !== -1)
    );
  }
}
