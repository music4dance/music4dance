import { DanceQueryBase } from "./DanceQueryBase";
import type { DanceDatabase } from "./DanceDatabase";
import type { NamedObject } from "./NamedObject";

declare const danceDatabase: DanceDatabase;

const danceEx = /DanceTags\/(any|all)\([^']*'(.*?)'/i;

export class RawDanceQuery extends DanceQueryBase {
  constructor(
    private odata?: string,
    private flags?: string,
  ) {
    super();
  }

  public get danceList(): string[] {
    return this.dances.map((ds) => ds.id);
  }

  public get singleDance(): boolean {
    return !!this.flagList.find((f) => f.toLowerCase() === "singledance");
  }

  // INT-TODO: this used to be danceStats and returned a danceStats object
  public get dances(): NamedObject[] {
    const dance = this.parseDance;
    return dance ? [danceDatabase.fromName(dance)!] : [];
  }

  private get parseDance(): string | undefined {
    if (!this.odata) {
      return;
    }
    const match = this.odata.match(danceEx);

    return match?.length === 3 ? match[2] : undefined;
  }

  public get flagList(): string[] {
    return this.flags ? (this.flags ?? "").split("|") : [];
  }
}
