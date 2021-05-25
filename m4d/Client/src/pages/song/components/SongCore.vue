<template>
  <div>
    <b-row>
      <b-col>
        <h1>
          <song-like-button
            :user="model.userName"
            :song="song"
            :scale="1"
            @click-like="onClickLike"
          ></song-like-button>
          <i
            ><field-editor
              name="Title"
              :value="song.title"
              :editing="editing"
              :isCreator="isCreator"
              role="isAdmin"
              @update-field="updateField($event)"
            ></field-editor
          ></i>
          <span
            v-if="song.artist"
            style="font-size: 0.75em; padding-left: 0.5em"
          >
            by
          </span>
          <field-editor
            name="Artist"
            :value="song.artist"
            :editing="editing"
            :isCreator="isCreator"
            role="isAdmin"
            @update-field="updateField($event)"
          >
            <span v-if="song.artist" style="font-size: 0.75em"
              ><a :href="artistLink">{{ song.artist }}</a></span
            >
          </field-editor>
        </h1>
      </b-col>
      <b-col v-if="editing || canTag" cols="auto">
        <b-button
          v-if="!editing && isAdmin"
          variant="outline-danger"
          class="mr-1"
          :href="deleteLink"
          >Delete</b-button
        >
        <b-button
          v-if="editing"
          variant="outline-primary"
          class="mr-1"
          @click="cancelChanges"
          >Cancel</b-button
        >
        <b-button v-else variant="outline-primary" class="mr-1" @click="setEdit"
          >Edit</b-button
        >
        <b-button
          v-if="(modified && hasDances) || isAdmin"
          variant="primary"
          @click="saveChanges"
          >{{ saveText }}</b-button
        >
      </b-col>
    </b-row>
    <b-row class="mb-2">
      <b-col md="4"
        ><purchase-section
          :purchaseInfos="song.getPurchaseInfos()"
          :filter="model.filter"
        ></purchase-section>
      </b-col>
      <b-col md="4">
        <tag-list-editor
          :container="song"
          :filter="filter"
          :user="model.userName"
          :editor="editor"
          :edit="edit"
          @edit="setEdit"
          @update-song="updateSong"
        ></tag-list-editor>
      </b-col>
      <b-col v-if="song.hasSample" md="4">
        <audio controls class="mx-auto">
          <source :src="song.sample" type="audio/mpeg" />
          Your browser does not support audio.
        </audio>
      </b-col>
    </b-row>
    <b-row class="mb-2">
      <b-col md="4">
        <dance-list
          title="Dances"
          class="mb-2"
          :song="song"
          :danceRatings="explicitDanceRatings"
          :user="model.userName"
          :filter="model.filter"
          :editor="editor"
          :edit="edit"
          @dance-vote="onDanceVote($event)"
          @update-song="updateSong"
          @edit="setEdit"
          @delete-dance="onDeleteDance($event)"
        />
        <b-button
          v-if="!!model.userName"
          v-b-modal.danceChooser
          variant="primary"
          >Add Dance Style</b-button
        >
      </b-col>
      <b-col md="4" v-if="hasInferredDances">
        <dance-list
          title="Dances (Inferred)"
          :song="song"
          :danceRatings="inferredDanceRatings"
          :user="model.userName"
          :filter="model.filter"
          :editor="editor"
          :edit="edit"
          @dance-vote="onDanceVote($event)"
          @update-song="updateSong"
          @delete-dance="onDeleteDance($event)"
        />
      </b-col>
      <b-col md="auto"
        ><song-stats
          :song="song"
          :editing="editing"
          :isCreator="isCreator"
          @update-field="updateField($event)"
        ></song-stats
        ><b-button
          v-if="hasUserChanges && !editing"
          @click="undoUserChanges"
          variant="outline-primary"
          >Undo My Changes</b-button
        >
        <form
          id="undoUserChanges"
          ref="undoUserChanges"
          action="/song/undoUserChanges"
          method="post"
          v-show="false"
        >
          <input
            type="hidden"
            name="__RequestVerificationToken"
            :value="context.xsrfToken"
          />
          <input type="hidden" name="id" :value="song.songId" />
          <input type="hidden" name="filter" :value="model.filter.query" />
        </form>
      </b-col>
    </b-row>
    <b-row>
      <b-col v-if="song.albums"
        ><album-list
          :albums="song.albums"
          :editing="edit"
          @delete-album="onDeleteAlbum($event)"
        ></album-list>
        <div v-if="isAdmin">
          <form
            id="adminEdit"
            action="/song/adminedit"
            method="post"
            enctype="multipart/form-data"
          >
            <h3>Admin Edit</h3>
            <input type="hidden" name="filter" :value="model.filter.query" />
            <input
              type="hidden"
              name="__RequestVerificationToken"
              :value="context.xsrfToken"
            />
            <input type="hidden" name="songId" :value="song.songId" />
            <b-form-group
              id="ae-properties-group"
              label="Properties:"
              label-for="ae-properties"
              description="Full list of properties to replace with the song"
              ><b-form-input
                name="properties"
                id="ae-properties"
                v-model="adminProperties"
                required
              ></b-form-input
            ></b-form-group>
            <b-button type="submit">Submit</b-button>
          </form>
          <h3>Undo User Edits</h3>
          <b-form
            v-for="modified in song.modifiedBy"
            :key="modified.userName"
            action="/song/undoUserChanges"
            method="post"
            class="m-1"
            style="display: inline-block"
          >
            <input
              type="hidden"
              name="__RequestVerificationToken"
              :value="context.xsrfToken"
            />
            <input type="hidden" name="id" :value="song.songId" />
            <input type="hidden" name="userName" :value="modified.userName" />
            <b-button type="submit">{{ modified.userName }}</b-button>
          </b-form>
        </div>
      </b-col>
      <b-col v-if="isAdmin && model.songHistory"
        ><song-history :history="model.songHistory"></song-history
      ></b-col>
    </b-row>
    <dance-chooser
      @chooseDance="addDance"
      :filterIds="explicitDanceIds"
      :tempo="song.tempo"
      :numerator="numerator"
    ></dance-chooser>
  </div>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Mixins, Prop, Watch } from "vue-property-decorator";
