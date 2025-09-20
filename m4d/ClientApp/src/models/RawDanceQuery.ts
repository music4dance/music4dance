import { DanceQueryBase } from "./DanceQueryBase";
import type { NamedObject } from "@/models/DanceDatabase/NamedObject";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { DanceQueryItem } from "./DanceQueryItem";

const danceEx = /DanceTags\/(any|all)\([^']*'(.*?)'/i;

export class RawDanceQuery extends DanceQueryBase {
  constructor(
    private odata?: string,
    private flags?: string,
  ) {
    super();
  }

  public get danceQueryItems(): DanceQueryItem[] {
    return this.danceObjects.map((d) => new DanceQueryItem({ id: d.id, threshold: 1 }));
  }

  public get singleDance(): boolean {
    return !!this.flagList.find((f) => f.toLowerCase() === "singledance");
  }

  private get danceObjects(): NamedObject[] {
    const dance = this.parseDance;
    return dance ? [safeDanceDatabase().fromName(dance)!] : [];
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
