<script setup lang="ts">
import { getMenuContext } from "@/helpers/GetMenuContext";
import { SongListModel } from "@/models/SongListModel";
import { TypedJSON } from "typedjson";
import { useSongSelector } from "@/composables/useSongSelector";

const context = getMenuContext();

declare const model_: string;
const model = TypedJSON.parse(model_, SongListModel)!;

const complexSearchWarning = model.rawCount > model.count && model.rawCount > 500;
const hiddenColumns = model.hiddenColumns ?? ["length", "track"];
const userName = context.userName;
const { songs: selected, select: selectSong } = useSongSelector();
</script>

<template>
  <PageFrame id="app">
    <BAlert v-if="complexSearchWarning" show dismissible variant="warning" style="margin-bottom: 0">
      This is a complex search that requries multiple passes to compute. We're limitting the initial
      pass to 500 songs, which may result in much less that 500 songs in the final results as well
      as incomplete results (the intial pass in this cases yields {{ model.rawCount }} songs). We
      believe we can solve this is a more general way, so please
      <a href="https://music4dance.blog/feedback/" target="_blank">send feedback</a>
      about what you are trying to accomplish with this search and we can either help you with an
      alternate search or increase the priority of building amore general solution.
    </BAlert>
    <SongLibraryHeader
      v-if="model.filter.isSimple(userName)"
      :filter="model.filter"
      :user="userName"
    />
    <SearchHeader v-else :filter="model.filter" :user="context.userName" />
    <SongTable
      :histories="model.histories!"
      :filter="model.filter"
      :hide-sort="false"
      :hidden-columns="hiddenColumns"
      @song-selected="selectSong"
    />
    <SongFooter :model="model" />
    <AdminFooter :model="model" :selected="selected" />
  </PageFrame>
</template>
