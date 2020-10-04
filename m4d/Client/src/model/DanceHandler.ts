import { ITaggableObject } from './ITaggableObject';
import { DanceRating } from './Song';
import { SongFilter } from './SongFilter';
import { Tag } from './Tag';
import { TagHandler } from './TagHandler';

export class DanceHandler extends TagHandler {
    constructor(
        public danceRating: DanceRating,
        tag: Tag,
        user?: string,
        filter?: SongFilter,
        parent?: ITaggableObject) {
        super(tag, user, filter, parent);
    }
}
