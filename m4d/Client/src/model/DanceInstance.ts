import "reflect-metadata";
import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { DanceException } from "./DanceException";
import { DanceObject } from "./DanceObject";
import { TempoRange } from "./TempoRange";

@jsonObject
export class DanceInstance extends DanceObject {
  @jsonMember public style!: string;
  @jsonMember public competitionGroup!: string;
  @jsonMember public compititionOrder!: number;
  @jsonArrayMember(DanceException) public exceptions!: DanceException[];

  public filteredTempo(organizations: string[]): TempoRange | undefined {
    if (!organizations.length) {
      return this.tempoRange;
    }

    // First - if any choice doesn't have an explicit exception,
    //  just return the instance tempo range
    const excs = this.exceptionsFromOrganization(organizations);
    if (!excs.length) {
      return this.tempoRange;
    }

    // INT-TODO: We can simplify this if we get rid of the second part of the organization
    // If there is an organization that isn't included in the exceptions, include the instance tempo range
    const includeTop = !!organizations
      .map((o) => o.split("-")[0])
      .find(
        (o) =>
          !excs.find(
            (e) => e.organization.toLocaleLowerCase() === o.toLocaleLowerCase()
          )
      );
    let ret: TempoRange | undefined = includeTop ? this.tempoRange : undefined;

    for (const exc of excs) {
      if (!exc.tempoRange) {
        continue;
      }
      const newRange = exc.tempoRange;
      if (!ret) {
        ret = newRange;
      } else {
        ret = ret.combine(newRange);
      }
    }
    return ret;
  }

  public get styleFamily(): string {
    return this.style.split(" ")[0];
  }

  private exceptionsFromOrganization(
    organizations: string[]
  ): DanceException[] {
    return this.exceptions.filter((e) =>
      organizations.find((o) => e.matchesFilter(o))
    );
  }

  public get shortName(): string {
    const styleFamily = this.styleFamily;
    return this.name.startsWith(styleFamily + " ")
      ? this.name.substring(styleFamily.length + 1)
      : this.name;
  }
}
