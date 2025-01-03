<script script setup lang="ts">
import { DanceRatingVote } from "@/models/DanceRatingDelta";
import { DanceHandler } from "@/models/DanceHandler";
import { DanceRating } from "@/models/DanceRating";
import { Song } from "@/models/Song";
import { SongChange } from "@/models/SongChange";
import { SongEditor } from "@/models/SongEditor";
import { SongFilter } from "@/models/SongFilter";
import { SongHistory } from "@/models/SongHistory";
import { SongSort, SortOrder } from "@/models/SongSort";
import { Tag } from "@/models/Tag";
import { TaggableObject } from "@/models/TaggableObject";
import { TagHandler } from "@/models/TagHandler";
import { computed, ref, watch } from "vue";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { useWindowSize } from "@vueuse/core";
import type { TableFieldRaw } from "bootstrap-vue-next";
import beat10 from "@/assets/images/icons/beat-10.png";
import energy10 from "@/assets/images/icons/Energy-10.png";
import mood10 from "@/assets/images/icons/mood-10.png";

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
const currentTag = ref<TagHandler>(
  new TagHandler(Tag.fromString("Placeholder:Other"), undefined, undefined, undefined, true),
);

const danceModalVisible = ref(false);
const currentDance = ref<DanceHandler>(
  new DanceHandler(
    new DanceRating({ danceId: "SWZ", weight: 0 }),
    Tag.fromString("Placeholder:Other"),
  ),
);

const playModalVisible = ref(false);
const likeModalVisible = ref(false);
const currentSong = ref<SongEditor>(new SongEditor());

const filter = props.filter;
const userQuery = filter.userQuery;

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

const smallFields = filterHiddenFields([textField, infoField].map((f) => filterSmallField(f)));

const fullFields = filterHiddenFields([
  ...(context.isAdmin && !isHidden(editField.key) ? [editField] : []),
  playField,
  titleField,
  artistField,
  trackField,
  tempoField,
  lengthField,
  echoField,
  dancesField,
  tagsField,
  props.showHistory || hasUser ? userChangeField : orderField,
]);

const { width: windowWidth } = useWindowSize();

const fields = computed(() => {
  const baseFields = windowWidth.value >= 992 ? fullFields : smallFields;
  return props.action ? [actionField, ...baseFields] : baseFields;
});

const likeHeader = computed(() => {
  return filter.singleDance ? ["likeDanceHeader"] : ["likeHeader"];
});

const titleHeaderTip = "Song Title: Click to sort alphabetically by title";
const artistHeaderTip = "Artist: Click to sort alphabetically by artist";
const tempoHeaderTip = "Tempo (Beats Per Minute): Click to sort numerically by tempo";
const lengthHeaderTip = "Duration of Song in seconds";
const beatTip =
  "Strength of the beat (fuller icons represent a stronger beat). Click to sort by strength of the beat.";
const energyTip =
  "Energy of the song (fuller icons represent a higher energy). Click to sort by energy.";
const moodTip = "Mood of the song (fuller icons represent a happier mood). Click to sort by mood.";
const orderString = filter.sort?.id;
const orderType = orderString ? (orderString as SortOrder) : SortOrder.Match;
const echoClass =
  orderString === "Mood" || orderString === "Beat" || orderString === "Energy"
    ? ["sortedEchoHeader"]
    : ["echoHeader"];
const dancesHeaderTip = "Dance: Click to sort by dance rating";
const orderHeaderTip = `Click to sort chronologically`;

const filterUser = (() => {
  const user = hasUser ? userQuery?.userName : "";
  return user === "me" ? context.userName : user;
})();

const filterDisplayName = (() => {
  const user = hasUser ? userQuery?.displayName : "";
  return user === "me" ? context.userName! : user;
})();

const changeHeader = props.showHistory ? "Latest Changes" : `${filterDisplayName}'s Changes`;

const sortOrder = filter.sort ?? new SongSort("Modified");

const sortableDances = !props.hideSort && filter.singleDance;

const getUserChange = (history: SongHistory): SongChange | undefined => {
  if (props.showHistory) {
    return sortOrder?.id == SortOrder.Comments ? history.latestComment() : history.latestChange();
  } else if (filterUser) {
    const user = filterUser;
    return history.recentUserChange(user);
  }
};

const songRef = (song: Song): string => {
  return `/song/details/${song.songId}?filter=${filter.encodedQuery}`;
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
    case SortOrder.Modified:
      return `Last Modified ${song.modified} (${song.modifiedOrderVerbose} ago)`;
    case SortOrder.Created:
      return `Added ${song.created} (${song.createdOrderVerbose} ago)`;
    case SortOrder.Edited:
      return `Last Edited ${song.edited} (${song.editedOrderVerbose} ago)`;
    default:
      return `Error: SongId(${song.songId})`;
  }
};

const orderValue = (song: Song): string => {
  switch (orderType) {
    case SortOrder.Modified:
      return song.modifiedOrder;
    case SortOrder.Created:
      return song.createdOrder;
    case SortOrder.Edited:
      return song.editedOrder;
    default:
      return `Error: SongId(${song.songId})`;
  }
};

