<script script setup lang="ts">
import { DanceRatingVote, VoteDirection } from "@/models/DanceRatingDelta";
import { DanceHandler } from "@/models/DanceHandler";
import { DanceRating } from "@/models/DanceRating";
import { Song } from "@/models/Song";
import { SongChange } from "@/models/SongChange";
import { SongEditor } from "@/models/SongEditor";
import { SongFilter } from "@/models/SongFilter";
import { SongHistory } from "@/models/SongHistory";
import { SongSort, SortOrder } from "@/models/SongSort";
import { Tag, TagContext } from "@/models/Tag";
import { TaggableObject } from "@/models/TaggableObject";
import { TagHandler } from "@/models/TagHandler";
import { computed, ref, watch } from "vue";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { useWindowSize } from "@vueuse/core";
import { useModal, type TableFieldRaw } from "bootstrap-vue-next";
import beat10 from "@/assets/images/icons/beat-10.png";
import energy10 from "@/assets/images/icons/Energy-10.png";
import mood10 from "@/assets/images/icons/mood-10.png";
import { formatDate } from "@/helpers/timeHelpers";

const { hide } = useModal();

type SongField = Exclude<TableFieldRaw<SongEditor>, string>;

const actionField: SongField = { key: "action", label: "" };
const editField: SongField = { key: "edit", label: "" };
const playField: SongField = { key: "play" };
const titleField: SongField = { key: "title" };
const artistField: SongField = { key: "artist" };
const trackField: SongField = { key: "track" };
const tempoField: SongField = { key: "tempo", label: "Tempo (BPM)" };
const echoField: SongField = { key: "echo" };
const dancesField: SongField = { key: "dances" };
const tagsField: SongField = { key: "tags" };
const orderField: SongField = { key: "order" };
const userChangeField: SongField = { key: "change", label: "" };
const textField: SongField = { key: "text" };
const infoField: SongField = { key: "info" };
const lengthField: SongField = { key: "length" };

const context = getMenuContext();

const props = defineProps<{
  histories: SongHistory[];
  filter: SongFilter;
  hideSort?: boolean;
  hiddenColumns?: string[];
  action?: string;
  showHistory?: boolean;
}>();

const emit = defineEmits<{
  "song-selected": [songId: string, selected: boolean];
}>();

const tagModalVisible = ref(false);
const currentTag = ref<TagHandler>(new TagHandler({ tag: Tag.fromString("Placeholder:Other") }));

const danceModalVisible = ref(false);
const currentDance = ref<DanceHandler>(
  new DanceHandler({
    danceRating: new DanceRating({ danceId: "SWZ", weight: 0 }),
    tag: Tag.fromString("Placeholder:Other"),
    user: context.userName,
  }),
);

const playModalVisible = ref(false);
const likeModalVisible = ref(false);
const orderModalVisible = ref(false);
const addDanceModalVisible = ref(false);

const currentSong = ref<SongEditor>(new SongEditor());

const filter = computed(() => props.filter);
const userQuery = filter.value.userQuery;

const singleDance = computed(() =>
  filter.value.singleDance ? filter.value.danceQuery?.danceList[0] : undefined,
);

// Get the current dance name for the dance-specific tags column header
const currentDanceName = computed(() => {
  const danceId = singleDance.value;
  return danceId ? (safeDanceDatabase().fromId(danceId)?.name ?? "") : "";
});

// Update field labels based on context
const tagsFieldWithLabel = computed((): SongField => {
  const hasDances = !!filter.value.dances;
  return {
    key: "tags",
    label: hasDances ? "Song Tags" : "Tags",
  };
});

const danceTagsFieldWithLabel = computed(
  (): SongField => ({
    key: "danceTags",
    label: `${currentDanceName.value} Tags`,
  }),
);

