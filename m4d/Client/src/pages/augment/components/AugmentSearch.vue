<template>
  <div>
    <p>
      Search for a song by entering title, artist and optional album and
      clicking the <b>Search</b> button
    </p>
    <b-form
      @submit="onSubmit"
      @reset="onReset"
      :validated="validated"
      class="mb-3"
      novalidate
    >
      <b-form-group id="title-group" label="Title:" label-for="title">
        <b-form-input
          id="input-1"
          v-model="title"
          type="text"
          placeholder="A very danceable song"
          required
        ></b-form-input>
        <div class="invalid-feedback">You must include a Title</div>
      </b-form-group>

      <b-form-group id="artist-group" label="Artist:" label-for="artist">
        <b-form-input
          id="artist"
          v-model="artist"
          type="text"
          placeholder="An amazing artist"
          required
        ></b-form-input>
        <div class="invalid-feedback">You must include an Artist</div>
      </b-form-group>

      <b-button type="submit" variant="primary" class="mr-2">Search</b-button>
      <b-button type="reset" variant="danger">Reset</b-button>
    </b-form>
    <b-alert v-show="searching && !songs" show variant="info">
      <b-spinner class="mr-3"></b-spinner>
      <span>Searching for </span> {{ title }} by {{ artist }} in the music4dance
      catalog.
    </b-alert>
    <div v-if="songs && !tracks">
      <b-alert v-show="songs.length" show variant="success"
        >We found some songs in the music4dance catalog that may be a match. If
        one of the songs below is a match, please click the "Edit" button next
        the song, otherwise search on iTunes or spotify.</b-alert
      >
      <b-alert v-show="!songs.length" show variant="warning"
        >No songs matching, click one of the buttons below to search
        <b>Spotify</b> or <b>Apple Music</b>.</b-alert
      >
    </div>
    <div v-if="songs">
      <b-alert v-show="searching && songs" show variant="info">
        <b-spinner class="mr-3"></b-spinner>
        <span>Searching for </span> {{ title }} by {{ artist }} in the publisher
        catalog.
      </b-alert>
      <b-button @click="searchSpotify" variant="secondary" class="mr-2"
        >Search Spotify</b-button
      >
      <b-button @click="searchItunes" variant="secondary"
        >Search Apple Music</b-button
      >
    </div>
    <song-table
      v-if="songs && songs.length && !tracks"
      :histories="songs"
      :filter="filter"
      :hideSort="true"
      action="Edit"
      :hiddenColumns="[
        'dances',
        'echo',
        'edit',
        'order',
        'play',
        'tags',
        'track',
      ]"
      @song-selected="editSong"
    ></song-table>
    <div v-if="tracks" class="my-2">
      <b-alert v-show="tracks.length" show variant="success"
        >We found some songs that may be a match. If one of the songs below is a
        match, please click the "Add" button next. Otherwise, try searching the
        other service (Apple Music or Spotify). If none of that works, try
        changing the title or artist - less is sometimes better, so perhaps just
        use artist last name or a fragment of the title.</b-alert
      >
      <b-alert v-show="!tracks.length" show variant="warning"
        >No matching songs. Please try searching the other service (Apple Music
        or Spotify). If none of that works, try changing the title or artist -
        less is sometimes better, so perhaps just use artist last name or a
        fragment of the title.</b-alert
      >
    </div>

    <b-list-group v-if="tracks && tracks.length" flush>
      <b-list-group-item v-for="track in tracks" :key="track.trackId">
        <track-item :track="track" @add-track="addTrack" />
      </b-list-group-item>
    </b-list-group>
  </div>
</template>

<script lang="ts">
import AdminTools from "@/mix-ins/AdminTools";
import { SongDetailsModel } from "@/model/SongDetailsModel";
import { SongFilter } from "@/model/SongFilter";
import { SongHistory } from "@/model/SongHistory";
import { TrackModel } from "@/model/TrackModel";
import axios from "axios";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { Component, Mixins, Watch } from "vue-property-decorator";
import SongTable from "../../../components/SongTable.vue";
import TrackItem from "../../../pages/song/components/TrackItem.vue";

// TODO:
//  - Consider adding Album/track column
//  = Consider formatting time as mm:ss

@Component({ components: { SongTable, TrackItem } })
export default class AugmentSearch extends Mixins(AdminTools) {
  private title = "";
  private artist = "";
  private searching = false;
  private adding = false;
  private validated = false;
  private filter = new SongFilter();
  private songs: SongHistory[] | null = null;
  private tracks: TrackModel[] | null = null;
  private service: string | null = null;

  private async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.validated = true;

    const title = this.title;
    const artist = this.artist;

    if (!title || !artist) {
      return;
    }
    try {
      this.searching = true;
      const results = await this.axiosXsrf.get(
        `/api/song/?title=${title}&artist=${artist}`
      );
      this.songs = TypedJSON.parseAsArray(results.data, SongHistory);
    } catch (e) {
      this.songs = [];
    }

    this.searching = false;
  }

  private onReset(event: Event): void {
    event.preventDefault();
    this.reset();
  }

  private reset(): void {
    this.title = "";
    this.artist = "";
    this.searching = false;
    this.coreReset();
  }

  private editSong(songId: string): void {
    const history = this.songs?.find((s) => s.id === songId);
    if (!history) {
      throw new Error(
        `Augment search in a bad state: unable to find ${songId}`
      );
    }
    const model = new SongDetailsModel({
      created: false,
      songHistory: history,
      filter: this.filter,
    });

    this.reset();
    this.$emit("edit-song", model);
  }

  private async searchSpotify(): Promise<void> {
    await this.searchService("S");
  }

  private async searchItunes(): Promise<void> {
    await this.searchService("I");
  }

  @Watch("title")
  private onTitleChanged(): void {
    this.coreReset();
  }

  @Watch("artist")
  private onArtistChanged(): void {
    this.coreReset();
  }

  private coreReset(): void {
    this.validated = false;
    this.songs = null;
    this.tracks = null;
  }

  private async searchService(service: string): Promise<void> {
    try {
      this.service = service;
      this.searching = true;
      const results = await axios.get(
        `/api/musicservice/?service=${service}&title=${this.title}&artist=${this.artist}`
      );
      this.tracks = TypedJSON.parseAsArray(results.data, TrackModel);
    } catch (e) {
      this.tracks = [];
      this.service = null;
    }
    this.searching = false;
  }

  private async addTrack(track: TrackModel): Promise<void> {
    this.adding = true;
    try {
      const uri = `/api/servicetrack/${this.service}${track.trackId}`;
      const response = await axios.get(uri);
      const songModel = TypedJSON.parse(response.data, SongDetailsModel);
      if (songModel) {
        this.$emit("edit-song", songModel);
      }
    } catch (e) {
      // eslint-disable-next-line no-console
      console.log(e);
    }
    this.adding = false;
  }
}
</script>

<style lang="scss" scoped>
#service-id {
  width: 25rem;
}
</style>
