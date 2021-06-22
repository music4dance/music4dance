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
        {{ danceName }} page...
      </b-list-group-item>
      <b-list-group-item
        :href="includeDance"
        variant="warning"
        v-if="hasFilter && !isFiltered"
      >
        Filter the list to include only songs tagged as <em>{{ danceName }}</em>
      </b-list-group-item>
      <b-list-group-item :href="includeOnly" variant="success">
        List all {{ danceName }} songs.
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
        >I enjoy dancing <b>{{ danceName }}</b> to {{ this.title }}.</span
      >
      <div>
        The top song in the {{ danceName }} category has {{ maxWeight }} votes.
      </div>
    </div>
  </b-modal>
</template>

<script lang="ts">
import "reflect-metadata";
import TagModalBase from "./TagModalBase";
import TagButton from "./TagButton.vue";
import DanceVote from "@/components/DanceVote.vue";
import { Component } from "vue-property-decorator";
import { Tag } from "@/model/Tag";
import { DanceHandler } from "@/model/DanceHandler";
import { DanceStats } from "@/model/DanceStats";
import { TagHandler } from "@/model/TagHandler";

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
    return `/dances/${this.danceName}`;
  }

  private get includeOnly(): string {
    return `/song/search/?dances=${this.danceHandler.danceRating?.danceId}`;
  }

  private get includeDance(): string {
    return `${this.includeOnly}&filter=${
      this.danceHandler.filter!.encodedQuery
    }`;
  }

  private get danceName(): string {
    return this.dance?.danceName ?? "";
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
    return `I enjoy dancing ${this.danceName} to ${this.title}.`;
  }

  private get authenticated(): boolean {
    return !!this.danceHandler.user;
  }

  private get hasFilter(): boolean {
    const filter = this.danceHandler.filter;
    if (!this.danceHandler.danceRating) {
      console.log(JSON.stringify(this.danceHandler));
    }
    return (
      !!filter &&
      !filter.isDefaultDance(
        this.danceHandler.danceRating.id,
        this.danceHandler.user
      )
    );
  }

  private get isFiltered(): boolean {
    const filter = this.danceHandler.filter;
    const id = this.danceHandler.danceRating.id;
    return !!filter && !!filter.danceQuery.danceList.find((d) => d === id);
  }
}
</script>
