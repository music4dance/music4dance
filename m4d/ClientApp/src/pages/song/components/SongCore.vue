<script setup lang="ts">
import CommentEditor from "@/components/CommentEditor.vue";
import DanceChooser from "@/components/DanceChooser.vue";
import SongLikeButton from "@/components/SongLikeButton.vue";
import TagListEditor from "@/components/TagListEditor.vue";
import { DanceRatingVote, VoteDirection } from "@/models/DanceRatingDelta";
import { AlbumDetails } from "@/models/AlbumDetails";
import { DanceRating } from "@/models/DanceRating";
import { Song } from "@/models/Song";
import { SongDetailsModel } from "@/models/SongDetailsModel";
import { SongEditor } from "@/models/SongEditor";
import { PropertyType, SongProperty } from "@/models/SongProperty";
import { Tag } from "@/models/Tag";
import { TrackModel } from "@/models/TrackModel";
import AlbumList from "./AlbumList.vue";
import DanceList from "./DanceList.vue";
import FieldEditor from "./FieldEditor.vue";
import PurchaseSection from "./PurchaseSection.vue";
import SongHistoryLog from "./SongHistoryLog.vue";
import SongHistoryViewer from "./SongHistoryViewer.vue";
import SongStats from "./SongStats.vue";
import TrackList from "./TrackList.vue";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { computed, onBeforeMount, onBeforeUnmount, ref, watch } from "vue";
import type { SongHistory } from "@/models/SongHistory";
import { useToast, useModalController } from "bootstrap-vue-next";
import { TagHandler } from "@/models/TagHandler";

const context = getMenuContext();
const danceDB = safeDanceDatabase();
const { show } = useToast();
const { confirm, hide } = useModalController();

const props = defineProps<{
  model: SongDetailsModel;
  startEditing?: boolean;
  creating?: boolean;
}>();

const emit = defineEmits<{
  "cancel-changes": [];
  "song-saved": [];
}>();

const undoForm = ref<HTMLFormElement | null>(null);

const songStore = ref(Song.fromHistory(props.model.songHistory, props.model.userName));
const editor = ref<SongEditor | null>(
  props.model.userName
    ? new SongEditor(context.axiosXsrf, props.model.userName, props.model.songHistory)
    : null,
);
const toastShown = ref(false);
const edit = ref(props.startEditing);

const tagModalVisible = ref(false);
const currentTag = ref<TagHandler>(
  new TagHandler(Tag.fromString("Placeholder:Other"), undefined, undefined, undefined, true),
);

const adminProperties = computed<string>({
  get: () => {
    if (!context.isAdmin) {
      throw new Error("Unauthorized");
    }

    const properties = editor.value
      ? editor.value.history.properties
      : props.model.songHistory.properties;

    return computePropertyString(properties as SongProperty[]);
  },
  set: (properties: string) => {
    safeEditor.value.adminEdit(properties);
  },
});
const song = computed(() => (editor.value ? editor.value.song : songStore.value));
const safeEditor = computed(() => {
  if (!editor.value) {
    throw new Error("Can't edit if not logged in");
  }
  return editor.value;
});
const filter = computed(() => props.model.filter);
const showSave = computed(
  () => (modified.value && checkDances.value) || (context.isAdmin && edit.value),
);
const history = computed(() => (editor.value ? editor.value.history : props.model.songHistory));
const modified = computed(() => {
  const modified = editor.value?.modified ?? false;
  if (modified && !toastShown.value) {
    show?.({
      props: {
        title: "Don't Forget!",
        variant: "primary",
        pos: "top-end",
        value: 5000,
        interval: 100,
        solid: true,
        body: `Click '${saveText.value}' to save your changes`,
      },
    });
  }
  return modified;
});
watch(modified, (value) => {
  if (value) {
    toastShown.value = true;
  }
});

