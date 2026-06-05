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
import { TagContext } from "@/models/Tag";

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

const emit = defineEmits<{
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

const filterFamilyTag = computed(() => props.filter.familyTag);

const canEditDanceTempo = computed(() => !!props.user && context.hasRole("canTag"));

/** Display value for per-dance tempo: override if set, else song-level tempo. */
const displayTempo = (dr: DanceRating): string => (dr.tempo ?? props.song.tempo)?.toString() ?? "";

const onTempoChange = (dr: DanceRating, value: string): void => {
  if (!props.editor) return;
  props.editor.setDanceTempo(dr.danceId, value || undefined);
  emit("edit");
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
          :vote="song.danceVote(dr.danceId)"
          :dance-rating="dr"
          :authenticated="!!user"
          :filter-family-tag="filterFamilyTag"
          v-bind="$attrs"
        />
        <a :href="danceLink(dr)"
          ><DanceName :dance="danceFromRating(dr)" :show-synonyms="true"
        /></a>
        <span v-if="canEditDanceTempo" class="ms-2 small text-muted">
          <template v-if="edit">
            Tempo:
            <input
              type="number"
              class="form-control form-control-sm d-inline"
              style="width: 5em"
              :value="displayTempo(dr)"
              :placeholder="song.tempo?.toString() ?? ''"
              @blur="onTempoChange(dr, ($event.target as HTMLInputElement).value)"
              @keyup.enter="onTempoChange(dr, ($event.target as HTMLInputElement).value)"
            />
            BPM
            <span
              v-if="dr.tempo && dr.tempo !== song.tempo"
              class="text-warning ms-1"
              title="Per-dance override active"
              ><IBiExclamationCircle
            /></span>
          </template>
          <template v-else-if="dr.tempo && dr.tempo !== song.tempo"> {{ dr.tempo }} BPM </template>
        </span>
        <span v-if="dr.tags" style="margin-left: 0.25rem; line-height: 2.75rem">
          <TagListEditor
            :container="dr"
            :filter="filter"
            :user="user"
            :editor="editor"
            :edit="edit"
            :context="TagContext.Dance"
            @update-song="$emit('update-song')"
            @tag-clicked="$emit('tag-clicked', $event)"
            @edit="$emit('edit')"
          />
        </span>
        <CommentEditor
          :container="dr"
          :editor="editor"
          :edit="edit"
          :rows="3"
          placeholder="Please add a note on why you voted for/against dancing this dance to this song"
          v-bind="$attrs"
        /> </BListGroupItem
    ></BListGroup>
  </BCard>
</template>
