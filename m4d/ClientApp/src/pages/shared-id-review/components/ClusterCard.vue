<script setup lang="ts">
import { computed, ref } from "vue";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { type ClusterChoice, leaveOut, lookupIds } from "../ClusterChoice";
import type {
  SharedIdAction,
  SharedIdReviewSong,
  SharedIdServiceTrack,
} from "../SharedIdReviewModel";
import { formatArtists, formatDances, formatDate, formatLength } from "../format";
import { mergeArtists } from "../mergeArtists";
import { sortOutIds } from "../sortOut";
import AlbumIds from "./AlbumIds.vue";
import NameChoiceRow from "./NameChoiceRow.vue";

// One cluster of the review page: its songs side by side with what merging them all would
// make, the choice for it, Merge and edit, and a lookup of what Spotify and iTunes say its ids
// are (shown in the ids' tooltips).
const props = defineProps<{
  choice: ClusterChoice;
  // Unique on the page, for the controls' names and ids
  prefix: string;
}>();

const context = getMenuContext();

const cluster = computed(() => props.choice.cluster);
const songs = computed(() => cluster.value.songs);
const merged = computed(() => cluster.value.merged);
const proposal = computed(() => cluster.value.proposal);

const actions: { action: SharedIdAction; label: string }[] = [
  { action: "Merge", label: "Merge groups" },
  { action: "Reject", label: "Not the same" },
  { action: "Leave", label: "Leave for later" },
];

const groupOptions = computed(() => [
  ...props.choice.groupNames.map((g) => ({ value: g, text: g })),
  { value: leaveOut, text: "Leave out" },
]);

const mergeAlone = computed(() =>
  proposal.value && merged.value && proposal.value.title !== merged.value.title
    ? (merged.value.title ?? "")
    : undefined,
);

const songTitle = (s: SharedIdReviewSong) => s.title ?? "";
const songArtist = (s: SharedIdReviewSong) => s.artist ?? "";
const songArtists = (s: SharedIdReviewSong) => formatArtists(s.artists);
const songTempo = (s: SharedIdReviewSong) => s.tempo?.toString() ?? "";

function setAction(value: unknown): void {
  props.choice.setAction(value as SharedIdAction);
}

function setGroup(songId: string, value: unknown): void {
  props.choice.setGroup(songId, String(value ?? leaveOut));
}

function setDeleted(songId: string, value: unknown): void {
  props.choice.setDeleted(songId, !!value);
}

// The service lookup, kept with the choice until the page reloads
const tracks = computed(() => props.choice.tracks);
const lookingUp = ref(false);
const lookupError = ref("");
const canLookUp = computed(() => lookupIds(songs.value).length > 0);

async function lookUp(): Promise<void> {
  lookingUp.value = true;
  lookupError.value = "";
  try {
    const response = await context.axiosXsrf.post<SharedIdServiceTrack[]>("/api/sharedids/tracks", {
      ids: lookupIds(songs.value),
    });
    props.choice.setTracks(response.data);
  } catch (e: unknown) {
    lookupError.value = `Lookup failed: ${e instanceof Error ? e.message : String(e)}`;
  } finally {
    lookingUp.value = false;
  }
}

const canSortOut = computed(() => lookupIds(songs.value).length > 0);
const sortingOut = ref(false);
const sortOutError = ref("");

async function sortOut(): Promise<void> {
  sortingOut.value = true;
  sortOutError.value = "";
  try {
    await sortOutIds(props.choice);
  } catch (e: unknown) {
    sortOutError.value = `Sort out ids failed: ${e instanceof Error ? e.message : String(e)}`;
  } finally {
    sortingOut.value = false;
  }
}

// Merge artist checks the combined artists against a track the songs have; Force doesn't.
// What it said is kept with the choice, so Merge artists on this page shows here too.
const mergingArtists = ref(false);
const artistsResult = computed(() => props.choice.artistsResult);

async function onMergeArtists(force: boolean): Promise<void> {
  mergingArtists.value = true;
  try {
    await mergeArtists(props.choice, force);
  } finally {
    mergingArtists.value = false;
  }
}

const ownerText: Record<string, string> = {
  Owned: "belongs to",
  Bad: "is no song here: off every song",
  Unsure: "unsure",
};
</script>

