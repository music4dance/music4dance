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

function getTagLink(modifier: string, exclusive: boolean, includeDanceAll = false): string {
  // Start with appropriate base filter based on exclusive flag
  const baseFilter = exclusive
    ? new SongFilter() // For "List all", start with empty filter
    : handler.value.filter
      ? handler.value.filter.clone()
      : new SongFilter(); // For filtering, preserve existing

  // Set the action to filtersearch
  baseFilter.action = "filtersearch";

  // For dance-specific filtering, put the tag in the DanceQuery
  if (handler.value.danceId && !includeDanceAll) {
    // Create a TagQuery from parts instead of string manipulation
    const singleTagString = modifier + tag.value.key;
    const tagList = new TagList(singleTagString);
    const tagQuery = TagQuery.fromParts(tagList, false);

    // For filtering (non-exclusive), combine with existing dances using DanceQuery
    if (!exclusive && baseFilter.dances) {
      // Parse existing dance query
      const existingDanceQuery = new DanceQuery(baseFilter.dances);
      const existingDanceItems = existingDanceQuery.danceQueryItems;

      // Find existing entry for this dance (if any) to preserve its tags
      const existingDanceItem = existingDanceItems.find(
        (item) => item.id === handler.value.danceId,
      );

      // Create new dance item with the tag
      const newDanceItem = new DanceQueryItem({
        id: handler.value.danceId,
        threshold: existingDanceItem?.threshold ?? 1,
        tags: existingDanceItem?.tags
          ? existingDanceItem.tagQuery?.addTag(tag.value.key, modifier === "+")?.query
          : tagQuery.query,
      });

      // Filter out the old entry for this dance and add the new one
      const filteredDanceItems = existingDanceItems.filter(
        (item) => item.id !== handler.value.danceId,
      );
      const allDanceItems = [...filteredDanceItems, newDanceItem];

      // Convert back to strings and create new DanceQuery
      const allDanceStrings = allDanceItems.map((item) => item.toString());
      const combinedDanceQuery = DanceQuery.fromParts(allDanceStrings, true); // true = exclusive
      baseFilter.dances = combinedDanceQuery.query;
    } else {
      // For exclusive or no existing dances, create a DanceQuery properly
      const newDanceItem = new DanceQueryItem({
        id: handler.value.danceId,
        threshold: 1,
        tags: tagQuery.query,
      });
      const danceQuery = new DanceQuery(newDanceItem.toString());
      baseFilter.dances = danceQuery.query;
    }
  } else {
    // For global tags, create TagList and use fromParts with includeDancesAll
    const singleTagString = modifier + tag.value.key;
    const tagList = new TagList(singleTagString);
    const newTagQuery = TagQuery.fromParts(tagList, includeDanceAll);

    // For filtering (non-exclusive), combine with existing tags using TagQuery
    if (!exclusive && baseFilter.tags) {
      // Parse existing tag query and add the new tag to it
      const existingTagQuery = new TagQuery(baseFilter.tags);
      const updatedTagQuery = existingTagQuery.addTag(
        tag.value.key,
        modifier === "+",
        includeDanceAll,
      );
      baseFilter.tags = updatedTagQuery.query;
    } else {
      // For exclusive or no existing tags, just set the new tag
      baseFilter.tags = newTagQuery.query;
    }
  }

  // Generate the URL using the filter's built-in functionality
  return `/song/filtersearch?filter=${baseFilter.encodedQuery}`;
}

function getDanceAllTagLink(modifier: string, exclusive: boolean): string {
  return getTagLink(modifier, exclusive, true);
}

function getDanceSpecificTagLink(modifier: string, exclusive: boolean): string {
  return getTagLink(modifier, exclusive, false);
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
