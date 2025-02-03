import { DanceRating } from "./DanceRating";
import type { SongEditor } from "./SongEditor";
import { TagHandler } from "./TagHandler";

export class DanceHandler extends TagHandler {
  public danceRating: DanceRating;
  public editor?: SongEditor;
  constructor(init?: Partial<DanceHandler>) {
    super(init);
    this.danceRating = init?.danceRating ?? new DanceRating();
    this.editor = init?.editor;
  }

  public clone(): DanceHandler {
    return new DanceHandler(this);
  }
}