const buildEditor = (history: SongHistory) => {
  const userId = context.userId;
  const userName = context.userName;
  const axiosXsrf = context.axiosXsrf;
  if (userId && userName) {
    return new SongEditor(axiosXsrf, userName, history.Deanonymize(userName, userId));
  } else {
    return new SongEditor(axiosXsrf, userName, history);
  }
};

const buildEditors = (histories: SongHistory[]) => {
  return histories.map((h) => buildEditor(h));
};

const histories = ref<SongHistory[]>(props.histories.map((h) => new SongHistory(h)));

watch(
  () => props.histories,
  (newHistories) => {
    console.log("Updating histories");
    histories.value = newHistories.map((h) => new SongHistory(h));
  },
);

const songs = computed(() => {
  return buildEditors(histories.value as SongHistory[]);
});

const isHidden = (column: string): boolean => {
  const hidden = props.hiddenColumns;
  const col = column.toLowerCase();
  return !!hidden && !!hidden.find((c) => c.toLowerCase() === col);
};

const hasUser = !!userQuery?.userName && userQuery.include;

const filterSmallField = (field: SongField): SongField => {
  if (field === infoField && isHidden("dances")) {
    return tagsField;
  } else {
    return field;
  }
};

const filterHiddenFields = (fields: SongField[]): SongField[] => {
  const hidden = props.hiddenColumns;
  return hidden ? fields.filter((f) => !isHidden(f.key)) : fields;
};

const shouldShowDanceTags = computed(() => {
  // Check if we should show dance-specific tags
  return filter.value.singleDance && !props.hiddenColumns?.includes("danceTags");
});

const smallFields = filterHiddenFields([textField, infoField].map((f) => filterSmallField(f)));

const fullFields = computed(() => {
  const fields = [
    ...(context.isAdmin && !isHidden(editField.key) ? [editField] : []),
    playField,
    titleField,
    artistField,
    trackField,
    tempoField,
    lengthField,
    echoField,
    dancesField,
    tagsFieldWithLabel.value,
    // Add dance tags column only when we have a specific dance filter
    ...(shouldShowDanceTags.value ? [danceTagsFieldWithLabel.value] : []),
    props.showHistory || hasUser ? userChangeField : orderField,
  ];

  const filtered = filterHiddenFields(fields);
  return filtered;
});

const { width: windowWidth } = useWindowSize();

const isSmall = computed(() => windowWidth.value < 992);

const fields = computed(() => {
  const baseFields = isSmall.value ? smallFields : fullFields.value;
  return props.action ? [actionField, ...baseFields] : baseFields;
});

const likeHeaderClasses = computed(() => {
  return filter.value.singleDance ? ["likeDanceHeader"] : ["likeHeader"];
});

const tagsLabel = computed(() => {
  return filter.value.singleDance && !isSmall.value ? "Song Tags" : "Tags";
});

const titleHeaderSortTip = "Song Title: Click to sort alphabetically by title";
const titleHeaderCurrentTip = "Click on the song title to view the song details";
const artistHeaderSortTip = "Artist: Click to sort alphabetically by artist";
const artistHeaderCurrentTip = "Click on the artist name to view a list of songs by that artist";
const tempoHeaderSortTip = "Click to sort numerically by tempo";
const tempoHeaderCurrentTip =
  "Tempo in beats per minute (BPM) - click a tempo to view dances danced to that tempo.";
const lengthHeaderCurrentTip = "Duration of Song in seconds";
const beatCurrentTip = "Strength of the beat (fuller icons represent a stronger beat).";
const beatSortTip = "Click to sort by strength of the beat.";
const energyCurrentTip = "Energy of the song (fuller icons represent a higher energy).";
const energySortTip = "Click to sort by energy.";
const moodCurrentTip = "Mood of the song (fuller icons represent a happier mood).";
const moodSortTip = "Click to sort by mood.";
const orderString = filter.value.sort?.id;
const orderType = orderString ? (orderString as SortOrder) : SortOrder.Match;
const echoClass =
  orderString === "Mood" || orderString === "Beat" || orderString === "Energy"
    ? ["sortedEchoHeader"]
    : ["echoHeader"];
