<script setup lang="ts">
import { DanceRatingVote } from "@/models/DanceRatingDelta";
import { SongEditor } from "@/models/SongEditor";
import { computed, ref } from "vue";
import type { DanceRating } from "@/models/DanceRating";
import type { SongHistory } from "@/models/SongHistory";

// eslint-disable-next-line vue/valid-define-props
const props = defineProps<{
  editor: SongEditor;
}>();

const emit = defineEmits<{
  "edit-song": [history: SongHistory];
}>();

const instance = ref<SongEditor | null>(null);

const id = `like-${props.editor.songId}`;
const like = computed(() => instance.value?.likeState ?? undefined);
const favoritesText = computed(() =>
  like.value === true ? "In your Favorites" : "Add to Favorites",
);
const blockedText = computed(() =>
  like.value === false ? "In your Blocked List" : "Add to Blocked List",
);
const removeText = computed(() =>
  like.value === true
    ? "Remove from Favorites"
    : like.value === false
      ? "Remove from Blocked"
      : "Not in either list",
);
// INT-TODO: Don't know why we need the explicit cast here
const danceRatings = computed(() => (instance.value?.song.danceRatings ?? []) as DanceRating[]);
const changed = computed(() => instance.value?.modified ?? false);

const getDanceVote = (danceId: string): boolean | undefined => {
  return instance.value?.song.danceVote(danceId) ?? undefined;
};

const setDanceVote = (vote: DanceRatingVote): void => {
  instance.value?.danceVote(vote);
};

const setLike = (value: boolean | null): void => {
  instance.value?.setLike(value);
};

const resetModal = (): void => {
  instance.value = props.editor.clone();
};

const onSave = (): void => {
  if (changed.value) {
    const changes = props.editor.getExternalChanges(instance.value! as SongEditor);
    // INT-TODO: Don't know why we need the explicit cast here
    emit("edit-song", changes);
  }
};
</script>

<template>
  <BModal
    :id="id"
    :title="editor.song.title"
    header-bg-variant="primary"
    header-text-variant="light"
    size="sm"
    :ok-disabled="!changed"
    @show="resetModal"
    @ok="onSave"
  >
    <template #modal-title>
      {{ editor.song.title }}
    </template>
    <BContainer fluid>
      <BRow class="mb-1"><BCol>Favorites/Blocked Lists:</BCol></BRow>
      <BRow>
        <BCol cols="3" align-self="center">
          <LikeIcon :state="like" :scale="2" />
        </BCol>
        <BCol>
          <BButtonGroup vertical class="ms-2">
            <BButton
              block
              variant="outline-primary"
              size="sm"
              :pressed="like === true"
              @click="setLike(true)"
              >{{ favoritesText }}</BButton
            >
            <BButton
              block
              variant="outline-primary"
              size="sm"
              :pressed="like === false"
              @click="setLike(false)"
              >{{ blockedText }}</BButton
            >
            <BButton
              block
              variant="outline-primary"
              size="sm"
              :pressed="like === undefined || like === null"
              @click="setLike(null)"
              >{{ removeText }}</BButton
            >
          </BButtonGroup>
        </BCol>
      </BRow>
      <BRow
        ><BCol><hr /></BCol
      ></BRow>
      <BRow
        ><BCol><div class="mb-2">Vote on Dancability by Style:</div></BCol>
      </BRow>
      <BRow
        ><BCol>
          <DanceVoteItem
            v-for="rating in danceRatings"
            :key="rating.danceId"
            :rating="rating"
            :vote="getDanceVote(rating.danceId)"
            @dance-vote="setDanceVote($event)"
          /> </BCol
      ></BRow>
    </BContainer>
  </BModal>
</template>
