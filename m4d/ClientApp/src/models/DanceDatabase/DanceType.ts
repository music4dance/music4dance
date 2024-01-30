import { kebabToWords, wordsToKebab } from "@/helpers/StringHelpers";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { DanceInstance } from "./DanceInstance";
import { DanceObject } from "./DanceObject";
import { TempoRange } from "./TempoRange";
import { assign } from "@/helpers/ObjectHelpers";
import type { DanceGroup } from "./DanceGroup";

@jsonObject({ onDeserialized: "onDeserialized" })
export class DanceType extends DanceObject {
  @jsonArrayMember(String) public organizations: string[] = [];
  @jsonMember(String) public link!: string;
  @jsonArrayMember(DanceInstance) public instances: DanceInstance[] = [];
  public groups: DanceGroup[] = [];

  public constructor(init?: Partial<DanceType>) {
    super();
    if (init) {
      assign(this, init);
      this.onDeserialized();
    }
  }

  private onDeserialized(): void {
    if (this.instances) {
      for (const inst of this.instances) {
        inst.danceType = this;
      }
    }
  }

  public reduce(instances: DanceInstance[]): DanceType {
    const other = new DanceType(this);
    other.instances = [...instances];
    for (const instance of other.instances) {
      instance.danceType = other;
    }

    return other;
  }

  public get styles(): string[] {
    return this.instances.map((inst) => inst.style);
  }

  public get competitionDances(): DanceInstance[] {
    return this.instances.filter((inst) => inst.competitionGroup);
  }

  public get tempoRange(): TempoRange {
    return this.instances.map((d) => d.tempoRange).reduce((acc, inst) => acc.include(inst));
  }

  public inGroup(group: string): boolean {
    return this.groups.find((g) => g.name === group) !== undefined;
  }

  public filteredStyles(filter: string[]): string[] {
    return filter ? this.styles.filter((s) => filter.indexOf(wordsToKebab(s)) !== -1) : this.styles;
  }

  public filteredTempo(styles: string[], organizations: string[]): TempoRange | undefined {
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
        ret = ret.include(newRange);
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
    return prefixes.filter((p) => organizations.find((o) => o.indexOf(p) !== -1));
  }
}
