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
      <template v-slot:cell(action)="data">
        <b-button @click="onAction(data.item.song, $event)">{{
          action
        }}</b-button>
      </template>
      <template v-slot:head(play)>
        <div :class="likeHeader">Like/Play</div>
      </template>
      <template v-slot:cell(play)="data">
        <play-cell :editor="data.item" :filter="filter"></play-cell>
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
      <template v-slot:head(length)>
        <sortable-header
          id="Lempo"
          title="Length"
          :tip="lengthHeaderTip"
          :enableSort="false"
          :filter="filter"
        ></sortable-header>
      </template>
      <template v-slot:cell(length)="data">
        {{ lengthValue(data.item.song) }}
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
      <template v-slot:head(user)>
        <div class="userHeader">{{ filterDisplayName }}'s Changes</div>
      </template>
      <template v-slot:cell(user)="data">
        <song-change-viewer
          v-if="getUserChange(data.item.history)"
          :change="getUserChange(data.item.history)"
          :oneUser="true"
        ></song-change-viewer>
      </template>

      <template v-slot:head(text)>
        <sortable-header
          id="Title"
          :tip="titleHeaderTip"
          :enableSort="!hideSort"
          :filter="filter"
        ></sortable-header>
        <template v-if="!isHidden('artist')">
          -
          <sortable-header
            id="Artist"
            :tip="titleHeaderTip"
            :enableSort="!hideSort"
            :filter="filter"
          ></sortable-header>
        </template>
      </template>
      <template v-slot:cell(text)="data">
        <play-cell :editor="data.item" :filter="filter"></play-cell>
        <a :href="songRef(data.item.song)" class="ml-1">{{
          data.item.song.title
        }}</a>
        <template v-if="!isHidden('artist')">
          by
          <a :href="artistRef(data.item.song)">{{ data.item.song.artist }}</a>
        </template>
        <span v-if="tempoValue(data.item.song)">
          @
          <a :href="tempoRef(data.item.song)"
            >{{ tempoValue(data.item.song) }} BPM</a
          >
        </span>
        <span v-if="lengthValue(data.item.song) && !isHidden('length')">
          - {{ lengthValue(data.item.song) }}s
        </span>
        <song-change-viewer
          v-if="hasUser && getUserChange(data.item.history)"
          :change="getUserChange(data.item.history)"
          :oneUser="false"
        ></song-change-viewer>
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
import { DanceRatingVote } from "@/DanceRatingDelta";
import AdminTools from "@/mix-ins/AdminTools";
import { DanceHandler } from "@/model/DanceHandler";
import { Song } from "@/model/Song";
import { SongChange } from "@/model/SongChange";
import { SongEditor } from "@/model/SongEditor";
import { SongFilter } from "@/model/SongFilter";
import { SongHistory } from "@/model/SongHistory";
import { SongSort, SortOrder } from "@/model/SongSort";
import { Tag } from "@/model/Tag";
import { TaggableObject } from "@/model/TaggableObject";
import { TagHandler } from "@/model/TagHandler";
import SongChangeViewer from "@/pages/song/components/SongChangeViewer.vue";
import { BvTableFieldArray } from "bootstrap-vue";
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";
import DanceButton from "./DanceButton.vue";
import EchoIcon from "./EchoIcon.vue";
import PlayCell from "./PlayCell.vue";
import SortableHeader from "./SortableHeader.vue";
import TagButton from "./TagButton.vue";

// TODO:
//  Look at mixin's again - particularly with respect to danceId->dance translations
//  Go through and audit no-explicit-any, interface-name warning
//  Get tag cloud at bottom of dance page to include add to filter + filter on

interface Field {
  key: string;
  label?: string;
}

const actionField = { key: "action", label: "" };
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
const userField = { key: "user", label: "" };
const textField = { key: "text" };
const infoField = { key: "info" };
const lengthField = { key: "length" };

@Component({
  components: {
    DanceButton,
    EchoIcon,
    PlayCell,
    SongChangeViewer,
    SortableHeader,
    TagButton,
  },
})
export default class SongTable extends Mixins(AdminTools) {
  @Prop() private readonly histories!: SongHistory[];
  @Prop() private readonly filter!: SongFilter;
  @Prop() private readonly hideSort?: boolean;
  @Prop() private readonly hiddenColumns?: string[];
  @Prop() private readonly action?: string;

  private get songs(): SongEditor[] {
    const userId = this.userId;
    const userName = this.userName;
    if (userId && userName) {
      return this.histories.map(
        (h) => new SongEditor(this.userName, h.Deanonymize(userName, userId))
      );
    } else {
      return this.histories.map((h) => new SongEditor(this.userName, h));
    }
  }

  private get songMap(): Map<string, SongEditor> {
    return new Map(this.songs.map((s) => [s.song.id, s]));
  }

  private get fields(): BvTableFieldArray {
    /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
    const mq = (this as any).$mq;

    const baseFields =
      mq === "sm" || mq === "md" ? this.smallFields : this.fullFields;

    return this.action ? [actionField, ...baseFields] : baseFields;
  }

  private get smallFields(): BvTableFieldArray {
    const smallFields = [textField, infoField];
    return this.filterHiddenFields(
      smallFields.map((f) => this.filterSmallField(f))
    );
  }

  private get fullFields(): BvTableFieldArray {
    const fields = [
      playField,
      titleField,
      artistField,
      trackField,
      tempoField,
      lengthField,
      echoField,
      dancesField,
      tagsField,
      orderField,
    ];

    const hasUser = this.hasUser;
    const temp = this.filterHiddenFields(fields).map((f) =>
      f.key === orderField.key && hasUser ? userField : f
    );
    if (this.isAdmin && !this.isHidden(editField.key)) {
      return [editField, ...temp];
    } else {
      return temp;
    }
  }

  private filterSmallField(field: Field): Field {
    if (field === infoField && this.isHidden("dances")) {
      return tagsField;
    } else {
      return field;
    }
  }

  private filterHiddenFields(fields: Field[]): Field[] {
    const hidden = this.hiddenColumns;
    return hidden ? fields.filter((f) => !this.isHidden(f.key)) : fields;
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

  private get lengthHeaderTip(): string {
    return "Duration of Song in seconds";
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

  private lengthValue(song: Song): string {
    const length = song.length;
    return length && !this.isHidden("length") ? length.toString() : "";
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

  private get hasUser(): boolean {
    const query = this.filter.userQuery;
    return !!query.userName && query.include;
  }

  private getUserChange(history: SongHistory): SongChange | undefined {
    const user = this.filterUser;
    return history.recentUserChange(user);
  }

  private get filterUser(): string {
    const user = this.hasUser ? this.filter.userQuery.userName : "";
    return user === "me" ? this.userName! : user;
  }

  private get filterDisplayName(): string {
    const user = this.hasUser ? this.filter.userQuery.displayName : "";
    return user === "me" ? this.userName! : user;
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

  private editRef(song: Song): string {
    return `/song/edit?id=${song.songId}&filter=${this.filter.encodedQuery}`;
  }

  private onSelect(song: Song, selected: boolean): void {
    this.$emit("song-selected", song.songId, selected);
  }

  private onAction(song: Song): void {
    this.$emit("song-selected", song.songId, true);
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

.userHeader {
  min-width: 12em;
}
</style>
