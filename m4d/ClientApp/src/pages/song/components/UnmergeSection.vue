<script setup lang="ts">
import { getMenuContext } from "@/helpers/GetMenuContext";
import type { MergeSource, SongHistory } from "@/models/SongHistory";
import { useModal } from "bootstrap-vue-next";
import { computed, ref, watch } from "vue";

// Admin-only: split one or more of a simple merge's sources back out as songs of their own
// (POST /song/unmerge, see architecture/plans/song-unmerge.md). It commits immediately, so it is
// disabled while the song has unsaved edits.

const props = defineProps<{
  history: SongHistory;
  disabled?: boolean;
}>();

const context = getMenuContext();
const { create } = useModal();

const sources = computed(() => props.history.mergeSources);
const selected = ref<string[]>([]);
const noMerge = ref(true);

// Drop checks for sources the history no longer lists
watch(sources, () => {
  selected.value = selected.value.filter((id) => sources.value.some((s) => s.id === id));
});

const describe = (source: MergeSource): string => {
  const parts = [
    source.title,
    source.artist,
    source.user ? `added by ${source.user}` : undefined,
    source.created?.toLocaleDateString(),
    `${source.edits} ${source.edits === 1 ? "edit" : "edits"}`,
  ];
  return parts.filter((p) => !!p).join(" · ");
};

const options = computed(() => sources.value.map((s) => ({ value: s.id, text: describe(s) })));

const canSubmit = computed(
  () =>
    !props.disabled && selected.value.length > 0 && selected.value.length < sources.value.length,
);

const onSubmit = async (event: Event): Promise<void> => {
  const form = event.target as HTMLFormElement;
  if (!canSubmit.value) {
    return;
  }
  const count = selected.value.length;
  const result = await create({
    title: "Please Confirm",
    body:
      `Split ${count === 1 ? "this song" : `these ${count} songs`} back out of the merge? ` +
      "Each gets its own history back under its original id; edits made after the merge stay " +
      `on this song.${noMerge.value ? " The restored songs will be marked NoMerge." : ""}`,
    size: "sm",
    buttonSize: "sm",
    okVariant: "danger",
    okTitle: "Unmerge",
    cancelTitle: "Cancel",
  }).show();
  if (result?.ok) {
    form.submit();
  }
};
</script>

<template>
  <div v-if="sources.length" class="mb-2" data-test="unmerge-section">
    <h3>Unmerge</h3>
    <p class="small text-body-secondary mb-1">
      Choose the songs to split back out of this merge. At least one has to stay.
    </p>
    <BForm action="/song/unmerge" method="post" novalidate @submit.prevent="onSubmit">
      <input type="hidden" name="__RequestVerificationToken" :value="context.xsrfToken" />
      <input type="hidden" name="id" :value="history.id" />
      <BFormCheckboxGroup
        v-model="selected"
        name="extract"
        :options="options"
        :disabled="disabled"
        stacked
      />
      <input type="hidden" name="noMerge" :value="noMerge ? 'true' : 'false'" />
      <BFormCheckbox v-model="noMerge" :disabled="disabled" class="mt-1"
        >Mark unmerged songs NoMerge</BFormCheckbox
      >
      <BButton type="submit" variant="outline-danger" class="mt-1" :disabled="!canSubmit"
        >Unmerge selected</BButton
      >
    </BForm>
  </div>
</template>
