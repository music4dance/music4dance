<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from "vue";
import { TypedJSON } from "typedjson";
import type { BvEvent } from "bootstrap-vue-next";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { homeCrumb, type BreadCrumbItem } from "@/models/BreadCrumbItem";
import { ClusterChoice } from "./ClusterChoice";
import {
  SharedIdReviewModel,
  type SharedIdApplyRequest,
  type SharedIdApplyResponse,
} from "./SharedIdReviewModel";
import { formatDateTime } from "./format";
import { navigateTo, reviewPageUrl } from "./navigate";
import { sortOutIds } from "./sortOut";
import ClusterCard from "./components/ClusterCard.vue";

declare const model_: string;

const model: SharedIdReviewModel = TypedJSON.parse(model_, SharedIdReviewModel)!;
const context = getMenuContext();

const title = "Shared Song Ids";
const breadcrumbs: BreadCrumbItem[] = [
  homeCrumb,
  { text: "Admin", href: "/Admin" },
  { text: title, active: true },
];

// The page is loaded once per page of the queue, so the choices are made once
const choices = model.clusters.map((c) => reactive(new ClusterChoice(c)) as ClusterChoice);

const changedCount = computed(() => choices.filter((c) => c.isDirty).length);

const applying = ref(false);
const error = ref("");

const deleteCount = computed(() => choices.reduce((n, c) => n + c.deletions.size, 0));

async function apply(): Promise<void> {
  if (
    deleteCount.value > 0 &&
    !window.confirm(`Apply will delete ${deleteCount.value} songs. Delete them?`)
  ) {
    return;
  }
  applying.value = true;
  error.value = "";
  const request: SharedIdApplyRequest = {
    page: model.page,
    // A cluster whose songs changed while the page loaded has nothing to apply
    choices: choices.filter((c) => c.hasSongs).map((c) => c.toRequest()),
  };
  try {
    const response = await context.axiosXsrf.post<SharedIdApplyResponse>(
      "/api/sharedids/apply",
      request,
    );
    // An Apply re-clusters the queue, so the page reloads rather than updating in place
    navigateTo(reviewPageUrl(model.page, response.data.message));
  } catch (e: unknown) {
    applying.value = false;
    error.value = `Apply failed: ${e instanceof Error ? e.message : String(e)}`;
  }
}

// Sort out ids on this page: one cluster at a time, so the lookups stay sequential and the
// progress shows
const toSort = computed(() => choices.filter((c) => c.needsSorting));
const sorting = ref<{ done: number; total: number; failed: number }>();

async function sortOutPage(): Promise<void> {
  const clusters = toSort.value;
  const progress = { done: 0, total: clusters.length, failed: 0 };
  sorting.value = progress;
  for (const choice of clusters) {
    try {
      await sortOutIds(choice);
    } catch {
      progress.failed += 1;
    }
    progress.done += 1;
    sorting.value = { ...progress };
  }
}

const sortingText = computed(() => {
  const s = sorting.value;
  if (!s) {
    return "";
  }
  const failed = s.failed > 0 ? `, ${s.failed} failed` : "";
  return s.done < s.total
    ? `Sorting out ids: ${s.done} of ${s.total} clusters${failed}`
    : `Sorted out the ids of ${s.total} clusters${failed}. Check the proposals, then Apply.`;
});
const sortingBusy = computed(() => !!sorting.value && sorting.value.done < sorting.value.total);

function onPageClick(event: BvEvent, page: number): void {
  event.preventDefault();
  navigateTo(reviewPageUrl(page));
}

// The background tasks are form posts that land on /Admin/AdminStatus
function confirmSubmit(event: Event, message: string): void {
  if (!window.confirm(message)) {
    event.preventDefault();
  }
}

const seedPrompt = "Scan the index and queue every pair of songs that already share an id?";
const mergeAllPrompt =
  "Merge every pending cluster whose songs share an id and also have the same title and " +
  "artist? Clusters with a rejected pair, a .NoMerge song, a song with no artist, or lengths " +
  "more than 15 seconds apart are skipped.";
const mergeFeaturingPrompt =
  "Merge every pending cluster whose titles and artists are the same once their feat. clauses " +
  "come out, moving the featured artists into the individual artists? Clusters with a rejected " +
  "pair, a .NoMerge song, a song with no artist, or lengths more than 15 seconds apart are " +
  "skipped.";
