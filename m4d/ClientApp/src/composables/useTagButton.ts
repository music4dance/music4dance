import { TagHandler } from "@/models/TagHandler";
import { Tag } from "@/models/Tag";
import type { BaseButtonVariant } from "bootstrap-vue-next";

export function useTagButton(handler: TagHandler) {
  const tag = handler.tag;
  // INT-TODO: Is there a cleaner way to do this?
  const v = tag.category.toLocaleLowerCase();
  const tagInfo = Tag.tagInfo.get(v);
  if (!tagInfo) {
    throw new Error(`Couldn't find tagInfo for ${v}`);
  }
  const icon = tagInfo.iconName;
  const selectedIcon = handler.user && handler.isSelected ? "i-bi-check-circle" : undefined;

  const showModal = () => {
    console.log(`useTagButton: show modal for ${handler.id}`);
  };

  const variant = v as unknown as keyof BaseButtonVariant;
  return { icon, tag, variant, selectedIcon, showModal };
}
