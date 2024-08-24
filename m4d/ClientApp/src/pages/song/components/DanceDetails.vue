<script setup lang="ts">
import { DanceRating } from "@/models/DanceRating";
import { Song } from "@/models/Song";
import { SongEditor } from "@/models/SongEditor";
import { SongFilter } from "@/models/SongFilter";
import { computed } from "vue";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import type { NamedObject } from "@/models/DanceDatabase/NamedObject";
import { MenuContext } from "@/models/MenuContext";
import type { TagHandler } from "@/models/TagHandler";

const danceDB = safeDanceDatabase();
const context = new MenuContext();

defineOptions({ inheritAttrs: false });

const props = defineProps<{
  song: Song;
  title: string;
  danceRatings: DanceRating[];
  filter: SongFilter;
  user?: string;
  editor?: SongEditor;
  edit?: boolean;
}>();

defineEmits<{
  "delete-dance": [dr: DanceRating];
  "tag-clicked": [tag: TagHandler];
  "update-song": [];
  edit: [];
}>();

const danceRatingsFiltered = computed(() => {
  return props.danceRatings.filter((dr) => dr).sort((a, b) => b.weight - a.weight);
});
const hasDances = computed(() => {
  return danceRatingsFiltered.value.length > 0;
});

const danceFromRating = (dr: DanceRating): NamedObject => {
  return danceDB.fromId(dr.danceId)!;
};

const danceLink = (dr: DanceRating): string => {
  return `/dances/${danceFromRating(dr).seoName}`;
};
</script>

<template>
  <BCard :header="title" header-text-variant="primary" no-body border-variant="primary"
    ><BListGroup v-if="hasDances" flush
      ><BListGroupItem v-for="dr in danceRatingsFiltered" :key="dr.danceId">
        <BCloseButton
          v-if="context.isAdmin && edit"
          text-variant="danger"
          @click="$emit('delete-dance', dr)"
          ><IBiX variant="danger"
        /></BCloseButton>
        <DanceVote
          :song="song"
          :vote="song.danceVote(dr.danceId)"
          :dance-rating="dr"
          :authenticated="!!user"
          v-bind="$attrs"
        ></DanceVote>
        <a :href="danceLink(dr)"
          ><DanceName :dance="danceFromRating(dr)" :show-synonyms="true"></DanceName
        ></a>
        <span v-if="dr.tags" style="margin-left: 0.25rem; line-height: 2.75rem">
          <TagListEditor
            :container="dr"
            :filter="filter"
            :user="user"
            :editor="editor"
            :edit="edit"
            @update-song="$emit('update-song')"
            @tag-clicked="$emit('tag-clicked', $event)"
            @edit="$emit('edit')"
          ></TagListEditor>
        </span>
        <CommentEditor
          :container="dr"
          :editor="editor"
          :edit="edit"
          :rows="3"
          placeholder="Please add a note on why you voted for/against dancing this dance to this song"
          v-bind="$attrs"
        ></CommentEditor> </BListGroupItem
    ></BListGroup>
  </BCard>
</template>
