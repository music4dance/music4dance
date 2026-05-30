<script setup lang="ts">
import { ref } from "vue";
import { TypedJSON } from "typedjson";
import type { BvEvent, TableFieldRaw } from "bootstrap-vue-next";
import { ActivityLogPageModel, ActivityLogEntry } from "./ActivityLogPageModel";

declare const model_: string;

const model: ActivityLogPageModel = TypedJSON.parse(model_, ActivityLogPageModel)!;

// --- URL builders ---
function pageUrl(p: number): string {
  return "/ActivityLog/Index?page=" + p;
}

// --- Table fields ---
const fields: Exclude<TableFieldRaw<ActivityLogEntry>, string>[] = [
  { key: "id" as keyof ActivityLogEntry, label: "Id", sortable: false },
  { key: "date" as keyof ActivityLogEntry, label: "Date", sortable: false },
  { key: "userName" as keyof ActivityLogEntry, label: "User Name", sortable: false },
  { key: "action" as keyof ActivityLogEntry, label: "Action", sortable: false },
  { key: "details" as keyof ActivityLogEntry, label: "Details", sortable: false },
];

const editPageNumber = ref(model.page);

function onPageClick(event: BvEvent, pageNum: number): void {
  event.preventDefault();
  window.location.href = pageUrl(pageNum);
}

function goToPage(): void {
  let page = editPageNumber.value;
  if (typeof page !== "number" || isNaN(page)) {
    editPageNumber.value = model.page;
    return;
  }
  page = Math.floor(page);
  if (page < 1) page = 1;
  if (page > model.totalPages) page = model.totalPages;
  editPageNumber.value = page;
  if (page !== model.page) {
    window.location.href = pageUrl(page);
  }
}

function formatDate(dateStr: string | undefined): string {
  if (!dateStr) return "";
  return new Date(dateStr).toLocaleString("en-US", {
    month: "numeric",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}
</script>

<template>
  <PageFrame id="app" title="Activity Log">
    <!-- Activity log table -->
    <BTable
      id="activity-log-table"
      :fields="fields"
      :items="model.entries"
      striped
      class="table-songs col-sm"
    >
      <template #cell(date)="{ item }">{{ formatDate(item.date) }}</template>
      <template #cell(userName)="{ item }">
        <span v-if="item.userName">{{ item.userName }}</span>
        <span v-else class="text-muted">no user</span>
      </template>
    </BTable>

    <!-- Pagination -->
    <BRow v-if="model.totalPages > 1" class="mt-3">
      <BCol md="8">
        <BPagination
          v-model="editPageNumber"
          :total-rows="model.totalPages"
          :per-page="1"
          limit="9"
          first-number
          last-number
          aria-label="Activity log pages"
          @page-click="onPageClick"
        />
      </BCol>
      <BCol md="4">
        Page
        <input
          v-model.number="editPageNumber"
          type="number"
          step="1"
          :min="1"
          :max="model.totalPages"
          class="form-control form-control-sm d-inline-block"
          style="width: 4em"
          aria-label="Page number"
          @keydown.enter="goToPage"
          @blur="goToPage"
        />
        of {{ model.totalPages }}
      </BCol>
    </BRow>
  </PageFrame>
</template>
