<script setup lang="ts">
import { SongSort, SortOrder } from "@/models/SongSort";
import { BContainer, useModal, type RadioValue } from "bootstrap-vue-next";
import { computed } from "vue";

const props = defineProps<{
  order: SortOrder;
}>();

const emit = defineEmits<{
  "update:order": [order: SortOrder];
}>();

const { hide } = useModal();

const chronOrder = computed(() => {
  const order = props.order;
  return new SongSort(order).isChronological ? order : SortOrder.Match;
});

const chooseSort = (order: RadioValue) => {
  // INT-TODO: May be a bug in BFormRadioGroup value==="" evaluates to true
  const fixed = order === true ? SortOrder.Match : (order as SortOrder);
  emit("update:order", fixed);
  hide();
};
</script>

<template>
  <BModal id="chron-modal" title="Chronological Sort" hide-footer size="sm">
    <BContainer>
      <BRow>
        <BCol>
          <p>
            Choose one of the options below to sort the song list chronologically with most recent
            first
          </p>
        </BCol>
      </BRow>
      <BRow>
        <BCol class="d-flex justify-content-center">
          <BFormRadioGroup
            v-model="chronOrder"
            buttons
            stacked
            button-variant="outline-primary"
            @update:model-value="chooseSort($event)"
          >
            <BFormRadio :value="SortOrder.Created">When Added</BFormRadio>
            <BFormRadio :value="SortOrder.Modified">Last Changed</BFormRadio>
            <BFormRadio :value="SortOrder.Edited">Last Edited</BFormRadio>
            <BFormRadio :value="SortOrder.Comments">Last Commented</BFormRadio>
            <BFormRadio :value="SortOrder.Match">No chronological sort</BFormRadio>
          </BFormRadioGroup>
        </BCol>
      </BRow>
    </BContainer>
  </BModal>
</template>
