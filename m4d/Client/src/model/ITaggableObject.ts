import { Tag } from "./Tag";

/* eslint-disable-next-line @typescript-eslint/interface-name-prefix */
export interface ITaggableObject {
  description: string;
  id: string;
  tags: Tag[];
  currentUserTags: Tag[];
}
