import { DanceEnvironment } from "./DanceEnvironment";
import { DanceQueryBase } from "./DanceQueryBase";
import { DanceStats } from "./DanceStats";

declare const environment: DanceEnvironment;

const danceEx = /DanceTags\/(any|all)\([^']*'(.*?)'/i;

export class RawDanceQuery extends DanceQueryBase {
  constructor(private odata?: string, private flags?: string) {
    super();
  }

  public get danceList(): string[] {
    return this.danceStats.map((ds) => ds.id);
  }

  public get singleDance(): boolean {
    return !!this.flagList.find((f) => f.toLowerCase() === "singledance");
  }

  private get danceStats(): DanceStats[] {
    const dance = this.parseDance;
    return dance ? [environment.fromName(dance)!] : [];
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
