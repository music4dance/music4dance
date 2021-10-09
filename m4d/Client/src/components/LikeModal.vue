<template>
  <b-modal
    :id="id"
    header-bg-variant="primary"
    header-text-variant="light"
    size="sm"
    :ok-disabled="!changed"
    @show="resetModal"
    @ok="onSave"
  >
    <template v-slot:modal-title>
      {{ editor.song.title }}
    </template>
    <b-container fluid>
      <b-row class="mb-1"><b-col>Favorites/Blocked Lists:</b-col></b-row>
      <b-row>
        <b-col cols="3" align-self="center">
          <icon-button
            :state="like"
            trueIcon="heart-fill"
            falseIcon="heart"
            trueVariant="danger"
            falseVariant="secondary"
            :scale="2"
          ></icon-button>
        </b-col>
        <b-col>
          <b-button-group vertical class="ml-2">
            <b-button
              block
              variant="outline-primary"
              size="sm"
              :pressed="like === true"
              @click="setLike(true)"
              >{{ favoritesText }}</b-button
            >
            <b-button
              block
              variant="outline-primary"
              size="sm"
              :pressed="like === false"
              @click="setLike(false)"
              >{{ blockedText }}</b-button
            >
            <b-button
              block
              variant="outline-primary"
              size="sm"
              :pressed="like === null"
              @click="setLike(null)"
              >{{ removeText }}</b-button
            >
          </b-button-group>
        </b-col>
      </b-row>
      <b-row
        ><b-col><hr /></b-col
      ></b-row>
      <b-row
        ><b-col><div class="mb-2">Vote on Dancability by Style:</div></b-col>
      </b-row>
      <b-row
        ><b-col>
          <dance-vote-item
            v-for="rating in danceRatings"
            :key="rating.danceId"
            :rating="rating"
            :vote="getDanceVote(rating.danceId)"
            @dance-vote="setDanceVote($event)"
          >
          </dance-vote-item> </b-col
      ></b-row>
    </b-container>
  </b-modal>
</template>

<script lang="ts">
import DanceVoteItem from "@/components/DanceVoteItem.vue";
import IconButton from "@/components/IconButton.vue";
import { DanceRatingVote } from "@/DanceRatingDelta";
import AdminTools from "@/mix-ins/AdminTools";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceRating } from "@/model/DanceRating";
import { SongEditor } from "@/model/SongEditor";
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";

@Component({
  components: { IconButton, DanceVoteItem },
})
export default class LikeModal extends Mixins(EnvironmentManager, AdminTools) {
  @Prop() private readonly editor!: SongEditor;
  private instance: SongEditor | null = null;

  private get id(): string {
    return `like-${this.editor.songId}`;
  }

  private get like(): boolean | null {
    const like = this.instance?.likeState;
    return like === undefined ? null : like;
  }

  private get favoritesText(): string {
    return this.like === true ? "In your Favorites" : "Add to Favorites";
  }

  private get blockedText(): string {
    return this.like === false ? "In your Blocked List" : "Add to Blocked List";
  }

  private get removeText(): string {
    switch (this.like) {
      case true:
        return "Remove from Favorites";
      case false:
        return "Remove from Blocked";
      default:
        return "Not in either list";
    }
  }

  private get danceRatings(): DanceRating[] {
    const instance = this.instance;
    return instance ? instance.song.danceRatings ?? [] : [];
  }

  private getDanceVote(danceId: string): boolean | null {
    const vote = this.instance?.song.danceVote(danceId);
    return vote === undefined ? null : vote;
  }

  private setDanceVote(vote: DanceRatingVote): void {
    this.instance?.danceVote(vote);
  }

  private setLike(value: boolean | null): void {
    this.instance?.setLike(value);
  }

  private resetModal(): void {
    this.instance = new SongEditor(this.userName, this.editor.songHistory);
  }

  private forceNull(value?: boolean): boolean | null {
    return value === undefined ? null : value;
  }

  private get changed(): boolean {
    const editor = this.instance;
    return editor ? editor.modified : false;
  }

  private onSave(): void {
    if (this.changed) {
      this.editor.saveExternalChanges(this.instance!);
    }
  }
}
</script>
