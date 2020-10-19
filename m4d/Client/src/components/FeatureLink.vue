<template>
    <div>
        <div><img :src="icon" :alt="this.info.type" width="24" height="24"> {{info.title}}</div>
        <div style="margin-left: 1em">
            <feature-button
                title="Try It!" type="play" :variant="info.type" :link="info.tryIt"
                style="margin-bottom: .25em"
            ></feature-button>
            <feature-button
                title="Documentation" type="docs" :variant="info.type" :link="info.docs"
                style="margin-bottom: .25em"
             ></feature-button>
            <feature-button
                title="Blog Posts" type="blog" :variant="info.type" :link="info.posts"
                style="margin-bottom: .25em"
            ></feature-button>
        </div>
    </div>
</template>

<script lang="ts">
import 'reflect-metadata';
import { Component, Prop, Vue } from 'vue-property-decorator';
import { FeatureInfo } from '@/model/FeatureInfo';
import FeatureButton from './FeatureButton.vue';

@Component({
    components: {
        FeatureButton,
    },
})
export default class FeatureLink extends Vue {
    @Prop() private readonly info!: FeatureInfo;

    private get icon(): string {
        return `/images/icons/${this.info.type}.png`;
    }
 }
</script>