const artistLink = computed(() => {
  const artist = song.value.artist;
  return artist ? `/song/artist?name=${artist}` : undefined;
});
const explicitDanceIds = computed(() => {
  const tags = song.value.tags;
  return tags
    ? tags
        .filter(
          (t) => t.category === "Dance" && !t.value.startsWith("!") && !t.value.startsWith("-"),
        )
        .map((t) => danceDB.fromName(t.value)!.id)
    : [];
});
const explicitDanceRatings = computed(() => {
  const ratings = song.value.danceRatings ?? [];
  return explicitDanceIds.value.map((id) => ratings.find((dr) => dr.danceId === id)!);
});
const numerator = computed(() => {
  if (hasMeterTag(4)) {
    return 4;
  } else if (hasMeterTag(3)) {
    return 3;
  } else if (hasMeterTag(2)) {
    return 2;
  }
  return undefined;
});
const hasUserChanges = computed(() => !!editor.value?.userHasPreviousChanges);
const editing = computed(() => modified.value || edit.value);
const isCreator = computed(() => {
  const userName = props.model.userName;
  return !!userName && song.value.isCreator(userName);
});
const checkDances = computed(
  () => (song.value.hasDances || editor.value?.initialSong.hasDances) ?? false,
);
const deleteLink = computed(() => `/song/delete?id=${props.model.songHistory.id}`);
const updateServices = computed(
  () => `/song/UpdateSongAndServices?id=${song.value.songId}&filter=${filter.value.query}`,
);
const commentPlaceholder = computed(() => {
  return (
    `Add comments about this song and its general dancability.  If you have any comments about ` +
    `how this song relates to a particular dance style please vote on that dance style in the ` +
    `"Dances" section and add your comments there.`
  );
});
const saveText = computed(() => (props.creating ? "Add Song" : "Save Changes"));

const computePropertyString = (properties: SongProperty[]): string => {
  return properties.map((p) => p.toString()).join("\t");
};
const onDanceVote = (vote: DanceRatingVote): void => {
  safeEditor.value.danceVote(vote);
  edit.value = true;
};
const onDeleteAlbum = (album: AlbumDetails): void => {
  safeEditor.value.addAlbumProperty(PropertyType.albumField, undefined, album.index!);
};
const onDeleteDance = (dr: DanceRating): void => {
  const tag = Tag.fromParts(danceDB.fromId(dr.danceId)!.name, "Dance");
  safeEditor.value.addProperty(PropertyType.deleteTag, tag.key);
};
const addProperty = (property: SongProperty): void => {
  safeEditor.value.addProperty(property.name, property.value);
};
const hasMeterTag = (numerator: number): boolean => {
  return !!song.value.tags.find((t) => t.key === `${numerator}/4:Tempo`);
};
const onClickLike = (): void => {
  safeEditor.value.toggleLike();
};
const addDance = (danceId?: string, persist?: boolean): void => {
  if (danceId) {
    safeEditor.value.danceVote(new DanceRatingVote(danceId, VoteDirection.Up));

    if (!persist) {
      hide();
    }
    edit.value = true;
  }
};
const updateField = (property: SongProperty): void => {
  safeEditor.value.modifyProperty(property.name, property.value);
};
const onReplaceHistory = (properties: SongProperty[]): void => {
  safeEditor.value.replaceAll(properties);
  edit.value = true;
};
const undoUserChanges = async (): Promise<void> => {
  try {
    if (
      await confirm?.({
        props: {
          title: "Please Confirm",
          body: "Are you sure you want to undo all of your edits to this song?",
          size: "sm",
          buttonSize: "sm",
          okVariant: "danger",
          okTitle: "YES",
          cancelTitle: "NO",
        },
      })
    ) {
      undoForm.value?.submit();
    }
  } catch (err) {
    // eslint-disable-next-line no-console
    console.error(err);
  }
};
const adminUndoUserChanges = (event: Event) => {
  if (event.defaultPrevented) {
    (event.target as HTMLFormElement).submit();
  }
};

