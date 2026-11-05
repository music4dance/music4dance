<script setup lang="ts">
import { SongProperty } from "@/models/SongProperty";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { computed, ref, watch } from "vue";

const context = getMenuContext();

const props = defineProps<{
  name: string;
  value: string;
  editing?: boolean;
  type?: string;
  role?: string;
  /** Any one of these roles may edit. Use instead of `role` when more than one qualifies. */
  roles?: string[];
  isCreator?: boolean;
  overridePermission?: boolean;
  placeholder?: string;
  inputClass?: string;
  inputWidth?: string;
  /** Other values the field has held (e.g. from the songs merged into this one), offered as links while editing. */
  alternatives?: string[];
}>();

const emit = defineEmits<{
  "update-field": [property: SongProperty];
}>();

// Local value lets the user see real-time changes (including arrow-key increments)
// without immediately calling modifyProperty. The value is only committed—and
// update-field emitted—when the user explicitly blurs the field or presses Enter.
const localValue = ref(props.value);
watch(
  () => props.value,
  (newVal) => {
    localValue.value = newVal;
  },
);
const commitValue = () => {
  if (localValue.value !== props.value) {
    emit("update-field", new SongProperty({ name: props.name, value: localValue.value }));
  }
};

const isNumber = computed(() => props.type === "number");
const normalize = (v: string): string =>
  isNumber.value && v.trim() ? String(Number(v)) : v.trim();
const choices = computed(() => {
  const current = normalize(String(localValue.value ?? ""));
  return [...new Set((props.alternatives ?? []).map(normalize))].filter((v) => v && v !== current);
});
const choose = (v: string) => {
  localValue.value = v;
  commitValue();
};
const computedType = computed(() => props.type ?? "text");
const hasEditPermission = computed(() => {
  if (props.overridePermission || props.isCreator) {
    return true;
  }

  const roles = props.roles ?? (props.role ? [props.role] : []);
  return roles.some((role) => context.hasRole(role));
});
</script>

<template>
  <span>
    <input
      v-if="editing && hasEditPermission"
      v-model="localValue"
      :name="name"
      :data-field-name="name"
      :type="computedType"
      :placeholder="placeholder"
      class="form-control ml-2"
      :style="{ display: 'inline', width: inputWidth }"
      :class="[{ number: isNumber, text: !isNumber }, inputClass]"
      @blur="commitValue"
      @keyup.enter="commitValue"
    />
    <span v-else
      ><slot>{{ value }}</slot></span
    >
    <span v-if="editing && hasEditPermission && choices.length" class="field-alternatives">
      <BButton
        v-for="choice in choices"
        :key="choice"
        variant="link"
        size="sm"
        class="p-0 me-2"
        :data-alternative="choice"
        @click="choose(choice)"
        >{{ choice }}</BButton
      >
    </span>
  </span>
</template>

<style lang="scss" scoped>
.number {
  width: 5em;
}
.text {
  width: auto;
}
.field-alternatives {
  display: block;
  font-size: 1rem;
  font-style: normal;
  font-weight: normal;
}
</style>
