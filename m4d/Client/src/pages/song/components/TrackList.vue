<template>
  <b-card
    header="Tracks"
    header-text-variant="primary"
    no-body
    border-variant="primary"
  >
    <b-form class="m-2">
      <b-form-input
        id="track-title"
        v-model="title"
        class="mb-2"
        placeholder="Title Override"
      ></b-form-input>

      <b-form-input
        id="track-artist"
        v-model="artist"
        class="mb-2"
        placeholder="Artist Override"
      ></b-form-input>

      <b-button variant="primary" @click="lookup()">Lookup</b-button>
      <b-button variant="primary-outline" @click="lookup('S')"
        >Spotify</b-button
      >
      <b-button variant="primary-outline" @click="lookup('I')">iTunes</b-button>
      <b-alert show v-if="error" variant="danger" class="mt-2">{{
        error
      }}</b-alert>
      <b-alert show v-if="tracks.length > 0" variant="success" class="mt-2">
        {{ tracks.length }} track found. {{ newTracks.length }} not already
        added.
      </b-alert>
    </b-form>
    <b-list-group flush>
      <b-list-group-item v-for="track in newTracks" :key="track.trackId">
        <track-item :track="track" :enableProperties="true" v-on="$listeners" />
      </b-list-group-item>
    </b-list-group>
  </b-card>
</template>

<script lang="ts">
import "reflect-metadata";
import axios from "axios";
import TrackItem from "./TrackItem.vue";
import { Component, Prop, Vue } from "vue-property-decorator";
import { AlbumDetails } from "@/model/AlbumDetails";
import { Song } from "@/model/Song";
import { TypedJSON } from "typedjson";
import { TrackModel } from "@/model/TrackModel";

@Component({ components: { TrackItem } })
export default class TrackList extends Vue {
  @Prop() private readonly song!: Song;
  @Prop() private readonly editing?: boolean;
  private tracks: TrackModel[] = [];
  private error = "";
  private title = "";
  private artist = "";

  private albumLink(album: AlbumDetails): string {
    return `/song/album?title=${album.name}`;
  }

  private async lookup(service: string): Promise<void> {
    try {
      this.error = "";
      let parameters = service ? `service=${service}&` : "";
      const title = this.title;
      if (title) {
        parameters += `title=${encodeURIComponent(title)}&`;
      }
      const artist = this.artist;
      if (artist) {
        parameters += `artist=${encodeURIComponent(artist)}&`;
      }
      const results = await axios.get(
        `/api/musicservice/${this.song.songId}?${parameters}`
      );
      this.tracks = TypedJSON.parseAsArray(results.data, TrackModel);
    } catch (e: unknown) {
      this.error = e as string;
      this.tracks = [];
    }
  }

  private get newTracks(): TrackModel[] {
    const albums = this.song.albums;
    if (!albums) {
      return this.tracks;
    }

    return this.tracks.filter(
      (track) =>
        !albums.find((album) => {
          const purchaseList = album.purchase.decode();
          return purchaseList.find(
            (purchase) =>
              purchase.service === track.serviceType &&
              purchase.songId === track.trackId
          );
        })
    );
  }
}
</script>
