<script setup lang="ts">
import { SongFilter } from "@/models/SongFilter";
import { Tag, TagBucket, type TagInfo } from "@/models/Tag";
import { TagHandler } from "@/models/TagHandler";
import { computed, reactive, ref } from "vue";
import { type ColorVariant } from "bootstrap-vue-next";

class TagButton implements TagInfo {
  public static get buttons() {
    return Tag.tagKeys
      .filter((t) => t !== "dance")
      .map((t) => new TagButton(t, Tag.tagInfo.get(t)!));
  }

  public key: string;
  public variant: ColorVariant;
  public iconName: string;
  public description: string;
  public state: boolean;

  constructor(key: string, info: TagInfo) {
    this.key = key;
    this.variant = key as ColorVariant;
    this.iconName = info.iconName;
    this.description = info.description;
    this.state = true;
  }
}

const props = defineProps<{
  tags: Tag[];
  hideFilter?: boolean;
  songFilter?: SongFilter;
  user?: string;
}>();

const modalVisible = ref(false);
const currentTag = ref<TagHandler>(new TagHandler({ tag: Tag.fromString("Placeholder:Other") }));
const tagButtons = reactive(TagButton.buttons);
const filter = ref(6);

// INT-TODO: Test for custom variants
// const topButtonVariant: ColorVariant = "music";
// console.log(topButtonVariant);

const activeTags = computed(() => {
  const tags = tagButtons.filter((b) => b.state).map((b) => b.key);
  return tags;
});

const tagBuckets = computed(() => {
  const active = activeTags;
  const tags = props.tags.filter(
    (t) => active.value.includes(t.category.toLowerCase()) && t.count && t.count > 0,
  );
  let bucketized = TagBucket.bucketize(tags);
  if (filter.value < 10) {
    const threshold = 10 - filter.value;
    bucketized = TagBucket.bucketize(bucketized.filter((b) => b.bucket! > threshold));
  }
  return bucketized.sort((a, b) => a.value.localeCompare(b.value));
});

function classesForTag(tag: TagBucket): string[] {
  return ["cloud-" + tag.bucket];
}

function showModal(key: string): void {
  currentTag.value = getTagHandler(Tag.fromString(key));
  modalVisible.value = true;
}

function getTagHandler(tag: Tag): TagHandler {
  return new TagHandler({ tag, user: props.user, filter: props.songFilter });
}
</script>

<template>
  <div>
    <div v-if="!hideFilter" class="row" style="margin-bottom: 0.5rem">
      <BButtonGroup size="sm" class="col-sm">
        <BButton
          v-for="btn in tagButtons"
          :id="'filter-' + btn.key"
          :key="btn.key"
          v-model:pressed="btn.state"
          :variant="btn.variant"
        >
          <TagIcon :name="btn.iconName" />
          {{ btn.description }}
          <IBiCheckCircle v-if="btn.state" />
          <IBiCircle v-else />
        </BButton>
      </BButtonGroup>
      <div style="width: 250px" class="mx-auto col-sm">
        <BFormInput id="strictness" v-model="filter" type="range" min="2" max="10" step="1" />
        <label
          for="strictness"
          class="d-flex justify-content-between"
          style="margin-bottom: 0; margin-top: -0.75rem"
        >
          <span>less</span><span class="ms-auto">more</span>
        </label>
      </div>
    </div>
    <div v-if="tagBuckets.length > 0">
      <BBadge
        v-for="tag in tagBuckets"
        :key="tag.key"
        :variant="tag.variant"
        style="margin-inline-end: 0.25em; margin-bottom: 0.25em"
        :class="classesForTag(tag)"
        href="#"
        role="button"
        @click="showModal(tag.key)"
      >
        <TagIcon :name="tag.icon!" />{{ tag.value }}
      </BBadge>
    </div>
    <div v-else>
      <h4>Please select one or more tag classes (style, tempo, musical genre, oother)</h4>
    </div>
    <TagModal v-model="modalVisible" :tag-handler="currentTag as TagHandler" />
  </div>
</template>

<style scoped>
.cloud-0 {
  font-size: 80%;
}

.cloud-1 {
  font-size: 90%;
}

.cloud-2 {
  font-size: 100%;
}

.cloud-3 {
  font-size: 110%;
}

.cloud-4 {
  font-size: 120%;
}

.cloud-5 {
  font-size: 130%;
}

.cloud-6 {
  font-size: 140%;
}

.cloud-7 {
  font-size: 150%;
}

.cloud-8 {
  font-size: 160%;
}

.cloud-9 {
  font-size: 170%;
}
</style>