const dancesHeaderSortTip = "Dance: Click to sort by dance rating";
const dancesHeaderCurrentTip =
  "A list of dances for the song. Click on a dance to view more actions and to vote on the dance for the song.";
const tagsHeaderCurrentTip =
  "A list of tags associated with the song. Click on a tag to filter by it.";
const trackHeaderCurrentTip = "The track number of the song in the album.";
const orderHeaderSortTip = "Click to choose chronological sort order.";
const orderHeaderCurrentTip = (() => {
  switch (orderType) {
    default:
      return "Values represent last modified date";
    case SortOrder.Modified:
      return "Sorted by last modified date";
    case SortOrder.Created:
      return "Sorted by creation date";
    case SortOrder.Edited:
      return "Sorted by last edited date";
  }
})();

const filterUser = (() => {
  const user = hasUser ? userQuery?.userName : "";
  return user === "me" ? context.userName : user;
})();

const filterDisplayName = (() => {
  const user = hasUser ? userQuery?.displayName : "";
  return user === "me" ? context.userName! : user;
})();

const changeHeader = props.showHistory ? "Latest Changes" : `${filterDisplayName}'s Changes`;

const sortOrder = filter.value.sort ?? new SongSort("Modified");

const sortableDances = !props.hideSort && filter.value.singleDance;

const getUserChange = (history: SongHistory): SongChange | undefined => {
  if (props.showHistory) {
    return sortOrder?.id == SortOrder.Comments ? history.latestComment() : history.latestChange();
  } else if (filterUser) {
    const user = filterUser;
    return history.recentUserChange(user);
  }
};

const songRef = (song: Song): string => {
  return `/song/details/${song.songId}?filter=${filter.value.encodedQuery}`;
};

const artistRef = (song: Song): string => {
  return `/song/artist/?name=${encodeURIComponent(song.artist)}`;
};

const tempoRef = (song: Song): string => {
  return `/home/counter?numerator=4&tempo=${song.tempo}`; // TODO: smart numerator?
};

const tempoValue = (song: Song): string => {
  const tempo = song.tempo;
  return tempo && !isHidden("tempo") ? `${Math.round(tempo)}` : "";
};

const lengthValue = (song: Song): string => {
  const length = song.length;
  return length && !isHidden("length") ? length.toString() : "";
};

const danceTags = (song: Song): Tag[] => {
  return song.tags.filter(
    (t) =>
      !t.value.startsWith("!") && !t.value.startsWith("-") && t.category.toLowerCase() === "dance",
  );
};

const dances = (song: Song): Tag[] => {
  return danceTags(song).filter((t) => song.findDanceRatingByName(t.value));
};

const orderTip = (song: Song): string => {
  switch (orderType) {
    default:
    case SortOrder.Modified:
      return `Last Modified ${formatDate(song.modified)} (${song.modifiedOrderVerbose} ago)`;
    case SortOrder.Created:
      return `Added ${formatDate(song.created)} (${song.createdOrderVerbose} ago)`;
    case SortOrder.Edited:
      return `Last Edited ${formatDate(song.edited)} (${song.editedOrderVerbose} ago)`;
  }
};

const orderValue = (song: Song): string => {
  switch (orderType) {
    default:
    case SortOrder.Modified:
      return song.modifiedOrder;
    case SortOrder.Created:
      return song.createdOrder;
    case SortOrder.Edited:
      return song.editedOrder;
  }
};

const danceHandler = (tag: Tag, filter: SongFilter, editor: SongEditor): DanceHandler => {
  const song = editor.song;
  const danceRating = song.findDanceRatingByName(tag.value);
  return new DanceHandler({
    danceRating: danceRating!,
    tag,
    user: context.userName,
    filter,
    parent: song,
    editor,
  });
};

