<script setup lang="ts">
import { SongFilter } from "@/models/SongFilter";
import { SongListModel } from "@/models/SongListModel";
import { SortOrder } from "@/models/SongSort";
import { TypedJSON } from "typedjson";
import { useSongSelector } from "@/composables/useSongSelector";

declare const model_: string;
const model = TypedJSON.parse(model_, SongListModel)!;

const { songs: selected, select: selectSong } = useSongSelector();

const added = model.filter.sortOrder === SortOrder.Created;
const changed = model.filter.sortOrder === SortOrder.Modified;
const commented = model.filter.sortOrder === SortOrder.Comments;

const filter = model?.filter ?? new SongFilter();

const histories = model.histories ?? [];

const navigate = (order: SortOrder): void => {
  window.location.href = `/song/newmusic?type=${order}`;
};

const clickAdded = (): void => {
  navigate(SortOrder.Created);
};

const clickChanged = (): void => {
  navigate(SortOrder.Modified);
};

const clickCommented = (): void => {
  navigate(SortOrder.Comments);
};
</script>

<template>
  <PageFrame id="app" title="New Music">
    <BButtonGroup>
      <BButton variant="outline-primary" :pressed="added" @click="clickAdded"
        >Recently Added</BButton
      >
      <BButton variant="outline-primary" :pressed="changed" @click="clickChanged"
        >Recently Changed</BButton
      >
      <BButton variant="outline-primary" :pressed="commented" @click="clickCommented"
        >Recently Commented</BButton
      >
    </BButtonGroup>
    <SongTable
      :histories="histories"
      :filter="filter"
      :hide-sort="true"
      :hidden-columns="['length', 'track']"
      :show-history="true"
      @song-selected="selectSong"
    />
    <SongFooter :model="model" />
    <AdminFooter :model="model" :selected="selected" />
  </PageFrame>
</template>
