<script setup lang="ts">
import { PlaylistViewerModel } from "@/models/PlaylistViewerModel";
import { TypedJSON } from "typedjson";
import { SongFilter } from "@/models/SongFilter";
import { computed } from "vue";

declare const model_: string;
const model = TypedJSON.parse(model_, PlaylistViewerModel)!;

const hiddenColumns = ["length", "track"];
const userLink = `https://open.spotify.com/user/${model.ownerId}`;
const playlistLink = `https://open.spotify.com/playlist/${model.id}`;

// More catalog matches exist than the viewer's subscription tier shows.
const isLimited = computed(() => model.histories.length < model.matchedCount);
// Playlist tracks with no catalog match at all (independent of the tier limit above).
const hasUnmatched = computed(() => model.matchedCount < model.totalCount);

const unmatchedFields = [{ key: "title" }, { key: "artist" }, { key: "add", label: "" }];

const addSongLink = (trackId: string): string => `/song/augment?id=${trackId}`;
</script>

<template>
  <PageFrame id="app">
    <h1>
      <BLink :href="playlistLink" target="_blank" title="Open in Spotify" underline-opacity="0"
        ><BImg
          src="/images/icons/spotify-logo.png"
          alt="Spotify Logo"
          style="vertical-align: middle"
        /><span class="ps-2">{{ model.name }}</span></BLink
      >
    </h1>
    <p v-html="model.description" />
    <SongTable
      :histories="model.histories!"
      :filter="new SongFilter()"
      :hide-sort="true"
      :hidden-columns="hiddenColumns"
    />
    <SpotifyPlayer :playlist="model.id" />
    <p>
      by <BLink :href="userLink" target="_blank">{{ model.ownerName }}</BLink>
    </p>
    <BAlert v-if="isLimited" show variant="info" class="mt-3"
      >Showing {{ model.histories.length }} of {{ model.matchedCount }} matched songs.
      <a href="https://music4dance.blog/music4dance-help/subscriptions/" target="_blank"
        >Upgrade your subscription</a
      >
      to see more.</BAlert
    >
    <BAlert v-if="hasUnmatched && !model.canAddSongs" show variant="info" class="mt-3"
      >Some of the songs in the spotify playlist aren't shown because there are no entries in the
      music4dance catalog. <a href="/Identity/Account/Login">Sign in</a> to add them yourself, or
      <a href="https://music4dance.blog/feedback/" target="_blank">contact us</a> if you would like
      us to support a specific playlist.</BAlert
    >
    <div v-if="hasUnmatched && model.canAddSongs" class="mt-3">
      <h2>Songs Not Yet in the Catalog</h2>
      <p>
        These songs from the playlist aren't in music4dance yet &mdash; add them so you (and
        everyone else) can dance to them.
      </p>
      <BTable striped hover :items="model.unmatched" :fields="unmatchedFields">
        <template #cell(add)="data">
          <a
            :href="addSongLink(data.item.trackId)"
            target="_blank"
            role="button"
            title="Add to music4dance"
            ><IBiPlusCircle
          /></a>
        </template>
      </BTable>
    </div>
  </PageFrame>
</template>
