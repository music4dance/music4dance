<template>
  <div>
    <b-table
      striped
      hover
      no-local-sorting
      sort-icon-left
      borderless
      :items="songs"
      :fields="fields"
    >
      <template v-slot:cell(edit)="data">
        <b-form-checkbox
          @change="onSelect(data.item.song, $event)"
        ></b-form-checkbox>
        <a :href="editRef(data.item.song)"><b-icon-pencil></b-icon-pencil></a>
      </template>
      <template v-slot:head(play)>
        <div :class="likeHeader">Like/Play</div>
      </template>
      <template v-slot:cell(play)="data">
        <dance-vote
          v-if="filter.singleDance"
          :song="data.item.song"
          :danceRating="getDanceRating(data.item.song)"
          :authenticated="!!userName"
          @dance-vote="onDanceVote(data.item, $event)"
          style="margin-right: 0.25em"
        ></dance-vote>
        <song-like-button
          :song="data.item.song"
          :user="userName"
          @click-like="onClickLike(data.item)"
          :scale="1.75"
        ></song-like-button>
        &nbsp;
        <a
          href="#"
          @click.prevent="showPlayModal(data.item.song)"
          role="button"
        >
          <b-iconstack font-scale="1.75">
            <b-icon stacked icon="circle"></b-icon>
            <b-icon stacked icon="play-fill" shift-h="1"></b-icon>
          </b-iconstack>
        </a>
        <play-modal :song="data.item.song"></play-modal>
      </template>
      <template v-slot:head(title)>
        <sortable-header
          id="Title"
          :tip="titleHeaderTip"
          :enableSort="!hideSort"
          :filter="filter"
        ></sortable-header>
      </template>
      <template v-slot:cell(title)="data">
        <a :href="songRef(data.item.song)">{{ data.item.song.title }}</a>
      </template>
      <template v-slot:head(artist)>
        <sortable-header
          id="Artist"
          :tip="titleHeaderTip"
          :enableSort="!hideSort"
          :filter="filter"
        ></sortable-header>
      </template>
      <template v-slot:cell(artist)="data">
        <a :href="artistRef(data.item.song)">{{ data.item.song.artist }}</a>
      </template>
      <template v-slot:cell(track)="data">
        {{ trackNumber(data.item.song) }}
      </template>
      <template v-slot:head(tempo)>
        <sortable-header
          id="Tempo"
          title="Tempo (BPM)"
          :tip="titleHeaderTip"
          :enableSort="!hideSort"
          :filter="filter"
        ></sortable-header>
      </template>
      <template v-slot:cell(tempo)="data">
        <a :href="tempoRef(data.item.song)">{{ tempoValue(data.item.song) }}</a>
      </template>
      <template v-slot:head(echo)>
        <div :class="echoClass">
          <sortable-header
            id="Beat"
            :tip="beatTip"
            :enableSort="!hideSort"
            :filter="filter"
          >
            <img src="/images/icons/beat-10.png" width="25" height="25" />
          </sortable-header>
          <sortable-header
            id="Energy"
            :tip="energyTip"
            :enableSort="!hideSort"
            :filter="filter"
          >
            <img src="/images/icons/energy-10.png" width="25" height="25" />
          </sortable-header>
          <sortable-header
            id="Mood"
            :tip="moodTip"
            :enableSort="!hideSort"
            :filter="filter"
          >
            <img src="/images/icons/mood-10.png" width="25" height="25" />
          </sortable-header>
        </div>
      </template>
      <template v-slot:cell(echo)="data" style="width: 100px">
        <echo-icon
          :value="data.item.song.danceability"
          type="beat"
          label="beat strength"
          maxLabel="strongest beat"
        ></echo-icon>
        <echo-icon
          :value="data.item.song.energy"
          type="energy"
          label="energy level"
          maxLabel="highest energy"
        ></echo-icon>
        <echo-icon
          :value="data.item.song.valence"
          type="mood"
          label="mood level"
          maxLabel="happiest"
        ></echo-icon>
      </template>
      <template v-slot:head(dances)>
        <sortable-header
          id="Dances"
          :tip="titleHeaderTip"
          :enableSort="sortableDances"
          :filter="filter"
        ></sortable-header>
      </template>
      <template v-slot:cell(dances)="data">
        <dance-button
          v-for="tag in dances(data.item.song)"
          :key="tag.key"
          :tagHandler="danceHandler(tag, filter, data.item.song)"
          @dance-vote="onDanceVote(data.item, $event)"
        ></dance-button>
      </template>
      <template v-slot:cell(tags)="data">
        <tag-button
          v-for="tag in tags(data.item.song)"
          :key="tag.key"
          :tagHandler="tagHandler(tag, filter, data.item.song)"
        ></tag-button>
      </template>
      <template v-slot:head(order)>
        <div class="orderHeader">
          <sortable-header
            :id="orderType"
            :tip="orderHeaderTip"
            :enableSort="!hideSort"
            :filter="filter"
          >
            <b-icon :icon="orderIcon"></b-icon>
          </sortable-header>
        </div>
      </template>
      <template v-slot:cell(order)="data">
        <span v-b-tooltip.hover.click.blur.topleft="orderTip(data.item.song)">{{
          data.item.song.modifiedOrder
        }}</span>
      </template>

      <template v-slot:head(text)>
        <sortable-header
          id="Title"
          :tip="titleHeaderTip"
          :enableSort="!hideSort"
          :filter="filter"
        ></sortable-header>
        -
        <sortable-header
          id="Artist"
          :tip="titleHeaderTip"
          :enableSort="!hideSort"
          :filter="filter"
        ></sortable-header>
      </template>
      <template v-slot:cell(text)="data">
        <a :href="songRef(data.item.song)">{{ data.item.song.title }}</a> by
        <a :href="artistRef(data.item.song)">{{ data.item.song.artist }}</a>
        <span v-if="tempoValue(data.item.song)">
          @
          <a :href="tempoRef(data.item.song)"
            >{{ tempoValue(data.item.song) }} BPM</a
          >
        </span>
      </template>
      <template v-slot:head(info)>
        <sortable-header
          id="Dances"
          :tip="titleHeaderTip"
          :enableSort="sortableDances"
          :filter="filter"
        ></sortable-header>
        - Tags
      </template>
      <template v-slot:cell(info)="data">
        <dance-button
          v-for="tag in dances(data.item.song)"
          :key="tag.key"
          :tagHandler="danceHandler(tag, filter, data.item.song)"
          @dance-vote="onDanceVote(data.item, $event)"
        ></dance-button>
        <tag-button
          v-for="tag in tags(data.item.song)"
          :key="tag.key"
          :tagHandler="tagHandler(tag, filter, data.item.song)"
        ></tag-button>
      </template>
    </b-table>
  </div>
