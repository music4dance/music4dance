import { ITaggableObject } from './ITaggableObject';
import { SongFilter } from './SongFilter';
import { Tag } from './Tag';

export class TagHandler {
    constructor(
        public tag: Tag,
        public user?: string,
        public filter?: SongFilter,
        public parent?: ITaggableObject) {
    }

    public get id(): string {
        const parent = this.parent;
        return parent ? `${parent.id}-${this.tag.key}` : this.tag.key;
    }
}
