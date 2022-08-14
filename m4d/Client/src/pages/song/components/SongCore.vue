<template>
  <div>
    <b-row>
      <b-col>
        <h1>
          <song-like-button
            :user="model.userName"
            :song="song"
            :scale="1"
            :toggleBehavior="true"
            @click-like="onClickLike"
          ></song-like-button>
          <i
            ><field-editor
              name="Title"
              :value="song.title"
              :editing="editing"
              :isCreator="isCreator"
              role="dbAdmin"
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
            role="dbAdmin"
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
        <b-button v-if="showSave" variant="primary" @click="saveChanges">{{
          saveText
        }}</b-button>
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
        <comment-editor
          :container="song"
          :editor="editor"
          :edit="edit"
          :rows="6"
          :placeholder="commentPlaceholder"
        ></comment-editor>
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
        <div v-if="isAdmin" class="mt-2">
          <track-list
            v-if="editing && isAdmin"
            :song="song"
            :editing="edit"
            @add-track="addTrack"
            @add-property="addProperty($event)"
          ></track-list>
          <h3>Admin Edit</h3>
          <b-form-textarea
            id="admin-edit"
            v-model="adminProperties"
            :readonly="!edit"
            rows="3"
            max-rows="6"
          ></b-form-textarea>
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
          <div class="mb-2">
            <h3>Admin Actions</h3>
            <b-button variant="outline-primary" :href="updateServices"
              >Update Services</b-button
            >
          </div>
          <song-history-log
            v-if="model.songHistory"
            :history="history"
            :editing="editing"
            @delete-property="onDeleteProperty($event)"
          >
          </song-history-log>
        </div>
      </b-col>
      <b-col v-if="model.songHistory">
        <song-history-viewer :history="history"> </song-history-viewer>
      </b-col>
    </b-row>
    <dance-chooser
      @choose-dance="addDance"
      @update-song="updateSong"
      :filterIds="explicitDanceIds"
      :tempo="song.tempo"
      :numerator="numerator"
      :hideNameLink="true"
    ></dance-chooser>
  </div>
</template>

<script lang="ts">
import CommentEditor from "@/components/CommentEditor.vue";
import DanceChooser from "@/components/DanceChooser.vue";
import SongLikeButton from "@/components/SongLikeButton.vue";
import TagButton from "@/components/TagButton.vue";
import TagListEditor from "@/components/TagListEditor.vue";
import { DanceRatingVote, VoteDirection } from "@/DanceRatingDelta";
import AdminTools from "@/mix-ins/AdminTools";
import { AlbumDetails } from "@/model/AlbumDetails";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { DanceRating } from "@/model/DanceRating";
import { Song } from "@/model/Song";
import { SongDetailsModel } from "@/model/SongDetailsModel";
import { SongEditor } from "@/model/SongEditor";
import { SongFilter } from "@/model/SongFilter";
import { SongHistory } from "@/model/SongHistory";
import { PropertyType, SongProperty } from "@/model/SongProperty";
import { Tag } from "@/model/Tag";
import { TrackModel } from "@/model/TrackModel";
import "reflect-metadata";
import { Component, Mixins, Prop, Watch } from "vue-property-decorator";
import AlbumList from "./AlbumList.vue";
import DanceList from "./DanceList.vue";
import FieldEditor from "./FieldEditor.vue";
import PurchaseSection from "./PurchaseSection.vue";
import SongHistoryLog from "./SongHistoryLog.vue";
import SongHistoryViewer from "./SongHistoryViewer.vue";
import SongStats from "./SongStats.vue";
import TrackList from "./TrackList.vue";

@Component({
  components: {
    AlbumList,
    CommentEditor,
    DanceChooser,
    DanceList,
    FieldEditor,
    PurchaseSection,
    SongHistoryLog,
    SongHistoryViewer,
    SongLikeButton,
    SongStats,
    TagButton,
    TagListEditor,
    TrackList,
  },
})
export default class SongCore extends Mixins(AdminTools) {
  @Prop() private readonly model!: SongDetailsModel;
  @Prop() private readonly environment!: DanceEnvironment | null;
  @Prop() private readonly startEditing?: boolean;
  @Prop() private readonly creating?: boolean;

  private songStore: Song;
  private editor: SongEditor | null;
  private toastShown = false;
  private edit = false;

  constructor() {
    super();
    this.songStore = new Song();
    this.editor = null;
  }

