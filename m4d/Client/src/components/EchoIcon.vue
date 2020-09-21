<template>
    <a :href="link" target="_blank" v-if="value">
        <img :src="image" v-b-tooltip.hover.click.left="tip" width="25" height="25">
    </a>
</template>

<script lang="ts">
import 'reflect-metadata';
import { Component, Prop, Vue } from 'vue-property-decorator';
import { Song, ModifiedRecord } from '@/model/Song';

@Component
export default class EchoIcon extends Vue {
    @Prop() private readonly type!: string;
    @Prop() private readonly value?: number;
    @Prop() private readonly label!: string;
    @Prop() private readonly maxLabel!: string;

    private get link(): string {
        return `https://music4dance.blog/music4dance-help/playing-or-purchasing-songs/echonest/#${this.type}`;
    }

    private get image(): string {
        return `/images/icons/${this.type}-${this.level}.png`;
    }

    private get level(): number {
        return this.value ? Math.floor(this.value * 10) + 1 : 10;
    }

    private get tip(): string {
        return `This song has a ${this.label} of ${this.value?.toFixed(2)} where 1.00 is the ${this.maxLabel}.`;
    }
 }
</script>