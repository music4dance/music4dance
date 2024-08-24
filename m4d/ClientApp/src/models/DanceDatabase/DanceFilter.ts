import type { DanceInstance } from "./DanceInstance";
import type { DanceType } from "./DanceType";
import type { Meter } from "./Meter";

export class DanceFilter {
  public styles?: string[];
  public organizations?: string[];
  public groups?: string[];
  public meters?: Meter[];

  constructor(init?: Partial<DanceFilter>) {
    this.styles = init?.styles;
    this.organizations = init?.organizations;
    this.groups = init?.groups;
    this.meters = init?.meters;
  }

  public reduce(type: DanceType): DanceType | null {
    if (!this.matchMeter(type) || !this.matchGroups(type) || !this.matchOrganizations(type)) {
      return null;
    }

    const coversOrgs = this.coversOrganizations(type);
    const instances = this.getMatchingInstances(type).map((inst) =>
      inst.reduceExceptions(coversOrgs ? undefined : this.organizations),
    );
    return instances.length > 0 ? type.reduce(instances) : null;
  }

  public filter(types: DanceType[]): DanceType[] {
    // Don't know why the explicit cast is needed here - I guess at some leve the guarantee
    // that the array elements are non-null happens at runtime, but not at compile time?
    return types.map((t) => this.reduce(t)).filter((type) => type !== null) as DanceType[];
  }

  private matchMeter(type: DanceType): boolean {
    return this.meters === undefined || !!this.meters.find((m) => m.equals(type.meter));
  }

  private matchGroups(type: DanceType): boolean {
    return this.groups === undefined || type.groups.some((g) => this.groups!.includes(g.name));
  }

  private matchOrganizations(type: DanceType): boolean {
    return (
      this.organizations === undefined ||
      type.organizations.some((o) => this.organizations!.includes(o))
    );
  }

  private getMatchingInstances(type: DanceType): DanceInstance[] {
    return this.styles !== undefined
      ? type.instances.filter((inst) => this.styles!.includes(inst.style))
      : type.instances;
  }

  private coversOrganizations(type: DanceType): boolean {
    return (
      this.organizations === undefined ||
      type.organizations.every((o) => this.organizations!.includes(o))
    );
  }
}