const tagHandler = (
  tag: Tag,
  filter?: SongFilter,
  parent?: TaggableObject,
  danceId?: string,
): TagHandler => {
  return new TagHandler({
    tag: tag,
    user: userQuery?.userName,
    filter,
    parent,
    context: danceId ? [TagContext.Song, TagContext.Dance] : [TagContext.Song], // Default to both song and dance_ALL
    danceId,
  });
};

const handlers = (song: Song): TagHandler[] => {
  return song.tags
    .filter((t) => !t.value.startsWith("!") && t.category.toLowerCase() !== "dance")
    .map((t) => tagHandler(t, filter.value, song));
};

const danceSpecificHandlers = (song: Song): TagHandler[] => {
  const danceId = singleDance.value;
  const danceRating = danceId ? song.findDanceRatingById(danceId) : undefined;
  const emptyFilter = new SongFilter();

  return danceRating?.tags.map((t) => tagHandler(t, emptyFilter, song, danceId)) ?? [];
};

const allHandlers = (song: Song): TagHandler[] => {
  return [
    ...handlers(song),
    ...(shouldShowDanceTags.value && isSmall.value ? danceSpecificHandlers(song) : []),
  ];
};

const trackNumber = (song: Song): string => {
  return song.albums?.[0]?.track?.toString() ?? "";
};

const findEditor = (songId: string): SongEditor | undefined => {
  return songs.value.find((e) => e.song.songId === songId);
};

const onSelect = (song: Song, selected: boolean): void => {
  emit("song-selected", song.songId, selected);
};

const onAction = (song: Song): void => {
  emit("song-selected", song.songId, true);
};

const onChronOrderChanged = (order: SortOrder): void => {
  const f = filter.value.clone();
  f.sortOrder = order;
  window.location.href = `/song/filterSearch?filter=${f.encodedQuery}`;
};

const showTagModal = (handler: TagHandler): void => {
  currentTag.value = handler;
  tagModalVisible.value = true;
};

const showDanceTagModal = (handler: TagHandler): void => {
  // Create a dance-specific version of the tag handler
  const danceSpecificHandler = new TagHandler({
    tag: handler.tag,
    user: handler.user,
    filter: handler.filter,
    parent: handler.parent,
    context: TagContext.Dance, // Dance context for dance-specific filtering
    danceId: currentDance.value.danceRating?.danceId, // Include the specific dance ID
  });

  currentTag.value = danceSpecificHandler;
  tagModalVisible.value = true;
};

const showDanceModal = (song: SongEditor, handler: DanceHandler): void => {
  currentSong.value = song;
  currentDance.value = handler;
  danceModalVisible.value = true;
};

const showPlayModal = (songId: string): void => {
  currentSong.value = findEditor(songId)!;
  playModalVisible.value = true;
};

const showLikeModal = (songId: string): void => {
  currentSong.value = findEditor(songId)!;
  likeModalVisible.value = true;
};

const showAddDanceModal = (songId: string): void => {
  const editor = findEditor(songId)!;
  currentSong.value = editor;
  addDanceModalVisible.value = true;
};

// INT-TODO: We should consider warning the user before removing the song from the list
//  Or throwing up a toast to click to get it back
const onDanceVote = (editor: SongEditor, vote: DanceRatingVote): void => {
  const idx = histories.value.findIndex((h) => h.id === editor.songId);
  const local = buildEditor(histories.value[idx] as SongHistory);
  local.danceVote(vote);
  const history = local.editHistory;
  const remove =
    !local.song.findDanceRatingById(vote.danceId) &&
    filter.value.singleDance &&
    filter.value.danceQuery.danceList[0] == vote.danceId;
  onEditSong(history, remove);
};

const onAddDance = (danceId?: string, persist?: boolean): void => {
  if (danceId) {
    onDanceVote(currentSong.value as SongEditor, new DanceRatingVote(danceId, VoteDirection.Up));
  }
  if (!persist) {
    hide();
  }
};

