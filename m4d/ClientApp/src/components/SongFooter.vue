<script setup lang="ts">
import { ref } from "vue";
import { SongListModel } from "@/models/SongListModel";
import { getMenuContext } from "@/helpers/GetMenuContext";
import type { BvEvent } from "bootstrap-vue-next";

// INT-TODO: Convert this back to BPaginationNav if BSVN supports it
const props = defineProps<{
  model: SongListModel;
  href?: string;
}>();

const context = getMenuContext();

const model = props.model;
const filter = props.model.filter;
const pageNumber = filter.page ?? 1;
const pageCount = Math.max(1, Math.ceil(model.count / 25));
const editPageNumber = ref(pageNumber);

const goToPage = () => {
  let page = editPageNumber.value;
  if (isNaN(page) || page < 1) page = 1;
  if (page > pageCount) page = pageCount;
  editPageNumber.value = page;
  if (page !== pageNumber) {
    window.location.href = linkGen(page);
  }
};
const newSearch = filter.isSimple(context.userName) ? "/song" : "/song/advancedsearchform";
const playListRef = filter.getPlayListRef(context.userName);

const exportRef =
  context.hasRole("showDiagnostics") || context.hasRole("beta")
    ? filter.getExportRef(context.userName)
    : undefined;

const linkGen = (pageNum: number) => {
  const href = props.href;
  return href ? pagedUrl(href, pageNum) : pagedUrl(filter.url, pageNum);
};

const pagedUrl = (url: string, pageNum: number) =>
  url.includes("?") ? `${url}&page=${pageNum}` : `${url}?page=${pageNum}`;

const onPageClick = (event: BvEvent, pageNum: number) => {
  event.preventDefault();
  window.location.href = linkGen(pageNum);
};
</script>

<template>
  <BRow>
    <BCol md="8">
      <BPagination
        v-model="pageNumber"
        :total-rows="model.count"
        :per-page="25"
        limit="9"
        first-number
        last-number
        @page-click="onPageClick"
      />
    </BCol>
    <BCol md="2"
      >Page
      <input
        v-model.number="editPageNumber"
        type="number"
        step="1"
        :min="1"
        :max="pageCount"
        class="form-control form-control-sm d-inline-block"
        style="width: 4em"
        @keydown.enter="goToPage"
        @blur="goToPage"
      />
      of {{ pageCount }} ({{ model.count }} songs found)</BCol
    >
    <BCol md="2"
      ><div><a :href="newSearch">New Search</a></div>
      <div v-if="playListRef">
        <a :href="playListRef">Create Spotify Playlist</a>
      </div>
      <div v-if="exportRef">
        <a :href="exportRef">Export to File</a>
      </div>
    </BCol>
  </BRow>
</template>
