<template>
  <div id="references">
    <h2>References:</h2>
    <div v-for="(link, index) in links" :key="index">
      <editable-link
        v-model="internalLinks[index]"
        :editing="editing"
        @delete="onDelete($event)"
      ></editable-link>
    </div>
    <b-button
      v-if="editing"
      block
      variant="outline-primary"
      class="mt-2"
      @click="onAdd"
      >Add Reference</b-button
    >
  </div>
</template>

<script lang="ts">
import { jsonCompare } from "@/helpers/ObjectHelpers";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceLink } from "@/model/DanceLink";
import "reflect-metadata";
import { PropType } from "vue";
import EditableLink from "./EditableLink.vue";

export default EnvironmentManager.extend({
  components: { EditableLink },
  model: {
    prop: "links",
    event: "update",
  },
  props: {
    links: { type: Array as PropType<DanceLink[]>, required: true },
    danceId: { type: String, required: true },
    editing: Boolean,
  },
  data() {
    return new (class {
      initialLinks?: DanceLink[];
    })();
  },
  computed: {
    internalLinks: {
      get: function (): DanceLink[] {
        return this.links;
      },
      set: function (value: DanceLink[]): void {
        this.$emit("update", value);
      },
    },
    isModified(): boolean {
      return !jsonCompare(this.links, this.initialLinks);
    },
  },
  methods: {
    commit(): void {
      this.initialLinks = this.cloneLinks(this.links);
    },
    onAdd(): void {
      this.$emit("update", [
        ...this.cloneLinks(this.links),
        new DanceLink({ danceId: this.danceId }),
      ]);
    },
    cloneLinks(value: DanceLink[]): DanceLink[] {
      return value.map((l) => new DanceLink(l));
    },
    onDelete(link: DanceLink): void {
      if (this.initialLinks) {
        const links = this.cloneLinks(this.initialLinks).filter(
          (l) => l.id !== link.id
        );
        this.$emit("update", links);
      }
    },
  },
  watch: {
    editing(val: boolean): void {
      if (val === false && this.initialLinks) {
        this.$emit("update", this.cloneLinks(this.initialLinks));
      }
    },
  },
  mounted(): void {
    this.initialLinks = this.cloneLinks(this.links);
  },
});
</script>