</template>

<script lang="ts">
import "reflect-metadata";
import { BvTableFieldArray } from "bootstrap-vue";
import DanceButton from "./DanceButton.vue";
import DanceVote from "@/components/DanceVote.vue";
import EchoIcon from "./EchoIcon.vue";
import SongLikeButton from "@/components/SongLikeButton.vue";
import PlayModal from "./PlayModal.vue";
import SortableHeader from "./SortableHeader.vue";
import TagButton from "./TagButton.vue";
import { Component, Prop, Mixins } from "vue-property-decorator";
import { DanceRating } from "@/model/DanceRating";
import { Song } from "@/model/Song";
import { Tag } from "@/model/Tag";
import { SongFilter } from "@/model/SongFilter";
import { DanceHandler } from "@/model/DanceHandler";
import { TagHandler } from "@/model/TagHandler";
import { TaggableObject } from "@/model/TaggableObject";
import { SongSort, SortOrder } from "@/model/SongSort";
import AdminTools from "@/mix-ins/AdminTools";
import { SongHistory } from "@/model/SongHistory";
import { SongEditor } from "@/model/SongEditor";
import { DanceRatingVote } from "@/DanceRatingDelta";

// TODO:
//  Look at mixin's again - particularly with respect to danceId->dance translations
//  Go through and audit no-explicit-any, interface-name warning
//  Get tag cloud at bottom of dance page to include add to filter + filter on

