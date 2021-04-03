import { TaggableObject } from "./TaggableObject";
import { DanceRating } from "./DanceRating";
import { SongFilter } from "./SongFilter";
import { Tag } from "./Tag";
import { TagHandler } from "./TagHandler";

export class DanceHandler extends TagHandler {
  constructor(
    public danceRating: DanceRating,
    tag: Tag,
    user?: string,
    filter?: SongFilter,
    parent?: TaggableObject
  ) {
    super(tag, user, filter, parent);
  }
}