const onEditSong = (history: SongHistory, remove: boolean = false): void => {
  const idx = histories.value.findIndex((h) => h.id === history.id);
  if (idx === -1 || !songs.value[idx]) {
    console.error("Couldn't find editor for history", history);
    return;
  }

  songs.value[idx]
    .appendHistory(history)
    .then(() => {
      console.log("Updated history", history);
    })
    .catch((e) => {
      console.error("Error updating history", e);
    });
  if (remove) {
    histories.value.splice(idx, 1);
  } else {
    histories.value[idx] = songs.value[idx].history;
  }
};
</script>

<template>
  <div>
    <BTable
      striped
      hover
      no-local-sorting
      sort-icon-left
      borderless
      :items="songs"
      :fields="fields"
    >
      <template #cell(edit)="data">
        <BFormCheckbox @change="onSelect(data.item.song, $event)" />
      </template>
      <template #cell(action)="data">
        <BButton @click="onAction(data.item.song)">{{ action }}</BButton>
      </template>
      <template #head(play)>
        <div :class="likeHeaderClasses">Like/Play</div>
      </template>
      <template #cell(play)="data">
        <PlayCell
          :editor="data.item"
          :filter="filter"
          @show-like="showLikeModal"
          @show-play="showPlayModal"
          @dance-vote="onDanceVote(data.item, $event)"
        />
      </template>
      <template #head(title)>
        <SortableHeader
          id="Title"
          :sort-tip="titleHeaderSortTip"
          :current-tip="titleHeaderCurrentTip"
          :enable-sort="!hideSort"
          :filter="filter"
        />
      </template>
      <template #cell(title)="data">
        <a :href="songRef(data.item.song)">{{ data.item.song.title }}</a>
      </template>
      <template #head(artist)>
        <SortableHeader
          id="Artist"
          :sort-tip="artistHeaderSortTip"
          :current-tip="artistHeaderCurrentTip"
          :enable-sort="!hideSort"
          :filter="filter"
        />
      </template>
      <template #cell(artist)="data">
        <a :href="artistRef(data.item.song)">{{ data.item.song.artist }}</a>
      </template>
      <template #head(track)>
        <SortableHeader
          id="Track"
          :sort-tip="artistHeaderSortTip"
          :current-tip="trackHeaderCurrentTip"
          :enable-sort="false"
          :filter="filter"
        />
      </template>
      <template #cell(track)="data">
        {{ trackNumber(data.item.song) }}
      </template>
      <template #head(tempo)>
        <SortableHeader
          id="Tempo"
          title="Tempo (BPM)"
          :sort-tip="tempoHeaderSortTip"
          :current-tip="tempoHeaderCurrentTip"
          :enable-sort="!hideSort"
          :filter="filter"
        />
      </template>
      <template #cell(tempo)="data">
        <a :href="tempoRef(data.item.song)">{{ tempoValue(data.item.song) }}</a>
      </template>
      <template #head(length)>
        <SortableHeader
          id="Length"
          title="Length"
          :current-tip="lengthHeaderCurrentTip"
          :enable-sort="false"
          :filter="filter"
        />
      </template>
      <template #cell(length)="data">
        {{ lengthValue(data.item.song) }}
      </template>
      <template #head(echo)>
        <div :class="echoClass">
          <SortableHeader
            id="Beat"
            :sort-tip="beatSortTip"
            :current-tip="beatCurrentTip"
            :enable-sort="!hideSort"
            :filter="filter"
          >
            <img :src="beat10" width="25" height="25" />
          </SortableHeader>
          <SortableHeader
            id="Energy"
            :sort-tip="energySortTip"
            :current-tip="energyCurrentTip"
            :enable-sort="!hideSort"
            :filter="filter"
          >
            <img :src="energy10" width="25" height="25" />
          </SortableHeader>
          <SortableHeader
            id="Mood"
            :sort-tip="moodSortTip"
            :current-tip="moodCurrentTip"
            :enable-sort="!hideSort"
            :filter="filter"
          >
            <img :src="mood10" width="25" height="25" />
          </SortableHeader>
        </div>
      </template>
      <template #cell(echo)="data">
        <EchoIcon
          :value="data.item.song.danceability"
          type="beat"
          label="beat strength"
          max-label="strongest beat"
        />
        <EchoIcon
          :value="data.item.song.energy"
          type="energy"
          label="energy level"
          max-label="highest energy"
        />
        <EchoIcon
          :value="data.item.song.valence"
          type="mood"
          label="mood level"
          max-label="happiest"
        />
      </template>
      <template #head(dances)>
        <SortableHeader
          id="Dances"
          :enable-sort="sortableDances"
          :current-tip="dancesHeaderCurrentTip"
          :sort-tip="dancesHeaderSortTip"
          :filter="filter"
        />
      </template>
      <template #cell(dances)="data">
        <DanceButton
          v-for="tag in dances(data.item.song)"
          :key="tag.key"
          :dance-handler="danceHandler(tag, filter, data.item)"
          @dance-clicked="showDanceModal(data.item, $event)"
          @dance-vote="onDanceVote(data.item, $event)"
        />
        <BButton
          v-if="!!context.userName"
          title="Add Dance"
          variant="dance"
          size="sm"
          style="margin-inline-end: 0.25em"
          @click="showAddDanceModal(data.item.song.id)"
        >
          <IBiPlusCircle />
        </BButton>
      </template>
      <template #head(tags)>
        <SortableHeader
          id="Tags"
          :title="tagsLabel"
          :enable-sort="false"
          :current-tip="tagsHeaderCurrentTip"
          :filter="filter"
        />
      </template>
      <template #cell(tags)="data">
        <TagButton
          v-for="handler in allHandlers(data.item.song)"
          :key="handler.tag.key"
          :tag-handler="handler"
          @tag-clicked="showTagModal"
        />
      </template>
      <template #cell(danceTags)="data">
        <TagButton
          v-for="handler in danceSpecificHandlers(data.item.song)"
          :key="handler.tag.key"
          :tag-handler="handler"
          @tag-clicked="showTagModal"
        />
      </template>
      <template #head(order)>
        <SortableHeader
          id="Order"
          class="orderHeader"
          :current-tip="orderHeaderCurrentTip"
          :sort-tip="orderHeaderSortTip"
          :filter="filter"
          :enable-sort="!hideSort"
          :custom-sort="true"
          @click="
            console.log('order clicked');
            if (!hideSort) {
              console.log('show modal');
              orderModalVisible = true;
            }
          "
          ><OrderIcon :order="orderType"
        /></SortableHeader>
      </template>
      <template #cell(order)="data">
        <BLink :id="`order_tip__${data.item.song.songId}`" underline-variant="light">{{
          orderValue(data.item.song)
        }}</BLink>
        <BTooltip
          :target="`order_tip__${data.item.song.songId}`"
          placement="left"
          :delay="{ show: 0, hide: 100 }"
          :auto-hide="false"
          >{{ orderTip(data.item.song) }}</BTooltip
        >
      </template>
      <template #head(change)>
        <div class="changeHeader">{{ changeHeader }}</div>
      </template>
      <template #cell(change)="data">
        <SongChangeViewer
          v-if="getUserChange(data.item.history)"
          :change="getUserChange(data.item.history)!"
          :one-user="!showHistory"
          @dance-clicked="showDanceModal(data.item, $event)"
          @tag-clicked="showTagModal"
        />
      </template>
      <template #head(text)>
        <SortableHeader
          id="Title"
          :current-tip="titleHeaderCurrentTip"
          :sort-tip="titleHeaderSortTip"
          :enable-sort="!hideSort"
          :filter="filter"
        />
        <template v-if="!isHidden('artist')">
          -
          <SortableHeader
            id="Artist"
            :current-tip="artistHeaderCurrentTip"
            :sort-tip="artistHeaderSortTip"
            :enable-sort="!hideSort"
            :filter="filter"
          />
        </template>
      </template>
      <template #cell(text)="data">
        <PlayCell
          :editor="data.item"
          :filter="filter"
          @show-like="showLikeModal"
          @show-play="showPlayModal"
          @dance-vote="onDanceVote(data.item, $event)"
        />
        <a :href="songRef(data.item.song)" class="ms-1">{{ data.item.song.title }}</a>
        <template v-if="!isHidden('artist')">
          by
          <a :href="artistRef(data.item.song)">{{ data.item.song.artist }}</a>
        </template>
        <span v-if="tempoValue(data.item.song)">
          @
          <a :href="tempoRef(data.item.song)">{{ tempoValue(data.item.song) }} BPM</a>
        </span>
        <span v-if="lengthValue(data.item.song) && !isHidden('length')">
          - {{ lengthValue(data.item.song) }}s
        </span>
        <SongChangeViewer
          v-if="(hasUser || showHistory) && getUserChange(data.item.history)"
          :change="getUserChange(data.item.history)!"
          :one-user="false"
          @dance-clicked="showDanceModal(data.item, $event)"
          @tag-clicked="showTagModal"
        />
      </template>
      <template #head(info)>
        <SortableHeader
          id="Dances"
          :current-tip="dancesHeaderCurrentTip"
          :sort-tip="dancesHeaderSortTip"
          :enable-sort="sortableDances"
          :filter="filter"
        />
        -
        <SortableHeader
          id="Tags"
          :enable-sort="false"
          :current-tip="tagsHeaderCurrentTip"
          :filter="filter"
        />
      </template>
      <template #cell(info)="data">
        <DanceButton
          v-for="tag in dances(data.item.song)"
          :key="tag.key"
          :dance-handler="danceHandler(tag, filter, data.item)"
          @dance-clicked="showDanceModal(data.item, $event)"
          @dance-vote="onDanceVote(data.item, $event)"
        />
        <TagButton
          v-for="handler in handlers(data.item.song)"
          :key="handler.tag.key"
          :tag-handler="handler"
          @tag-clicked="showTagModal"
        />
      </template>
    </BTable>
    <ChronModal
      v-model="orderModalVisible"
      :order="orderType"
      @update:order="onChronOrderChanged($event)"
    />
    <TagModal v-model="tagModalVisible" :tag-handler="currentTag as TagHandler" />
    <DanceModal
      v-model="danceModalVisible"
      :dance-handler="currentDance as DanceHandler"
      @dance-vote="onDanceVote(currentSong as SongEditor, $event)"
      @tag-clicked="showDanceTagModal"
    />
    <LikeModal
      v-model="likeModalVisible"
      :editor="currentSong as SongEditor"
      @edit-song="onEditSong"
    />
    <PlayModal v-model="playModalVisible" :song="currentSong.song as Song" />
    <DanceChooser
      id="add-dance-chooser"
      v-model="addDanceModalVisible"
      :filter-ids="currentSong.song.explicitDanceIds"
      :tempo="currentSong.song.tempo"
      :numerator="currentSong.song.numerator"
      :hide-name-link="true"
      @choose-dance="onAddDance"
    />
  </div>
</template>

<style scoped lang="scss">
.editHeader {
  min-width: 4em;
}

.likeHeader {
  min-width: 4em;
}

.likeDanceHeader {
  min-width: 6em;
}

.echoHeader {
  min-width: 75px;
}

.sortedEchoHeader {
  min-width: 100px;
}

.orderHeader {
  min-width: 3em;
}

.changeHeader {
  min-width: 12em;
}
</style>
