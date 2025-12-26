<script setup lang="ts">
import { getMenuContext } from "@/helpers/GetMenuContext";
import { SongListModel } from "@/models/SongListModel";
import { TypedJSON } from "typedjson";
import { useSongSelector } from "@/composables/useSongSelector";

const context = getMenuContext();

declare const model_: string;
const model = TypedJSON.parse(model_, SongListModel)!;

const complexSearchWarning = model.rawCount > model.count && model.rawCount > 500;
const hiddenColumns = model.hiddenColumns ?? ["danceTags", "length", "track"];
const userName = context.userName;
const { songs: selected, select: selectSong } = useSongSelector();
const hasData = model.histories && model.histories.length > 0;
const searchAvailable = context.searchHealthy !== false;
</script>

<template>
  <PageFrame id="app">
    <template v-if="searchAvailable">
      <BAlert
        v-if="complexSearchWarning"
        :model-value="true"
        dismissible
        variant="warning"
        style="margin-bottom: 0"
      >
        This is a complex search that requries multiple passes to compute. We're limitting the
        initial pass to 500 songs, which may result in much less that 500 songs in the final results
        as well as incomplete results (the intial pass in this cases yields
        {{ model.rawCount }} songs). We believe we can solve this is a more general way, so please
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
        v-if="hasData"
        :histories="model.histories!"
        :filter="model.filter"
        :hide-sort="false"
        :hidden-columns="hiddenColumns"
        @song-selected="selectSong"
      />
      <BAlert v-else variant="warning" :model-value="true" class="mt-4">
        <p class="mb-0">No songs found for this search.</p>
      </BAlert>
      <SongFooter v-if="hasData" :model="model" />
      <AdminFooter :model="model" :selected="selected" />
    </template>
  </PageFrame>
</template>
