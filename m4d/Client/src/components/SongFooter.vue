<template>
  <b-row>
    <b-col md="8">
      <b-pagination-nav
        :link-gen="linkGen"
        :number-of-pages="pageCount"
        v-model="pageNumber"
        limit="9"
        first-number
        last-number
      ></b-pagination-nav>
    </b-col>
    <b-col md="2"
      >Page {{ pageNumber }} of {{ pageCount }} ({{ model.count }} songs
      found)</b-col
    >
    <b-col md="2"
      ><div><a :href="newSearch">New Search</a></div>
      <div v-if="playListRef">
        <a :href="playListRef">Create Spotify Playlist</a>
      </div>
      <div v-if="exportRef">
        <a :href="exportRef">Export to File</a>
      </div>
    </b-col>
  </b-row>
</template>

<script lang="ts">
import AdminTools from "@/mix-ins/AdminTools";
import { SongFilter } from "@/model/SongFilter";
import { SongListModel } from "@/model/SongListModel";
import { PropType } from "vue";

export default AdminTools.extend({
  components: {},
  props: {
    model: { type: Object as PropType<SongListModel>, required: true },
    href: String,
  },
  data() {
    return new (class {
      editAction = "";
      editProperties = "";
      editUser = "";
      tempoMultiplier = 0.5;
    })();
  },
  computed: {
    filter(): SongFilter {
      const model = this.model;
      return model ? model.filter : new SongFilter();
    },
    // used
    pageNumber: {
      get(): number {
        return this.filter.page ?? 1;
      },
      set(n: number): void {
        if (this.filter) {
          this.filter.page = n;
        }
      },
    },
    // used
    pageCount(): number {
      return Math.max(1, Math.ceil(this.model.count / 25));
    },
    // used
    newSearch(): string {
      return this.filter.isSimple(this.userName)
        ? "/song"
        : "/song/advancedsearchform";
    },
    // used
    playListRef(): string | undefined {
      return this.filter.getPlayListRef(this.userName);
    },
    // used
    exportRef(): string | undefined {
      return this.hasRole("showDiagnostics") || this.hasRole("beta")
        ? this.filter.getExportRef(this.userName)
        : undefined;
    },
  },
  methods: {
    // used
    linkGen(pageNum: number): string {
      const href = this.href;
      return href
        ? this.pagedUrl(href, pageNum)
        : this.pagedUrl(this.filter.url, pageNum);
    },
    pagedUrl(url: string, pageNum: number): string {
      return url.includes("?")
        ? `${url}&page=${pageNum}`
        : `${url}?page=${pageNum}`;
    },
  },
});
</script>