const updateSong = (): void => {
  songStore.value = editor.value!.song;
};
const setEdit = (): void => {
  edit.value = true;
};
const addTrack = (track: TrackModel): void => {
  editor.value?.addAlbumFromTrack(track);
};
const onInsertSongProperty = (index: number, prop: string): void => {
  safeEditor.value.insertProperty(index, prop);
};
const leaveWarning = (event: BeforeUnloadEvent): void => {
  if (modified.value) {
    // CHECKIN-TODO: What to do about this???
    event.returnValue = "You have unsaved changes.  Are you sure you want to leave?";
  }
};
const cancelChanges = (): void => {
  editor.value!.revert();
  edit.value = false;
  emit("cancel-changes");
};
const saveChanges = async (): Promise<void> => {
  if (props.creating) {
    await editor.value!.create();
  } else {
    await editor.value!.saveChanges();
  }

  edit.value = false;

  if (props.startEditing) {
    emit("song-saved");
  }
};

const showTagModal = (handler: TagHandler): void => {
  currentTag.value = handler;
  tagModalVisible.value = true;
};

onBeforeMount(() => {
  window.addEventListener("beforeunload", leaveWarning);
});
onBeforeUnmount(() => {
  window.removeEventListener("beforeunload", leaveWarning);
});
</script>