const danceHandler = (tag: Tag, filter: SongFilter, editor: SongEditor): DanceHandler => {
  const song = editor.song;
  const danceRating = song.findDanceRatingByName(tag.value);
  return new DanceHandler(danceRating!, tag, userQuery?.userName, filter, song, editor);
};

const tagHandler = (tag: Tag, filter?: SongFilter, parent?: TaggableObject): TagHandler => {
  return new TagHandler(tag, userQuery?.userName, filter, parent);
};

const tags = (song: Song): Tag[] => {
  return song.tags.filter((t) => !t.value.startsWith("!") && t.category.toLowerCase() !== "dance");
};

const trackNumber = (song: Song): string => {
  return song.albums && song.albums.length > 0 && song.albums[0].track
    ? song.albums[0].track.toString()
    : "";
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
  const f = filter.clone();
  f.sortOrder = order;
  window.location.href = `/song/filterSearch?filter=${f.encodedQuery}`;
};

const showTagModal = (handler: TagHandler): void => {
  currentTag.value = handler;
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

// INT-TODO: We should consider warning the user before removing the song from the list
//  Or throwing up a toast to click to get it back
const onDanceVote = (editor: SongEditor, vote: DanceRatingVote): void => {
  const idx = histories.value.findIndex((h) => h.id === editor.songId);
  const local = buildEditor(histories.value[idx] as SongHistory);
  local.danceVote(vote);
  const history = local.editHistory;
  const remove =
    !local.song.findDanceRatingById(vote.danceId) &&
    filter.singleDance &&
    filter.danceQuery.danceList[0] == vote.danceId;
  onEditSong(history, remove);
};

const onEditSong = (history: SongHistory, remove: boolean = false): void => {
  const idx = histories.value.findIndex((h) => h.id === history.id);
  if (idx === -1) {
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
        <div :class="likeHeader">Like/Play</div>
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
          :tip="titleHeaderTip"
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
          :tip="artistHeaderTip"
          :enable-sort="!hideSort"
          :filter="filter"
        />
      </template>
      <template #cell(artist)="data">
        <a :href="artistRef(data.item.song)">{{ data.item.song.artist }}</a>
      </template>
      <template #cell(track)="data">
        {{ trackNumber(data.item.song) }}
      </template>
      <template #head(tempo)>
        <SortableHeader
          id="Tempo"
          title="Tempo (BPM)"
          :tip="tempoHeaderTip"
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
          :tip="lengthHeaderTip"
          :enable-sort="false"
          :filter="filter"
        />
      </template>
      <template #cell(length)="data">
        {{ lengthValue(data.item.song) }}
      </template>
      <template #head(echo)>
        <div :class="echoClass">
          <SortableHeader id="Beat" :tip="beatTip" :enable-sort="!hideSort" :filter="filter">
            <img :src="beat10" width="25" height="25" />
          </SortableHeader>
          <SortableHeader id="Energy" :tip="energyTip" :enable-sort="!hideSort" :filter="filter">
            <img :src="energy10" width="25" height="25" />
          </SortableHeader>
          <SortableHeader id="Mood" :tip="moodTip" :enable-sort="!hideSort" :filter="filter">
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
          :tip="dancesHeaderTip"
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
      </template>
      <template #cell(tags)="data">
        <TagButton
          v-for="tag in tags(data.item.song)"
          :key="tag.key"
          :tag-handler="tagHandler(tag, filter, data.item.song)"
          @tag-clicked="showTagModal"
        />
      </template>
      <template #head(order)>
        <div class="orderHeader">
          <OrderIcon v-if="hideSort" :order="orderType" />
          <OrderIcon
            v-else
            v-b-modal.chron-modal
            v-b-tooltip.hover.blur="{ title: orderHeaderTip, id: 'order_tip' }"
            :order="orderType"
          />
          <SortIcon v-if="!hideSort" type="" :direction="filter.sort.direction" />
        </div>
      </template>
      <template #cell(order)="data">
        <span
          v-b-tooltip.hover.click.topleft="{
            title: orderTip(data.item.song),
            id: `order_tip__${data.item.song.songId}`,
          }"
          >{{ orderValue(data.item.song) }}</span
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
          :tip="titleHeaderTip"
          :enable-sort="!hideSort"
          :filter="filter"
        />
        <template v-if="!isHidden('artist')">
          -
          <SortableHeader
            id="Artist"
            :tip="artistHeaderTip"
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
          :tip="dancesHeaderTip"
          :enable-sort="sortableDances"
          :filter="filter"
        />
        - Tags
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
          v-for="tag in tags(data.item.song)"
          :key="tag.key"
          :tag-handler="tagHandler(tag, filter, data.item.song)"
          @tag-clicked="showTagModal"
        />
      </template>
    </BTable>
    <ChronModal :order="orderType" @update:order="onChronOrderChanged($event)" />
    <TagModal v-model="tagModalVisible" :tag-handler="currentTag" />
    <DanceModal
      v-model="danceModalVisible"
      :dance-handler="currentDance"
      @dance-vote="onDanceVote(currentSong as SongEditor, $event)"
      @tag-clicked="showTagModal"
    />
    <LikeModal
      v-model="likeModalVisible"
      :editor="currentSong as SongEditor"
      @edit-song="onEditSong"
    />
    <PlayModal v-model="playModalVisible" :song="currentSong.song as Song" />
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