const mergeCreditsPrompt =
  "Merge every pending cluster whose titles match and whose songs name the same artists, " +
  "however they're credited, or whose credits leave out artists that the songs' own Spotify " +
  "or iTunes tracks list? This looks the tracks up. The same clusters are skipped as for the " +
  "other Merge all buttons.";

function leaveWarning(event: BeforeUnloadEvent): void {
  if (changedCount.value > 0 && !applying.value) {
    event.preventDefault();
  }
}

onMounted(() => window.addEventListener("beforeunload", leaveWarning));
onBeforeUnmount(() => window.removeEventListener("beforeunload", leaveWarning));
</script>

<template>
  <PageFrame id="app" :title="title" :breadcrumbs="breadcrumbs">
    <p>
      Songs that hold the same Spotify, iTunes or ISRC id. When enrichment is about to give a song
      an id another song already holds, it leaves the id off and queues the pair here. Choose what
      to do with each cluster on the page, then <strong>Apply this page</strong>.
      <strong>Merge groups</strong> folds the songs of each group (A, B, …) into one, keeping all of
      their history, and marks the songs of different groups not the same; a song left out stays in
      the queue. Every song starts in group A, and the <em>If merged</em> column shows what merging
      all of them would give. <strong>Not the same</strong> leaves the ids where they are and stops
      the pairs from being queued again. <strong>Leave for later</strong> changes nothing, and moves
      the cluster to the end of the queue until the server restarts. Clusters with a rejected pair,
      a .NoMerge song or lengths more than 15 seconds apart (probably different recordings) start as
      Leave for later; the rest start as Merge groups. <strong>Merge and edit</strong> merges a
      cluster straight away and opens it for editing.
    </p>
    <p>
      The <em>Title</em>, <em>Artist</em> and <em>Individual artists</em> rows choose what the
      merged song is called. They start on the <em>If merged</em> column, which takes any feat. (or
      "with") clause and remaster or single-version note out of the title, cuts it back to the
      shortest of the songs' titles it starts with ("Song - Salsa Version" and "Song" give "Song"),
      and adds the featured artists to the credit and the individual artists. Type over any of its
      names to name the merged song whatever you like (individual artists separated by |); that
      works only while the songs are merged in one group. When a choice isn't what the merge gives
      on its own, an edit by the music4dance account sets it after the merge. A song left out can't
      name the merge. When the songs are split into groups, each song's choice is marked with the
      group it names; a group without a choice of its own gets the same cleanup of its own merge.
    </p>
    <p>
      In the song columns, the box before an album removes the album from that song, and the box
      before an id removes just that id. Removals are made first, by an edit of yours, whatever the
      cluster's choice, and pairs whose songs no longer share an id afterwards are marked not the
      same. So to keep two different songs apart, remove the shared id from the one it doesn't
      belong to; <em>Leave for later</em> is enough. The <em>If merged</em> column doesn't show
      removals. <strong>Delete</strong> on a song deletes it on Apply, whatever the cluster's
      choice; it's left out of the groups. <strong>Look up on Spotify and iTunes</strong> puts what
      each service says the cluster's ids are in their tooltips; an id it doesn't know turns red.
    </p>
    <p>
      <strong>Sort out ids</strong> works out which song each shared Spotify or iTunes id belongs
      to, by looking it up and comparing the track's title, artist and length with each song's (a
      song with exactly the track's title wins over one whose title is only close). It checks the
      boxes that take the id off the other songs, or off every song when no song is the track, and
      an album whose ids would all go. An ISRC goes with the Spotify or iTunes id whose track
      reports it, and an album that loses its Spotify or iTunes id loses its ISRC too. Under the
      albums it lists each shared id's owner, or why there's none. A cluster it changes becomes
      Leave for later, so Apply separates the songs instead of merging them; change any box before
      you apply. <strong>Sort out ids on this page</strong> does this for every cluster that's
      probably different songs: lengths too far apart, titles or artists that are only close or
      differ, a rejected pair or a .NoMerge song.
    </p>
    <p>
      The <strong>Merge all</strong> buttons merge in bulk, with the same feat. cleanup:
      <em>with matching title and artist</em> merges the clusters whose songs have the same title
      and artist, <em>that differ only by feat.</em> the ones that are the same once their feat.
      clauses come out, and <em>with artists credited differently</em> the ones whose titles match
      and whose songs name the same artists however they're credited ("X and His Orchestra" and
      "X"), or leave out artists ("X" and "X &amp; Y") when the song credited to X alone has a
      Spotify or iTunes track of its own that lists them all. All of them skip clusters with a
      rejected pair, a .NoMerge song, a song with no artist, or lengths more than 15 seconds apart.
      <strong>Preview merges</strong> works out what each would merge and what the merged songs
      would be called, without merging anything, and sorts this page by it;
      <strong>Show preview</strong> lists the result.
    </p>

    <BAlert v-if="model.message" :model-value="true" variant="info">{{ model.message }}</BAlert>
    <BAlert v-if="error" :model-value="true" variant="danger">{{ error }}</BAlert>

    <p id="queue-summary">
      {{ model.pendingCandidates.toLocaleString() }} pending pairs in
      {{ model.clusterCount.toLocaleString() }} clusters.
      <template v-if="model.sortedBy">
        Sorted by the preview run {{ formatDateTime(model.sortedBy) }}: the
        {{ model.withoutFeaturingCount.toLocaleString() }} clusters still pending that differed only
        in their artist credits (feat. included) come first, then the
        {{ model.closeCount.toLocaleString() }} close matches. That preview found
        {{ (model.wouldMergeCount ?? 0).toLocaleString() }} clusters with matching titles and
        artists, {{ (model.wouldMergeFeaturingCount ?? 0).toLocaleString() }} that differ only by
        feat. and {{ (model.wouldMergeCreditsCount ?? 0).toLocaleString() }} with artists credited
        differently to merge (<a href="/SharedIds/Preview">show preview</a>).
      </template>
      <template v-else>
        Run <strong>Preview merges</strong> to see what the Merge all buttons would merge, and to
        sort the clusters that differ only in their artist credits and the close matches first.
      </template>
      <template v-if="model.deferredCount">
        The {{ model.deferredCount.toLocaleString() }} clusters left for later come last.
      </template>
    </p>

    <div class="mb-3 d-flex flex-wrap gap-2">
      <form method="post" action="/SharedIds/Seed" @submit="confirmSubmit($event, seedPrompt)">
        <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
        <BButton type="submit" variant="outline-secondary">Queue existing shared ids</BButton>
      </form>
      <form
        method="post"
        action="/SharedIds/MergeMatching"
        @submit="confirmSubmit($event, mergeAllPrompt)"
      >
        <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
        <BButton type="submit" variant="outline-danger"
          >Merge all with matching title and artist</BButton
        >
      </form>
      <form
        method="post"
        action="/SharedIds/MergeFeaturing"
        @submit="confirmSubmit($event, mergeFeaturingPrompt)"
      >
        <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
        <BButton type="submit" variant="outline-danger"
          >Merge all that differ only by feat.</BButton
        >
      </form>
      <form
        method="post"
        action="/SharedIds/MergeCredits"
        @submit="confirmSubmit($event, mergeCreditsPrompt)"
      >
        <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
        <BButton type="submit" variant="outline-danger"
          >Merge all with artists credited differently</BButton
        >
      </form>
      <form method="post" action="/SharedIds/PreviewMergeMatching">
        <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
        <BButton type="submit" variant="outline-secondary">Preview merges</BButton>
      </form>
      <BButton variant="link" href="/SharedIds/Preview">Show preview</BButton>
    </div>

    <template v-if="choices.length > 0">
      <div class="mb-3">
        <BButton class="apply-button" variant="primary" :disabled="applying" @click="apply"
          >Apply this page</BButton
        >
        <BButton
          v-if="toSort.length > 0"
          class="ms-2 sort-out-page-button"
          variant="outline-secondary"
          :disabled="applying || sortingBusy"
          @click="sortOutPage"
          >Sort out ids on this page ({{ toSort.length }})</BButton
        >
        <span v-if="changedCount > 0" class="ms-2 text-muted changed-count"
          >{{ changedCount }} changed from their defaults</span
        >
        <div v-if="sortingText" class="mt-2 small sorting-progress">{{ sortingText }}</div>
      </div>

      <ClusterCard
        v-for="(choice, i) in choices"
        :key="choice.cluster.key"
        :choice="choice"
        :prefix="`cluster-${i}`"
      />

      <div class="mb-3">
        <BButton class="apply-button" variant="primary" :disabled="applying" @click="apply"
          >Apply this page</BButton
        >
      </div>
    </template>

    <BPagination
      v-if="model.pageCount > 1"
      :model-value="model.page"
      :total-rows="model.pageCount"
      :per-page="1"
      limit="9"
      first-number
      last-number
      aria-label="Review queue pages"
      @page-click="onPageClick"
    />
  </PageFrame>
</template>