<template>
  <BCard class="mb-4" no-body>
    <BCardHeader>
      <strong>{{ cluster.serviceIds.join(", ") }}</strong>
      <BBadge v-if="cluster.match === 'Exact'" variant="success" class="ms-2"
        >Title and artist match</BBadge
      >
      <BBadge v-else-if="cluster.match === 'WithoutFeaturing'" variant="info" class="ms-2"
        >Differ only by feat.</BBadge
      >
      <BBadge v-else-if="cluster.match === 'SameArtists'" variant="info" class="ms-2"
        >Same artists, credited differently</BBadge
      >
      <BBadge v-else-if="cluster.match === 'FewerArtists'" variant="info" class="ms-2"
        >Same lead artist</BBadge
      >
      <BBadge v-else-if="cluster.match === 'Close'" variant="secondary" class="ms-2"
        >Close match</BBadge
      >
      <BBadge v-if="cluster.cleansFeaturing" variant="primary" class="ms-2">Cleans up feat.</BBadge>
      <BBadge v-if="cluster.hasRejectedPair" variant="warning" class="ms-2"
        >Has a rejected pair</BBadge
      >
      <BBadge v-if="cluster.hasNoMergeSong" variant="warning" class="ms-2">.NoMerge</BBadge>
      <BBadge v-if="cluster.lengthsDiffer" variant="warning" class="ms-2"
        >Lengths {{ formatLength(cluster.lengthSpread) }} apart</BBadge
      >
      <BBadge v-else-if="cluster.temposAgree" variant="info" class="ms-2 tempos-agree"
        >Lengths {{ formatLength(cluster.lengthSpread) }} apart, tempos agree</BBadge
      >
      <BBadge v-if="choice.isDirty" variant="dark" class="ms-2">Changed</BBadge>
    </BCardHeader>
    <BCardBody>
      <p v-if="!choice.hasSongs" class="text-muted">
        These songs changed while the page loaded. Reload to see them.
      </p>
      <template v-else>
        <div class="table-responsive">
          <table class="table table-sm table-bordered">
            <tbody>
              <tr>
                <th />
                <th
                  v-for="(song, k) in songs"
                  :key="song.songId"
                  :class="{ 'table-danger': choice.isDeleted(song.songId) }"
                >
                  <BFormSelect
                    :model-value="choice.groupOf(song.songId)"
                    :options="groupOptions"
                    :disabled="choice.isDeleted(song.songId)"
                    size="sm"
                    class="d-inline-block w-auto me-1"
                    :aria-label="`Group for song ${k + 1}`"
                    @update:model-value="setGroup(song.songId, $event)"
                  />
                  <a
                    :href="`/song/details/${song.songId}`"
                    target="_blank"
                    rel="noopener noreferrer"
                    :class="{ 'text-decoration-line-through': choice.isDeleted(song.songId) }"
                    >Song {{ k + 1 }}</a
                  >
                  <BFormCheckbox
                    inline
                    class="ms-2 me-0 fw-normal"
                    :model-value="choice.isDeleted(song.songId)"
                    :aria-label="`Delete song ${k + 1}`"
                    @update:model-value="setDeleted(song.songId, $event)"
                  >
                    Delete
                  </BFormCheckbox>
                </th>
                <th class="table-info">
                  If merged
                  <div v-if="choice.isSplit" class="small fw-normal text-muted split-note">
                    Split: each group gets its own cleanup instead.
                  </div>
                </th>
              </tr>
              <NameChoiceRow
                :choice="choice"
                field="title"
                :prefix="prefix"
                heading="Title"
                :text-of="songTitle"
                :proposed="proposal?.title"
                :merge-alone="mergeAlone"
              />
              <NameChoiceRow
                :choice="choice"
                field="artist"
                :prefix="prefix"
                heading="Artist"
                :text-of="songArtist"
                :proposed="proposal?.artist"
              />
              <NameChoiceRow
                :choice="choice"
                field="artists"
                :prefix="prefix"
                heading="Individual artists"
                :text-of="songArtists"
                :proposed="formatArtists(proposal?.artists)"
              />
              <tr>
                <th>Length</th>
                <td v-for="song in songs" :key="song.songId">{{ formatLength(song.length) }}</td>
                <td class="table-info">{{ formatLength(merged?.length) }}</td>
              </tr>
              <NameChoiceRow
                :choice="choice"
                field="tempo"
                :prefix="prefix"
                heading="Tempo"
                :text-of="songTempo"
                :proposed="merged?.tempo?.toString()"
                input-type="number"
              />
              <tr>
                <th>Dances</th>
                <td v-for="song in songs" :key="song.songId">{{ formatDances(song.dances) }}</td>
                <td class="table-info">{{ formatDances(merged?.dances) }}</td>
              </tr>
              <tr>
                <th>Albums</th>
                <td v-for="(song, k) in songs" :key="song.songId">
                  <AlbumIds
                    :song="song"
                    :choice="choice"
                    :label="`song ${k + 1}`"
                    :tracks="tracks"
                  />
                </td>
                <td class="table-info">
                  <AlbumIds v-if="merged" :song="merged" :tracks="tracks" />
                </td>
              </tr>
              <tr>
                <th>Created</th>
                <td v-for="song in songs" :key="song.songId">{{ formatDate(song.created) }}</td>
                <td class="table-info">{{ formatDate(merged?.created) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <p v-if="!merged" class="small text-muted">The merge couldn't be previewed.</p>

        <ul v-if="choice.owners" class="small owners">
          <li v-for="owner in choice.owners" :key="owner.id">
            <strong>{{ owner.id }}</strong
            ><span v-if="owner.shared === false" class="text-muted"> (one song's)</span>
            {{ ownerText[owner.status] }}
            <template v-if="owner.status === 'Owned'">{{ choice.songLabel(owner.song) }}</template
            ><span v-if="owner.reason" class="text-muted">: {{ owner.reason }}</span>
          </li>
          <li v-if="choice.owners.length === 0" class="text-muted">The songs changed: reload.</li>
        </ul>

        <ul class="small">
          <li v-for="candidate in cluster.candidates" :key="candidate.id">
            {{ candidate.source }}, {{ formatDate(candidate.created) }}: {{ candidate.serviceIds }}
            <span v-if="candidate.detail" class="text-muted">({{ candidate.detail }})</span>
          </li>
        </ul>

        <div class="d-flex flex-wrap align-items-center gap-3">
          <BFormRadio
            v-for="a in actions"
            :id="`${prefix}-action-${a.action}`"
            :key="a.action"
            :name="`${prefix}-action`"
            :value="a.action"
            :model-value="choice.action"
            inline
            @update:model-value="setAction"
          >
            {{ a.label }}
          </BFormRadio>
          <form method="post" action="/Song/BulkEdit" target="_blank" class="d-inline">
            <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
            <input type="hidden" name="action" value="Merge" />
            <input
              v-for="song in songs"
              :key="song.songId"
              type="hidden"
              name="selectedSongs"
              :value="song.songId"
            />
            <BButton type="submit" size="sm" variant="outline-secondary">Merge and edit</BButton>
          </form>
          <BButton
            v-if="canLookUp"
            size="sm"
            variant="outline-secondary"
            class="look-up-button"
            :disabled="lookingUp"
            @click="lookUp"
          >
            {{ tracks ? "Look up again" : "Look up on Spotify and iTunes" }}
          </BButton>
          <BButton
            v-if="canSortOut"
            size="sm"
            variant="outline-secondary"
            class="sort-out-button"
            :disabled="sortingOut"
            @click="sortOut"
          >
            Sort out ids
          </BButton>
          <BButton
            v-if="choice.artistsMerged"
            size="sm"
            variant="outline-secondary"
            class="unmerge-artists-button"
            @click="choice.untypeArtists()"
          >
            Unmerge artist
          </BButton>
          <template v-else>
            <BButton
              size="sm"
              variant="outline-secondary"
              class="merge-artists-button"
              :disabled="mergingArtists"
              @click="onMergeArtists(false)"
            >
              Merge artist
            </BButton>
            <BButton
              size="sm"
              variant="outline-secondary"
              class="force-merge-artists-button"
              :disabled="mergingArtists"
              @click="onMergeArtists(true)"
            >
              Force merge artist
            </BButton>
          </template>
        </div>
        <p
          v-if="artistsResult"
          class="small mt-2 mb-0 artists-result"
          :class="artistsResult.ok ? 'text-success' : 'text-danger'"
        >
          {{ artistsResult.message }}
        </p>
        <BAlert v-if="lookupError" :model-value="true" variant="danger" class="mt-2 mb-0">
          {{ lookupError }}
        </BAlert>
        <BAlert v-if="sortOutError" :model-value="true" variant="danger" class="mt-2 mb-0">
          {{ sortOutError }}
        </BAlert>
      </template>
    </BCardBody>
  </BCard>
</template>
