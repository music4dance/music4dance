<template>
  <div>
    <div v-if="!hideFilter" class="row" style="margin-bottom: 0.5rem">
      <b-button-group size="sm" class="col-sm">
        <b-button
          v-for="btn in tagButtons"
          :key="btn.key"
          :pressed.sync="btn.state"
          :variant="btn.key"
        >
          <b-icon :icon="btn.iconName"></b-icon>
          {{ btn.description }}
          <b-icon icon="check-circle" v-if="btn.state"></b-icon>
          <b-icon icon="circle" v-else></b-icon>
        </b-button>
      </b-button-group>
      <div style="width: 250px" class="mx-auto col-sm">
        <b-form-input
          id="strictness"
          v-model="filter"
          type="range"
          min="2"
          max="10"
          step="1"
        ></b-form-input>
        <label
          for="strictness"
          class="d-flex justify-content-between"
          style="margin-bottom: 0; margin-top: -0.75rem"
        >
          <span>less</span><span class="ml-auto">more</span>
        </label>
      </div>
    </div>
    <div v-if="tagBuckets.length > 0">
      <b-badge
        v-for="tag in tagBuckets"
        :key="tag.key"
        :variant="tag.variant"
        style="margin-inline-end: 0.25em; margin-bottom: 0.25em"
        :class="classesForTag(tag)"
        href="#"
        @click="showModal(tag.key)"
        role="button"
      >
        <b-icon :icon="tag.icon"></b-icon>{{ tag.value }}
        <tag-modal :tagHandler="tagHandler(tag)"></tag-modal>
      </b-badge>
    </div>
    <div v-else>
      <h4>
        Please select one or more tag classes (style, tempo, musical genre,
        oother)
      </h4>
    </div>
  </div>
</template>

<script lang="ts">
import { SongFilter } from "@/model/SongFilter";
import { Tag, TagBucket, TagInfo } from "@/model/Tag";
import { TagHandler } from "@/model/TagHandler";
import "reflect-metadata";
import Vue, { PropType } from "vue";
import TagModal from "./TagModal.vue";

class TagButton implements TagInfo {
  public static get buttons() {
    return Tag.tagKeys
      .filter((t) => t !== "dance")
      .map((t) => new TagButton(t, Tag.TagInfo.get(t)!));
  }

  public key: string;
  public iconName: string;
  public description: string;
  public state: boolean;

  constructor(key: string, info: TagInfo) {
    this.key = key;
    this.iconName = info.iconName;
    this.description = info.description;
    this.state = true;
  }
}

export default Vue.extend({
  components: { TagModal },
  props: {
    tags: { type: Array as PropType<Tag[]>, required: true },
    hideFilter: Boolean,
    songFilter: { type: Object as PropType<SongFilter> },
    user: String,
  },
  data() {
    return new (class {
      tagButtons: TagButton[] = TagButton.buttons;
      filter = 10;
    })();
  },
  computed: {
    activeTags(): string[] {
      return this.tagButtons.filter((b) => b.state).map((b) => b.key);
    },
    tagBuckets(): TagBucket[] {
      const active = this.activeTags;
      const tags = this.tags.filter(
        (t) =>
          active.includes(t.category.toLowerCase()) && t.count && t.count > 0
      );
      let bucketized = TagBucket.bucketize(tags);
      if (this.filter < 10) {
        const threshold = 10 - this.filter;
        bucketized = TagBucket.bucketize(
          bucketized.filter((b) => b.bucket! > threshold)
        );
      }
      return bucketized.sort((a, b) => a.value.localeCompare(b.value));
    },
  },
  methods: {
    classesForTag(tag: TagBucket): string[] {
      return ["cloud-" + tag.bucket];
    },
    showModal(key: string): void {
      this.$bvModal.show(key);
    },
    tagHandler(tag: Tag): TagHandler {
      return new TagHandler(tag, this.user, this.songFilter, undefined, true);
    },
  },
});
</script>
