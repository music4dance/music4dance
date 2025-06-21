<script setup lang="ts">
import { PlaylistViewerModel } from "@/models/PlaylistViewerModel";
import { TypedJSON } from "typedjson";
import { SongFilter } from "@/models/SongFilter";

declare const model_: string;
const model = TypedJSON.parse(model_, PlaylistViewerModel)!;

const hiddenColumns = ["length", "track"];
const userLink = `https://open.spotify.com/user/${model.ownerId}`;
const playlistLink = `https://open.spotify.com/playlist/${model.id}`;
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
    <BAlert v-if="model.histories.length < model.totalCount" show variant="info" class="mt-3"
      >Some of the songs in the spotify playlist aren't shown because there are not entries in the
      music4dance catalog. You can
      <a href="https://music4dance.blog/music4dance-help/add-songs/" target="_blank">add songs</a>
      to the catalog or
      <a href="https://music4dance.blog/feedback/" target="_blank">contact us</a> if you would like
      us to support a specific playlist.</BAlert
    >
  </PageFrame>
</template>
