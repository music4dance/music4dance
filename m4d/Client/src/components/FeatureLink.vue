<template>
    <div>
        <dt><img :src="icon" :alt="type" width="24" height="24">{{title}}</dt>
        <dd style="margin-left: 1em">            
            <b-button
                v-if="tryIt"
                variant="primary"
                size="sm"
                style="margin-left: 1em"
                :href="tryIt">
                <b-icon icon="play"></b-icon>&nbsp;Try It!
            </b-button>
            <b-button
                v-if="docs"
                variant="primary"
                size="sm"
                style="margin-left: 1em"
                :href="docs">
                <b-icon icon="file-text"></b-icon>&nbsp;Documentation
            </b-button>
            <b-button
                v-if="posts"
                variant="primary"
                size="sm"
                style="margin-left: 1em"
                :href="docs">
                <b-icon icon="pencil-square"></b-icon>&nbsp;Blog Posts
            </b-button>
        </dd>
    </div>
</template>

<script lang="ts">
import 'reflect-metadata';
import { Component, Prop, Vue } from 'vue-property-decorator';
import FeatureButton from './FeatureButton.vue';

@Component({
    components: {
        FeatureButton,
    },
})
export default class FeatureLink extends Vue {
    @Prop() private readonly title!: string;
    @Prop() private readonly type!: string;
    @Prop() private readonly tryIt!: string;
    @Prop() private readonly docs!: string;
    @Prop() private readonly posts!: string;

    private get icon(): string {
        return `/images/icons/${this.type}.png`;
    }
 }
</script>