interface Field {
  key: string;
  label?: string;
}

const editField = { key: "edit", label: "" };
const playField = { key: "play" };
const titleField = { key: "title" };
const artistField = { key: "artist" };
const trackField = { key: "track" };
const tempoField = { key: "tempo", label: "Tempo (BPM)" };
const echoField = { key: "echo" };
const dancesField = { key: "dances" };
const tagsField = { key: "tags" };
const orderField = { key: "order" };
const textField = { key: "text" };
const infoField = { key: "info" };

@Component({
  components: {
    DanceButton,
    DanceVote,
    EchoIcon,
    PlayModal,
    SongLikeButton,
    SortableHeader,
    TagButton,
  },
})
export default class SongTable extends Mixins(AdminTools) {
  @Prop() private readonly histories!: SongHistory[];
  @Prop() private readonly filter!: SongFilter;
  @Prop() private readonly hideSort?: boolean;
  @Prop() private readonly hiddenColumns?: string[];

  private get songs(): SongEditor[] {
    return this.histories.map((h) => new SongEditor(this.userName, h));
  }

  private get songMap(): Map<string, SongEditor> {
    return new Map(this.songs.map((s) => [s.song.id, s]));
  }

  private get fields(): BvTableFieldArray {
    /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
    const mq = (this as any).$mq;

    const fields = [
      playField,
      titleField,
      artistField,
      trackField,
      tempoField,
      echoField,
      dancesField,
      tagsField,
      orderField,
    ];

    const smallFields = [playField, textField, infoField];

    const hidden = this.hiddenColumns;
    if (mq === "sm" || mq === "md") {
      return smallFields.map((f) => this.filterSmallField(f));
    } else {
      const temp = hidden
        ? fields.filter((f) => !this.isHidden(f.key))
        : fields;
      if (this.isAdmin) {
        return [editField, ...temp];
      } else {
        return temp;
      }
    }
  }

  private filterSmallField(field: Field): Field {
    if (field === textField && this.isHidden("artist")) {
      return titleField;
    } else if (field === infoField && this.isHidden("dances")) {
      return tagsField;
    } else {
      return field;
    }
  }

  private get likeHeader(): string[] {
    return this.filter.singleDance ? ["likeDanceHeader"] : ["likeHeader"];
  }

  private get titleHeaderTip(): string {
    return "Song Title: Click to sort alphabetically by title";
  }

  private songRef(song: Song): string {
    return `/song/details/${song.songId}?filter=${this.filter.encodedQuery}`;
  }

  private get artistHeaderTip(): string {
    return "Artist: Click to sort alphabetically by artist";
  }

  private artistRef(song: Song): string {
    return `/song/artist/?name=${song.artist}`;
  }

  private trackNumber(song: Song): string {
    return song.albums && song.albums.length > 0 && song.albums[0].track
      ? song.albums[0].track.toString()
      : "";
  }

  private get tempoHeaderTip(): string {
    return "Tempo (Beats Per Minute): Click to sort numerically by tempo";
  }

  private tempoRef(song: Song): string {
    return `/home/counter?numerator=4&tempo=${song.tempo}`; // TODO: smart numerator?
  }

  private tempoString(song: Song): string {
    const tempo = song.tempo;
    return tempo ? `@ ${Math.round(tempo)} BPM` : "";
  }

  private tempoValue(song: Song): string {
    const tempo = song.tempo;
    return tempo && !this.isHidden("tempo") ? `${Math.round(tempo)}` : "";
  }

  private isHidden(column: string): boolean {
    const hidden = this.hiddenColumns;
    const col = column.toLowerCase();
    return !!hidden && !!hidden.find((c) => c.toLowerCase() === col);
  }

