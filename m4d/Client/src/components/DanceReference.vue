<template>
  <p>
    <a :href="danceLink">Browse</a> all <b>{{ danceCount }}</b>
    {{ dance.danceName }} songs in the <a href="/">music4dance</a
    ><span> </span> <a href="/song">catalog</a>.
  </p>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";
import { DanceStats } from "@/model/DanceStats";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";

@Component
export default class DanceReference extends Mixins(EnvironmentManager) {
  @Prop() private readonly danceId?: string;

  private get dance(): DanceStats | undefined {
    const id = this.danceId;
    return id ? this.environment.fromId(id) : undefined;
  }

  private get danceLink(): string | undefined {
    const id = this.danceId;
    return id ? `/song/search?dances=${id}` : undefined;
  }

  private get danceCount(): number | undefined {
    return this.dance?.songCountExplicit;
  }
}
</script>
