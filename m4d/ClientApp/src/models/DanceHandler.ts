import { DanceRating } from "./DanceRating";
import type { SongEditor } from "./SongEditor";
import { SongFilter } from "./SongFilter";
import { Tag } from "./Tag";
import { TaggableObject } from "./TaggableObject";
import { TagHandler } from "./TagHandler";

export class DanceHandler extends TagHandler {
  constructor(
    public danceRating: DanceRating,
    tag: Tag,
    user?: string,
    filter?: SongFilter,
    parent?: TaggableObject,
    public editor?: SongEditor,
  ) {
    super(tag, user, filter, parent);
  }

  public clone(): DanceHandler {
    return new DanceHandler(
      this.danceRating,
      this.tag,
      this.user,
      this.filter,
      this.parent,
      this.editor,
    );
  }
}
