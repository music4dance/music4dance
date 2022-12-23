<template>
  <span>
    <dance-vote
      v-if="filter.singleDance"
      :song="editor.song"
      :danceRating="danceRating"
      :authenticated="!!userName"
      @dance-vote="onDanceVote(editor, $event)"
      style="margin-right: 0.25em"
    ></dance-vote>
    <song-like-button
      :song="song"
      :user="userName"
      @click-like="onClickLike()"
      :scale="1.75"
    ></song-like-button>
    <a href="#" @click.prevent="showPlayModal()" role="button" class="ml-1">
      <b-iconstack font-scale="1.75">
        <b-icon stacked icon="circle"></b-icon>
        <b-icon stacked icon="play-fill" shift-h="1"></b-icon>
      </b-iconstack>
    </a>
    <play-modal :song="song"></play-modal>
    <like-modal :id="likeId" :editor="editor"></like-modal>
  </span>
</template>

<script lang="ts">
import { DanceRatingVote } from "@/DanceRatingDelta";
import AdminTools from "@/mix-ins/AdminTools";
import { DanceRating } from "@/model/DanceRating";
import { Song } from "@/model/Song";
import { SongEditor } from "@/model/SongEditor";
import { SongFilter } from "@/model/SongFilter";
import { PropType } from "vue";
import DanceVote from "./DanceVote.vue";
import LikeModal from "./LikeModal.vue";
import PlayModal from "./PlayModal.vue";
import SongLikeButton from "./SongLikeButton.vue";

export default AdminTools.extend({
  components: { DanceVote, LikeModal, SongLikeButton, PlayModal },
  props: {
    editor: { type: Object as PropType<SongEditor>, required: true },
    filter: { type: Object as PropType<SongFilter>, required: true },
  },
  computed: {
    song(): Song {
      return this.editor.song;
    },
    danceRating(): DanceRating {
      return this.song.findDanceRatingById(
        this.filter.danceQuery.danceList[0]
      )!;
    },
    likeId(): string {
      return `like-${this.editor.songId}`;
    },
  },
  methods: {
    onDanceVote(editor: SongEditor, vote: DanceRatingVote): void {
      editor.danceVote(vote);
      editor.saveChanges();
    },
    onClickLike(): void {
      this.$bvModal.show(this.likeId);
    },
    showPlayModal(): void {
      this.$bvModal.show(this.song.songId);
    },
  },
});
</script>
