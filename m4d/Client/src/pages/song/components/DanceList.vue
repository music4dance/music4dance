<template>
  <b-card
    :header="title"
    header-text-variant="primary"
    no-body
    border-variant="primary"
    ><b-list-group v-if="hasDances" flush
      ><b-list-group-item v-for="dr in danceRatingsFiltered" :key="dr.danceId">
        <b-button-close
          v-if="isAdmin && edit"
          text-variant="danger"
          @click="$emit('delete-dance', dr)"
          ><b-icon-x variant="danger"></b-icon-x
        ></b-button-close>
        <dance-vote
          :song="song"
          :danceRating="dr"
          :authenticated="!!user"
          v-on="$listeners"
        ></dance-vote>
        <a :href="danceLink(dr)"
          ><dance-name
            :dance="statsFromRating(dr)"
            :showSynonyms="true"
          ></dance-name
        ></a>
        <span v-if="dr.tags" style="margin-left: 0.25rem; line-height: 2.75rem">
          <tag-list-editor
            :container="dr"
            :filter="filter"
            :user="user"
            :editor="editor"
            :edit="edit"
            @update-song="$emit('update-song')"
            @edit="$emit('edit')"
          ></tag-list-editor>
        </span>
        <comment-editor
          :container="dr"
          :editor="editor"
          :edit="edit"
          :rows="3"
          placeholder="Please add a note on why you voted for/against dancing this dance to this song"
          v-on="$listeners"
        ></comment-editor> </b-list-group-item
    ></b-list-group>
  </b-card>
</template>

<script lang="ts">
import CommentEditor from "@/components/CommentEditor.vue";
import DanceName from "@/components/DanceName.vue";
import DanceVote from "@/components/DanceVote.vue";
import TagButton from "@/components/TagButton.vue";
import TagListEditor from "@/components/TagListEditor.vue";
import AdminTools from "@/mix-ins/AdminTools";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceRating } from "@/model/DanceRating";
import { DanceStats } from "@/model/DanceStats";
import { Song } from "@/model/Song";
import { SongEditor } from "@/model/SongEditor";
import { SongFilter } from "@/model/SongFilter";
import { Tag } from "@/model/Tag";
import { TagHandler } from "@/model/TagHandler";
import "reflect-metadata";
import { PropType } from "vue";
import mixins from "vue-typed-mixins";

export default mixins(EnvironmentManager, AdminTools).extend({
  components: { CommentEditor, DanceName, DanceVote, TagButton, TagListEditor },
  props: {
    song: { type: Object as PropType<Song>, required: true },
    title: { type: String, required: true },
    danceRatings: Array as PropType<DanceRating[]>,
    user: String,
    filter: Object as PropType<SongFilter>,
    editor: Object as PropType<SongEditor>,
    edit: Boolean,
  },
  computed: {
    danceRatingsFiltered(): DanceRating[] {
      const danceRatings = this.danceRatings;
      return danceRatings ? danceRatings.filter((dr) => dr) : [];
    },

    hasDances(): boolean {
      return this.danceRatingsFiltered.length > 0;
    },
  },
  methods: {
    statsFromRating(dr: DanceRating): DanceStats {
      return this.environment.fromId(dr.danceId)!;
    },

    danceName(dr: DanceRating): string {
      return this.statsFromRating(dr).name;
    },

    danceLink(dr: DanceRating): string {
      return `/dances/${this.statsFromRating(dr).seoName}`;
    },

    subTagHandler(dr: DanceRating, tag: Tag): TagHandler {
      return new TagHandler(tag, this.user, this.filter, dr);
    },
  },
});
</script>
