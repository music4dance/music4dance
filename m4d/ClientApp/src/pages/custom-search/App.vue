<script setup lang="ts">
import { toTitleCase, wordsToKebab } from "@/helpers/StringHelpers";
import { homeCrumb, songCrumb } from "@/models/BreadCrumbItem";
import { CustomSearchModel } from "@/models/CustomSearchModel";
import { TypedJSON } from "typedjson";
import { useSongSelector } from "@/composables/useSongSelector";

declare const model_: string;

const model = TypedJSON.parse(model_, CustomSearchModel)!;
const { songs: selected, select: selectSong } = useSongSelector();
const dance = model.dance ?? "";

const histories = model.histories ?? [];
const name = toTitleCase(model.name);
const title = model.dance ? `${name} ${toTitleCase(model.dance)} Music` : `${name} Dance Music`;
const allLink = `/customsearch?name=${model.name}`;
const pageLink = model.dance ? `${allLink}&dance=${model.dance}` : allLink;
const danceLink = `/dances/${wordsToKebab(dance)}`;
const danceName = model.dance ? toTitleCase(dance) : undefined;
const breadcrumbs = (() => {
  const breadcrumbs = [homeCrumb, songCrumb];
  const text = `${name} Music`;
  if (model.dance) {
    breadcrumbs.push({ text, href: allLink });
    breadcrumbs.push({ text: toTitleCase(model.dance), active: true });
  } else {
    breadcrumbs.push({ text, active: true });
  }
  return breadcrumbs;
})();
</script>

<template>
  <PageFrame id="app" :breadcrumbs="breadcrumbs">
    <h1>{{ title }}</h1>
    <CustomSearchHelp
      v-if="model.count === 0"
      :name="model.name"
      :dance="model.dance!"
      :empty="true"
    />
    <div v-else>
      <p v-if="model.dance">
        This page includes a list of all of the
        <a :href="danceLink">{{ danceName }}</a> music on the site that has been tagged as
        {{ model.description }}. Click <a :href="allLink">here</a> to see {{ model.name }} music for
        all dance styles.
      </p>
      <p v-else>
        This page includes a list of all of the
        <a href="/dances/ballroom-competition-categories">Ballroom</a> and other
        <a href="/dances">partner dance</a> music on the site that has been tagged as
        {{ model.description }}.
      </p>
      <SongTable
        :histories="histories"
        :filter="model.filter"
        :hide-sort="true"
        :hidden-columns="['Track']"
        @song-selected="selectSong"
      />
      <SongFooter :model="model" :href="pageLink" />
    </div>
    <SpotifyPlayer v-if="model.playListId" :playlist="model.playListId" />
    <CustomSearchDanceChooser :name="model.name" :dance="dance" :count="model.count" />
    <AdminFooter :model="model" :selected="selected" />
  </PageFrame>
</template>
