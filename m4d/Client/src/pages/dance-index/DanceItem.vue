<template>
    <div class="d-flex justify-content-between">
        <a :href="danceLink" :style="safeStyle">{{dance.danceName}}</a>
        <b-badge :href="countLink" :variant="safeVariant" style="line-height: 1.5">{{dance.songCount}}</b-badge>
    </div>
</template>

<script lang="ts">
import { Component, Prop, Vue } from 'vue-property-decorator';
import { DanceStats } from '@/model/DanceStats';


@Component
export default class DanceItem extends Vue {
    @Prop() private dance!: DanceStats;
    @Prop() private variant: string | undefined;
    @Prop() private textStyle: string | undefined;

    private get danceLink(): string {
        return `/dances/${this.dance.seoName}`;
    }

    private get countLink(): string {
        return `/song/index?filter=.-OOX,${this.dance.danceId}-Dances`;
    }

    private get safeVariant(): string {
        return this.variant ?? 'primary';
    }

    private get safeStyle(): string {
        return this.textStyle ?? '';
    }

}
</script>