  private get song(): Song {
    const editor = this.editor;
    return editor ? editor.song : this.songStore;
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

  private get showSave(): boolean {
    return (this.modified && this.checkDances) || (this.isAdmin && this.edit);
  }

  private get history(): SongHistory {
    const editor = this.editor;
    return editor ? editor.history : this.model.songHistory;
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

  private get adminProperties(): string {
    if (!this.isAdmin) {
      throw new Error("Unauthorized");
    }

    const editor = this.editor;
    const properties = editor
      ? editor.history.properties
      : this.model.songHistory.properties;

    return this.computePropertyString(properties);
  }

  private set adminProperties(properties: string) {
    this.editor!.adminEdit(properties);
  }

  private computePropertyString(properties: SongProperty[]): string {
    return properties.map((p) => p.toString()).join("\t");
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

    const stats = environment.tree;
    if (stats) {
      if (this.model.userName) {
        this.editor = new SongEditor(
          this.axiosXsrf,
          this.model.userName,
          this.model.songHistory
        );
      }
      this.songStore = Song.fromHistory(
        this.model.songHistory,
        this.model.userName
      );
    }
  }

  private onDanceVote(vote: DanceRatingVote): void {
    const editor = this.editor;
    if (!editor) {
      throw new Error("Can't edit if not logged in");
    }
    this.editor!.danceVote(vote);
    this.edit = true;
  }

  private onDeleteAlbum(album: AlbumDetails): void {
    this.editor!.addAlbumProperty(
      PropertyType.albumField,
      undefined,
      album.index!
    );
  }

  private onDeleteDance(dr: DanceRating): void {
    const tag = Tag.fromParts(
      this.environment!.fromId(dr.danceId)!.name,
      "Dance"
    );
    this.editor!.addProperty(PropertyType.deleteTag, tag.key);
  }

  private addProperty(property: SongProperty): void {
    this.editor!.addProperty(property.name, property.value);
  }

  private get artistLink(): string | undefined {
    const artist = this.song?.artist;
    return artist ? `/song/artist?name=${artist}` : undefined;
  }

  private get hasExplicitDances(): boolean {
    return !!this.explicitDanceIds?.length;
  }

  private get explicitDanceRatings(): DanceRating[] {
    const ratings = this.song.danceRatings ?? [];
    return this.explicitDanceIds.map(
      (id) => ratings.find((dr) => dr.danceId === id)!
    );
  }

  private get explicitDanceIds(): string[] {
    const tags = this.song.tags;
    return this.environment && tags
      ? tags
          .filter(
            (t) =>
              t.category === "Dance" &&
              !t.value.startsWith("!") &&
              !t.value.startsWith("-")
          )
          .map((t) => this.environment!.fromName(t.value)!.id)
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
  }

  private addDance(danceId?: string, persist?: boolean): void {
    const editor = this.editor;
    if (!editor) {
      throw new Error("Can't edit if not logged in");
    }
    if (danceId) {
      this.editor!.danceVote(new DanceRatingVote(danceId, VoteDirection.Up));

      if (!persist) {
        this.$bvModal.hide("danceChooser");
      }
      this.edit = true;
    }
  }

  private updateField(property: SongProperty): void {
    const editor = this.editor;
    if (!editor) {
      throw new Error("Can't edit if not logged in");
    }
    this.editor?.modifyProperty(property.name, property.value);
  }

  private onDeleteProperty(index: number): void {
    const editor = this.editor;
    if (!editor) {
      throw new Error("Can't edit if not logged in");
    }
    editor.deleteProperty(index);
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

  private get checkDances(): boolean {
    return (this.song.hasDances || this.editor?.initialSong.hasDances) ?? false;
  }

  private updateSong(): void {
    this.songStore = this.editor!.song;
  }

  private setEdit(): void {
    this.edit = true;
  }

  private addTrack(track: TrackModel): void {
    this.editor?.addAlbumFromTrack(track);
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

  private get updateServices(): string {
    return `/song/UpdateSongAndServices?id=${this.song.songId}&filter=${this.model.filter.query}`;
  }

  private get commentPlaceholder(): string {
    return (
      `Add comments about this song and its general dancability.  If you have any comments about ` +
      `how this song relates to a particular dance style please vote on that dance style in the ` +
      `"Dances" section and add your comments there.`
    );
  }

  private get saveText(): string {
    return this.creating ? "Add Song" : "Save Changes";
  }

  private cancelChanges(): void {
    this.editor!.revert();
    this.edit = false;
    this.$emit("cancel-changes");
  }

  private async saveChanges(): Promise<void> {
    if (this.creating) {
      await this.editor!.create();
    } else {
      await this.editor!.saveChanges();
    }

    this.edit = false;

    if (this.startEditing) {
      this.$emit("song-saved");
    }
  }
}
</script>
