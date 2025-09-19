<script setup lang="ts">
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { TagHandler } from "@/models/TagHandler";
import { SongFilter } from "@/models/SongFilter";
import { DanceQuery } from "@/models/DanceQuery";
import { DanceQueryItem } from "@/models/DanceQueryItem";
import { TagQuery } from "@/models/TagQuery";
import { TagList } from "@/models/TagList";
import { computed } from "vue";

const props = defineProps<{
  tagHandler: TagHandler;
}>();

const modelValue = defineModel<boolean>({ default: false });

const handler = computed(() => props.tagHandler);

const tag = computed(() => handler.value.tag);

const title = computed(() => {
  const parent = handler.value.parent;
  return parent ? parent.description : tag.value.value;
});

const hasFilter = computed(() => {
  const filter = handler.value.filter;
  return !!filter && !filter.isDefault(handler.value.user) && !filter.isRaw;
});

const danceName = computed(() => {
  // First try to get from danceId if it's a dance-specific context
  if (handler.value.danceId) {
    return safeDanceDatabase().danceFromId(handler.value.danceId)?.name;
  }

  // Then try to get from filter's danceQuery
  const danceNames = handler.value.filter?.danceQuery.danceNames;
  if (danceNames && danceNames.length > 0) {
    return danceNames[0];
  }

  // Return empty string as fallback - this will prevent showing dance-specific options
  return "";
});

const isDanceSpecific = computed(() => {
  // Only show dance-specific options if we have a danceId context AND this is actually a dance tag
  // Don't show dance-specific options for regular song tags just because we're filtered to a single dance
  return !!handler.value.danceId && !!danceName.value;
});

function getTagLink(modifier: string, clear: boolean): string {
  const baseFilter =
    clear || !handler.value.filter ? new SongFilter() : handler.value.filter.clone();
  baseFilter.action = "filtersearch";

  if (handler.value.danceId) {
    // For dance-specific filtering, put the tag in the DanceQuery
    const singleTagString = modifier + tag.value.key;
    const tagList = new TagList(singleTagString);
    const tagQuery = TagQuery.fromParts(tagList, false);

    if (baseFilter.dances) {
      const existingDanceQuery = new DanceQuery(baseFilter.dances);
      const existingDanceItems = existingDanceQuery.danceQueryItems;
      const existingDanceItem = existingDanceItems.find(
        (item) => item.id === handler.value.danceId,
      );
      const newDanceItem = new DanceQueryItem({
        id: handler.value.danceId,
        threshold: existingDanceItem?.threshold ?? 1,
        tags: existingDanceItem?.tags
          ? existingDanceItem.tagQuery?.addTag(tag.value.key, modifier === "+")?.query
          : tagQuery.query,
      });
      const filteredDanceItems = existingDanceItems.filter(
        (item) => item.id !== handler.value.danceId,
      );
      const allDanceItems = [...filteredDanceItems, newDanceItem];
      const allDanceStrings = allDanceItems.map((item) => item.toString());
      const combinedDanceQuery = DanceQuery.fromParts(allDanceStrings, true);
      baseFilter.dances = combinedDanceQuery.query;
    } else {
      const newDanceItem = new DanceQueryItem({
        id: handler.value.danceId,
        threshold: 1,
        tags: tagQuery.query,
      });
      const danceQuery = new DanceQuery(newDanceItem.toString());
      baseFilter.dances = danceQuery.query;
    }
  } else {
    // For global tags, create TagList and use fromParts
    const singleTagString = modifier + tag.value.key;
    const tagList = new TagList(singleTagString);

    if (baseFilter.tags) {
      // Parse existing tag query and add the new tag to it
      const existingTagQuery = new TagQuery(baseFilter.tags);
      const updatedTagQuery = existingTagQuery.addTag(tag.value.key, modifier === "+");
      baseFilter.tags = updatedTagQuery.query;
    } else {
      baseFilter.tags = TagQuery.fromParts(tagList).query;
    }
  }
  return `/song/filtersearch?filter=${baseFilter.encodedQuery}`;
}

function getDanceAllTagLink(modifier: string, clear: boolean): string {
  return getTagLink(modifier, clear);
}

function getDanceSpecificTagLink(modifier: string, clear: boolean): string {
  return getTagLink(modifier, clear);
}
</script>

<template>
  <BModal
    :id="handler.id"
    v-model="modelValue"
    :header-bg-variant="tag.variant"
    header-text-variant="light"
    no-footer
  >
    <template #title><TagIcon :name="tag.icon!" />&nbsp;{{ title }} </template>
    <!-- Single unified options list -->
    <BListGroup class="mb-2">
      <!-- Dance-specific options (only show if we have a specific dance and valid dance name) -->
      <template v-if="isDanceSpecific && danceName">
        <!-- Filter options (only show if there's an existing filter) -->
        <template v-if="hasFilter">
          <BListGroupItem :href="getDanceSpecificTagLink('+', false)" variant="success">
            Filter current list to {{ danceName }} songs tagged as <em>{{ tag.value }}</em>
          </BListGroupItem>
          <BListGroupItem :href="getDanceSpecificTagLink('-', false)" variant="danger">
            Filter current list to {{ danceName }} songs <b>not</b> tagged as
            <em>{{ tag.value }}</em>
          </BListGroupItem>
        </template>

        <!-- List all options (always show for dance-specific) -->
        <BListGroupItem :href="getDanceSpecificTagLink('+', true)" variant="success">
          List all {{ danceName }} songs tagged as <em>{{ tag.value }}</em>
        </BListGroupItem>
        <BListGroupItem :href="getDanceSpecificTagLink('-', true)" variant="danger">
          List all {{ danceName }} songs <b>not</b> tagged as <em>{{ tag.value }}</em>
        </BListGroupItem>
      </template>

      <!-- Global options (always show) -->
      <!-- Filter options (only show if there's an existing filter) -->
      <template v-if="hasFilter">
        <BListGroupItem :href="getDanceAllTagLink('+', false)" variant="success">
          Filter current list to songs tagged as <em>{{ tag.value }}</em>
        </BListGroupItem>
        <BListGroupItem :href="getDanceAllTagLink('-', false)" variant="danger">
          Filter current list to songs <b>not</b> tagged as <em>{{ tag.value }}</em>
        </BListGroupItem>
      </template>

      <!-- List all options (always show for global) -->
      <BListGroupItem :href="getDanceAllTagLink('+', true)" variant="success">
        List all songs tagged as <em>{{ tag.value }}</em>
      </BListGroupItem>
      <BListGroupItem :href="getDanceAllTagLink('-', true)" variant="danger">
        List all songs <b>not</b> tagged as <em>{{ tag.value }}</em>
      </BListGroupItem>
    </BListGroup>

    <!-- Help Section -->
    <BListGroup>
      <BListGroupItem href="https://music4dance.blog/tag-filtering" variant="info" target="_blank">
        Help
      </BListGroupItem>
    </BListGroup>
  </BModal>
</template>
