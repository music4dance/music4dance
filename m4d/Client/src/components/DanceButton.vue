<template>
  <b-button
    :title="tag.value"
    :variant="variant"
    size="sm"
    style="margin-inline-end: 0.25em; margin-bottom: 0.25em"
    @click="showModal()"
  >
    <b-icon :icon="icon"></b-icon>
    {{ tag.value }}
    <b-badge variant="light">{{ weight }}</b-badge>
    <b-icon-tags-fill
      v-if="hasTags"
      style="margin-left: 0.25em"
    ></b-icon-tags-fill>
    <dance-modal :danceHandler="danceHandler"></dance-modal>
  </b-button>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import DanceModal from "./DanceModal.vue";
import { Tag } from "@/model/Tag";
import { DanceHandler } from "@/model/DanceHandler";
import { DanceRating } from "@/model/Song";

@Component({
  components: {
    DanceModal,
  },
})
export default class DanceButton extends Vue {
  @Prop() private readonly danceHandler!: DanceHandler;

  private get variant(): string {
    return this.tag.category.toLowerCase();
  }

  private get tag(): Tag {
    return this.danceHandler.tag;
  }

  private get danceRating(): DanceRating {
    return this.danceHandler.danceRating;
  }

  private get icon(): string {
    const tagInfo = Tag.TagInfo.get(this.variant);

    if (tagInfo) {
      return tagInfo.iconName;
    }

    const message = `Couldn't find tagInfo for ${this.variant}`;
    // tslint:disable-next-line:no-console
    console.log(message);
    throw new Error(message);
  }

  private get weight(): number {
    return this.danceRating ? this.danceRating.weight : 0;
  }

  private get hasTags(): boolean {
    return this.danceRating?.tags?.length > 0;
  }

  private showModal(): void {
    this.$bvModal.show(this.danceHandler.id);
  }
}
</script>
