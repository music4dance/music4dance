<template>
  <div>
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
    <b-row v-if="isAdmin">
      <b-col>
        <b-button-toolbar aria-label="Admin song modifiers">
          <b-dropdown right text="Multi-Edit" class="mx-1 mb-1">
            <b-dropdown-item @click="onBulkEdit('SimpleMerge')"
              >Simple Merge</b-dropdown-item
            >
            <b-dropdown-item @click="onBulkEdit('Merge')"
              >Merge</b-dropdown-item
            >
            <b-dropdown-item @click="onBulkEdit('Delete')"
              >Delete</b-dropdown-item
            >
            <b-dropdown-item @click="onBulkEdit('CleanupAlbums')"
              >Cleanup Albums</b-dropdown-item
            >
          </b-dropdown>
          <b-dropdown right text="Clean" class="mx-1 mb-1">
            <b-dropdown-item :href="batchUrl('batchcleanservice', -1)"
              >Services</b-dropdown-item
            >
            <b-dropdown-item :href="batchUrl('batchcleanservice', -1, 'SP')"
              >Spotify</b-dropdown-item
            >
            <b-dropdown-item :href="batchUrl('batchcleanupproperties', -1)"
              >Properties</b-dropdown-item
            >
            <b-dropdown-item :href="batchUrl('batchreloadsongs', -1)"
              >Reload</b-dropdown-item
            >
          </b-dropdown>
          <b-dropdown right text="Download" class="mx-1 mb-1">
            <b-dropdown-item :href="batchUrl('downloadJson', -1, 'S')"
              >Songs</b-dropdown-item
            >
            <b-dropdown-item :href="batchUrl('downloadJson', -1, 'H')"
              >History</b-dropdown-item
            >
          </b-dropdown>
          <b-dropdown right text="Update" class="mx-1">
            <b-dropdown-item
              :href="batchUrlBase('batchUpdateService', -1, 'serviceType=I')"
              >iTunes</b-dropdown-item
            >
            <b-dropdown-item :href="batchUrlBase('batchEchoNest', -1)"
              >EchoNest</b-dropdown-item
            >
            <b-dropdown-item :href="batchUrlBase('batchSamples', -1)"
              >Samples</b-dropdown-item
            >
            <b-dropdown-item>User Tags (deprecated)</b-dropdown-item>
            <b-dropdown-item>Tags (deprecated)</b-dropdown-item>
            <b-dropdown-item>Tag Summaries</b-dropdown-item>
          </b-dropdown>
        </b-button-toolbar>
        <form
          id="bulkEdit"
          ref="bulkEdit"
          action="/song/bulkedit"
          method="post"
          v-show="false"
        >
          <input
            type="hidden"
            name="__RequestVerificationToken"
            :value="context.xsrfToken"
          />
          <input
            v-for="song in selectedSongs"
            :key="song"
            type="hidden"
            name="selectedSongs"
            :value="song"
          />
          <input type="hidden" name="filter" :value="model.filter.query" />
          <input type="submit" name="action" :value="editAction" />
          <input type="submit" ref="Merge" name="action" value="Merge" />
          <input
            type="submit"
            ref="SimpleMerge"
            name="action"
            value="SimpleMerge"
          />
          <input type="submit" ref="Delete" name="action" value="Delete" />
          <input
            type="submit"
            ref="ClaanupAlbums"
            name="action"
            value="cleanupAlbums"
          />
        </form>
      </b-col>
      <b-col>
        <form
          id="batchAdminEdit"
          action="/song/batchadminedit"
          method="post"
          enctype="multipart/form-data"
        >
          <h3>Bulk Admin Edit</h3>
          <input type="hidden" name="filter" :value="model.filter.query" />
          <input
            type="hidden"
            name="__RequestVerificationToken"
            :value="context.xsrfToken"
          />
          <b-form-group
            id="bae-user-group"
            label="User Name:"
            label-for="bea-user"
            description="User name to attribute the edits to."
            ><b-form-input
              name="user"
              id="bea-user"
              v-model="editUser"
              required
            ></b-form-input
          ></b-form-group>
          <b-form-group
            id="bae-properties-group"
            label="Properties:"
            label-for="bea-properties"
            description="Properties to append to each song in the current filter."
            ><b-form-input
              name="properties"
              id="bea-properties"
              v-model="editProperties"
              required
            ></b-form-input
          ></b-form-group>
          <b-button type="submit">Submit</b-button>
        </form>
      </b-col>
      <b-col>
        <form
          id="batchAdminModify"
          action="/song/batchadminmodify"
          method="post"
          enctype="multipart/form-data"
        >
          <h3>Bulk Admin Modify</h3>
          <input type="hidden" name="filter" :value="model.filter.query" />
          <input
            type="hidden"
            name="__RequestVerificationToken"
            :value="context.xsrfToken"
          />
          <b-form-group
            id="bme-properties-group"
            label="Song Modifier:"
            label-for="bma-properties"
            description="A SongModifier object in JSON format"
            ><b-form-input
              name="properties"
              id="bma-properties"
              v-model="editProperties"
              required
            ></b-form-input
          ></b-form-group>
          <b-button type="submit">Submit</b-button>
        </form>
      </b-col>
      <b-col>
        <form
          id="batchCorrectTempo"
          action="/song/batchcorrecttempo"
          method="post"
          enctype="multipart/form-data"
        >
          <h3>Bulk Correct Tempo</h3>
          <input type="hidden" name="filter" :value="model.filter.query" />
          <input
            type="hidden"
            name="__RequestVerificationToken"
            :value="context.xsrfToken"
          />
          <b-form-group
            id="bct-multiplier-group"
            label="Tempo Multiplier:"
            label-for="bct-multiplier"
            description="Multiplier to change each song's tempo by"
            ><b-form-input
              name="multiplier"
              id="bct-multiplier"
              v-model="tempoMultiplier"
              required
            ></b-form-input
          ></b-form-group>
          <b-button type="submit">Submit</b-button>
        </form>
      </b-col>
    </b-row>
  </div>
