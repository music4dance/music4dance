<template>
  <b-card
    :header="title"
    header-text-variant="primary"
    no-body
    border-variant="primary"
    ><b-list-group v-if="hasDances" flush
      ><b-list-group-item v-for="dr in danceRatingsFiltered" :key="dr.danceId">
        <b-button-close
          v-if="isAdmin && edit"
          text-variant="danger"
          @click="$emit('delete-dance', dr)"
          ><b-icon-x variant="danger"></b-icon-x
        ></b-button-close>
        <dance-vote
          :song="song"
          :danceRating="dr"
          :authenticated="!!user"
          v-on="$listeners"
        ></dance-vote>
        <a :href="danceLink(dr)">{{ danceName(dr) }}</a>
        <span v-if="dr.tags" style="margin-left: 0.25rem; line-height: 2.75rem">
          <tag-list-editor
            :container="dr"
            :filter="filter"
            :user="user"
            :editor="editor"
            :edit="edit"
            @update-song="$emit('update-song')"
            @edit="$emit('edit')"
          ></tag-list-editor>
        </span> </b-list-group-item
    ></b-list-group>
  </b-card>
</template>

<script lang="ts">
import DanceVote from "@/components/DanceVote.vue";
import TagButton from "@/components/TagButton.vue";
import TagListEditor from "@/components/TagListEditor.vue";
import AdminTools from "@/mix-ins/AdminTools";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceRating } from "@/model/DanceRating";
import { DanceStats } from "@/model/DanceStats";
import { Song } from "@/model/Song";
import { SongEditor } from "@/model/SongEditor";
import { SongFilter } from "@/model/SongFilter";
import { Tag } from "@/model/Tag";
import { TagHandler } from "@/model/TagHandler";
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";

@Component({
  components: {
    DanceVote,
    TagButton,
    TagListEditor,
  },
})
export default class DanceList extends Mixins(EnvironmentManager, AdminTools) {
  @Prop() private readonly song!: Song;
  @Prop() private readonly title!: string;
  @Prop() private readonly danceRatings!: DanceRating[];
  @Prop() private readonly user!: string;
  @Prop() private readonly filter!: SongFilter;
  @Prop() private readonly editor!: SongEditor;
  @Prop() private readonly edit!: boolean;

  private get danceRatingsFiltered(): DanceRating[] {
    const danceRatings = this.danceRatings;
    return danceRatings ? danceRatings.filter((dr) => dr) : [];
  }

  private get hasDances(): boolean {
    return this.danceRatingsFiltered.length > 0;
  }

  private statsFromRating(dr: DanceRating): DanceStats {
    return this.environment.fromId(dr.danceId)!;
  }

  private danceName(dr: DanceRating): string {
    return this.statsFromRating(dr).name;
  }

  private danceLink(dr: DanceRating): string {
    return `/dances/${this.statsFromRating(dr).seoName}`;
  }

  private subTagHandler(dr: DanceRating, tag: Tag): TagHandler {
    return new TagHandler(tag, this.user, this.filter, dr);
  }
}
</script>
