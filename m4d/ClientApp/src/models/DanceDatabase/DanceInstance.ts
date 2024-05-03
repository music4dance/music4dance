import { jsonArrayMember, jsonMember, jsonObject } from "typedjson";
import { DanceException } from "./DanceException";
import { DanceObject } from "./DanceObject";
import { TempoRange } from "./TempoRange";
import type { DanceType } from "./DanceType";
import { assign } from "@/helpers/ObjectHelpers";
import type { Meter } from "./Meter";

@jsonObject
export class DanceInstance extends DanceObject {
  @jsonMember(String) public style!: string;
  @jsonMember(String) public competitionGroup!: string;
  @jsonMember(Number) public competitionOrder!: number;
  @jsonArrayMember(DanceException) public exceptions: DanceException[] = [];
  public danceType!: DanceType;

  public static excludeKeys = ["danceType"];

  public constructor(init?: Partial<DanceInstance>) {
    super();
    assign(this, init);
  }

  public get id(): string {
    return this.danceType.id + this.styleId;
  }

  public get name(): string {
    return this.shortStyle + " " + this.danceType.name;
  }

  public get meter(): Meter {
    return this.danceType.meter;
  }

  public get shortStyle(): string {
    return this.style.split(" ")[0];
  }

  public get styleId(): string {
    return this.shortStyle.substring(0, 1);
  }

  public reduceExceptions(orgs: string[] | undefined): DanceInstance {
    const other = new DanceInstance(this);
    if (orgs != undefined) {
      const exceptions = this.exceptions.filter((de) => orgs.includes(de.organization));
      if (exceptions.length > 0) {
        other.internalTempoRange = exceptions.reduce(
          (current: TempoRange | null, de: DanceException) => de.tempoRange.include(current),
          null,
        )!;
      }
    }
    other.exceptions = [];
    return other;
  }

  // FILTER-TODO: This can probably go when new filtering is in place
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
      .find((o) => !excs.find((e) => e.organization.toLocaleLowerCase() === o.toLocaleLowerCase()));
    let ret: TempoRange | undefined = includeTop ? this.tempoRange : undefined;

    for (const exc of excs) {
      if (!exc.tempoRange) {
        continue;
      }
      const newRange = exc.tempoRange;
      if (!ret) {
        ret = newRange;
      } else {
        ret = ret.include(newRange);
      }
    }
    return ret;
  }

  public get styleFamily(): string {
    return this.style.split(" ")[0];
  }

  private exceptionsFromOrganization(organizations: string[]): DanceException[] {
    const exceptions = this.exceptions;
    return exceptions
      ? this.exceptions.filter((e) => organizations.find((o) => e.matchesFilter(o)))
      : [];
  }

  public get shortName(): string {
    const styleFamily = this.styleFamily;
    return this.name.startsWith(styleFamily + " ")
      ? this.name.substring(styleFamily.length + 1)
      : this.name;
  }
}