</template>

<script lang="ts">
import AdminTools from "@/mix-ins/AdminTools";
import { DanceQueryBase } from "@/model/DanceQueryBase";
import { SongFilter } from "@/model/SongFilter";
import { SongListModel } from "@/model/SongListModel";
import { PropType } from "vue";

// TODO: Consider generalizing bulk edit functionality and creating a component
//  Also, look at generalizing tag capability (this currently won't catch individual
//  tags when the are glommed together in a single property)

export default AdminTools.extend({
  components: {},
  props: {
    model: { type: Object as PropType<SongListModel>, required: true },
    href: String,
    canShowImplicitMessage: Boolean,
    selected: Array as PropType<string[]>,
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
    pageCount(): number {
      return Math.max(1, Math.ceil(this.model.count / 25));
    },
    danceQuery(): DanceQueryBase | undefined {
      return this.filter.isRaw ? undefined : this.filter.danceQuery;
    },
    newSearch(): string {
      return this.filter.isSimple(this.userName)
        ? "/song"
        : "/song/advancedsearchform";
    },
    selectedSongs(): string[] {
      return this.selected ?? [];
    },
    playListRef(): string | undefined {
      return this.filter.getPlayListRef(this.userName);
    },
    exportRef(): string | undefined {
      return this.hasRole("showDiagnostics") || this.hasRole("beta")
        ? this.filter.getExportRef(this.userName)
        : undefined;
    },
  },
  methods: {
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
    batchUrl(
      name: string,
      count: number,
      type?: string,
      options?: string
    ): string {
      const typeParam = type ? `type=${type}` : "";
      const optionsParam = options ? `options=${options}` : "";
      const separator = typeParam && optionsParam ? "&" : "";
      return this.batchUrlBase(
        name,
        count,
        typeParam + separator + optionsParam
      );
    },
    batchUrlBase(name: string, count: number, additional?: string): string {
      additional = additional ? `&${additional}` : "";
      return `/song/${name}?count=${count}${additional}&filter=${this.filter.encodedQuery}`;
    },
    onBulkEdit(type: string): void {
      this.editAction = type;
      const submit = this.$refs[type];
      const button = submit as HTMLButtonElement;
      button.click();
    },
  },
  mounted(): void {
    this.editUser = this.userName ?? "";
  },
});
</script>
