<script setup lang="ts">
import { DanceHandler } from "@/models/DanceHandler";
import { Tag } from "@/models/Tag";
import { TagHandler } from "@/models/TagHandler";
import { computed, ref } from "vue";
import type { DanceRatingVote } from "@/models/DanceRatingDelta";
import type { DanceRating } from "@/models/DanceRating";
import type { Song } from "@/models/Song";
import { SongFilter } from "@/models/SongFilter";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";

const props = defineProps<{
  danceHandler: DanceHandler;
}>();

const instance = ref<DanceHandler>(props.danceHandler);

const emit = defineEmits<{
  "dance-vote": [vote: DanceRatingVote];
  "tag-clicked": [tag: TagHandler];
}>();

const modelValue = defineModel<boolean>({ default: false });

const rating = computed(() => instance.value.danceRating as DanceRating);
const song = computed(() => instance.value.parent as Song);
const pageLink = computed(() => `/dances/${name.value}`);
const includeOnly = computed(() => `/song/search/?dances=${rating.value.danceId}`);
const includeDance = computed(
  () => `${includeOnly.value}&filter=${props.danceHandler.filter!.encodedQuery}`,
);
const name = computed(() => rating.value.dance.name ?? "");

const maxWeight = computed(() => safeDanceDatabase().getMaxWeight(rating.value.id));
const tags = computed(() => rating.value.tags);
const tag = computed(() => props.danceHandler.tag);
const title = computed(() => {
  const parent = props.danceHandler.parent;
  return parent ? parent.description : tag.value.value;
});
const authenticated = computed(() => !!props.danceHandler.user);
const filter = computed(() => props.danceHandler.filter);
const filterStyleTag = computed(() => filter.value?.styleTag);
const hasFilter = computed(() => {
  const filter = props.danceHandler.filter;
  return !!filter && !filter.isDefault(rating.value.id);
});
const isFiltered = computed(() => {
  const filter = (props.danceHandler as DanceHandler).filter;
  const id = rating.value.id;
  return !!filter && !!filter.danceQuery.danceList.find((d) => d === id);
});
const subTagHandler = (tag: Tag): TagHandler => {
  const handler = props.danceHandler;
  return new TagHandler({
    tag,
    user: handler.user,
    filter: handler.filter,
    parent: handler.danceRating,
  });
};
const resetModal = (): void => {
  instance.value = props.danceHandler.clone();
};

const onDanceVote = (vote: DanceRatingVote): void => {
  const editor = instance.value.editor!.clone();
  editor.danceVote(vote);
  if (editor.song.findDanceRatingById(vote.danceId)) {
    instance.value = new DanceHandler({
      danceRating: editor.song.findDanceRatingById(vote.danceId)!,
      tag: tag.value,
      user: instance.value.user,
      filter: (instance.value?.filter ?? new SongFilter()) as SongFilter,
      parent: editor.song,
      editor,
    });
    instance.value.parent = editor.song;
    instance.value.danceRating = editor.song.findDanceRatingById(vote.danceId)!;
  } else {
    modelValue.value = false;
  }
  emit("dance-vote", vote);
};

const onTagClicked = (tag: TagHandler): void => {
  emit("tag-clicked", tag);
};
</script>

<template>
  <BModal
    :id="danceHandler.id"
    v-model="modelValue"
    :title="title"
    :header-bg-variant="tag.variant"
    header-text-variant="light"
    no-footer
    @show="resetModal"
  >
    <template #modal-title> <TagIcon :name="tag.icon!" />&nbsp;{{ title }} </template>
    <BListGroup>
      <BListGroupItem :href="pageLink" variant="secondary"> {{ name }} page... </BListGroupItem>
      <BListGroupItem v-if="hasFilter && !isFiltered" :href="includeDance" variant="warning">
        Filter the list to include only songs tagged as <em>{{ name }}</em>
      </BListGroupItem>
      <BListGroupItem :href="includeOnly" variant="success">
        List all {{ name }} songs.
      </BListGroupItem>
      <BListGroupItem href="https://music4dance.blog/tag-filtering" variant="info" target="_blank"
        >Help</BListGroupItem
      >
    </BListGroup>
    <div v-if="tags" style="margin-top: 0.5em">
      <TagButton
        v-for="t in tags"
        :key="t.key"
        :tag-handler="subTagHandler(t)"
        @tag-clicked="onTagClicked"
      />
    </div>
    <div v-if="danceHandler.parent">
      <DanceVote
        :vote="song.danceVote(rating.danceId)"
        :dance-rating="rating"
        :authenticated="authenticated"
        :filter-style-tag="filterStyleTag"
        @dance-vote="onDanceVote"
      />
      <span style="padding-inline-start: 1em"
        >I enjoy dancing <b>{{ name }}</b> to {{ title }}.</span
      >
      <div>The top song in the {{ name }} category has {{ maxWeight }} votes.</div>
    </div>
  </BModal>
</template>
