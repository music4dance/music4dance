<script setup lang="ts">
import { SongHistory } from "@/models/SongHistory";
import { SongProperty } from "@/models/SongProperty";
import type { BaseColorVariant } from "bootstrap-vue-next";
import { computed } from "vue";

// TODO:
//  - At some point may want to make a best guess at attribution for existing merge
//  - At some point may want to have move keep date in sync (e.g. update date - but maybe with a second field?)

const props = defineProps<{
  history: SongHistory;
  editing: boolean;
}>();

const emit = defineEmits<{
  "replace-history": [history: SongProperty[]];
  "insert-property": [index: number, property: string];
  "delete-property": [index: number];
  "move-property-first": [index: number];
  "move-property-up": [index: number];
  "move-property-down": [index: number];
  "move-property-last": [index: number];
  "promote-property": [index: number];
}>();

const sortTitle = computed(() => (props.history.isSorted ? "sorted" : "sort"));
const annotateTitle = computed(() => (props.history.isAnnotated ? "annotated" : "annotate"));
const getVariant = (prop: SongProperty): keyof BaseColorVariant | undefined =>
  prop.isAction ? "primary" : undefined;
const sort = (): void => {
  emit("replace-history", props.history.sorted);
};
const annotate = (): void => {
  emit("replace-history", props.history.annotated);
};
const showFirst = (prop: SongProperty, index: number): boolean => {
  if (prop.isAction) {
    return index > 0;
  } else {
    return index > 0 && !props.history.properties[index - 1].isAction;
  }
};
const showLast = (prop: SongProperty, index: number): boolean => {
  const properties = props.history.properties;
  if (prop.isAction) {
    return !!properties.find((p, i) => i > index && p.isAction);
  } else {
    return index < properties.length - 1 && !properties[index + 1].isAction;
  }
};
const showPrevious = (prop: SongProperty, index: number): boolean => {
  if (prop.isAction) {
    return index > 0;
  } else {
    return index > 0 && !props.history.properties[index - 1].isAction;
  }
};
const showNext = (prop: SongProperty, index: number): boolean => {
  if (prop.isAction) {
    return !!props.history.properties.find((p, i) => i > index && p.isAction);
  } else {
    const length = props.history.properties.length;
    return index < length - 1 && !props.history.properties[index + 1].isAction;
  }
};
const insertNew = (index: number): void => {
  const prop = prompt("Enter the property", "Name=Value");
  emit("insert-property", index, prop ?? "");
  // eslint-disable-next-line no-console
  console.log(prop);
};
</script>

<template>
  <BCard header-text-variant="primary" no-body border-variant="primary">
    <BCardHeader header-class="d-flex justify-content-between"
      ><span class="me-1">History Log</span
      ><span
        ><BButton
          :disabled="history.isAnnotated"
          variant="primary"
          size="sm"
          class="mx-1"
          @click="annotate"
          >{{ annotateTitle }}</BButton
        ><BButton
          :disabled="history.isSorted"
          variant="primary"
          size="sm"
          class="mx-1"
          @click="sort"
          >{{ sortTitle }}</BButton
        ></span
      >
    </BCardHeader>
    <BListGroup flush>
      <BListGroupItem
        v-for="(property, index) in history.properties"
        :key="index"
        :variant="getVariant(property)"
        class="d-flex justify-content-between"
        :class="{ subprop: !property.isAction }"
      >
        {{ property.name }}={{ property.value }}
        <BButtonGroup v-if="editing">
          <BButton
            v-if="!property.isAction"
            size="sm"
            variant="success"
            @click="$emit('promote-property', index)"
          >
            <IBiPersonPlusFill />
          </BButton>
          <BButton
            :disabled="!showFirst(property, index)"
            size="sm"
            variant="primary"
            @click="$emit('move-property-first', index)"
          >
            <IBiChevronBarUp />
          </BButton>
          <BButton
            :disabled="!showPrevious(property, index)"
            size="sm"
            variant="primary"
            @click="$emit('move-property-up', index)"
          >
            <IBiChevronUp />
          </BButton>
          <BButton
            :disabled="!showNext(property, index)"
            size="sm"
            variant="primary"
            @click="$emit('move-property-down', index)"
          >
            <IBiChevronDown />
          </BButton>
          <BButton
            :disabled="!showLast(property, index)"
            size="sm"
            variant="primary"
            @click="$emit('move-property-last', index)"
          >
            <IBiChevronBarDown />
          </BButton>
          <BButton v-if="!property.isAction" size="sm" variant="success" @click="insertNew(index)">
            <IBiNodePlus />
          </BButton>
          <BCloseButton
            text-variant="danger"
            class="ms-2"
            @click="$emit('delete-property', index)"
          />
        </BButtonGroup>
      </BListGroupItem>
    </BListGroup>
  </BCard>
</template>

<style lang="scss" scoped>
.subprop {
  padding-left: 2rem;
}
</style>
