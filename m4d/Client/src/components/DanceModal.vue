<template>
  <b-modal
    :id="danceHandler.id"
    :header-bg-variant="tag.variant"
    header-text-variant="light"
    hide-footer
  >
    <template v-slot:modal-title>
      <b-icon :icon="tag.icon"></b-icon>&nbsp;{{ title }}
    </template>
    <b-list-group>
      <b-list-group-item :href="pageLink" variant="secondary">
        {{ name }} page...
      </b-list-group-item>
      <b-list-group-item
        :href="includeDance"
        variant="warning"
        v-if="hasFilter && !isFiltered"
      >
        Filter the list to include only songs tagged as <em>{{ name }}</em>
      </b-list-group-item>
      <b-list-group-item :href="includeOnly" variant="success">
        List all {{ name }} songs.
      </b-list-group-item>
      <b-list-group-item
        href="https://music4dance.blog/tag-filtering"
        variant="info"
        target="_blank"
        >Help</b-list-group-item
      >
    </b-list-group>
    <div v-if="tags" style="margin-top: 0.5em">
      <tag-button
        v-for="tag in tags"
        :key="tag.key"
        :tagHandler="subTagHandler(tag)"
      >
      </tag-button>
    </div>
    <div v-if="danceHandler.parent">
      <dance-vote
        :song="danceHandler.parent"
        :danceRating="danceHandler.danceRating"
        :authenticated="authenticated"
        v-on="$listeners"
      ></dance-vote>
      <span style="padding-inline-start: 1em"
        >I enjoy dancing <b>{{ name }}</b> to {{ this.title }}.</span
      >
      <div>
        The top song in the {{ name }} category has {{ maxWeight }} votes.
      </div>
    </div>
  </b-modal>
</template>

<script lang="ts">
import DanceVote from "@/components/DanceVote.vue";
import { DanceHandler } from "@/model/DanceHandler";
import { DanceStats } from "@/model/DanceStats";
import { Tag } from "@/model/Tag";
import { TagHandler } from "@/model/TagHandler";
import "reflect-metadata";
import TagButton from "./TagButton.vue";
import TagModalBase from "./TagModalBase";

export default TagModalBase.extend({
  components: { DanceVote, TagButton },
  computed: {
    danceHandler(): DanceHandler {
      return this.tagHandler as DanceHandler;
    },
    pageLink(): string {
      return `/dances/${this.name}`;
    },
    includeOnly(): string {
      return `/song/search/?dances=${this.danceHandler.danceRating?.danceId}`;
    },
    includeDance(): string {
      return `${this.includeOnly}&filter=${
        this.danceHandler.filter!.encodedQuery
      }`;
    },
    name(): string {
      return this.dance?.name ?? "";
    },
    maxWeight(): number {
      return this.dance?.maxWeight ?? 0;
    },
    dance(): DanceStats | undefined {
      return this.danceHandler.danceRating
        ? this.environment.fromId(this.danceHandler.danceRating.danceId)
        : undefined;
    },
    tags(): Tag[] | undefined {
      return this.danceHandler?.danceRating?.tags;
    },
    spinTitle(): string {
      return `I enjoy dancing ${this.name} to ${this.title}.`;
    },
    authenticated(): boolean {
      return !!this.danceHandler.user;
    },
    hasFilter(): boolean {
      const filter = this.danceHandler.filter;
      return !!filter && !filter.isDefault(this.danceHandler.danceRating.id);
    },
    isFiltered(): boolean {
      const filter = this.danceHandler.filter;
      const id = this.danceHandler.danceRating.id;
      return !!filter && !!filter.danceQuery.danceList.find((d) => d === id);
    },
  },
  methods: {
    subTagHandler(tag: Tag): TagHandler {
      const handler = this.danceHandler;
      return new TagHandler(
        tag,
        handler.user,
        handler.filter,
        handler.danceRating
      );
    },
  },
});
</script>