  private get beatTip(): string {
    return "Strength of the beat (fuller icons represent a stronger beat). Click to sort by strength of the beat.";
  }

  private get energyTip(): string {
    return "Energy of the song (fuller icons represent a higher energy). Click to sort by energy.";
  }

  private get moodTip(): string {
    return "Mood of the song (fuller icons represent a happier mood). Click to sort by mood.";
  }

  private get echoClass(): string[] {
    const order = this.filter.sort.order;
    return order === "Mood" || order === "Beat" || order === "Energy"
      ? ["sortedEchoHeader"]
      : ["echoHeader"];
  }

  private echoLink(type: string): string {
    return `https://music4dance.blog/music4dance-help/playing-or-purchasing-songs/echonest/#${type}`;
  }

  private get dancesHeaderTip(): string {
    return "Dance: Click to sort by dance rating";
  }

  private dances(song: Song): Tag[] {
    return this.danceTags(song).filter((t) =>
      song.findDanceRatingByName(t.value)
    );
  }

  private danceTags(song: Song): Tag[] {
    return song.tags.filter(
      (t) =>
        !t.value.startsWith("!") &&
        !t.value.startsWith("-") &&
        t.category.toLowerCase() === "dance"
    );
  }

  private tags(song: Song): Tag[] {
    return song.tags.filter(
      (t) => !t.value.startsWith("!") && t.category.toLowerCase() !== "dance"
    );
  }

  private orderTip(song: Song): string {
    return `Last Modified ${song.modified} (${song.modifiedOrderVerbose} ago)`;
  }

  private get orderHeaderTip(): string {
    return `Click to sort by date ${this.orderType.toLowerCase()}`;
  }

  private get orderType(): string {
    return this.sortOrder.order ?? SortOrder.Created;
  }

  private get orderIcon(): string {
    switch (this.sortOrder.order) {
      case SortOrder.Created:
        return "file-earmark-plus";
      case SortOrder.Modified:
        return "pencil";
      case SortOrder.Edited:
        return "pencil-fill";
      default:
        return "asterisk";
    }
  }

  private danceHandler(tag: Tag, filter: SongFilter, song: Song): DanceHandler {
    const danceRating = song.findDanceRatingByName(tag.value);
    return new DanceHandler(danceRating!, tag, this.userName, filter, song);
  }

  private tagHandler(
    tag: Tag,
    filter?: SongFilter,
    parent?: TaggableObject
  ): TagHandler {
    return new TagHandler(tag, this.userName, filter, parent);
  }

  private get sortOrder(): SongSort {
    return this?.filter?.sort ?? new SongSort("Modified");
  }

  private get sortableDances(): boolean {
    return !this.hideSort && this.filter.singleDance;
  }

  private getDanceRating(song: Song): DanceRating {
    return song.findDanceRatingById(this.filter.danceQuery.danceList[0])!;
  }

  private editRef(song: Song): string {
    return `/song/edit?id=${song.songId}&filter=${this.filter.encodedQuery}`;
  }

  private showPlayModal(song: Song): void {
    this.$bvModal.show(song.songId);
  }

  private onSelect(song: Song, selected: boolean): void {
    this.$emit("song-selected", song.songId, selected);
  }

  private onClickLike(editor: SongEditor): void {
    editor.toggleLike();
    editor.saveChanges();
  }

  private onDanceVote(editor: SongEditor, vote: DanceRatingVote): void {
    editor.danceVote(vote);
    editor.saveChanges();
  }
}
</script>

<style scoped lang="scss">
.editHeader {
  min-width: 4em;
}

.likeHeader {
  min-width: 4em;
}

.likeDanceHeader {
  min-width: 6em;
}

.echoHeader {
  min-width: 75px;
}
.sortedEchoHeader {
  min-width: 100px;
}
.orderHeader {
  min-width: 3em;
}
</style>
