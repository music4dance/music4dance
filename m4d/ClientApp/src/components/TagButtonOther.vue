<script setup lang="ts">
import { useTagButton } from "@/composables/useTagButton";
import type { TagHandler } from "@/models/TagHandler";
import { useModalController, type BaseColorVariant } from "bootstrap-vue-next";

const props = defineProps<{
  tagHandler: TagHandler;
  isDelete?: boolean;
}>();

const emit = defineEmits<{
  "change-tag": [tag: TagHandler];
}>();

const { icon, tag, variant } = useTagButton(props.tagHandler);

const emitChange = () => {
  emit("change-tag", props.tagHandler);
};

const { create } = useModalController();

const handleDelete = async () => {
  if (
    await create({
      title: "Please Confirm",
      body: `Are you sure? Clicking yes will completely remove ${props.tagHandler.tag.value}.`,
      size: "sm",
      buttonSize: "sm",
      okVariant: "danger",
      okTitle: "YES",
      cancelTitle: "NO",
    }).show()
  ) {
    emitChange();
  }
};

const onClick = () => {
  if (props.isDelete) {
    handleDelete();
  } else {
    emitChange();
  }
};

const onCtrlClick = () => {
  emitChange();
};
</script>

<template>
  <BBadge
    :title="tag.value"
    :variant="variant as keyof BaseColorVariant"
    hef="#"
    style="margin-inline-end: 0.25em; margin-bottom: 0.25em"
    role="button"
    @click.ctrl="onCtrlClick"
    @click.exact="onClick"
  >
    <TagIcon :name="icon" />
    {{ tag.value }}
    <IBiXCircleFill v-if="isDelete" variant="danger" />
    <IBiPlusCircle v-else />
  </BBadge>
</template>
