import { Tag } from "./Tag";

export interface DanceStats {
  id: string;
  name: string;
  description?: string;
  blogTag?: string;
  seoName: string;
  songCount: number;
  maxWeight: number;
  songTags: string;
  isGroup: boolean;
  tags: Tag[];
}
