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

// We only checked part of the playlist, bounded by the viewer's subscription tier.
const isPartial = computed(() => model.checkedCount < model.totalCount);
// The playlist itself has no tracks.
const isEmptyPlaylist = computed(() => model.totalCount === 0);
// We checked at least one track, and none of them are in the catalog.
const noMatches = computed(() => model.checkedCount > 0 && model.matchedCount === 0);
// Some of the tracks we did check aren't in the catalog yet (covers both noMatches and partial).
const hasUnmatched = computed(() => model.matchedCount < model.checkedCount);

const unmatchedFields = [{ key: "title" }, { key: "artist" }, { key: "add", label: "" }];

const addSongLink = (trackId: string): string => `/song/augment?id=${trackId}`;
const loginLink = computed(
  () => `/Identity/Account/Login?ReturnUrl=${encodeURIComponent(`/song/playlist?id=${model.id}`)}`,
);
</script>

<template>
  <PageFrame id="app">
    <BAlert v-if="isPartial" show variant="info"
      >We checked the first {{ model.checkedCount }} of {{ model.totalCount }} songs in this
      playlist for music4dance matches. <a href="/Home/Contribute">Upgrade your membership</a> to
      have us check more. See our
      <a href="https://music4dance.blog/music4dance-help/subscriptions/" target="_blank"
        >subscription page</a
      >
      for details.</BAlert
    >
    <BAlert v-if="hasUnmatched && !model.canAddSongs" show variant="info"
      >{{
        noMatches ? "None of the songs we checked are" : "Some of the songs we checked aren't"
      }}
      in the music4dance catalog yet. <a :href="loginLink">Sign in</a> to add them yourself, or
      <a href="https://music4dance.blog/feedback/" target="_blank">contact us</a> if you would like
      us to support a specific playlist.</BAlert
    >
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
    <BAlert v-if="isEmptyPlaylist" show variant="warning"
      >This Spotify playlist doesn't have any tracks.</BAlert
    >
    <BAlert v-else-if="noMatches" show variant="warning"
      >None of the songs in this playlist are in the music4dance catalog yet.</BAlert
    >
    <SongTable
      v-else
      :histories="model.histories!"
      :filter="new SongFilter()"
      :hide-sort="true"
      :hidden-columns="hiddenColumns"
    />
    <SpotifyPlayer v-if="!isEmptyPlaylist" :playlist="model.id" />
    <p>
      by <BLink :href="userLink" target="_blank">{{ model.ownerName }}</BLink>
    </p>
    <div v-if="hasUnmatched && model.canAddSongs" class="mt-3">
      <h2>Songs Not Yet in the Catalog</h2>
      <p>
        {{
          noMatches
            ? "None of the songs in this playlist are in music4dance yet"
            : "These songs from the playlist aren't in music4dance yet"
        }}
        &mdash; add them so you (and everyone else) can dance to them.
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
