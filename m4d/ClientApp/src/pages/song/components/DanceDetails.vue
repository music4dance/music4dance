<script setup lang="ts">
import { DanceRating } from "@/models/DanceRating";
import { Song } from "@/models/Song";
import { SongEditor } from "@/models/SongEditor";
import { SongFilter } from "@/models/SongFilter";
import { computed } from "vue";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import type { NamedObject } from "@/models/DanceDatabase/NamedObject";
import { getMenuContext } from "@/helpers/GetMenuContext";
import type { TagHandler } from "@/models/TagHandler";
import { TagContext } from "@/models/Tag";

const danceDB = safeDanceDatabase();
const context = getMenuContext();

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
        <span v-if="displayTempo(dr)" class="ms-2 small">
          <template v-if="edit && canEditDanceTempo">
            <!-- Edit mode (canTag): labelled input, placeholder = inherited value -->
            <span :class="dr.tempo != null ? 'text-success' : 'text-muted'"
              >Tempo:<IBiLink45deg
                v-if="dr.tempo == null"
                class="ms-1 me-1"
                style="font-size: 0.75em; opacity: 0.6"
                title="Inheriting song tempo"
            /></span>
            <input
              type="number"
              class="form-control form-control-sm d-inline mx-1"
              style="width: 5em"
              :value="dr.tempo ?? ''"
              :placeholder="song.tempo?.toString() ?? '???'"
              :title="
                dr.tempo != null
                  ? 'Per-dance override — clear to inherit song tempo'
                  : 'Inheriting song tempo'
              "
              @blur="onTempoChange(dr, ($event.target as HTMLInputElement).value)"
              @keyup.enter="onTempoChange(dr, ($event.target as HTMLInputElement).value)"
            />
            BPM
            <BButton
              v-if="dr.tempo != null"
              type="button"
              variant="link"
              class="p-0 ms-1 align-baseline text-muted"
              title="Clear per-dance override (revert to song tempo)"
              @click="onTempoChange(dr, '')"
              ><IBiXCircle style="font-size: 0.85em"
            /></BButton>
          </template>
          <template v-else>
            <!-- View mode (all users): show effective tempo, style by inherited vs override -->
            <span
              :class="dr.tempo != null ? 'text-success' : 'text-muted'"
              :title="dr.tempo != null ? 'Per-dance tempo override' : 'Inherited from song tempo'"
              >{{ displayTempo(dr) }} BPM<IBiLink45deg
                v-if="dr.tempo == null"
                class="ms-1"
                style="font-size: 0.75em; opacity: 0.6"
            /></span>
          </template>
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
