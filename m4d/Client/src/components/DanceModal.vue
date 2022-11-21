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
import { Component } from "vue-property-decorator";
import TagButton from "./TagButton.vue";
import TagModalBase from "./TagModalBase";

@Component({
  components: {
    DanceVote,
    TagButton,
  },
})
export default class DanceModal extends TagModalBase {
  private get danceHandler(): DanceHandler {
    return this.tagHandler as DanceHandler;
  }

  private get pageLink(): string {
    return `/dances/${this.name}`;
  }

  private get includeOnly(): string {
    return `/song/search/?dances=${this.danceHandler.danceRating?.danceId}`;
  }

  private get includeDance(): string {
    return `${this.includeOnly}&filter=${
      this.danceHandler.filter!.encodedQuery
    }`;
  }

  private get name(): string {
    return this.dance?.name ?? "";
  }

  private get maxWeight(): number {
    return this.dance?.maxWeight ?? 0;
  }

  private get dance(): DanceStats | undefined {
    return this.danceHandler.danceRating
      ? this.environment.fromId(this.danceHandler.danceRating.danceId)
      : undefined;
  }

  private get tags(): Tag[] | undefined {
    return this.danceHandler?.danceRating?.tags;
  }

  private subTagHandler(tag: Tag): TagHandler {
    const handler = this.danceHandler;
    return new TagHandler(
      tag,
      handler.user,
      handler.filter,
      handler.danceRating
    );
  }

  private get spinTitle(): string {
    return `I enjoy dancing ${this.name} to ${this.title}.`;
  }

  private get authenticated(): boolean {
    return !!this.danceHandler.user;
  }

  private get hasFilter(): boolean {
    const filter = this.danceHandler.filter;
    return !!filter && !filter.isDefault(this.danceHandler.danceRating.id);
  }

  private get isFiltered(): boolean {
    const filter = this.danceHandler.filter;
    const id = this.danceHandler.danceRating.id;
    return !!filter && !!filter.danceQuery.danceList.find((d) => d === id);
  }
}
</script>