<template>
  <div>
    <BRow>
      <BCol>
        <h1>
          <SongLikeButton
            :user="model.userName"
            :song="song as Song"
            :scale="1"
            :toggle-behavior="true"
            @click-like="onClickLike"
          ></SongLikeButton>
          <i
            ><FieldEditor
              name="Title"
              :value="song.title"
              :editing="editing"
              :is-creator="isCreator"
              role="dbAdmin"
              @update-field="updateField($event)"
            ></FieldEditor
          ></i>
          <span v-if="song.artist" style="font-size: 0.75em; padding-left: 0.5em"> by </span>
          <FieldEditor
            name="Artist"
            :value="song.artist"
            :editing="editing"
            :is-creator="isCreator"
            role="dbAdmin"
            @update-field="updateField($event)"
          >
            <span v-if="song.artist" style="font-size: 0.75em"
              ><a :href="artistLink">{{ song.artist }}</a></span
            >
          </FieldEditor>
        </h1>
      </BCol>
      <BCol v-if="editing || context.canTag" cols="auto">
        <BButton
          v-if="!editing && context.isAdmin"
          variant="outline-danger"
          class="me-1"
          :href="deleteLink"
          >Delete</BButton
        >
        <BButton v-if="editing" variant="outline-primary" class="me-1" @click="cancelChanges"
          >Cancel</BButton
        >
        <BButton v-else variant="outline-primary" class="me-1" @click="setEdit">Edit</BButton>
        <BButton v-show="showSave" variant="primary" @click="saveChanges">{{ saveText }}</BButton>
      </BCol>
    </BRow>
    <BRow class="mb-2">
      <BCol md="4"
        ><PurchaseSection
          :purchase-infos="song.getPurchaseInfos()"
          :filter="model.filter"
        ></PurchaseSection>
      </BCol>
      <BCol md="4">
        <TagListEditor
          :container="song"
          :filter="filter"
          :user="model.userName"
          :editor="editor as SongEditor"
          :edit="edit"
          @edit="setEdit"
          @update-song="updateSong"
          @tag-clicked="showTagModal"
        />
      </BCol>
      <BCol v-if="song.hasSample" md="4">
        <audio controls class="mx-auto">
          <source :src="song.sample" type="audio/mpeg" />
          Your browser does not support audio.
        </audio>
        <CommentEditor
          :container="song"
          :editor="editor as SongEditor"
          :edit="edit"
          :rows="6"
          :placeholder="commentPlaceholder"
        ></CommentEditor>
      </BCol>
    </BRow>
    <BRow class="mb-2">
      <BCol md="4">
        <DanceList
          title="Dances"
          class="mb-2"
          :song="song as Song"
          :dance-ratings="explicitDanceRatings as DanceRating[]"
          :user="model.userName"
          :filter="model.filter"
          :editor="editor as SongEditor"
          :edit="edit"
          @dance-vote="onDanceVote($event)"
          @update-song="updateSong"
          @edit="setEdit"
          @delete-dance="onDeleteDance($event)"
          @tag-clicked="showTagModal"
        />
        <BButton v-if="!!model.userName" v-b-modal.dance-chooser variant="primary"
          >Add Dance Style</BButton
        >
      </BCol>
      <BCol md="auto"
        ><SongStats
          :song="song as Song"
          :editing="editing"
          :is-creator="isCreator"
          @update-field="updateField($event)"
        ></SongStats
        ><BButton
          v-if="hasUserChanges && !editing"
          variant="outline-primary"
          @click="undoUserChanges"
          >Undo My Changes</BButton
        >
        <form
          v-show="false"
          id="undoForm"
          ref="undoForm"
          action="/song/undoUserChanges"
          method="post"
        >
          <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
          <input type="hidden" name="id" :value="song.songId" />
          <input type="hidden" name="filter" :value="model.filter.query" />
        </form>
      </BCol>
    </BRow>
    <BRow>
      <BCol v-if="song.albums"
        ><AlbumList
          :albums="song.albums as AlbumDetails[]"
          :editing="edit"
          @delete-album="onDeleteAlbum($event)"
        ></AlbumList>
        <div v-if="context.isAdmin" class="mt-2">
          <TrackList
            v-if="editing && context.isAdmin"
            :song="song as Song"
            :editing="edit"
            @add-track="addTrack"
            @add-property="addProperty($event)"
          ></TrackList>
          <h3>Admin Edit</h3>
          <BFormTextarea
            id="admin-edit"
            v-model="adminProperties"
            :readonly="!edit"
            rows="3"
            max-rows="6"
          ></BFormTextarea>
          <h3>Undo User Edits</h3>
          <BForm
            v-for="mb in song.modifiedBy"
            :id="`${mb.userName}-undo`"
            :key="mb.userName"
            action="/song/undoUserChanges"
            method="post"
            class="m-1"
            style="display: inline-block"
            novalidate
            @submit="adminUndoUserChanges"
          >
            <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
            <input type="hidden" name="id" :value="song.songId" />
            <input type="hidden" name="userName" :value="mb.userName" />
            <BButton type="submit">{{ mb.userName }}</BButton>
          </BForm>
          <div class="mb-2">
            <h3>Admin Actions</h3>
            <BButton variant="outline-primary" :href="updateServices">Update Services</BButton>
          </div>
          <SongHistoryLog
            v-if="model.songHistory"
            :history="history as SongHistory"
            :editing="editing"
            @delete-property="safeEditor.deleteProperty($event)"
            @promote-property="safeEditor.promoteProperty($event)"
            @move-property-up="safeEditor.movePropertyUp($event)"
            @move-property-down="safeEditor.movePropertyDown($event)"
            @move-property-first="safeEditor.movePropertyFirst($event)"
            @move-property-last="safeEditor.movePropertyLast($event)"
            @insert-property="onInsertSongProperty"
            @replace-history="onReplaceHistory($event)"
          >
          </SongHistoryLog>
        </div>
      </BCol>
      <BCol v-if="model.songHistory">
        <SongHistoryViewer :history="history as SongHistory"> </SongHistoryViewer>
      </BCol>
    </BRow>
    <DanceChooser
      :filter-ids="explicitDanceIds"
      :tempo="song.tempo"
      :numerator="numerator"
      :hide-name-link="true"
      @choose-dance="addDance"
      @update-song="updateSong"
    ></DanceChooser>
    <TagModal v-model="tagModalVisible" :tag-handler="currentTag" />
  </div>
</template>
