import { Tag } from './Tag';

export interface ITaggableObject {
    description: string;
    id: string;
    tags: Tag[];
    currentUserTags: Tag[];
}
