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
      <b-col md="2"><a :href="newSearch">New Search</a></b-col>
    </b-row>
    <b-row v-if="showExplicitMessage">
      <b-col>
        Not seeing as many songs as you expect in this list? You are currently
        seeing only songs that someone has explicitly tagged as
        {{ danceQuery.description }}. You can widen the search by choosing to
        include songs inferred by tempo on the
        <a href="/song/advancedsearchform">advanced search form</a>
        or just <a :href="implicitFilter">clicking here</a>.
      </b-col>
    </b-row>
    <b-row v-if="isAdmin">
      <b-col>
        <b-button-toolbar aria-label="Admin song modifiers">
          <b-dropdown right text="Multi-Edit" class="mx-1">
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
          <b-dropdown right text="Batch Lookup" class="mx-1">
            <b-dropdown-item :href="batchUrl('batchmusicservice', 50, 'A')"
              >Amazon</b-dropdown-item
            >
            <b-dropdown-item :href="batchUrl('batchechonest', 1000)"
              >Echo</b-dropdown-item
            >
            <b-dropdown-item :href="batchUrl('batchmusicservice', 50, 'I')"
              >iTunes</b-dropdown-item
            >
            <b-dropdown-item :href="batchUrl('batchmusicservice', 50, 'S')"
              >Spotify</b-dropdown-item
            >
            <b-dropdown-item :href="batchUrl('batchsamples', 1000)"
              >Sample</b-dropdown-item
            >
            <b-dropdown-item :href="batchUrl('batchmusicservice', 50, '-', 'S')"
              >Canonical Spotify</b-dropdown-item
            >
            <b-dropdown-item :href="batchUrl('batchmusicservice', 50, '-', 'R')"
              >Canonical (R)</b-dropdown-item
            >
          </b-dropdown>
          <b-dropdown right text="Clean" class="mx-1">
            <b-dropdown-item :href="batchUrl('batchcleanservice', -1)"
              >Services</b-dropdown-item
            >
            <b-dropdown-item :href="batchUrl('batchcleanservice', -1, 'SP')"
              >Spotify</b-dropdown-item
            >
            <b-dropdown-item :href="batchUrl('batchclearupdate', -1)"
              >Update Flag</b-dropdown-item
            >
          </b-dropdown>
          <b-dropdown right text="Update" class="mx-1">
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
          <input type="submit" name="action" :value="editAction" />
          <input type="submit" ref="Merge" name="action" value="Merge" />
          <input type="submit" ref="Delete" name="action" value="Merge" />
          <input
            type="submit"
            ref="CelanupAlbums"
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
              v-model="adminEdit.user"
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
              v-model="adminEdit.properties"
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
              v-model="adminEdit.properties"
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
import { Component, Prop, Vue } from "vue-property-decorator";
import { SongFilter } from "@/model/SongFilter";
import { SongListModel } from "@/model/SongListModel";
import { DanceQueryBase } from "@/model/DanceQueryBase";
import { MenuContext } from "@/model/MenuContext";

declare const menuContext: MenuContext;

interface AdminEdit {
  user: string;
  properties: string;
}

// TODO: Consider generalizing bulk edit functionality and creating a component
//  Also, look at generalizing tag capability (this currently won't catch individual
//  tags when the are glommed together in a single property)
@Component
export default class SongFooter extends Vue {
  @Prop() private readonly model!: SongListModel;
  @Prop() private readonly href?: string;
  @Prop() private readonly canShowImplicitMessage?: boolean;
  @Prop() private readonly selected?: string[];

  private pageNumber: number;
  private readonly context: MenuContext = menuContext;
  private editAction = "";

  private adminEdit: AdminEdit;
  private tempoMultiplier = 0.5;

  constructor() {
    super();
    this.pageNumber = this.filter.page ?? 1;
    this.adminEdit = { user: this.model.userName, properties: "" };
  }

  private get filter(): SongFilter {
    return this.model.filter;
  }

  private get pageCount(): number {
    return Math.max(1, Math.ceil(this.model.count / 25));
  }

  private get isAdmin(): boolean {
    return !!menuContext.isAdmin;
  }

  private get showExplicitMessage(): boolean {
    const danceQuery = this.danceQuery;
    return (
      !!this.canShowImplicitMessage &&
      !!danceQuery &&
      !danceQuery.includeInferred &&
      danceQuery.dances.length > 0
    );
  }

  private get danceQuery(): DanceQueryBase | undefined {
    return this.filter.isRaw ? undefined : this.filter.danceQuery;
  }

  private get newSearch(): string {
    return this.filter.isSimple(this.model.userName)
      ? "/song"
      : "/song/advancedsearchform";
  }

  private get implicitFilter(): string {
    const filter = this.filter.clone();
    filter.dances = filter.danceQuery.setIncludeInferred(true).query;
    return filter.url;
  }

  private get selectedSongs(): string[] {
    return this.selected ?? [];
  }

  private linkGen(pageNum: number): string {
    const href = this.href;
    return href
      ? this.pagedUrl(href, pageNum)
      : this.pagedUrl(this.filter.url, pageNum);
  }

  private pagedUrl(url: string, pageNum: number): string {
    return url.includes("?")
      ? `${url}&page=${pageNum}`
      : `${url}?page=${pageNum}`;
  }

  private batchUrl(
    name: string,
    count: number,
    type?: string,
    options?: string
  ): string {
    const typeParam = type ? `type=${type}&` : "";
    const optionsParam = options ? `options=${options}` : "";
    return `/song/${name}?${typeParam}${optionsParam}&count=${count}&filter=${this.model.filter.encodedQuery}`;
  }

  private onBulkEdit(type: string): void {
    this.editAction = type;
    const submit = this.$refs[type];
    const button = submit as HTMLButtonElement;
    button.click();
  }
}
</script>
