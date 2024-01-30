import { DanceType } from "@/models/DanceDatabase/DanceType";

export class DanceTypeCount extends DanceType {
  public constructor(
    danceType: DanceType,
    public count: number,
  ) {
    super(danceType);
    this.count = count;
  }
}