import AdminTools from "@/mix-ins/AdminTools";
import AlbumList from "./AlbumList.vue";
import DanceChooser from "@/components/DanceChooser.vue";
import DanceList from "./DanceList.vue";
import FieldEditor from "./FieldEditor.vue";
import PurchaseSection from "./PurchaseSection.vue";
import SongHistory from "./SongHistory.vue";
import SongLikeButton from "@/components/SongLikeButton.vue";
import SongStats from "./SongStats.vue";
import TagButton from "@/components/TagButton.vue";
import TagListEditor from "@/components/TagListEditor.vue";
import { SongEditor } from "@/model/SongEditor";
import { SongFilter } from "@/model/SongFilter";
import { SongDetailsModel } from "@/model/SongDetailsModel";
import { Song } from "@/model/Song";
import { DanceRating } from "@/model/DanceRating";
import { DanceRatingVote, VoteDirection } from "@/DanceRatingDelta";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { PropertyType, SongProperty } from "@/model/SongProperty";
import { AlbumDetails } from "@/model/AlbumDetails";
import { Tag } from "@/model/Tag";

@Component({
  components: {
    AlbumList,
    DanceChooser,
    DanceList,
    FieldEditor,
    PurchaseSection,
    SongHistory,
    SongLikeButton,
    SongStats,
    TagButton,
    TagListEditor,
  },
})
export default class SongCore extends Mixins(AdminTools) {
  @Prop() private readonly model!: SongDetailsModel;
  @Prop() private readonly environment!: DanceEnvironment | null;
  @Prop() private readonly startEditing?: boolean;
  @Prop() private readonly creating?: boolean;

  private song: Song;
  private editor: SongEditor | null;
  private toastShown = false;
  private adminProperties = "";
  private edit = false;

  constructor() {
    super();
    this.song = new Song();
    this.editor = null;
  }

  private beforeMount() {
    this.edit = !!this.startEditing;
    this.initialize();
    window.addEventListener("beforeunload", this.leaveWarning);
  }

  private beforeDestroy() {
    window.removeEventListener("beforeunload", this.leaveWarning);
  }

  private get filter(): SongFilter {
    return this.model.filter;
  }

  private get modified(): boolean {
    const modified = this.editor?.modified ?? false;
    if (modified && !this.toastShown) {
      this.$bvToast.toast(`Click '${this.saveText}' to save your changes`, {
        title: "Don't Forget!",
        variant: "primary",
        toaster: "b-toaster-top-center",
      });
    }
    return modified;
  }

  @Watch("environment")
  private onEnvironmentLoaded(): void {
    this.initialize();
  }

  private initialize(): void {
    const environment = this.environment;
    if (!environment) {
      return;
    }

    const stats = environment.stats;
    if (stats) {
      if (this.model.userName) {
        this.editor = new SongEditor(
          this.model.userName,
          this.model.songHistory
        );
        if (this.isAdmin) {
          this.adminProperties = this.model.songHistory.properties
            .map((p) => p.toString())
            .join("\t");
        }
      }
      this.song = Song.fromHistory(this.model.songHistory, this.model.userName);
    }
  }

