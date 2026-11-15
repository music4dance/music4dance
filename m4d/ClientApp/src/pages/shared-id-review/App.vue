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
  "artist? Clusters with a rejected pair or a .NoMerge song are skipped.";

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
      the cluster to the end of the queue until the server restarts. Clusters with a rejected pair
      or a .NoMerge song start as Leave for later; the rest start as Merge groups.
      <strong>Merge and edit</strong> merges a cluster straight away and opens it for editing.
    </p>
    <p>
      The <em>Title</em>, <em>Artist</em> and <em>Individual artists</em> rows choose what the
      merged song is called. They start on the <em>If merged</em> column, which takes any feat.
      clause out of the title and adds the featured artists to the individual artists. When a choice
      isn't what the merge gives on its own, an edit by the music4dance account sets it after the
      merge. A song left out can't name the merge. When the songs are split into groups, each song's
      choice is marked with the group it names; a group without a choice of its own gets the same
      cleanup of its own merge.
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

    <BAlert v-if="model.message" :model-value="true" variant="info">{{ model.message }}</BAlert>
    <BAlert v-if="error" :model-value="true" variant="danger">{{ error }}</BAlert>

    <p id="queue-summary">
      {{ model.pendingCandidates.toLocaleString() }} pending pairs in
      {{ model.clusterCount.toLocaleString() }} clusters.
      <template v-if="model.sortedBy">
        Sorted by the preview run {{ formatDateTime(model.sortedBy) }}: the
        {{ model.withoutFeaturingCount.toLocaleString() }} clusters still pending that differed only
        by feat. come first, then the {{ model.closeCount.toLocaleString() }} close matches.
      </template>
      <template v-else>
        Run <strong>Preview merge all</strong> to sort the clusters that differ only by feat. and
        the close matches first.
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
      <form method="post" action="/SharedIds/PreviewMergeMatching">
        <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
        <BButton type="submit" variant="outline-secondary">Preview merge all</BButton>
      </form>
      <BButton variant="link" href="/SharedIds/Preview">Last preview</BButton>
    </div>

    <template v-if="choices.length > 0">
      <div class="mb-3">
        <BButton class="apply-button" variant="primary" :disabled="applying" @click="apply"
          >Apply this page</BButton
        >
        <span v-if="changedCount > 0" class="ms-2 text-muted changed-count"
          >{{ changedCount }} changed from their defaults</span
        >
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
