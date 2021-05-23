<template>
  <b-badge
    :title="tag.value"
    :variant="variant"
    hef="#"
    style="margin-inline-end: 0.25em; margin-bottom: 0.25em"
    @click.ctrl="onCtrlClick"
    @click.exact="onClick"
    role="button"
  >
    <b-icon :icon="icon"></b-icon>
    {{ tag.value }}
    <b-icon-x-circle-fill
      v-if="isDelete"
      variant="danger"
    ></b-icon-x-circle-fill>
    <b-icon-plus-circle v-else></b-icon-plus-circle>
  </b-badge>
</template>

<script lang="ts">
import AdminTools from "@/mix-ins/AdminTools";
import { Component, Mixins, Prop } from "vue-property-decorator";
import TagButtonBase from "./TagButtonBase";

@Component
export default class TagButtonOther extends Mixins(TagButtonBase, AdminTools) {
  @Prop() private readonly isDelete!: boolean;

  private onClick(): void {
    if (this.isDelete) {
      this.handleDelete();
    } else {
      this.emitChange();
    }
  }

  private onCtrlClick(): void {
    this.emitChange();
  }

  private emitChange(): void {
    this.$emit("change-tag", this.tagHandler.tag);
  }

  private handleDelete(): void {
    // eslint-disable-next-line @typescript-eslint/no-this-alias
    const self = this;
    this.$bvModal
      .msgBoxConfirm(
        `Are you sure? Clicking yes will completely remove ${this.tagHandler.tag.value}.`,
        {
          title: "Please Confirm",
          size: "sm",
          buttonSize: "sm",
          okVariant: "danger",
          okTitle: "YES",
          cancelTitle: "NO",
          footerClass: "p-2",
          hideHeaderClose: false,
          centered: true,
        }
      )
      .then((value) => {
        if (value) {
          self.emitChange();
        }
      });
  }
}
</script>