  private onDanceVote(vote: DanceRatingVote): void {
    const editor = this.editor;
    if (!editor) {
      throw new Error("Can't edit if not logged in");
    }
    this.editor!.danceVote(vote);
    this.song = this.editor!.song;
  }

  private onDeleteAlbum(album: AlbumDetails): void {
    this.editor!.addAlbumProperty(
      PropertyType.albumField,
      undefined,
      album.index!
    );
    this.song = this.editor!.song;
  }

  private onDeleteDance(dr: DanceRating): void {
    // TODONEXT: Figure out why this isn't inducing an update...
    const tag = new Tag({
      value: this.environment!.fromId(dr.danceId)!.danceName,
      category: "Dance",
    });
    this.editor!.addProperty(PropertyType.deleteTag, tag.key);
    this.song = this.editor!.song;
  }

  private get artistLink(): string | undefined {
    const artist = this.song?.artist;
    return artist ? `/song/artist?name=${artist}` : undefined;
  }

  private get hasExplicitDances(): boolean {
    return !!this.explicitDanceIds?.length;
  }

  private get hasInferredDances(): boolean {
    return !!this.inferredDanceRatings?.length;
  }

  private get explicitDanceRatings(): DanceRating[] {
    const ratings = this.song.danceRatings ?? [];
    return this.explicitDanceIds.map(
      (id) => ratings.find((dr) => dr.danceId === id)!
    );
  }

  private get inferredDanceRatings(): DanceRating[] {
    const ratings = this.song.danceRatings ?? [];
    const explicitIds = this.explicitDanceIds;

    return ratings.filter((dr) => !explicitIds.find((id) => id === dr.danceId));
  }

  private get explicitDanceIds(): string[] {
    const tags = this.song.tags;
    return this.environment && tags
      ? tags
          .filter((t) => t.category === "Dance" && !t.value.startsWith("!"))
          .map((t) => this.environment!.fromName(t.value)!.danceId)
      : [];
  }

  private get numerator(): number | undefined {
    if (this.hasMeterTag(4)) {
      return 4;
    } else if (this.hasMeterTag(3)) {
      return 3;
    } else if (this.hasMeterTag(2)) {
      return 2;
    }
    return undefined;
  }

  private hasMeterTag(numerator: number): boolean {
    return !!this.song.tags.find((t) => t.key === `${numerator}/4:Tempo`);
  }

  private onClickLike(): void {
    const editor = this.editor;
    if (!editor) {
      throw new Error("Can't edit if not logged in");
    }
    editor.toggleLike();
    this.song = editor.song;
  }

  private addDance(danceId?: string): void {
    const editor = this.editor;
    if (!editor) {
      throw new Error("Can't edit if not logged in");
    }
    if (danceId) {
      this.editor!.danceVote(new DanceRatingVote(danceId, VoteDirection.Up));
      this.song = this.editor!.song;
      this.$bvModal.hide("danceChooser");
    }
  }

  private updateField(property: SongProperty): void {
    const editor = this.editor;
    if (!editor) {
      throw new Error("Can't edit if not logged in");
    }
    this.editor?.modifyProperty(property.name, property.value);
  }

  private get hasUserChanges(): boolean {
    return !!this.editor?.userHasPreviousChanges;
  }

  private undoUserChanges(): void {
    this.$bvModal
      .msgBoxConfirm(
        "Are you sure you want to undo all of your edits to this song?"
      )
      .then((value: boolean) => {
        if (value) {
          (this.$refs.undoUserChanges as HTMLFormElement).submit();
        }
      })
      .catch((err) => {
        console.log(err);
      });
  }

  private get editing(): boolean {
    return this.modified || this.edit;
  }

  private get isCreator(): boolean {
    const userName = this.userName;
    return !!userName && this.song.isCreator(userName);
  }

  private get hasDances(): boolean {
    return this.song.hasDances;
  }

  private updateSong(): void {
    this.song = this.editor!.song;
  }

  private setEdit(): void {
    this.edit = true;
  }

  private leaveWarning(event: BeforeUnloadEvent): void {
    if (this.modified) {
      event.returnValue =
        "You have unsaved changes.  Are you sure you want to leave?";
    }
  }

  private get deleteLink(): string {
    return `/song/delete?id=${this.song.songId}`;
  }

  private get saveText(): string {
    return this.creating ? "Add Song" : "Save Changes";
  }

  private cancelChanges(): void {
    this.editor!.revert();
    this.song = this.editor!.song;
    this.edit = false;
    this.$emit("cancel-changes");
  }

  private async saveChanges(): Promise<void> {
    if (this.creating) {
      await this.editor!.create();
    } else {
      await this.editor!.saveChanges();
    }

    this.song = this.editor!.song;
    this.edit = false;

    if (this.startEditing) {
      this.$emit("song-saved");
    }
  }
}
</script>
